using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using DOL.GS.Spells;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>
    /// Crowd control spell action for MimicNPC CC specialists
    /// Extends CastSpellAction with CC-specific cost calculation and group coordination via MimicGroup.CCTargets
    /// Leverages existing group coordination to prevent mezz breaking (DAoC-critical mechanic)
    /// </summary>
    /// <remarks>
    /// Design: Integrates with existing MimicGroup.CCTargets coordination list (Req 8.2)
    /// Cost calculation follows CC formula: base_cost = cast_time_seconds + (60 / control_duration_seconds)
    /// Targets already controlled get 500% cost increase (near-exclusion) (Req 11.11)
    /// Diminishing returns targets get 200% cost increase (Req 11.10)
    /// Longest-duration CC preferred (Req 11.33)
    ///
    /// DAoC Mezz Mechanics (CRITICAL):
    /// - Mezz breaks on ANY damage (even 1 point)
    /// - Mezz duration up to 60 seconds in DAoC
    /// - Group must coordinate to avoid breaking mezz
    /// - MimicGroup.CCTargets list tracks controlled enemies
    ///
    /// Reference: design.md "Component 5: Class-Specific Actions - CastSpellAction"
    /// Requirements: 4.1, 4.2, 4.4, 8.2, 11.9, 11.10, 11.11, 11.32, 11.33
    /// Leverage: Existing MimicBrain.CheckMainCC, MimicGroup.CCTargets, Body.CastSpell
    /// </remarks>
    public class CastCrowdControlAction : CastSpellAction
    {
        /// <summary>
        /// Constructs a new CastCrowdControlAction
        /// </summary>
        /// <param name="body">The MimicNPC body that will cast the CC spell</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        /// <param name="spell">The crowd control spell to cast</param>
        /// <param name="spellLine">The spell line the CC spell belongs to</param>
        public CastCrowdControlAction(MimicNPC body, MimicBrain brain, Spell spell, SpellLine spellLine)
            : base(body, brain, spell, spellLine, new CCCostCalculator())
        {
            // Verify this is actually a crowd control spell
            if (!IsCrowdControlSpell(spell))
                throw new ArgumentException($"Spell {spell.Name} (ID {spell.ID}) is not a crowd control spell", nameof(spell));

            // Set action name for debugging
            name = $"CastCC_{spell.Name}";

            // Override base preconditions with CC-specific ones
            preconditions.Clear();
            preconditions.Set(MimicWorldStateKeys.CAN_CAST, true);
            preconditions.Set(MimicWorldStateKeys.HAS_TARGET, true);
            preconditions.Set(MimicWorldStateKeys.IN_COMBAT, true); // Only CC during combat

            // Override base effects with CC-specific ones
            effects.Clear();
            effects.Set($"spell_{spell.ID}_cast", true);
            effects.Set("targetControlled", true);
            effects.Set(MimicWorldStateKeys.ADDS_CONTROLLED, true);

            // Additional effect for specific CC types
            if (IsMezzSpell(spell))
            {
                effects.Set("targetMezzed", true);
            }
            else if (IsRootSpell(spell))
            {
                effects.Set("targetRooted", true);
            }
            else if (IsStunSpell(spell))
            {
                effects.Set("targetStunned", true);
            }
        }

        /// <summary>
        /// Checks if preconditions are satisfied for this CC spell
        /// Validates against world state and checks group coordination via MimicGroup.CCTargets (Req 8.2)
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if CC spell can be cast, false otherwise</returns>
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

            // Check that target is a living entity (can't CC objects)
            GameLiving livingTarget = target as GameLiving;
            if (livingTarget == null || !livingTarget.IsAlive)
                return false;

            // Check target is hostile (don't CC friendlies)
            if (!GameServer.ServerRules.IsAllowedToAttack(_body, livingTarget, true))
                return false;

            // CRITICAL: Check if target is already controlled by group (Req 8.2, 11.11)
            // Prevents mezz breaking and duplicate CC
            if (IsTargetAlreadyControlledByGroup(livingTarget))
                return false; // Don't CC already controlled targets

            // CRITICAL: For mezz spells, check if target is already mezzed (DAoC mezz breaking prevention)
            if (IsMezzSpell(_spell) && livingTarget.IsMezzed)
                return false; // Never break mezz with another mezz

            // Check spell range
            object distanceObj = currentState.Get(MimicWorldStateKeys.TARGET_DISTANCE);
            int targetDistance = distanceObj != null ? Convert.ToInt32(distanceObj) : int.MaxValue;
            if (targetDistance > _spell.Range)
                return false;

            // Check if target is immune to CC (some mobs/players have CC immunity)
            if (IsTargetImmuneToCC(livingTarget))
                return false;

            return true;
        }

        /// <summary>
        /// Checks if target is already controlled by any group member via MimicGroup.CCTargets
        /// Leverages existing group coordination system (Req 8.2)
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <returns>True if target already controlled, false otherwise</returns>
        private bool IsTargetAlreadyControlledByGroup(GameLiving target)
        {
            // Check group's shared CC targets list (existing coordination mechanism)
            var mimicGroup = _body.Group?.MimicGroup;
            if (mimicGroup != null)
            {
                // Direct check: Is target in the CCTargets list?
                if (mimicGroup.CCTargets.Contains(target))
                    return true; // Target already controlled by group
            }

            // Additional check: Is target currently under CC effect?
            // This catches cases where CCTargets list may not be updated yet
            if (target.IsMezzed || target.IsStunned)
                return true;

            return false; // Target not controlled
        }

        /// <summary>
        /// Checks if target is immune to crowd control
        /// Some mobs and players have CC immunity
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <returns>True if immune, false otherwise</returns>
        private bool IsTargetImmuneToCC(GameLiving target)
        {
            // Check for CC immunity effect
            if (EffectListService.GetEffectOnTarget(target, eEffect.CCImmunity) != null)
                return true;

            // Check for mezz immunity effect
            if (IsMezzSpell(_spell) && EffectListService.GetEffectOnTarget(target, eEffect.MezzImmunity) != null)
                return true;

            // Check for stun immunity effect
            if (IsStunSpell(_spell) && EffectListService.GetEffectOnTarget(target, eEffect.StunImmunity) != null)
                return true;

            // Boss mobs may be immune (check if target is significantly higher level)
            if (target.Level > _body.Level + 5)
            {
                // High-level mobs may resist CC more often
                // This is a heuristic - actual immunity is handled by spell resist mechanics
            }

            return false;
        }

        /// <summary>
        /// Verifies that the CC spell had the expected effect
        /// Checks if target is now controlled and updates MimicGroup.CCTargets list (Req 8.2)
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

            // For CC spells, verify the control effect is now active
            bool ccApplied = false;

            if (IsMezzSpell(_spell))
            {
                // Verify target is now mezzed
                ccApplied = livingTarget.IsMezzed;
            }
            else if (IsStunSpell(_spell))
            {
                // Verify target is now stunned
                ccApplied = livingTarget.IsStunned;
            }
            else if (IsRootSpell(_spell))
            {
                // Verify target is now rooted (check via effect list)
                ccApplied = EffectListService.GetEffectOnTarget(livingTarget, eEffect.MovementSpeedDebuff) != null ||
                           EffectListService.GetEffectOnTarget(livingTarget, eEffect.Root) != null;
            }
            else
            {
                // Other CC types - assume success if cast completed
                ccApplied = true;
            }

            // If CC was successfully applied, update MimicGroup.CCTargets list (Req 8.2)
            if (ccApplied)
            {
                AddTargetToCCList(livingTarget);
            }

            return ccApplied;
        }

        /// <summary>
        /// Adds target to MimicGroup.CCTargets list for group coordination (Req 8.2)
        /// Prevents other group members from breaking the CC
        /// </summary>
        /// <param name="target">The controlled target</param>
        private void AddTargetToCCList(GameLiving target)
        {
            var mimicGroup = _body.Group?.MimicGroup;
            if (mimicGroup != null && target != null)
            {
                // Add to shared CC targets list (existing coordination mechanism)
                if (!mimicGroup.CCTargets.Contains(target))
                {
                    mimicGroup.CCTargets.Add(target);
                }
            }
        }

        /// <summary>
        /// Checks if spell is a mezz spell
        /// </summary>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if mezz spell, false otherwise</returns>
        private bool IsMezzSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("mesmerize") || spellType.Contains("mezz");
        }

        /// <summary>
        /// Checks if spell is a root spell
        /// </summary>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if root spell, false otherwise</returns>
        private bool IsRootSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("root") || spellType.Contains("snare");
        }

        /// <summary>
        /// Checks if spell is a stun spell
        /// </summary>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if stun spell, false otherwise</returns>
        private bool IsStunSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("stun");
        }

        /// <summary>
        /// Returns string representation for debugging
        /// Includes spell name, CC type, duration, and casting state
        /// </summary>
        public override string ToString()
        {
            string ccType = "CC";
            if (IsMezzSpell(_spell)) ccType = "Mezz";
            else if (IsStunSpell(_spell)) ccType = "Stun";
            else if (IsRootSpell(_spell)) ccType = "Root";

            return $"{GetName()} [Spell: {_spell.Name}, Type: {ccType}, Duration: {_spell.Duration / 1000}s, Casting: {_castStarted}, Failures: {_failureCount}]";
        }
    }

    /// <summary>
    /// Cost calculator for crowd control actions
    /// Implements CC-specific cost formula: base_cost = cast_time_seconds + (60 / control_duration_seconds)
    /// Targets already controlled get 500% cost increase (near-exclusion)
    /// Diminishing returns get 200% cost increase
    /// </summary>
    /// <remarks>
    /// Design: Strategy pattern for cost calculation
    /// CC prioritizes long-duration controls and avoids breaking existing CC
    ///
    /// Cost Formula (Req 11.9):
    /// - base_cost = cast_time_seconds + (60 / control_duration_seconds)
    /// - Favors long-duration CC (60s mezz = lowest cost)
    /// - Shorter duration = higher cost (less efficient)
    ///
    /// Diminishing Returns Modifier (Req 11.10):
    /// - If target has diminishing returns active, multiply cost by 3.0 (200% increase)
    /// - Penalizes re-controlling recently controlled targets
    ///
    /// Already Controlled Modifier (Req 11.11):
    /// - If target already controlled by group, multiply cost by 5.0 (500% increase = near-exclusion)
    /// - CRITICAL for DAoC: Never break mezz with another mezz
    ///
    /// Organic Behavior (Req 11.33):
    /// - Long-duration mezz (60s) preferred over short stuns (10s)
    /// - Group coordination prevents duplicate CC and mezz breaking
    ///
    /// Reference: design.md "Component 7: Cost Calculators - CCCostCalculator"
    /// Requirements: 4.4, 8.2, 11.9, 11.10, 11.11, 11.32, 11.33
    /// </remarks>
    public class CCCostCalculator : ICostCalculator
    {
        /// <summary>
        /// Calculates the cost of a crowd control action
        /// Lower cost = higher priority for planner
        /// </summary>
        /// <param name="action">The action object (must be a Spell for CC)</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Action cost (lower = higher priority)</returns>
        public float Calculate(object action, ReGoapState<string, object> worldState)
        {
            Spell spell = action as Spell;
            if (spell == null)
                throw new ArgumentException("CCCostCalculator requires a Spell object", nameof(action));

            // Calculate cast time in seconds
            float castTimeSeconds = spell.CastTime / 1000f;

            // Calculate control duration in seconds
            float durationSeconds = spell.Duration / 1000f;

            // Avoid division by zero for instant/permanent effects
            if (durationSeconds <= 0)
                durationSeconds = 1.0f;

            // Base cost formula (Req 11.9): cast_time_seconds + (60 / control_duration_seconds)
            // Favors long-duration CC (up to 60s in DAoC)
            float baseCost = castTimeSeconds + (60f / durationSeconds);

            // Get current target from world state
            object targetObj = worldState.Get(MimicWorldStateKeys.CURRENT_TARGET);
            GameLiving target = targetObj as GameLiving;

            if (target != null)
            {
                // CRITICAL: If target already controlled by group, astronomical cost (Req 11.11)
                // This prevents mezz breaking - the most critical mechanic in DAoC CC
                if (IsTargetAlreadyControlled(target, worldState))
                {
                    baseCost *= 5.0f; // 500% increase = near-exclusion (Req 11.11)
                }

                // Diminishing returns penalty (Req 11.10)
                if (HasDiminishingReturns(target, spell))
                {
                    baseCost *= 3.0f; // 200% increase for diminishing returns
                }

                // Additional penalty for targets that are immune or highly resistant
                if (IsTargetLikelyImmune(target, spell))
                {
                    baseCost *= 10.0f; // Very high cost to discourage wasting CC on immune targets
                }
            }

            return baseCost;
        }

        /// <summary>
        /// Checks if target is already controlled (mezzed, stunned, rooted)
        /// Uses world state when available, falls back to direct check
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>True if already controlled, false otherwise</returns>
        private bool IsTargetAlreadyControlled(GameLiving target, ReGoapState<string, object> worldState)
        {
            // Check world state for mezzed enemies list (from MezzStatusSensor)
            object mezzedEnemiesObj = worldState.Get("mezzedEnemies");
            if (mezzedEnemiesObj is List<GameLiving> mezzedEnemies)
            {
                if (mezzedEnemies.Contains(target))
                    return true; // Target is mezzed
            }

            // Fallback: Direct check on target properties
            if (target.IsMezzed || target.IsStunned)
                return true;

            // Check for root effect
            if (EffectListService.GetEffectOnTarget(target, eEffect.Root) != null)
                return true;

            return false; // Target not controlled
        }

        /// <summary>
        /// Checks if target has diminishing returns active for CC
        /// Diminishing returns reduce CC duration on repeated applications
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <param name="spell">The CC spell being evaluated</param>
        /// <returns>True if diminishing returns active, false otherwise</returns>
        private bool HasDiminishingReturns(GameLiving target, Spell spell)
        {
            // In DAoC, diminishing returns typically apply when a target was recently CC'd
            // Check if target has any recent CC immunity or resistance effects

            // Check for mezz immunity (indicates recent mezz)
            if (IsMezzSpell(spell))
            {
                var mezzImmunity = EffectListService.GetEffectOnTarget(target, eEffect.MezzImmunity);
                if (mezzImmunity != null)
                    return true; // Recently mezzed, diminishing returns active
            }

            // Check for stun immunity (indicates recent stun)
            if (IsStunSpell(spell))
            {
                var stunImmunity = EffectListService.GetEffectOnTarget(target, eEffect.StunImmunity);
                if (stunImmunity != null)
                    return true; // Recently stunned, diminishing returns active
            }

            // Default: No diminishing returns detected
            return false;
        }

        /// <summary>
        /// Checks if target is likely immune to CC
        /// High-level mobs and players with immunity effects
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <param name="spell">The CC spell being evaluated</param>
        /// <returns>True if likely immune, false otherwise</returns>
        private bool IsTargetLikelyImmune(GameLiving target, Spell spell)
        {
            // Check for general CC immunity
            if (EffectListService.GetEffectOnTarget(target, eEffect.CCImmunity) != null)
                return true;

            // Check for specific immunity based on spell type
            if (IsMezzSpell(spell) && EffectListService.GetEffectOnTarget(target, eEffect.MezzImmunity) != null)
                return true;

            if (IsStunSpell(spell) && EffectListService.GetEffectOnTarget(target, eEffect.StunImmunity) != null)
                return true;

            return false;
        }

        /// <summary>
        /// Checks if spell is a mezz spell
        /// </summary>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if mezz spell, false otherwise</returns>
        private bool IsMezzSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("mesmerize") || spellType.Contains("mezz");
        }

        /// <summary>
        /// Checks if spell is a stun spell
        /// </summary>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if stun spell, false otherwise</returns>
        private bool IsStunSpell(Spell spell)
        {
            string spellType = spell.SpellType.ToString().ToLower();
            return spellType.Contains("stun");
        }
    }
}
