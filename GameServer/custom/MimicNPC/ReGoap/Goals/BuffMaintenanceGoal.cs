using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Universal buff maintenance goal for out-of-combat defensive preparation
    /// Shared across support/healer roles (AugHealer, Support) and applicable to all classes
    /// Priority scales dynamically based on out-of-combat time and missing buffs
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - BuffMaintenanceGoal handles defensive spell casting when not in active combat
    /// - Low base priority ensures combat goals always take precedence
    /// - Priority increases with out-of-combat time (natural recovery window)
    /// - Checks for missing critical buffs (defensive, speed, stat buffs)
    /// - Works across all classes - healers, support, casters, and tanks all maintain buffs
    ///
    /// Priority Formula (from requirements.md 11.25-11.26 and design.md Component 4):
    /// base_priority = 0.5 (low priority, only when no combat)
    /// - If out of combat for 10+ seconds: multiply by 3.0 (becomes priority 1.5)
    /// - If in combat: return 0.0 (combat goals take absolute precedence)
    /// - If all buffs present: goal satisfied, priority irrelevant
    ///
    /// DAoC Buff Context (from daoc-role-analysis.md):
    /// - Speed Buffs: Speed 5/6 for melee groups (CRITICAL - without speed, melee cannot chase)
    /// - Armor Buffs: Base armor increase (defensive foundation)
    /// - Stat Buffs: Strength, constitution, dexterity (core combat effectiveness)
    /// - Damage Shields/Adds: Absorption/reflection, timer-based (situational defensive layer)
    /// - Resist Buffs: Elemental resists, damage type resists (RvR defensive layers)
    /// - Buff Stacking: Multiple classes may provide similar buffs (coordinate via action costs)
    /// - Buff Duration: Most buffs last 10-30 minutes, periodic refresh needed
    ///
    /// Organic Behavior Patterns (from requirements.md 11.34):
    /// - When group is out of combat: Buff maintenance naturally occurs (defensive goals highest priority)
    /// - Immediately after combat ends: Wait 10 seconds, then buff maintenance activates
    /// - During combat recovery: Healing takes precedence (priority 5.0+), buffs wait
    /// - When fully healed and buffed: PullTargetGoal can activate (priority 1.0)
    /// - Buff action cost calculation: Already-active buffs have 1000% cost penalty (effective exclusion)
    ///
    /// World State Dependencies:
    /// - IN_COMBAT: Combat state (goal only active out of combat)
    /// - OUT_OF_COMBAT_TIME: Seconds since last combat (for priority scaling)
    /// - BUFFS_MAINTAINED: Whether all critical buffs are active
    /// - CAN_CAST_DEFENSIVE_SPELLS: Whether mimic has defensive/buff spells available
    /// - GROUP_SPEED_ACTIVE: Speed buff status (critical for melee groups)
    /// - SPEED_CRITICAL: Speed missing with melee in group (escalates priority)
    ///
    /// Goal State: { "buffsMaintained": true }
    /// Satisfied when: All critical buffs active or in combat (buffs deferred to combat goals)
    ///
    /// Example Scenarios:
    /// - Group in combat: Priority = 0.0 (combat goals take precedence)
    /// - Combat just ended (2s recovery): Priority = 0.0 (too soon, let healing happen)
    /// - Out of combat 5 seconds, missing buffs: Priority = 0.5 (low priority maintenance)
    /// - Out of combat 15 seconds, missing buffs: Priority = 1.5 (active buff maintenance)
    /// - Out of combat 20 seconds, all buffs present: Goal satisfied (nothing to do)
    /// - Speed missing with melee group: Priority escalated (speed critical for combat)
    /// - Multiple support classes in group: Coordination via buff action costs (avoid duplicates)
    ///
    /// Buff Maintenance Sequence:
    /// 1. Combat ends → IN_COMBAT = false
    /// 2. Group recovers health/mana (0-10 seconds) → Healing goals active (priority 5.0+)
    /// 3. 10+ seconds out of combat → BuffMaintenanceGoal priority increases to 1.5
    /// 4. Planner evaluates missing buffs → Selects buff actions with lowest cost
    /// 5. Buff actions execute → Apply defensive/speed/stat buffs
    /// 6. All critical buffs active → Goal satisfied (priority becomes irrelevant)
    /// 7. PullTargetGoal can now activate (priority 1.0, group fully ready)
    ///
    /// Buff Priority Hierarchy (via Action Costs):
    /// - Speed buffs for melee groups: Extremely low cost (0.01-0.2) - always apply first
    /// - Armor buffs (base armor): Low cost (1-3) - apply second (defensive foundation)
    /// - Stat buffs (STR, CON, DEX): Low-medium cost (3-7) - apply third (combat effectiveness)
    /// - Damage shields/adds (timer-based): Medium cost (7-12) - apply fourth (situational)
    /// - Resist buffs: Higher cost (12-20) - apply last if time permits (RvR preparation)
    /// - Already active buffs: Extremely high cost (1000+) - effective exclusion
    ///
    /// Coordination with Other Goals:
    /// - HealGoal (5.0+ base): Much higher priority, must complete before buffs
    /// - BuffMaintenanceGoal (0.5 base, 1.5 with time): Moderate priority, pre-combat preparation
    /// - PullTargetGoal (1.0 base): Higher priority, only activates when buffs complete
    /// - Combat goals (2.0-10.0): Much higher priority, buffs pause during combat
    ///
    /// Multi-Class Coordination:
    /// - Multiple support classes can provide same buffs (e.g., multiple clerics with armor buff)
    /// - Buff action cost calculator checks if buff already active on target
    /// - If buff active: Cost multiplied by 1000% (near-exclusion, don't waste mana/time)
    /// - Natural coordination: First caster applies buff, others skip it automatically
    /// - No explicit coordination needed, cost calculation handles conflicts
    ///
    /// Result: Group naturally maintains buffs out of combat without manual commands
    /// </remarks>
    public class BuffMaintenanceGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for buff maintenance when out of combat
        /// Value of 0.5 places buff maintenance below pulling (1.0) and well below combat goals (2.0+)
        /// </summary>
        private const float BASE_PRIORITY = 0.5f;

        /// <summary>
        /// Out of combat time threshold before buff maintenance priority increases (seconds)
        /// 10 seconds allows for healing/mana recovery before starting buff application
        /// </summary>
        private const float BUFF_MAINTENANCE_TIME_THRESHOLD = 10.0f;

        /// <summary>
        /// Priority multiplier when out of combat time exceeds threshold
        /// 3.0x multiplier increases priority from 0.5 to 1.5 (above pulling, below combat)
        /// </summary>
        private const float EXTENDED_OUT_OF_COMBAT_MULTIPLIER = 3.0f;

        /// <summary>
        /// Additional priority multiplier when speed is critical (melee group missing speed)
        /// Speed buff is game-critical for melee effectiveness in DAoC
        /// </summary>
        private const float SPEED_CRITICAL_MULTIPLIER = 5.0f;

        /// <summary>
        /// Goal state key for buff maintenance completion
        /// </summary>
        private const string BUFFS_MAINTAINED = "buffsMaintained";

        /// <summary>
        /// Constructs a new BuffMaintenanceGoal for a mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public BuffMaintenanceGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on combat state and out-of-combat duration
        /// Priority scales with time out of combat (allows healing/mana recovery first)
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. In Combat: Return 0.0 (buff maintenance only out of combat)
        /// 2. Out of Combat, < 10s: Return 0.5 (low priority, let healing/recovery happen)
        /// 3. Out of Combat, >= 10s: Return 1.5 (0.5 * 3.0, active buff maintenance)
        /// 4. Speed Critical (missing speed with melee): Multiply by 5.0 (emergency priority)
        /// 5. No Defensive Spells Available: Return 0.0 (can't cast buffs)
        ///
        /// Example Scenarios:
        /// - Group in active combat: 0.0 (combat goals take precedence)
        /// - Combat ended 2s ago: 0.5 (low priority, healing may still be needed)
        /// - Combat ended 5s ago: 0.5 (low priority, mana regeneration in progress)
        /// - Combat ended 12s ago: 1.5 (active buff maintenance, ready for next pull)
        /// - Combat ended 20s ago: 1.5 (continued buff maintenance)
        /// - Speed missing, melee in group, 12s out of combat: 7.5 (1.5 * 5.0, emergency speed priority)
        /// - All buffs active: Goal satisfied (priority becomes irrelevant)
        /// - Mimic has no buff spells: 0.0 (cannot contribute to buff maintenance)
        ///
        /// Out-of-Combat Recovery Timeline:
        /// - 0-3s: Healing active (HealGoal priority 5.0+)
        /// - 3-10s: Mana regeneration, low buff priority (0.5)
        /// - 10+s: Active buff maintenance (1.5), ready for next pull
        /// - Speed critical: Immediate high priority (7.5) regardless of time
        ///
        /// Priority Hierarchy (Typical Out-of-Combat):
        /// 1. Healing (5.0+) - Keep group alive
        /// 2. Speed critical (7.5) - Melee group needs speed
        /// 3. Buff maintenance after 10s (1.5) - Prepare for combat
        /// 4. Pull initiation (1.0) - Start next fight
        /// 5. Buff maintenance before 10s (0.5) - Low priority maintenance
        ///
        /// Multi-Support Coordination:
        /// - Multiple support classes all evaluate BuffMaintenanceGoal with same priority
        /// - Planner selects actions based on cost (BuffCostCalculator handles coordination)
        /// - First support to apply a buff makes it expensive for others (1000% cost)
        /// - Natural staggering: Most important buffs (speed) applied first, others follow
        ///
        /// Speed Buff Special Handling:
        /// - Speed critical for melee groups (cannot chase without speed 5/6)
        /// - Speed missing with melee present: Priority multiplied by 5.0
        /// - MaintainSpeedGoal may also exist for dedicated speed maintenance
        /// - BuffMaintenanceGoal covers speed as part of general buff suite
        ///
        /// Result: Natural buff maintenance timing without manual coordination
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool inCombat = IsInCombat(currentState);
            float outOfCombatTime = GetOutOfCombatTime(currentState);
            bool canCastDefensiveSpells = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_DEFENSIVE_SPELLS, false);

            // Goal only applies when out of combat
            if (inCombat)
                return 0.0f; // Combat goals take absolute precedence

            // Can't contribute to buff maintenance without defensive spells
            if (!canCastDefensiveSpells)
                return 0.0f; // No buff spells available

            // Base buff maintenance priority
            float priority = BASE_PRIORITY;

            // Increase priority if been out of combat long enough (healing/mana recovery complete)
            if (outOfCombatTime >= BUFF_MAINTENANCE_TIME_THRESHOLD)
            {
                // Extended out of combat time - active buff maintenance
                priority *= EXTENDED_OUT_OF_COMBAT_MULTIPLIER; // 0.5 → 1.5
            }

            // Emergency priority for speed buffs (critical for melee groups)
            bool speedCritical = GetBool(currentState, MimicWorldStateKeys.SPEED_CRITICAL, false);
            if (speedCritical)
            {
                // Speed missing with melee in group - emergency priority
                priority *= SPEED_CRITICAL_MULTIPLIER; // Further multiply by 5.0
            }

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when buff maintenance goal is satisfied
        /// Goal: All critical defensive buffs are active on group members
        /// </summary>
        /// <returns>Goal state with "buffsMaintained" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "buffsMaintained" to true.
        /// Buff actions (CastSpellAction for defensive/buff spells) set "buffApplied" effects,
        /// which when all critical buffs are applied, satisfy "buffsMaintained".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against buff presence on group.
        ///
        /// Note: This goal focuses on ensuring buffs are present, not continuously recasting them.
        /// Once all critical buffs are active, goal is satisfied until a buff expires or combat begins.
        ///
        /// Critical Buffs (Priority Order):
        /// 1. Speed Buffs: Speed 5/6 for melee groups (game-critical)
        /// 2. Armor Buffs: Base armor increase (defensive foundation)
        /// 3. Stat Buffs: Strength, constitution, dexterity (core combat effectiveness)
        /// 4. Damage Shields/Adds: Absorption/reflection, timer-based (situational defense)
        /// 5. Resist Buffs: Elemental resists (RvR defensive preparation)
        ///
        /// Buff Application Sequence (via Planner):
        /// - Planner evaluates all available buff actions with BuffCostCalculator
        /// - Speed buffs have lowest cost (0.01-0.2) - applied first if missing
        /// - Armor buffs have low cost (1-3) - applied second (defensive foundation)
        /// - Stat buffs have low-medium cost (3-7) - applied third (core effectiveness)
        /// - Damage shields have medium cost (7-12) - applied fourth (situational)
        /// - Resist buffs have higher cost (12-20) - applied last (RvR preparation)
        /// - Already-active buffs have very high cost (1000+) - effectively excluded
        ///
        /// Multi-Buff Coordination:
        /// - Buff actions check for existing buffs via EffectListService
        /// - BuffCostCalculator increases cost 1000% if buff already active
        /// - First caster applies buff, subsequent casters skip it automatically
        /// - No need for explicit group coordination flags (unlike healing)
        ///
        /// Buff Expiration Handling:
        /// - Buffs expire after duration (10-30 minutes typically)
        /// - When buff expires: "buffsMaintained" becomes false
        /// - BuffMaintenanceGoal re-activates next time out of combat 10+ seconds
        /// - Natural periodic refresh without explicit timer tracking
        ///
        /// Combat Interaction:
        /// - When combat starts: Goal priority drops to 0.0 (combat goals take over)
        /// - Buffs remain active during combat (not dispelled by combat state)
        /// - After combat ends: Goal re-evaluates, refreshes expired buffs
        /// - Seamless transition: Combat → Recovery → Buffs → Next Combat
        ///
        /// Result: Group maintains defensive buffs naturally between combats
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: all critical buffs maintained
            goalState.Set(BUFFS_MAINTAINED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if buff maintenance goal is currently satisfied
        /// Satisfied when in combat (buffs deferred) or all critical buffs are active
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if in combat or buffs maintained, false if buffs needed</returns>
        /// <remarks>
        /// Override default satisfaction check to handle combat state and buff presence efficiently.
        ///
        /// BUFFS_MAINTAINED populated by sensors checking EffectListService on group members,
        /// uses existing buff tracking infrastructure (no duplication).
        ///
        /// Satisfaction Logic:
        /// - IN_COMBAT == true: Goal satisfied (buff maintenance deferred to post-combat)
        /// - BUFFS_MAINTAINED == true: All critical buffs present → goal satisfied
        /// - BUFFS_MAINTAINED == false: Missing critical buffs → goal not satisfied
        /// - No defensive spells available: Goal satisfied (can't contribute, don't block planner)
        ///
        /// Critical Buff Determination:
        /// - Speed buff: Critical if group has melee classes
        /// - Armor buff: Critical for all classes (base defensive layer)
        /// - Stat buffs: Critical for combat effectiveness (STR, CON, DEX)
        /// - Damage shield: Important but not critical (timer-based, situational)
        /// - Resist buffs: Important for RvR, not critical for PvE
        ///
        /// Edge Cases:
        /// - Solo mimic (no group): Only self-buffs matter, satisfaction based on self
        /// - Support with no buff spells: Goal satisfied (cannot contribute)
        /// - All group members already buffed by other source: Goal satisfied (redundant)
        /// - Mid-combat buff expires: Goal remains satisfied (combat takes precedence)
        ///
        /// Buff Check Optimization:
        /// - Sensors check buffs on group members every 500ms (think interval)
        /// - Uses existing EffectListService queries (no additional overhead)
        /// - Caches results in world state (BUFFS_MAINTAINED flag)
        /// - Planner only evaluates buff actions when flag is false
        ///
        /// Post-Combat Buff Refresh:
        /// - Combat ends → IN_COMBAT = false
        /// - Buffs checked → Some may have expired during combat
        /// - BUFFS_MAINTAINED = false if any critical buffs missing
        /// - Goal not satisfied → BuffMaintenanceGoal re-activates
        /// - Planner generates plan to reapply missing buffs
        ///
        /// Multi-Caster Scenarios:
        /// - Multiple support classes in group
        /// - All evaluate BuffMaintenanceGoal with same priority
        /// - First to act applies buffs, others see BUFFS_MAINTAINED = true
        /// - Natural coordination via shared world state (no explicit locks needed)
        ///
        /// Result: Efficient buff maintenance without redundant checks or casts
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool inCombat = IsInCombat(currentState);
            bool buffsMaintained = GetBool(currentState, MimicWorldStateKeys.BUFFS_MAINTAINED, true); // Default true if not tracked
            bool canCastDefensiveSpells = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_DEFENSIVE_SPELLS, false);

            // Goal satisfied if in combat (buff maintenance deferred) or cannot contribute
            if (inCombat || !canCastDefensiveSpells)
                return true;

            // Goal satisfied if all critical buffs are active
            if (buffsMaintained)
                return true;

            // Buffs missing and out of combat = goal not satisfied, need to apply buffs
            return false;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "BuffMaintenanceGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, combat state, buff status, and timing
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and buff maintenance details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool inCombat = IsInCombat(currentState);
            float outOfCombatTime = GetOutOfCombatTime(currentState);
            bool buffsMaintained = GetBool(currentState, MimicWorldStateKeys.BUFFS_MAINTAINED, true);
            bool canCastDefensiveSpells = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_DEFENSIVE_SPELLS, false);
            bool speedCritical = GetBool(currentState, MimicWorldStateKeys.SPEED_CRITICAL, false);
            bool groupSpeedActive = GetBool(currentState, MimicWorldStateKeys.GROUP_SPEED_ACTIVE, true);
            bool groupHasMelee = GetBool(currentState, MimicWorldStateKeys.GROUP_HAS_MELEE, false);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"InCombat: {inCombat}, BuffsMaintained: {buffsMaintained}, " +
                   $"CanCastDefensive: {canCastDefensiveSpells}, " +
                   $"OutOfCombatTime: {outOfCombatTime:F1}s, " +
                   $"SpeedCritical: {speedCritical}, SpeedActive: {groupSpeedActive}, " +
                   $"GroupHasMelee: {groupHasMelee})";
        }
    }
}
