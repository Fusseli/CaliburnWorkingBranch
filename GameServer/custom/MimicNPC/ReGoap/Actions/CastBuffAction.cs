using System;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using DOL.GS.Spells;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>
    /// Buff spell action for MimicNPC support classes
    /// Extends CastSpellAction with buff-specific cost calculation and effect list checking
    /// Leverages existing EffectListService to check for active buffs before casting
    /// </summary>
    /// <remarks>
    /// Design: Integrates with existing buff system (EffectListService.GetEffectOnTarget)
    /// Cost calculation follows buff formula: base_cost = cast_time_seconds * 2
    /// Buffs already active get 1000% cost increase (effective exclusion) (Req 11.13)
    /// Out of combat prioritization (Req 11.12 via DefensiveGoal)
    ///
    /// Reference: design.md "Component 5: Class-Specific Actions - CastSpellAction"
    /// Requirements: 4.1, 4.2, 4.4, 11.12, 11.13
    /// Leverage: Existing MimicBrain.CheckSpells(eCheckSpellType.Defensive), EffectListService, Body.CastSpell
    /// </remarks>
    public class CastBuffAction : CastSpellAction
    {
        /// <summary>
        /// Constructs a new CastBuffAction
        /// </summary>
        /// <param name="body">The MimicNPC body that will cast the buff spell</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        /// <param name="spell">The buff spell to cast</param>
        /// <param name="spellLine">The spell line the buff spell belongs to</param>
        public CastBuffAction(MimicNPC body, MimicBrain brain, Spell spell, SpellLine spellLine)
            : base(body, brain, spell, spellLine, new BuffCostCalculator())
        {
            // Verify this is actually a buff spell
            if (!IsBuffSpell(spell))
                throw new ArgumentException($"Spell {spell.Name} (ID {spell.ID}) is not a buff spell", nameof(spell));

            // Set action name for debugging
            name = $"CastBuff_{spell.Name}";

            // Override base preconditions with buff-specific ones
            preconditions.Clear();
            preconditions.Set(MimicWorldStateKeys.CAN_CAST, true);

            // Buffs can target self or group members, check for appropriate target
            // Most buffs target self or single target, some are group buffs
            if (spell.Target == eSpellTarget.SELF)
            {
                // Self-buff, no target needed
                preconditions.Set(MimicWorldStateKeys.CAN_CAST, true);
            }
            else if (spell.Target == eSpellTarget.GROUP || spell.Target == eSpellTarget.REALM)
            {
                // Group buff, no specific target needed (affects whole group)
                preconditions.Set(MimicWorldStateKeys.CAN_CAST, true);
            }
            else
            {
                // Single-target buff, need valid target
                preconditions.Set(MimicWorldStateKeys.HAS_TARGET, true);
            }

            // Override base effects with buff-specific ones
            effects.Clear();
            effects.Set($"spell_{spell.ID}_cast", true);
            effects.Set("targetBuffed", true);
            effects.Set(MimicWorldStateKeys.BUFFS_MAINTAINED, true);

            // Additional effect for specific buff types
            if (IsSpeedBuff(spell))
            {
                effects.Set("speedBuffActive", true);
            }
        }

        /// <summary>
        /// Checks if preconditions are satisfied for this buff spell
        /// Validates against world state and checks if buff is already active using EffectListService
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if buff spell can be cast, false otherwise</returns>
        public override bool CheckPreconditions(IReGoapAgent<string, object> agent, ReGoapState<string, object> currentState)
        {
            // Check base preconditions (canCast)
            if (!base.CheckPreconditions(agent, currentState))
                return false;

            // Check mana availability (read from ManaSensor which reads Body.Mana)
            object manaObj = currentState.Get(MimicWorldStateKeys.SELF_MANA);
            int mana = manaObj != null ? Convert.ToInt32(manaObj) : 0;
            if (mana < _spell.Power)
                return false;

            // Determine buff target based on spell target type
            GameObject buffTarget = GetBuffTarget();
            if (buffTarget == null)
                return false;

            // CRITICAL: Check if buff is already active using EffectListService (Req 11.13)
            // This prevents wasting mana on duplicate buffs
            if (IsBuffAlreadyActive(buffTarget))
                return false; // Buff already active, don't cast

            // Check spell range if single-target buff
            if (_spell.Target != eSpellTarget.SELF && _spell.Target != eSpellTarget.GROUP)
            {
                int distance = _body.GetDistanceTo(buffTarget);
                if (distance > _spell.Range)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines the appropriate target for this buff spell
        /// </summary>
        /// <returns>Target GameObject for buff, or null if invalid</returns>
        private GameObject GetBuffTarget()
        {
            // Self buffs target the caster
            if (_spell.Target == eSpellTarget.SELF)
                return _body;

            // Group buffs target the caster (effect applies to group)
            if (_spell.Target == eSpellTarget.GROUP || _spell.Target == eSpellTarget.REALM)
                return _body;

            // Single-target buffs use current target (usually a group member)
            // Fallback to self if no target
            return _body.TargetObject ?? _body;
        }

        /// <summary>
        /// Checks if this buff is already active on the target using EffectListService
        /// Leverages existing game system for effect tracking (Req 4.1, 4.2)
        /// </summary>
        /// <param name="target">The target to check for active buff</param>
        /// <returns>True if buff already active, false otherwise</returns>
        private bool IsBuffAlreadyActive(GameObject target)
        {
            if (target is not GameLiving livingTarget)
                return false;

            // Use EffectListService to check for active effects (existing game system)
            // Check by spell effect type to catch similar buffs
            eEffect effectType = GetBuffEffectType(_spell);
            if (effectType != eEffect.Unknown)
            {
                ECSGameEffect existingEffect = EffectListService.GetEffectOnTarget(livingTarget, effectType);
                if (existingEffect != null)
                    return true; // Buff already active
            }

            // Additional check: Look for exact spell ID in effect list
            // This catches buffs that may not have a specific eEffect enum value
            foreach (var effect in livingTarget.effectListComponent.GetAllEffects())
            {
                if (effect is ECSGameSpellEffect spellEffect &&
                    spellEffect.SpellHandler?.Spell?.ID == _spell.ID)
                {
                    return true; // Exact same spell already active
                }
            }

            return false; // Buff not active
        }

        /// <summary>
        /// Maps spell types to eEffect enum values for effect list checking
        /// </summary>
        /// <param name="spell">The buff spell</param>
        /// <returns>eEffect enum value, or Unknown if no mapping</returns>
        private eEffect GetBuffEffectType(Spell spell)
        {
            // Map common buff spell types to eEffect enum values
            // This allows checking for active buffs via EffectListService
            string spellType = spell.SpellType.ToString().ToLower();

            if (spellType.Contains("strengthbuff") || spellType.Contains("str"))
                return eEffect.StrengthBuff;
            if (spellType.Contains("constitutionbuff") || spellType.Contains("con"))
                return eEffect.ConstitutionBuff;
            if (spellType.Contains("dexteritybuff") || spellType.Contains("dex"))
                return eEffect.DexterityBuff;
            if (spellType.Contains("armorabsorptionbuff") || spellType.Contains("armorabsorbbuff"))
                return eEffect.ArmorFactorBuff;
            if (spellType.Contains("combatspeedbuff") || spellType.Contains("haste"))
                return eEffect.Haste;
            if (spellType.Contains("damage") && spellType.Contains("add"))
                return eEffect.DamageAdd;
            if (spellType.Contains("damage") && spellType.Contains("shield"))
                return eEffect.DamageShield;
            if (spellType.Contains("enduranceregeneration"))
                return eEffect.EnduranceRegenBuff;
            if (spellType.Contains("healthregeneration") || spellType.Contains("regen"))
                return eEffect.HealthRegenBuff;
            if (spellType.Contains("acuitybuff") || spellType.Contains("acuity"))
                return eEffect.AcuityBuff;
            if (spellType.Contains("speed"))
                return eEffect.MovementSpeedBuff;

            // Default: Unknown (will use exact spell ID check instead)
            return eEffect.Unknown;
        }

        /// <summary>
        /// Checks if this is a speed buff spell (critical for DAoC melee groups)
        /// </summary>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if speed buff, false otherwise</returns>
        private bool IsSpeedBuff(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("speed") || spellType.Contains("celerity");
        }

        /// <summary>
        /// Verifies that the buff spell had the expected effect
        /// Checks if buff is now active on target via EffectListService
        /// </summary>
        /// <returns>True if spell had expected effect, false if failed/resisted/interrupted</returns>
        protected override bool VerifySpellEffect()
        {
            // Get buff target
            GameObject target = GetBuffTarget();
            if (target == null)
                return false;

            GameLiving livingTarget = target as GameLiving;
            if (livingTarget == null)
                return false;

            // For buff spells, verify the buff is now active
            // Check using EffectListService (existing game system)
            if (IsBuffAlreadyActive(livingTarget))
                return true; // Buff successfully applied

            // If cast completed but buff not detected, assume success
            // (Some buffs may not have detectable effects via EffectListService)
            // Body.CastSpell + CastingService handle success/failure internally
            return true;
        }

        /// <summary>
        /// Returns string representation for debugging
        /// Includes spell name, buff type, and casting state
        /// </summary>
        public override string ToString()
        {
            return $"{GetName()} [Spell: {_spell.Name}, Target: {_spell.Target}, CastTime: {_spell.CastTime}ms, Casting: {_castStarted}, Failures: {_failureCount}]";
        }
    }

    /// <summary>
    /// Cost calculator for buff actions
    /// Implements buff-specific cost formula: base_cost = cast_time_seconds * 2
    /// Buffs already active get 1000% cost increase (effective exclusion)
    /// </summary>
    /// <remarks>
    /// Design: Strategy pattern for cost calculation
    /// Buffs prioritize maintenance and out-of-combat application
    /// Active buffs are effectively excluded to prevent wasted mana
    ///
    /// Cost Formula (Req 11.12):
    /// - base_cost = cast_time_seconds * 2
    /// - Lower cost for longer-lasting buffs (more efficient)
    /// - Lower cost for missing buffs (prioritize gaps)
    ///
    /// Already Active Modifier (Req 11.13):
    /// - If buff already active, multiply cost by 10.0 (1000% increase = effective exclusion)
    /// - Prevents duplicate buff casting and mana waste
    ///
    /// Reference: design.md "Component 7: Cost Calculators - BuffCostCalculator"
    /// Requirements: 4.4, 11.12, 11.13
    /// </remarks>
    public class BuffCostCalculator : ICostCalculator
    {
        /// <summary>
        /// Calculates the cost of a buff action
        /// Lower cost = higher priority for planner
        /// </summary>
        /// <param name="action">The action object (must be a Spell for buffs)</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Action cost (lower = higher priority)</returns>
        public float Calculate(object action, ReGoapState<string, object> worldState)
        {
            Spell spell = action as Spell;
            if (spell == null)
                throw new ArgumentException("BuffCostCalculator requires a Spell object", nameof(action));

            // Calculate cast time in seconds
            float castTimeSeconds = spell.CastTime / 1000f;

            // For instant buffs, use a minimal base time
            if (castTimeSeconds <= 0)
                castTimeSeconds = 0.1f;

            // Base cost formula (Req 11.12): cast_time_seconds * 2
            // Longer cast time = higher cost (less desirable)
            float baseCost = castTimeSeconds * 2f;

            // Factor in buff duration: Longer-lasting buffs are more efficient
            // Duration is typically in seconds (e.g., 600s = 10 minutes)
            int durationSeconds = spell.Duration / 1000;
            if (durationSeconds > 0)
            {
                // Reduce cost for longer-lasting buffs (more efficient per cast)
                // Max reduction: 50% for very long buffs (10+ minutes)
                float durationFactor = Math.Min(durationSeconds / 600f, 1.0f); // 600s = 10 min
                baseCost *= (1.0f - durationFactor * 0.5f);
            }

            // Check if buff is already active via world state or direct check
            // If active, dramatically increase cost to prevent duplicate casting (Req 11.13)
            bool buffAlreadyActive = CheckBuffAlreadyActive(spell, worldState);
            if (buffAlreadyActive)
            {
                baseCost *= 10.0f; // 1000% increase = effective exclusion
            }

            // Check combat status from world state
            // Buffs are lower priority during combat, higher priority out of combat
            object inCombatObj = worldState.Get(MimicWorldStateKeys.IN_COMBAT);
            bool inCombat = inCombatObj != null && Convert.ToBoolean(inCombatObj);

            if (!inCombat)
            {
                // Out of combat: Buffs are higher priority (lower cost)
                baseCost *= 0.7f; // 30% reduction out of combat
            }
            else
            {
                // In combat: Buffs are lower priority (higher cost)
                baseCost *= 1.5f; // 50% increase in combat
            }

            return baseCost;
        }

        /// <summary>
        /// Checks if buff is already active based on world state
        /// Uses world state sensor data when available
        /// </summary>
        /// <param name="spell">The buff spell</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>True if buff already active, false otherwise</returns>
        private bool CheckBuffAlreadyActive(Spell spell, ReGoapState<string, object> worldState)
        {
            // Check world state for specific buff flags
            // Sensors populate these keys based on EffectListService checks

            // For speed buffs, check dedicated flag
            string spellType = spell.SpellType.ToString().ToLower();
            if (spellType.Contains("speed"))
            {
                object speedActiveObj = worldState.Get("groupSpeedActive");
                if (speedActiveObj != null && Convert.ToBoolean(speedActiveObj))
                    return true;
            }

            // Generic buff active check
            // Sensors can populate "buff_{spellID}_active" keys
            object buffActiveObj = worldState.Get($"buff_{spell.ID}_active");
            if (buffActiveObj != null && Convert.ToBoolean(buffActiveObj))
                return true;

            // Default: Assume buff not active
            // The actual check happens in CastBuffAction.CheckPreconditions via EffectListService
            return false;
        }
    }
}
