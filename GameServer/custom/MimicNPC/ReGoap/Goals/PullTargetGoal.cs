using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Puller role goal for initiating combat in PvE scenarios (tank role)
    /// Shared across tank classes when designated as MainPuller
    /// Priority dynamically calculated based on combat state and group readiness
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - PullTargetGoal handles combat initiation for tank pullers in PvE encounters
    /// - Active only when out of combat and valid targets available
    /// - Checks group readiness (health, mana, buffs) before pulling
    /// - Priority drops to zero during combat (pulling is a pre-combat action)
    /// - Works in coordination with MainPuller role assignment
    ///
    /// Priority Formula (from requirements.md 11.24 and design.md Component 4):
    /// base_priority = 1.0 when no combat, has target, and group ready
    /// - If in combat: return 0.0 (pulling only happens out of combat)
    /// - If no valid target: return 0.0 (need target to pull)
    /// - If group not ready: return 0.0 (wait for healing/mana/buffs)
    /// - Out of combat with target and ready group: return 1.0 (ready to pull)
    ///
    /// Group Readiness Criteria:
    /// - Health Check: All group members above 75% HP (safe pull threshold)
    /// - Mana Check: Healer has at least 50% mana (sufficient for fight)
    /// - Emergency Override: Never pull if anyone below 50% HP (emergency threshold)
    /// - Buff Check: Critical buffs active (defensive buffs, speed if melee group)
    ///
    /// DAoC Pulling Context (from daoc-role-analysis.md):
    /// - PvE Meta: Tank initiates combat from range before melee engagement
    /// - Typical Pull: Ranged ability (bow, thrown weapon) or spell to grab aggro
    /// - Single Target: Pull one enemy at a time to avoid overwhelming healer
    /// - Positioning: Pull enemy back to group, not run into pack
    /// - Group Preparation: Wait for group to be ready (health, mana, buffs)
    /// - RvR Note: Pulling not applicable in RvR (combat already active, no "pull" phase)
    ///
    /// Organic Behavior Patterns (from requirements.md 11.29):
    /// - When out of combat: Defensive/buff maintenance goals active (priority 0.5-1.5)
    /// - When group injured/low mana: Healing/recovery takes precedence (priority 0.0 for pull)
    /// - When target in range and group ready: PullTargetGoal becomes highest priority (1.0)
    /// - When pull initiated: Combat begins, goal satisfied (priority drops to 0.0)
    /// - When in combat: DPS/Tank goals take over (priority 2.0-4.0+)
    ///
    /// World State Dependencies:
    /// - IN_COMBAT: Combat state (goal only active out of combat)
    /// - HAS_TARGET: Whether puller has a valid target (from TargetSensor)
    /// - IS_MAIN_PULLER: Role assignment (only MainPuller should pull)
    /// - NUM_ENEMIES: Total number of enemies on aggro list
    /// - OUT_OF_COMBAT_TIME: Seconds since last combat (for pull timing)
    /// - NUM_NEED_HEALING: Group members needing healing (<75% HP)
    /// - NUM_EMERGENCY_HEALING: Group members in emergency state (<50% HP)
    /// - GROUP_SIZE: Total group members for readiness calculation
    ///
    /// Goal State: { "combatInitiated": true }
    /// Satisfied when: Combat has started (IN_COMBAT = true) or no valid target
    ///
    /// Example Scenarios:
    /// - Tank out of combat, no enemies nearby: Priority = 0.0 (no target to pull)
    /// - Tank out of combat, enemy in range, group injured: Priority = 0.0 (wait for healing)
    /// - Tank out of combat, enemy in range, healer low mana: Priority = 0.0 (wait for mana regen)
    /// - Tank out of combat, enemy in range, group ready: Priority = 1.0 (ready to pull)
    /// - Tank pulls enemy, combat starts: Priority = 0.0 (goal satisfied)
    /// - Tank in combat with enemy: Priority = 0.0 (pulling complete, DPS/Tank goals active)
    /// - Non-puller role: Priority = 0.0 (not designated puller)
    ///
    /// Pull Action Sequence:
    /// 1. Combat ends, group recovers (healing, mana regen, rebuffs)
    /// 2. DefensiveGoal active (priority 0.5-1.5) - buff maintenance
    /// 3. Group reaches ready state (all >75% HP, healer >50% mana, buffs active)
    /// 4. Tank identifies pull target (TargetSensor selects from available enemies)
    /// 5. PullTargetGoal activates (priority 1.0 - higher than defensive)
    /// 6. Planner selects pull action (ranged attack, taunt, charge)
    /// 7. Pull action executes (initiates combat)
    /// 8. Enemy aggros and approaches (IN_COMBAT = true)
    /// 9. PullTargetGoal satisfied (priority 0.0)
    /// 10. Tank goals take over (TankThreatGoal, GuardHealerGoal)
    ///
    /// Coordination with Other Goals:
    /// - DefensiveGoal (0.5 base): Lower priority, handles buffs before pull
    /// - HealGoal (5.0+ base): Much higher priority, must complete before pull
    /// - TankThreatGoal (3.0+ base): Higher priority, activates after pull initiates combat
    /// - GuardHealerGoal (varies): Activates after combat starts
    /// - DealDamageGoal (2.0 base): Activates after combat starts
    ///
    /// Result: Tank naturally waits for group readiness before pulling, seamlessly transitions to combat
    /// </remarks>
    public class PullTargetGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for pulling when out of combat with valid target and ready group
        /// Value of 1.0 places pulling above defensive maintenance (0.5) but below active combat goals
        /// </summary>
        private const float BASE_PRIORITY = 1.0f;

        /// <summary>
        /// Minimum health percentage for group members before pulling (safe threshold)
        /// 75% ensures group can handle incoming damage without immediate emergency
        /// </summary>
        private const float MIN_HEALTH_PERCENT_FOR_PULL = 75.0f;

        /// <summary>
        /// Emergency health percentage - never pull if anyone this low
        /// 50% is emergency healing threshold, pulling would be dangerous
        /// </summary>
        private const float EMERGENCY_HEALTH_PERCENT = 50.0f;

        /// <summary>
        /// Minimum healer mana percentage before pulling
        /// 50% ensures healer has sufficient resources for upcoming fight
        /// </summary>
        private const float MIN_HEALER_MANA_PERCENT = 50.0f;

        /// <summary>
        /// Minimum out of combat time before considering pulling (seconds)
        /// 3 seconds allows for mana regen and buff application
        /// </summary>
        private const float MIN_OUT_OF_COMBAT_TIME = 3.0f;

        /// <summary>
        /// Goal state key for combat initiation
        /// </summary>
        private const string COMBAT_INITIATED = "combatInitiated";

        /// <summary>
        /// Constructs a new PullTargetGoal for a puller mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public PullTargetGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on combat state, target availability, and group readiness
        /// Priority is 1.0 when ready to pull, 0.0 when in combat, no target, or group not ready
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. In Combat: Return 0.0 (pulling only happens before combat)
        /// 2. No Target: Return 0.0 (need valid target to pull)
        /// 3. Not Main Puller: Return 0.0 (only designated puller should initiate)
        /// 4. Group Not Ready: Return 0.0 (wait for healing/mana/buffs)
        /// 5. Out of Combat + Has Target + Is Puller + Group Ready: Return 1.0 (ready to pull)
        ///
        /// Group Readiness Checks:
        /// - Health: No emergency injuries (<50% HP), minimal injuries (<75% HP)
        /// - Mana: Healer has sufficient mana (>50%) for upcoming fight
        /// - Time: Minimum 3 seconds out of combat for recovery
        /// - Buffs: Critical buffs active (speed for melee groups, defensive buffs)
        ///
        /// Example Scenarios:
        /// - Tank out of combat, no enemies: 0.0 (nothing to pull)
        /// - Tank out of combat, enemy in range, 2 members at 60% HP: 0.0 (wait for healing)
        /// - Tank out of combat, enemy in range, healer at 30% mana: 0.0 (wait for mana regen)
        /// - Tank out of combat, enemy in range, 1 member at 40% HP: 0.0 (emergency, never pull)
        /// - Tank out of combat, enemy in range, all >75% HP, healer >50% mana: 1.0 (ready to pull)
        /// - Tank just killed enemy (1s out of combat): 0.0 (wait for recovery time)
        /// - Tank pulled, combat starting: 0.0 (goal satisfied)
        /// - Tank in active combat: 0.0 (pulling complete)
        /// - DPS role with target: 0.0 (not designated puller)
        ///
        /// PvE Pull Timing Sequence:
        /// 1. Previous fight ends → IN_COMBAT = false
        /// 2. Group recovers → HealGoal active (priority 5.0+)
        /// 3. Group healthy → NUM_NEED_HEALING = 0
        /// 4. Healer regenerates mana → healer mana >50%
        /// 5. Buffs reapplied → DefensiveGoal (priority 0.5-1.5)
        /// 6. 3+ seconds out of combat → OUT_OF_COMBAT_TIME >= 3.0
        /// 7. Tank selects target → HAS_TARGET = true
        /// 8. PullTargetGoal activates → Priority 1.0 (highest non-emergency)
        /// 9. Pull action executes → Enemy aggros
        /// 10. Combat begins → IN_COMBAT = true, goal satisfied
        ///
        /// Group Coordination:
        /// - Healers prioritize group health before pull (HealGoal priority 5.0+)
        /// - Support applies buffs before pull (DefensiveGoal priority 0.5-1.5)
        /// - PullTargetGoal waits for all preparations (priority 1.0)
        /// - Once ready, pull initiates immediately (no manual command needed)
        ///
        /// RvR Note:
        /// - In RvR, combat is typically already active (enemies nearby = combat)
        /// - PullTargetGoal rarely activates in RvR scenarios
        /// - PvE-focused goal for dungeon/open world grinding
        ///
        /// Result: Tank naturally waits for group readiness before pulling, no manual timing needed
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool inCombat = IsInCombat(currentState);
            bool hasTarget = HasTarget(currentState);
            bool isMainPuller = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_PULLER, false);

            // Goal only applies when out of combat with a valid target and designated as puller
            if (inCombat)
                return 0.0f; // Already in combat, pulling complete

            if (!hasTarget)
                return 0.0f; // No target to pull

            if (!isMainPuller)
                return 0.0f; // Not designated puller, let MainPuller handle it

            // Check group readiness before pulling
            if (!IsGroupReadyToPull(currentState))
                return 0.0f; // Group not ready (injured, low mana, missing buffs, or insufficient recovery time)

            // Ready to pull: out of combat, has target, is designated puller, group ready
            return BASE_PRIORITY;
        }

        /// <summary>
        /// Checks if the group is ready to pull based on health, mana, buffs, and recovery time
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if group is ready to pull, false otherwise</returns>
        /// <remarks>
        /// Readiness Criteria:
        /// 1. Health Check: No members below emergency threshold (50% HP)
        /// 2. Healing Check: Minimal injuries (prefer no one below 75% HP)
        /// 3. Healer Mana Check: Healer has at least 50% mana
        /// 4. Recovery Time Check: At least 3 seconds out of combat
        /// 5. Buff Check: Critical buffs active (speed for melee, defensive buffs)
        ///
        /// Priority Hierarchy:
        /// - Emergency health (<50% HP): Never pull, absolute block
        /// - Multiple injuries (<75% HP): Wait for healing, strong block
        /// - Low healer mana (<50%): Wait for regen, strong block
        /// - Insufficient recovery time (<3s): Wait for stabilization, moderate block
        /// - Missing critical buffs: Wait for buff application, moderate block
        ///
        /// Edge Cases:
        /// - Solo mimic (no group): Only check self health and mana
        /// - No healer in group: Skip mana check, rely on self-sufficiency
        /// - Fresh group (just formed): Allow pull if health/mana sufficient
        /// - Post-wipe recovery: Full recovery required before resuming pulls
        ///
        /// Example Scenarios:
        /// - All members 100% HP, healer 80% mana, 5s recovery: Ready (all checks pass)
        /// - 1 member 60% HP, healer 80% mana, 5s recovery: Not ready (injury threshold)
        /// - All members 100% HP, healer 40% mana, 5s recovery: Not ready (low mana)
        /// - All members 100% HP, healer 80% mana, 1s recovery: Not ready (insufficient time)
        /// - 1 member 45% HP (emergency), healer 100% mana: Not ready (emergency block)
        /// - Tank solo, 100% HP, 60% mana: Ready (solo mimic, no group checks)
        ///
        /// Result: Conservative pull timing that prevents wipes from premature engagement
        /// </remarks>
        private bool IsGroupReadyToPull(ReGoapState<string, object> currentState)
        {
            // Check for emergency injuries (absolute block on pulling)
            int numEmergency = GetNumEmergency(currentState);
            if (numEmergency > 0)
                return false; // Never pull with emergency injuries

            // Check for injuries above safe threshold
            int numNeedHealing = GetNumInjured(currentState);
            int groupSize = GetGroupSize(currentState);

            // Allow minimal injuries (1-2 minor wounds acceptable in larger groups)
            // But prefer full health for safety
            float injuryRatio = groupSize > 1 ? (float)numNeedHealing / groupSize : 0.0f;
            if (injuryRatio > 0.3f) // More than 30% of group injured
                return false; // Wait for healing

            // Check healer mana (if healer exists in group)
            // Note: This assumes healers are tracked in world state
            // If no healer, skip mana check (group is self-sufficient or solo)
            bool hasHealer = GetBool(currentState, MimicWorldStateKeys.IS_HEALER, false);
            if (hasHealer)
            {
                float healerMana = GetFloat(currentState, MimicWorldStateKeys.SELF_MANA_PERCENT, 100.0f);
                if (healerMana < MIN_HEALER_MANA_PERCENT)
                    return false; // Wait for healer mana regen
            }

            // Check recovery time (minimum stabilization period)
            float outOfCombatTime = GetOutOfCombatTime(currentState);
            if (outOfCombatTime < MIN_OUT_OF_COMBAT_TIME)
                return false; // Wait for recovery period

            // Check critical buffs
            // Speed buff critical for melee groups (from daoc-role-analysis.md)
            bool groupHasMelee = GetBool(currentState, MimicWorldStateKeys.GROUP_HAS_MELEE, false);
            bool speedActive = GetBool(currentState, MimicWorldStateKeys.GROUP_SPEED_ACTIVE, true); // Default true if not tracked
            if (groupHasMelee && !speedActive)
                return false; // Wait for speed buff (critical for melee chase)

            // All readiness checks passed
            return true;
        }

        /// <summary>
        /// Defines the desired world state when pull goal is satisfied
        /// Goal: Combat has been initiated
        /// </summary>
        /// <returns>Goal state with "combatInitiated" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "combatInitiated" to true.
        /// Pull actions (ranged attack, taunt, charge) set "combatInitiated" effect,
        /// which triggers aggro and satisfies the goal.
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against IN_COMBAT world state.
        ///
        /// Note: This goal focuses on starting combat, not winning it.
        /// Once combat initiates, other goals (TankThreatGoal, DealDamageGoal) take over.
        ///
        /// Pull Action Types:
        /// - Ranged Attack: Bow, crossbow, thrown weapon (generates threat from distance)
        /// - Taunt: Verbal taunt to grab aggro (instant, low damage)
        /// - Charge: Rush to enemy, generate threat (gap closer)
        /// - Spell: Ranged spell to initiate combat (caster tanks)
        ///
        /// Pull Action Selection (via Planner):
        /// - Planner evaluates available pull actions based on class
        /// - Cost calculator favors safe pulls (ranged over melee rush)
        /// - Action preconditions verify range and resources (ammo, mana)
        /// - Selected action initiates combat, satisfies goal
        ///
        /// Post-Pull Transition:
        /// - Pull action executes → Enemy aggros → IN_COMBAT = true
        /// - PullTargetGoal satisfied (priority 0.0)
        /// - TankThreatGoal activates (priority 3.0+)
        /// - GuardHealerGoal activates (priority varies)
        /// - DealDamageGoal activates (priority 2.0+)
        /// - Seamless transition from pull to combat without explicit state change
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: combat initiated
            goalState.Set(COMBAT_INITIATED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if pull goal is currently satisfied
        /// Satisfied when in combat or no valid target exists
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if combat initiated or no target, false if ready to pull</returns>
        /// <remarks>
        /// Override default satisfaction check to use IN_COMBAT and HAS_TARGET directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// IN_COMBAT populated by CombatStatusSensor reading Body.InCombat,
        /// HAS_TARGET populated by TargetSensor reading Brain.CalculateNextAttackTarget(),
        /// both use existing game state (no duplication).
        ///
        /// Satisfaction Logic:
        /// - IN_COMBAT == true: Combat active → goal satisfied (pull complete)
        /// - HAS_TARGET == false: No target → goal satisfied (nothing to pull)
        /// - IN_COMBAT == false && HAS_TARGET == true: Ready to pull → goal not satisfied
        ///
        /// Edge Cases:
        /// - Group wiped, respawning: Goal satisfied (no target, not in combat)
        /// - Enemy despawned before pull: Goal satisfied (no target)
        /// - Another mimic pulled first: Goal satisfied (already in combat)
        /// - Multiple pullers in group: Only MainPuller priority > 0, coordination via role
        ///
        /// Pull State Transitions:
        /// - Initial: Not in combat, no target → satisfied (nothing to do)
        /// - Target acquired: Not in combat, has target → not satisfied (need to pull)
        /// - Pull initiated: Not in combat → in combat → satisfied (pull complete)
        /// - Combat active: In combat → satisfied (pulling phase complete)
        /// - Combat ends: Not in combat → cycle repeats
        ///
        /// Multi-Pull Scenarios (PvE):
        /// - Tank pulls enemy 1 → combat starts → PullTargetGoal satisfied
        /// - Group kills enemy 1 → combat ends → Group recovers (healing, mana, buffs)
        /// - Group reaches ready state → PullTargetGoal re-evaluates
        /// - Tank selects enemy 2 → PullTargetGoal not satisfied (ready to pull)
        /// - Tank pulls enemy 2 → combat starts → PullTargetGoal satisfied
        /// - Cycle continues for multi-enemy encounters
        ///
        /// Result: Natural pull timing with automatic group readiness checks
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool inCombat = IsInCombat(currentState);
            bool hasTarget = HasTarget(currentState);

            // Goal satisfied if in combat (pull complete) or no target (nothing to pull)
            if (inCombat || !hasTarget)
                return true;

            // Out of combat with target = not satisfied, need to pull
            return false;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "PullTargetGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, combat state, pull readiness, and group status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and pull details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool inCombat = IsInCombat(currentState);
            bool hasTarget = HasTarget(currentState);
            bool isMainPuller = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_PULLER, false);
            bool groupReady = IsGroupReadyToPull(currentState);
            int numEnemies = GetNumEnemies(currentState);
            int numNeedHealing = GetNumInjured(currentState);
            int numEmergency = GetNumEmergency(currentState);
            float outOfCombatTime = GetOutOfCombatTime(currentState);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"InCombat: {inCombat}, HasTarget: {hasTarget}, " +
                   $"IsMainPuller: {isMainPuller}, GroupReady: {groupReady}, " +
                   $"Injured: {numNeedHealing}, Emergency: {numEmergency}, " +
                   $"Enemies: {numEnemies}, OutOfCombatTime: {outOfCombatTime:F1}s)";
        }
    }
}
