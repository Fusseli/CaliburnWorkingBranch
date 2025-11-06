using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads group role assignments from MimicGroup
    /// Tracks which mimic has MainTank, MainAssist, MainCC, MainPuller, MainLeader roles
    /// Thin wrapper - performs zero logic, only reads existing role properties
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "Sensors are thin wrappers that read existing game state from MimicNPC.Body and MimicBrain properties"
    ///
    /// World State Keys Populated:
    /// - IS_MAIN_TANK: This mimic is the MainTank (MimicGroup.MainTank == Body)
    /// - IS_MAIN_ASSIST: This mimic is the MainAssist (MimicGroup.MainAssist == Body)
    /// - IS_MAIN_CC: This mimic is the MainCC (MimicGroup.MainCC == Body)
    /// - IS_MAIN_PULLER: This mimic is the MainPuller (MimicGroup.MainPuller == Body)
    /// - MAIN_ASSIST: Reference to the MainAssist player (MimicGroup.MainAssist)
    /// - MAIN_ASSIST_TARGET: MainAssist's current target (for assist train coordination)
    /// - TARGET_MATCHES_MAIN_ASSIST: Current target matches MainAssist's target (DAoC assist train)
    ///
    /// Integration Points:
    /// - MimicGroup.MainTank: Tank role assignment (existing property from MimicGroup.cs:17)
    /// - MimicGroup.MainAssist: Assist role assignment (existing property from MimicGroup.cs:16)
    /// - MimicGroup.MainCC: Crowd control role assignment (existing property from MimicGroup.cs:18)
    /// - MimicGroup.MainPuller: Puller role assignment (existing property from MimicGroup.cs:19)
    /// - MimicGroup.MainLeader: Leader role assignment (existing property from MimicGroup.cs:15)
    ///
    /// DAoC-Specific Role Context (from daoc-role-analysis.md):
    /// - MainTank: Responsible for threat generation, guarding healer, peeling adds
    /// - MainAssist: Target caller for assist train (all DPS follows assist's target)
    /// - MainCC: Primary crowd control (mezz/root adds), interrupt enemy casters
    /// - MainPuller: Initiates combat with ranged pull (PvE)
    /// - MainLeader: Group commander (not heavily used in current implementation)
    ///
    /// Goal Usage Examples:
    /// - TankGoal: Checks IS_MAIN_TANK to determine if this mimic should tank
    /// - GuardHealerGoal: Uses IS_MAIN_TANK to activate guard responsibility
    /// - AssistTrainGoal: Uses MAIN_ASSIST_TARGET and TARGET_MATCHES_MAIN_ASSIST for coordinated damage
    /// - PullerGoal: Checks IS_MAIN_PULLER to determine if this mimic should initiate combat
    /// - MezzPriorityGoal: Checks IS_MAIN_CC to determine primary CC responsibility
    ///
    /// Reference: design.md "Component 3: Sensor Framework - GroupRoleSensor"
    /// Requirements: 2.2 (Shared Sensor Framework), 2.5 (Cache reuse), 3.1 (Role-Based Goal System)
    /// Code Reuse: MimicGroup role properties (existing, see MimicGroup.cs:15-19)
    /// </remarks>
    public class GroupRoleSensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with current group role assignments from MimicGroup
        /// Direct property reads from existing role assignments - zero logic or calculation
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads and equality checks
        /// Thread Safety: MimicGroup role properties are set by group leader commands
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use role flags to determine activation:
        /// - TankGoal: Only activates if IS_MAIN_TANK == true
        ///   Priority = 3.0 + (numEnemiesNotOnTank * 2.0)
        ///   ×3 if losing threat (THREAT_PERCENT_OF_HIGHEST < 0.75)
        ///
        /// - AssistTrainGoal: All DPS mimics check TARGET_MATCHES_MAIN_ASSIST
        ///   Priority = 4.0 if targeting assist's target
        ///   Priority = 4.0 * 0.3 (1.2) if targeting wrong enemy (heavy penalty)
        ///
        /// - PullerGoal: Only activates if IS_MAIN_PULLER == true
        ///   Priority = 1.0 when no combat, 0.0 when in combat
        ///
        /// Actions use role context:
        /// - GuardAction: Precondition requires IS_MAIN_TANK == true
        /// - MarkTargetAction: Only MainAssist marks targets (IS_MAIN_ASSIST == true)
        /// - MezzAction: MainCC gets priority (lower cost when IS_MAIN_CC == true)
        ///
        /// Error Handling:
        /// - If not in group: All role flags set to false (safe defaults)
        /// - If MimicGroup is null: All role flags set to false
        /// - MainAssist target null: TARGET_MATCHES_MAIN_ASSIST = false
        ///
        /// DAoC Assist Train Mechanic (Requirement 3.1):
        /// The assist train is a core DAoC RvR mechanic where all DPS focuses the
        /// MainAssist's target for coordinated burst damage. This sensor provides:
        /// - MAIN_ASSIST: The player calling targets
        /// - MAIN_ASSIST_TARGET: The current kill target
        /// - TARGET_MATCHES_MAIN_ASSIST: Boolean for DPS goal priority calculation
        ///
        /// This enables AssistTrainGoal to heavily penalize off-target damage:
        /// - On-target: Priority = 4.0
        /// - Off-target: Priority = 4.0 * 0.3 = 1.2 (70% penalty)
        ///
        /// See daoc-role-analysis.md "Assist Train Coordination" for full context.
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before accessing group
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetDefaultRoleValues();
                return;
            }

            // Check if mimic is in a valid MimicGroup
            if (!IsInGroup())
            {
                // Not in group - set safe default values (no roles assigned)
                SetDefaultRoleValues();
                return;
            }

            // Get MimicGroup instance
            var mimicGroup = GetMimicGroup();
            if (mimicGroup == null)
            {
                // Group exists but not a MimicGroup - set safe defaults
                SetDefaultRoleValues();
                return;
            }

            // Direct property reads from existing MimicGroup role assignments
            // No calculation or logic - simple equality checks

            // Check if this mimic holds each role (compare Body reference)
            // MimicGroup.MainTank is a GameLiving reference (MimicGroup.cs:17)
            bool isMainTank = mimicGroup.MainTank == _body;
            SetBool(MimicWorldStateKeys.IS_MAIN_TANK, isMainTank);

            // MimicGroup.MainAssist is a GameLiving reference (MimicGroup.cs:16)
            bool isMainAssist = mimicGroup.MainAssist == _body;
            SetBool(MimicWorldStateKeys.IS_MAIN_ASSIST, isMainAssist);

            // MimicGroup.MainCC is a GameLiving reference (MimicGroup.cs:18)
            bool isMainCC = mimicGroup.MainCC == _body;
            SetBool(MimicWorldStateKeys.IS_MAIN_CC, isMainCC);

            // MimicGroup.MainPuller is a GameLiving reference (MimicGroup.cs:19)
            bool isMainPuller = mimicGroup.MainPuller == _body;
            SetBool(MimicWorldStateKeys.IS_MAIN_PULLER, isMainPuller);

            // Store MainAssist reference for assist train coordination
            // Used by DPS mimics to focus the correct target
            SetObject(MimicWorldStateKeys.MAIN_ASSIST, mimicGroup.MainAssist);

            // DAoC Assist Train Coordination (Requirement 3.1)
            // Get MainAssist's current target for coordinated focus fire
            GameObject mainAssistTarget = mimicGroup.MainAssist?.TargetObject;
            SetObject(MimicWorldStateKeys.MAIN_ASSIST_TARGET, mainAssistTarget);

            // Check if this mimic's target matches MainAssist's target
            // Used by AssistTrainGoal for priority calculation (on-target vs off-target)
            bool targetMatchesMainAssist = _body.TargetObject != null &&
                                            _body.TargetObject == mainAssistTarget;
            SetBool(MimicWorldStateKeys.TARGET_MATCHES_MAIN_ASSIST, targetMatchesMainAssist);

            // Note: IS_HEALER is determined by class/spec, not by MimicGroup role
            // It will be set by a separate ClassRoleSensor based on available healing spells
            // (Future implementation - not part of this task)
        }

        /// <summary>
        /// Sets safe default values when not in a valid group
        /// All role assignments set to false, references set to null
        /// </summary>
        private void SetDefaultRoleValues()
        {
            SetBool(MimicWorldStateKeys.IS_MAIN_TANK, false);
            SetBool(MimicWorldStateKeys.IS_MAIN_ASSIST, false);
            SetBool(MimicWorldStateKeys.IS_MAIN_CC, false);
            SetBool(MimicWorldStateKeys.IS_MAIN_PULLER, false);
            SetObject(MimicWorldStateKeys.MAIN_ASSIST, null);
            SetObject(MimicWorldStateKeys.MAIN_ASSIST_TARGET, null);
            SetBool(MimicWorldStateKeys.TARGET_MATCHES_MAIN_ASSIST, false);
        }

        /// <summary>
        /// Gets debug information showing current group role assignments
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid())
                return $"{GetType().Name} (Body Invalid)";

            if (!IsInGroup())
                return $"{GetType().Name} (Not in Group)";

            var mimicGroup = GetMimicGroup();
            if (mimicGroup == null)
                return $"{GetType().Name} (Not a MimicGroup)";

            // Read current role assignments
            bool isMainTank = mimicGroup.MainTank == _body;
            bool isMainAssist = mimicGroup.MainAssist == _body;
            bool isMainCC = mimicGroup.MainCC == _body;
            bool isMainPuller = mimicGroup.MainPuller == _body;

            // Build role string (e.g., "Roles: Tank, Assist")
            var roles = new System.Collections.Generic.List<string>();
            if (isMainTank) roles.Add("Tank");
            if (isMainAssist) roles.Add("Assist");
            if (isMainCC) roles.Add("CC");
            if (isMainPuller) roles.Add("Puller");

            string roleString = roles.Count > 0 ? string.Join(", ", roles) : "None";

            // Show MainAssist's target for assist train context
            var mainAssistTarget = mimicGroup.MainAssist?.TargetObject;
            bool targetMatchesAssist = _body.TargetObject != null && _body.TargetObject == mainAssistTarget;

            return $"{GetType().Name} (Roles: {roleString}, " +
                   $"AssistTarget: {mainAssistTarget?.Name ?? "None"}, " +
                   $"OnTarget: {targetMatchesAssist})";
        }
    }
}
