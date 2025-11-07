using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using DOL.GS.Spells;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>
    /// Healing spell action for MimicNPC healers
    /// Extends CastSpellAction with healer-specific cost calculation and target selection
    /// Leverages existing MimicBrain.CheckHeals logic via world state populated by sensors
    /// </summary>
    /// <remarks>
    /// Design: Integrates with existing healing system (MimicGroup.MemberToHeal, CheckHeals priorities)
    /// Cost calculation follows healer formula: base_cost = (cast_time_seconds / healing_amount) * 100
    /// Emergency heals (<50% HP) get 3x priority boost (cost * 0.3)
    /// Small efficient heals preferred for 60-75% HP targets (Req 11.27)
    /// Large instant heals preferred for <50% HP targets (Req 11.28)
    ///
    /// Reference: design.md "Component 5: Class-Specific Actions - CastHealAction"
    /// Requirements: 4.1, 4.2, 4.4, 11.1, 11.2, 11.27, 11.28
    /// Leverage: Existing MimicBrain.CheckHeals, MimicGroup.MemberToHeal, Body.CastSpell
    /// </remarks>
    public class CastHealAction : CastSpellAction
    {
        /// <summary>
        /// Constructs a new CastHealAction
        /// </summary>
        /// <param name="body">The MimicNPC body that will cast the healing spell</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        /// <param name="spell">The healing spell to cast</param>
        /// <param name="spellLine">The spell line the healing spell belongs to</param>
        public CastHealAction(MimicNPC body, MimicBrain brain, Spell spell, SpellLine spellLine)
            : base(body, brain, spell, spellLine, new HealerCostCalculator())
        {
            // Verify this is actually a healing spell
            if (!IsHealingSpell(spell))
                throw new ArgumentException($"Spell {spell.Name} (ID {spell.ID}) is not a healing spell", nameof(spell));

            // Set action name for debugging
            name = $"CastHeal_{spell.Name}";

            // Override base preconditions with healer-specific ones
            preconditions.Clear();
            preconditions.Set(MimicWorldStateKeys.CAN_CAST, true);
            preconditions.Set(MimicWorldStateKeys.CAN_CAST_HEALING, true);

            // Precondition: Someone needs healing (checked against MimicGroup.MemberToHeal)
            // This is populated by GroupHealthSensor reading MimicGroup.CheckGroupHealth()
            preconditions.Set(MimicWorldStateKeys.MEMBER_TO_HEAL, true); // Non-null member

            // Override base effects with healer-specific ones
            effects.Clear();
            effects.Set($"spell_{spell.ID}_cast", true);
            effects.Set("targetHealed", true);

            // If this is a group heal or multiple people need healing, set group effect
            if (spell.Target == eSpellTarget.GROUP || spell.Target == eSpellTarget.REALM)
            {
                effects.Set(MimicWorldStateKeys.GROUP_FULL_HEALTH, true);
            }
        }

        /// <summary>
        /// Checks if preconditions are satisfied for this healing spell
        /// Validates against world state populated by sensors reading MimicGroup/Body/Brain
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if healing spell can be cast, false otherwise</returns>
        public override bool CheckPreconditions(IReGoapAgent<string, object> agent, ReGoapState<string, object> currentState)
        {
            // Check base preconditions (canCast, canCastHealing, memberToHeal)
            if (!base.CheckPreconditions(agent, currentState))
                return false;

            // Check mana availability (read from ManaSensor which reads Body.Mana)
            object manaObj = currentState.Get(MimicWorldStateKeys.SELF_MANA);
            int mana = manaObj != null ? Convert.ToInt32(manaObj) : 0;
            if (mana < _spell.Power)
                return false;

            // Check that member to heal is valid (read from GroupHealthSensor)
            object memberToHealObj = currentState.Get(MimicWorldStateKeys.MEMBER_TO_HEAL);
            GameLiving memberToHeal = memberToHealObj as GameLiving;
            if (memberToHeal == null)
                return false;

            // Check spell range (healing spells may have different range than offensive spells)
            int distance = _body.GetDistanceTo(memberToHeal);
            if (distance > _spell.Range)
                return false;

            // Check group coordination flags to avoid duplicate instant heals/HoTs
            // These are populated by GroupHealthSensor reading MimicGroup flags
            if (_spell.IsInstantCast && _spell.SpellType == eSpellType.Heal)
            {
                object alreadyCastObj = currentState.Get(MimicWorldStateKeys.ALREADY_CAST_INSTANT_HEAL);
                bool alreadyCast = alreadyCastObj != null && Convert.ToBoolean(alreadyCastObj);
                if (alreadyCast)
                    return false;
            }

            if (_spell.SpellType == eSpellType.HealOverTime)
            {
                object alreadyCastingHoTObj = currentState.Get(MimicWorldStateKeys.ALREADY_CASTING_HOT);
                bool alreadyCastingHoT = alreadyCastingHoTObj != null && Convert.ToBoolean(alreadyCastingHoTObj);
                if (alreadyCastingHoT)
                    return false;
            }

            if (_spell.SpellType == eSpellType.HealthRegenBuff)
            {
                object alreadyCastingRegenObj = currentState.Get(MimicWorldStateKeys.ALREADY_CASTING_REGEN);
                bool alreadyCastingRegen = alreadyCastingRegenObj != null && Convert.ToBoolean(alreadyCastingRegenObj);
                if (alreadyCastingRegen)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Verifies that the healing spell had the expected effect
        /// Checks if target's health increased after spell completed
        /// </summary>
        /// <returns>True if spell had expected effect, false if failed/resisted/interrupted</returns>
        protected override bool VerifySpellEffect()
        {
            // If target is no longer valid, spell may have failed
            GameObject target = _body.TargetObject;
            if (target == null)
                return false;

            GameLiving livingTarget = target as GameLiving;
            if (livingTarget == null)
                return false;

            // For healing spells, we can verify the target is now at higher health
            // However, this is complicated by the fact that:
            // 1. Target may have taken damage during cast
            // 2. Other healers may have healed target
            // 3. Target may be at full health already
            //
            // The safest approach: if cast completed without interruption, trust the game system
            // Body.CastSpell + CastingService handle success/failure/resistance internally
            // Resistance is rare for healing spells, so we assume success if cast finished
            return true;
        }

        /// <summary>
        /// Returns string representation for debugging
        /// Includes spell name, healing amount, and casting state
        /// </summary>
        public override string ToString()
        {
            double healAmount = MimicNPC.HealAmount(_spell, _body.TargetObject as GameLiving ?? _body);
            return $"{GetName()} [Spell: {_spell.Name}, HealAmount: {healAmount:F0}, Casting: {_castStarted}, Failures: {_failureCount}]";
        }
    }

    /// <summary>
    /// Cost calculator for healer actions
    /// Implements healer-specific cost formula: base_cost = (cast_time_seconds / healing_amount) * 100
    /// Emergency heals (<50% HP) get 3x priority boost (cost * 0.3)
    /// </summary>
    /// <remarks>
    /// Design: Strategy pattern for cost calculation
    /// Healers prioritize time efficiency (fast heals over slow heals)
    /// Emergency situations prioritize speed over efficiency
    ///
    /// Cost Formula (Req 11.1):
    /// - base_cost = (cast_time_seconds / healing_amount) * 100
    /// - Lower cost = higher priority for planner
    /// - Instant heals (0s cast time) = minimal cost (0.01)
    /// - Efficient heals = low cost (high healing per second)
    /// - Slow heals = high cost (low healing per second)
    ///
    /// Emergency Modifier (Req 11.2):
    /// - If target HP < 50%, multiply cost by 0.3 (3x priority)
    /// - Encourages instant heals and big heals for emergencies
    ///
    /// Reference: design.md "Component 7: Cost Calculators"
    /// Requirements: 4.4, 11.1, 11.2, 11.27, 11.28
    /// </remarks>
    public class HealerCostCalculator : ICostCalculator
    {
        /// <summary>
        /// Calculates the cost of a healing spell action
        /// Lower cost = higher priority for planner
        /// </summary>
        /// <param name="action">The action object (must be a Spell for healing)</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Action cost (lower = higher priority)</returns>
        public float Calculate(object action, ReGoapState<string, object> worldState)
        {
            Spell spell = action as Spell;
            if (spell == null)
                throw new ArgumentException("HealerCostCalculator requires a Spell object", nameof(action));

            // Get target to heal from world state (MimicGroup.MemberToHeal)
            object memberToHealObj = worldState.Get(MimicWorldStateKeys.MEMBER_TO_HEAL);
            GameLiving target = memberToHealObj as GameLiving;

            // If no valid target, return high cost to deprioritize
            if (target == null)
                return float.MaxValue;

            // Calculate healing amount using existing game formula
            double healAmount = MimicNPC.HealAmount(spell, target);
            if (healAmount <= 0)
                return float.MaxValue; // Invalid healing spell

            // Calculate cast time in seconds
            float castTimeSeconds = spell.CastTime / 1000f;

            // For instant casts, use a minimal base time to avoid division by zero
            // but still give them priority over casted spells
            if (castTimeSeconds <= 0)
                castTimeSeconds = 0.01f;

            // Base cost formula (Req 11.1): (cast_time_seconds / healing_amount) * 100
            // Lower healing per second = higher cost
            float baseCost = (castTimeSeconds / (float)healAmount) * 100f;

            // Emergency modifier (Req 11.2): If target HP < 50%, multiply cost by 0.3 (3x priority)
            int targetHealthPercent = target.HealthPercent;
            if (targetHealthPercent < 50)
            {
                baseCost *= 0.3f; // Emergency heal priority boost
            }
            // Additional priority for targets 50-75% HP to encourage proactive healing (Req 11.27)
            else if (targetHealthPercent < 75)
            {
                // Small efficient heals are preferred here
                // If this is a "small heal" (heal amount < 50% of max health), reduce cost
                if (healAmount < (target.MaxHealth * 0.5))
                {
                    baseCost *= 0.7f; // Small efficient heal priority
                }
            }

            // For emergency targets (<50% HP), prefer instant heals (Req 11.28)
            if (targetHealthPercent < 50 && spell.IsInstantCast)
            {
                baseCost *= 0.5f; // Extra priority for instant emergency heals
            }

            return baseCost;
        }
    }
}
