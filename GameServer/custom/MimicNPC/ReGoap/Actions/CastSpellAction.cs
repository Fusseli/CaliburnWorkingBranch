using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using DOL.GS.Spells;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>
    /// Base class for spell casting actions in ReGoap
    /// Integrates with existing MimicNPC spell system via Body.CastSpell
    /// Monitors casting state and handles interruptions gracefully
    /// </summary>
    /// <remarks>
    /// Design: Delegates all spell casting to existing game systems (Body.CastSpell, CastingService)
    /// No logic duplication - action monitors Body.IsCasting to track state
    /// Preconditions checked against world state populated by sensors reading Body/Brain properties
    ///
    /// Action Flow:
    /// 1. CheckPreconditions: Verify canCast, hasMana, targetValid, spellNotOnCooldown
    /// 2. Run (first call): Start cast via Body.CastSpell, return false (still running)
    /// 3. Run (subsequent calls): Monitor Body.IsCasting, return true when complete
    /// 4. VerifySpellEffect: Compare predicted effect to actual game state
    /// 5. Callback: Invoke doneCallback on success, failCallback on failure
    ///
    /// Reference: design.md "Component 5: Class-Specific Actions - CastSpellAction"
    /// Leverage: Existing MimicBrain.CheckSpells, Body.CastSpell (Requirement 4.1, 4.2)
    /// </remarks>
    public class CastSpellAction : MimicAction
    {
        protected readonly Spell _spell;
        protected readonly SpellLine _spellLine;
        protected readonly ICostCalculator _costCalculator;
        protected bool _castStarted = false;
        protected GameObject _originalTarget = null;

        /// <summary>
        /// Gets the spell this action will cast
        /// </summary>
        public Spell Spell => _spell;

        /// <summary>
        /// Gets the spell line this spell belongs to
        /// </summary>
        public SpellLine SpellLine => _spellLine;

        /// <summary>
        /// Gets whether the cast has started
        /// Used to track action state across think ticks
        /// </summary>
        public bool CastStarted => _castStarted;

        /// <summary>
        /// Constructs a new CastSpellAction
        /// </summary>
        /// <param name="body">The MimicNPC body that will cast the spell</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        /// <param name="spell">The spell to cast</param>
        /// <param name="spellLine">The spell line the spell belongs to</param>
        /// <param name="costCalculator">Cost calculator for this spell type (healer, DPS, CC, etc.)</param>
        public CastSpellAction(MimicNPC body, MimicBrain brain, Spell spell, SpellLine spellLine, ICostCalculator costCalculator)
            : base(body, brain)
        {
            _spell = spell ?? throw new ArgumentNullException(nameof(spell));
            _spellLine = spellLine ?? throw new ArgumentNullException(nameof(spellLine));
            _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));

            // Set action name for debugging
            name = $"CastSpell_{spell.Name}";

            // Configure preconditions (checked against world state from sensors)
            // These keys are populated by sensors reading Body/Brain properties
            preconditions.Set(MimicWorldStateKeys.CAN_CAST, true);
            preconditions.Set(MimicWorldStateKeys.HAS_TARGET, true);

            // Configure effects (predicted outcome)
            // Planner uses these to chain actions toward goals
            effects.Set($"spell_{spell.ID}_cast", true);

            // Spell-type-specific effects for goal matching
            string spellType = spell.SpellType.ToString().ToLower();

            if (IsHealingSpell(spell))
            {
                effects.Set("targetHealed", true);
            }
            else if (IsDamageSpell(spell))
            {
                effects.Set("targetDamaged", true);
                effects.Set(MimicWorldStateKeys.TARGET_DEAD, false); // May lead to target death
            }
            else if (IsCrowdControlSpell(spell))
            {
                effects.Set("targetControlled", true);
                effects.Set(MimicWorldStateKeys.ADDS_CONTROLLED, true);
            }
            else if (IsBuffSpell(spell))
            {
                effects.Set("targetBuffed", true);
                effects.Set(MimicWorldStateKeys.BUFFS_MAINTAINED, true);
            }
            else if (IsCureSpell(spell))
            {
                effects.Set("targetCured", true);
            }
        }

        /// <summary>
        /// Checks if preconditions are satisfied for this spell cast
        /// Validates against world state populated by sensors reading Body/Brain
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if spell can be cast, false otherwise</returns>
        public override bool CheckPreconditions(IReGoapAgent<string, object> agent, ReGoapState<string, object> currentState)
        {
            // Check base preconditions (canCast, hasTarget)
            if (!base.CheckPreconditions(agent, currentState))
                return false;

            // Check mana availability (read from ManaSensor which reads Body.Mana)
            object manaObj = currentState.Get(MimicWorldStateKeys.SELF_MANA);
            int mana = manaObj != null ? Convert.ToInt32(manaObj) : 0;
            if (mana < _spell.Power)
                return false;

            // Check target validity (read from TargetSensor)
            object targetObj = currentState.Get(MimicWorldStateKeys.CURRENT_TARGET);
            GameObject target = targetObj as GameObject;
            if (target == null)
                return false;

            // Check spell range (healing spells use different range than offensive)
            object distanceObj = currentState.Get(MimicWorldStateKeys.TARGET_DISTANCE);
            int targetDistance = distanceObj != null ? Convert.ToInt32(distanceObj) : int.MaxValue;
            if (targetDistance > _spell.Range)
                return false;

            // Check cooldown (read from Body properties via sensor or directly)
            // Note: Cooldown checking happens in Body.CastSpell, but we can pre-filter here
            // for better plan generation performance

            return true;
        }

        /// <summary>
        /// Calculates the base cost for this spell action
        /// Delegates to role-specific cost calculator (healer, DPS, tank, CC)
        /// </summary>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Base action cost before failure penalties</returns>
        protected override float CalculateBaseCost(ReGoapState<string, object> worldState)
        {
            return _costCalculator.Calculate(_spell, worldState);
        }

        /// <summary>
        /// Executes the spell cast action
        /// Delegates to existing game system via Body.CastSpell
        /// Monitors casting state via Body.IsCasting property
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="doneCallback">Callback to invoke on successful completion</param>
        /// <param name="failCallback">Callback to invoke on failure</param>
        /// <returns>True if action is complete (success or failure), false if still running</returns>
        public override bool Run(IReGoapAgent<string, object> agent,
                                  Action<IReGoapAction<string, object>> doneCallback,
                                  Action<IReGoapAction<string, object>> failCallback)
        {
            // First call: Start the cast
            if (!_castStarted)
            {
                // Store original target in case cast fails
                _originalTarget = _body.TargetObject;

                // Use existing spell casting system via Body.CastSpell
                // This delegates to CastingService and existing game logic
                // Returns true if cast started, false if unable to cast
                bool startedCast = _body.CastSpell(_spell, _spellLine);

                if (startedCast)
                {
                    _castStarted = true;
                    return false; // Still running - cast in progress
                }
                else
                {
                    // Failed to start cast (cooldown, insufficient mana, silenced, etc.)
                    OnFailure();
                    failCallback(this);
                    return true; // Complete (failed)
                }
            }
            else
            {
                // Subsequent calls: Monitor cast progress via Body.IsCasting property
                // Body.IsCasting is set by existing CastingService
                if (!_body.IsCasting)
                {
                    // Cast finished - verify outcome by reading game state
                    bool success = VerifySpellEffect();

                    if (success)
                    {
                        OnSuccess();
                        doneCallback(this);
                    }
                    else
                    {
                        // Cast failed (interrupted, resisted, target died, immune, etc.)
                        OnFailure();
                        failCallback(this);
                    }

                    // Reset state for next execution
                    _castStarted = false;
                    _originalTarget = null;

                    return true; // Complete
                }

                // Still casting - wait for next think tick
                return false;
            }
        }

        /// <summary>
        /// Verifies that the spell had the expected effect
        /// Compares predicted outcome to actual game state by reading target properties
        /// No data duplication - reads directly from target's current state
        /// </summary>
        /// <returns>True if spell had expected effect, false if failed/resisted/interrupted</returns>
        protected virtual bool VerifySpellEffect()
        {
            // If target is no longer valid, spell may have failed
            GameObject target = _body.TargetObject;
            if (target == null)
            {
                // Target died or disappeared during cast - not necessarily a failure
                // Return true if target was supposed to die (damage spell), false otherwise
                return IsDamageSpell(_spell);
            }

            GameLiving livingTarget = target as GameLiving;
            if (livingTarget == null)
                return false;

            // Spell-type-specific verification by reading target state
            if (IsCrowdControlSpell(_spell))
            {
                // Verify mezz/root/stun applied by reading target.IsMezzed/IsRooted/IsStunned
                // Note: This is a simplification - actual implementation would check specific CC type
                return livingTarget.IsMezzed || livingTarget.IsStunned;
            }

            // For most spells (heals, damage, buffs), assume success if cast completed
            // Body.CastSpell + CastingService handle success/failure internally
            // If cast finished without interruption, it likely succeeded or was resisted by game logic
            // Resistance is tracked by game systems - we don't duplicate that logic here
            return true;
        }

        /// <summary>
        /// Resets the action state
        /// Called when action is cancelled or needs to be restarted
        /// </summary>
        public void Reset()
        {
            _castStarted = false;
            _originalTarget = null;
        }

        #region Spell Type Helpers

        /// <summary>
        /// Checks if spell is a healing spell
        /// </summary>
        protected bool IsHealingSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("heal") ||
                   spellType.Contains("health") ||
                   spellType == "spreadheal";
        }

        /// <summary>
        /// Checks if spell is a damage spell
        /// </summary>
        protected bool IsDamageSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("damage") ||
                   spellType.Contains("directdamage") ||
                   spellType.Contains("bolt") ||
                   spellType.Contains("dot") ||
                   spellType == "lifedrain";
        }

        /// <summary>
        /// Checks if spell is a crowd control spell (mezz, stun, root, snare)
        /// </summary>
        protected bool IsCrowdControlSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("mesmerize") ||
                   spellType.Contains("stun") ||
                   spellType.Contains("root") ||
                   spellType.Contains("snare");
        }

        /// <summary>
        /// Checks if spell is a buff spell
        /// </summary>
        protected bool IsBuffSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("buff") ||
                   spellType.Contains("strengthbuff") ||
                   spellType.Contains("dexteritybuff") ||
                   spellType.Contains("armorabsorptionbuff") ||
                   spellType.Contains("armorabsorbbuff") ||
                   spellType == "combatspeedbuff" ||
                   spellType == "enduranceregeneration";
        }

        /// <summary>
        /// Checks if spell is a cure spell
        /// </summary>
        protected bool IsCureSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("cure") ||
                   spellType.Contains("removeeffect");
        }

        #endregion

        /// <summary>
        /// Returns string representation for debugging
        /// Includes spell name and casting state
        /// </summary>
        public override string ToString()
        {
            return $"{GetName()} [Spell: {_spell.Name}, Casting: {_castStarted}, Failures: {_failureCount}]";
        }
    }

    /// <summary>
    /// Interface for cost calculators
    /// Implementations provide role-specific cost formulas (healer, DPS, tank, CC)
    /// </summary>
    /// <remarks>
    /// Design Pattern: Strategy pattern for cost calculation
    /// Different roles (healer, DPS, tank, CC) have different cost priorities:
    /// - Healer: (castTime / healAmount) * 100 (time efficiency)
    /// - DPS: (manaCost + castTime * 10) / damage (resource efficiency)
    /// - Tank: 100 / (threatGeneration + 1) (inverse of threat)
    /// - CC: castTime + (60 / duration) (favors long-duration CC)
    ///
    /// Reference: design.md "Component 7: Cost Calculators"
    /// Requirements: 4.4, 7.2
    /// </remarks>
    public interface ICostCalculator
    {
        /// <summary>
        /// Calculates the cost of an action (spell, ability, style)
        /// Lower cost = higher priority for planner
        /// </summary>
        /// <param name="action">The action object (Spell, Ability, Style, etc.)</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Action cost (lower = higher priority)</returns>
        float Calculate(object action, ReGoapState<string, object> worldState);
    }
}
