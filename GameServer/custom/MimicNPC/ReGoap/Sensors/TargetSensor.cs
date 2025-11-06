using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads target information using MimicBrain.CalculateNextAttackTarget()
    /// Provides target validity, health, distance, and range information for action preconditions
    /// Thin wrapper - leverages existing target selection algorithm, performs minimal calculation
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "TargetSensor uses existing target selection algorithm - Brain.CalculateNextAttackTarget()"
    ///
    /// World State Keys Populated:
    /// - CURRENT_TARGET: GameObject from Brain.CalculateNextAttackTarget()
    /// - HAS_TARGET: Boolean (currentTarget != null)
    /// - TARGET_HEALTH_PERCENT: target.HealthPercent (0-100)
    /// - TARGET_DISTANCE: Body.GetDistanceTo(target) in units
    /// - TARGET_IN_MELEE_RANGE: Body.IsWithinRadius(target, 200)
    /// - TARGET_IN_SPELL_RANGE: Body.IsWithinRadius(target, 1500)
    ///
    /// Example from design.md:
    /// ```csharp
    /// // TargetSensor reads from Brain.CalculateNextAttackTarget() and Body.TargetObject
    /// public override void UpdateSensor()
    /// {
    ///     // Use existing target selection algorithm
    ///     var target = _brain.CalculateNextAttackTarget();
    ///     worldState.Set("currentTarget", target);
    ///     worldState.Set("hasTarget", target != null);
    ///
    ///     if (target != null)
    ///     {
    ///         // Direct property reads from target
    ///         worldState.Set("targetHealthPercent", target.HealthPercent);
    ///         worldState.Set("targetDistance", _body.GetDistanceTo(target));
    ///         worldState.Set("targetInMeleeRange", _body.IsWithinRadius(target, 200));
    ///         worldState.Set("targetInSpellRange", _body.IsWithinRadius(target, 1500));
    ///     }
    /// }
    /// ```
    ///
    /// Integration Points:
    /// - Brain.CalculateNextAttackTarget(): Existing target selection algorithm (reused)
    /// - Body.TargetObject: Current target reference (existing property)
    /// - Body.GetDistanceTo(target): Distance calculation (existing method)
    /// - Body.IsWithinRadius(target, range): Range check (existing method)
    /// - target.HealthPercent: Target health percentage (existing property)
    ///
    /// Reference: design.md "TargetSensor - Reads Brain.CalculateNextAttackTarget() and Body.TargetObject"
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates)
    /// Code Reuse: MimicBrain.CalculateNextAttackTarget() (existing algorithm)
    /// </remarks>
    public class TargetSensor : MimicSensor
    {
        /// <summary>
        /// Standard melee range in units (200 units = typical melee attack range in DAoC)
        /// </summary>
        private const int MELEE_RANGE = 200;

        /// <summary>
        /// Standard spell range in units (1500 units = typical caster range in DAoC)
        /// </summary>
        private const int SPELL_RANGE = 1500;

        /// <summary>
        /// Updates world state with current target information using Brain's target selection
        /// Leverages existing CalculateNextAttackTarget() algorithm - no logic duplication
        /// </summary>
        /// <remarks>
        /// Performance: Calls existing target selection once, then simple property reads and range checks
        /// Thread Safety: Brain.CalculateNextAttackTarget() and Body methods are thread-safe for reads
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Actions use these values for preconditions:
        /// - CastSpellAction: Precondition requires HAS_TARGET = true and TARGET_IN_SPELL_RANGE = true
        /// - MeleeAttackAction: Precondition requires HAS_TARGET = true and TARGET_IN_MELEE_RANGE = true
        /// - All offensive actions: Check HAS_TARGET before attempting to execute
        ///
        /// Goals use these values to determine relevance:
        /// - DPSGoal: Returns priority 0.0 if HAS_TARGET = false (no target to damage)
        /// - AssistTrainGoal: Uses CURRENT_TARGET to compare with Main Assist's target
        ///
        /// Error Handling (from design.md "Scenario 3: Target Dies Mid-Plan"):
        /// - TargetSensor reads Brain.CalculateNextAttackTarget(), detects hasTarget = false
        /// - Next action precondition check fails (requires hasTarget = true)
        /// - Trigger replanning with updated world state
        /// - DPS goal selects new target from aggro list
        /// - Result: Smooth target switching when current target dies
        ///
        /// DAoC Range Constants:
        /// - MELEE_RANGE (200): Standard melee attack range
        /// - SPELL_RANGE (1500): Standard caster range (varies by spell, 1500 is typical)
        /// - These constants match DAoC 1.65 game mechanics
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body and Brain references before calling methods
            if (!IsBodyValid() || !IsBrainValid())
            {
                // Set safe default values if references are invalid
                SetObject(MimicWorldStateKeys.CURRENT_TARGET, null);
                SetBool(MimicWorldStateKeys.HAS_TARGET, false);
                SetFloat(MimicWorldStateKeys.TARGET_HEALTH_PERCENT, 0f);
                SetInt(MimicWorldStateKeys.TARGET_DISTANCE, int.MaxValue);
                SetBool(MimicWorldStateKeys.TARGET_IN_MELEE_RANGE, false);
                SetBool(MimicWorldStateKeys.TARGET_IN_SPELL_RANGE, false);
                return;
            }

            // Read current target from Body (set by Brain's target selection logic)
            // Brain already handles target selection in Think() - we just read the result
            // This is the ONLY place we access target - zero duplication
            // Brain considers: aggro list, MainAssist's target, target validity, etc.
            GameObject target = _body.TargetObject;

            // Store target reference and basic validity
            SetObject(MimicWorldStateKeys.CURRENT_TARGET, target);
            SetBool(MimicWorldStateKeys.HAS_TARGET, target != null);

            // If we have a valid target, populate detailed target information
            if (target != null)
            {
                // Cast to GameLiving to access health and other living entity properties
                var livingTarget = target as GameLiving;

                if (livingTarget != null)
                {
                    // Direct property read - target maintains its own health percentage
                    SetFloat(MimicWorldStateKeys.TARGET_HEALTH_PERCENT, livingTarget.HealthPercent);
                }
                else
                {
                    // Non-living targets (doors, siege weapons, etc.) - assume full health
                    SetFloat(MimicWorldStateKeys.TARGET_HEALTH_PERCENT, 100f);
                }

                // Use existing distance calculation method from Body
                // GetDistanceTo() returns units (integer distance)
                int distance = _body.GetDistanceTo(target);
                SetInt(MimicWorldStateKeys.TARGET_DISTANCE, distance);

                // Use existing range check methods from Body
                // IsWithinRadius() handles 3D distance calculation and checks radius
                bool inMeleeRange = _body.IsWithinRadius(target, MELEE_RANGE);
                SetBool(MimicWorldStateKeys.TARGET_IN_MELEE_RANGE, inMeleeRange);

                bool inSpellRange = _body.IsWithinRadius(target, SPELL_RANGE);
                SetBool(MimicWorldStateKeys.TARGET_IN_SPELL_RANGE, inSpellRange);
            }
            else
            {
                // No target - set safe default values for target-specific properties
                SetFloat(MimicWorldStateKeys.TARGET_HEALTH_PERCENT, 0f);
                SetInt(MimicWorldStateKeys.TARGET_DISTANCE, int.MaxValue);
                SetBool(MimicWorldStateKeys.TARGET_IN_MELEE_RANGE, false);
                SetBool(MimicWorldStateKeys.TARGET_IN_SPELL_RANGE, false);
            }
        }

        /// <summary>
        /// Gets debug information showing current target state
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid() || !IsBrainValid())
                return $"{GetType().Name} (Body/Brain Invalid)";

            var target = _body.TargetObject;

            if (target == null)
                return $"{GetType().Name} (No Target)";

            var livingTarget = target as GameLiving;
            int distance = _body.GetDistanceTo(target);
            bool inMelee = _body.IsWithinRadius(target, MELEE_RANGE);
            bool inSpell = _body.IsWithinRadius(target, SPELL_RANGE);

            string healthInfo = livingTarget != null ? $"{livingTarget.HealthPercent}% HP" : "N/A";

            return $"{GetType().Name} (Target: {target.Name}, {healthInfo}, " +
                   $"Dist: {distance}, Melee: {inMelee}, Spell: {inSpell})";
        }
    }
}
