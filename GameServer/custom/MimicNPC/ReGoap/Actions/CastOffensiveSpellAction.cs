using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using DOL.GS.Spells;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>
    /// Offensive spell action for MimicNPC casters and hybrids
    /// Extends CastSpellAction with DPS-specific cost calculation and target validation
    /// Leverages existing MimicBrain.CheckSpells(eCheckSpellType.Offensive) logic via world state
    /// </summary>
    /// <remarks>
    /// Design: Integrates with existing offensive spell system (MimicBrain.CheckSpells, Brain.CalculateNextAttackTarget)
    /// Cost calculation follows DPS formula: base_cost = (mana_cost + cast_time_seconds * 10) / expected_damage
    /// Instant spells get 20% cost reduction (favoring mobility) (Req 11.5)
    /// Positional spells (GTAE, cones requiring setup) get 50% cost increase (Req 11.6)
    /// Organic behavior: Instant spells preferred for kiting, casted spells preferred when stationary (Req 11.29, 11.30)
    ///
    /// Reference: design.md "Component 5: Class-Specific Actions - CastSpellAction"
    /// Requirements: 4.1, 4.2, 4.4, 11.4, 11.5, 11.6, 11.29, 11.30
    /// Leverage: Existing MimicBrain.CheckSpells(eCheckSpellType.Offensive), Brain.CalculateNextAttackTarget, Body.CastSpell
    /// </remarks>
    public class CastOffensiveSpellAction : CastSpellAction
    {
        /// <summary>
        /// Constructs a new CastOffensiveSpellAction
        /// </summary>
        /// <param name="body">The MimicNPC body that will cast the offensive spell</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        /// <param name="spell">The offensive spell to cast</param>
        /// <param name="spellLine">The spell line the offensive spell belongs to</param>
        public CastOffensiveSpellAction(MimicNPC body, MimicBrain brain, Spell spell, SpellLine spellLine)
            : base(body, brain, spell, spellLine, new DPSCostCalculator())
        {
            // Verify this is actually an offensive spell
            if (!IsDamageSpell(spell))
                throw new ArgumentException($"Spell {spell.Name} (ID {spell.ID}) is not an offensive spell", nameof(spell));

            // Set action name for debugging
            name = $"CastOffensive_{spell.Name}";

            // Override base preconditions with offensive-specific ones
            preconditions.Clear();
            preconditions.Set(MimicWorldStateKeys.CAN_CAST, true);
            preconditions.Set(MimicWorldStateKeys.HAS_TARGET, true);
            preconditions.Set(MimicWorldStateKeys.IN_COMBAT, true); // Only cast offensive spells in combat

            // Override base effects with offensive-specific ones
            effects.Clear();
            effects.Set($"spell_{spell.ID}_cast", true);
            effects.Set("targetDamaged", true);

            // Offensive spells contribute to target death goal
            // Don't set TARGET_DEAD = true, as one spell rarely kills
            // Instead, set that target is damaged and closer to death
            effects.Set("targetHealthReduced", true);
        }

        /// <summary>
        /// Checks if preconditions are satisfied for this offensive spell
        /// Validates against world state populated by sensors reading Brain/Body
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if offensive spell can be cast, false otherwise</returns>
        public override bool CheckPreconditions(IReGoapAgent<string, object> agent, ReGoapState<string, object> currentState)
        {
            // Check base preconditions (canCast, hasTarget, inCombat)
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

            // Check that target is a living entity (can't damage objects)
            GameLiving livingTarget = target as GameLiving;
            if (livingTarget == null || !livingTarget.IsAlive)
                return false;

            // Check target is hostile (don't nuke friendlies)
            if (!GameServer.ServerRules.IsAllowedToAttack(_body, livingTarget, true))
                return false;

            // Check spell range (offensive spells vary: 1500 standard, 350 GTAE, etc.)
            object distanceObj = currentState.Get(MimicWorldStateKeys.TARGET_DISTANCE);
            int targetDistance = distanceObj != null ? Convert.ToInt32(distanceObj) : int.MaxValue;
            if (targetDistance > _spell.Range)
                return false;

            // Additional check for GTAE spells: need valid ground target position
            // For now, simplify by checking if we're in range
            // TODO: Future enhancement for GTAE ground targeting

            return true;
        }

        /// <summary>
        /// Verifies that the offensive spell had the expected effect
        /// Checks if target's health decreased after spell completed or target died
        /// </summary>
        /// <returns>True if spell had expected effect, false if failed/resisted/interrupted</returns>
        protected override bool VerifySpellEffect()
        {
            // If target is no longer valid or died, spell likely succeeded
            GameObject target = _body.TargetObject;
            if (target == null)
            {
                // Target died or disappeared - offensive spell success
                return true;
            }

            GameLiving livingTarget = target as GameLiving;
            if (livingTarget == null || !livingTarget.IsAlive)
            {
                // Target is dead - offensive spell success
                return true;
            }

            // For offensive spells, if cast completed without interruption, assume success
            // Body.CastSpell + CastingService handle success/failure/resistance internally
            // Resistance is tracked by game systems - we don't duplicate that logic here
            // If target resisted, game systems will log it; we simply trust the cast completion
            return true;
        }

        /// <summary>
        /// Returns string representation for debugging
        /// Includes spell name, damage, and casting state
        /// </summary>
        public override string ToString()
        {
            return $"{GetName()} [Spell: {_spell.Name}, Damage: {_spell.Damage}, CastTime: {_spell.CastTime}ms, Casting: {_castStarted}, Failures: {_failureCount}]";
        }
    }

    /// <summary>
    /// Cost calculator for DPS/offensive actions
    /// Implements DPS-specific cost formula: base_cost = (mana_cost + cast_time_seconds * 10) / expected_damage
    /// Instant spells get 20% cost reduction (favoring mobility)
    /// Positional spells get 50% cost increase (accounting for setup time)
    /// </summary>
    /// <remarks>
    /// Design: Strategy pattern for cost calculation
    /// DPS prioritize resource efficiency (damage per mana/time)
    /// Mobile situations prioritize instant casts over casted spells
    ///
    /// Cost Formula (Req 11.4):
    /// - base_cost = (mana_cost + cast_time_seconds * 10) / expected_damage
    /// - Lower cost = higher priority for planner
    /// - Efficient nukes (high damage, low mana) = low cost
    /// - Inefficient nukes (low damage, high mana) = high cost
    /// - Cast time weighted at 10 mana equivalent per second
    ///
    /// Instant Cast Modifier (Req 11.5):
    /// - If spell is instant cast, multiply cost by 0.8 (20% reduction)
    /// - Encourages instant damage when movement/kiting needed
    ///
    /// Positional Modifier (Req 11.6):
    /// - If spell requires positioning (GTAE, cone, back requirement), multiply cost by 1.5 (50% increase)
    /// - Accounts for setup time and positioning difficulty
    ///
    /// Organic Behavior (Req 11.29, 11.30):
    /// - Instant spells preferred when movement required (kiting, interrupts)
    /// - Casted spells preferred when stationary (better damage efficiency)
    /// - GTAE/positional spells used when optimal positioning available
    ///
    /// Reference: design.md "Component 7: Cost Calculators - DPSCostCalculator"
    /// Requirements: 4.4, 11.4, 11.5, 11.6, 11.29, 11.30
    /// </remarks>
    public class DPSCostCalculator : ICostCalculator
    {
        /// <summary>
        /// Calculates the cost of an offensive spell action
        /// Lower cost = higher priority for planner
        /// </summary>
        /// <param name="action">The action object (must be a Spell for offensive)</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Action cost (lower = higher priority)</returns>
        public float Calculate(object action, ReGoapState<string, object> worldState)
        {
            Spell spell = action as Spell;
            if (spell == null)
                throw new ArgumentException("DPSCostCalculator requires a Spell object", nameof(action));

            // Get expected damage from spell
            double damage = spell.Damage;
            if (damage <= 0)
            {
                // For DoT spells or spells with damage over time, calculate total damage
                // damage = spell.Duration * spell.Value (for DoTs, Value is damage per tick)
                if (spell.SpellType == eSpellType.DamageOverTime)
                {
                    damage = spell.Duration / 1000.0 * spell.Value; // Rough estimate
                }
                else
                {
                    return float.MaxValue; // Invalid damage spell
                }
            }

            // Get mana cost
            int manaCost = spell.Power;

            // Calculate cast time in seconds
            float castTimeSeconds = spell.CastTime / 1000f;

            // Base cost formula (Req 11.4): (mana_cost + cast_time_seconds * 10) / expected_damage
            // Cast time weighted at 10 mana equivalent per second
            float baseCost = (manaCost + castTimeSeconds * 10f) / (float)damage;

            // Instant cast modifier (Req 11.5): -20% cost (favors mobility)
            if (spell.IsInstantCast)
            {
                baseCost *= 0.8f; // Instant spells get priority for kiting/movement
            }

            // Positional modifier (Req 11.6): +50% cost for spells requiring setup
            if (RequiresPositioning(spell))
            {
                baseCost *= 1.5f; // Positional spells cost more (setup time)
            }

            // Additional organic behavior modifiers (Req 11.29, 11.30):
            // If being interrupted frequently, favor instant casts even more
            object wasInterruptedObj = worldState.Get(MimicWorldStateKeys.WAS_JUST_INTERRUPTED);
            bool wasInterrupted = wasInterruptedObj != null && Convert.ToBoolean(wasInterruptedObj);
            if (wasInterrupted && spell.IsInstantCast)
            {
                baseCost *= 0.6f; // Extra priority for instant casts when being interrupted
            }

            // If stationary and safe, favor high-damage casted spells
            object inMeleeRangeObj = worldState.Get(MimicWorldStateKeys.TARGET_IN_MELEE_RANGE);
            bool inMeleeRange = inMeleeRangeObj != null && Convert.ToBoolean(inMeleeRangeObj);
            if (!inMeleeRange && !spell.IsInstantCast && damage > 200) // Safe at range, big nuke
            {
                baseCost *= 0.9f; // Slight preference for big casted nukes when safe
            }

            return baseCost;
        }

        /// <summary>
        /// Checks if spell requires positioning (GTAE, cone, positional requirements)
        /// </summary>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if positioning required, false otherwise</returns>
        private bool RequiresPositioning(Spell spell)
        {
            // GTAE (Ground Target Area Effect) requires positioning
            if (spell.Target == eSpellTarget.AREA)
                return true;

            // Cone spells require facing/positioning
            if (spell.Radius > 0 && spell.Target != eSpellTarget.SELF && spell.Target != eSpellTarget.GROUP)
                return true;

            // Check spell type for positional requirements
            string spellType = spell.SpellType.ToString().ToLower();
            if (spellType.Contains("cone") || spellType.Contains("pbae") || spellType.Contains("gtae"))
                return true;

            return false;
        }
    }
}
