using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads combat status directly from MimicNPC.Body properties
    /// Tracks combat state, casting status, control effects, and out-of-combat time
    /// Thin wrapper - performs minimal logic (only time tracking), reads existing properties
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "CombatStatusSensor reads Body.InCombat, Body.IsCasting, etc."
    ///
    /// World State Keys Populated:
    /// - IN_COMBAT: Body.InCombat (currently in combat)
    /// - IS_CASTING: Body.IsCasting (currently casting a spell)
    /// - IS_ATTACKING: Body.IsAttacking (currently performing melee attack)
    /// - IS_STUNNED: Body.IsStunned (cannot act - stunned)
    /// - IS_MEZZED: Body.IsMezzed (crowd controlled - mezzed)
    /// - CAN_CAST: Derived from !IsCasting && !IsStunned && !IsMezzed
    /// - OUT_OF_COMBAT_TIME: Seconds since last combat (tracked via GameLoop.GameLoopTime)
    ///
    /// Example from design.md:
    /// ```csharp
    /// // CombatStatusSensor reads directly from Body properties
    /// public override void UpdateSensor()
    /// {
    ///     // Direct property reads from Body
    ///     bool inCombat = _body.InCombat;
    ///     worldState.Set("inCombat", inCombat);
    ///     worldState.Set("isCasting", _body.IsCasting);
    ///     worldState.Set("isAttacking", _body.IsAttacking);
    ///     worldState.Set("isStunned", _body.IsStunned);
    ///     worldState.Set("isMezzed", _body.IsMezzed);
    ///     worldState.Set("canCast", !_body.IsCasting && !_body.IsStunned && !_body.IsMezzed);
    ///
    ///     // Track out of combat time
    ///     if (inCombat)
    ///         _lastCombatTime = GameLoop.GameLoopTime;
    ///
    ///     float outOfCombatTime = (GameLoop.GameLoopTime - _lastCombatTime) / 1000f;
    ///     worldState.Set("outOfCombatTime", outOfCombatTime);
    /// }
    /// ```
    ///
    /// Integration Points:
    /// - Body.InCombat: Currently in combat (existing property)
    /// - Body.IsCasting: Currently casting a spell (existing property)
    /// - Body.IsAttacking: Currently performing melee attack (existing property)
    /// - Body.IsStunned: Cannot act - stunned (existing property)
    /// - Body.IsMezzed: Crowd controlled - mezzed (existing property)
    /// - GameLoop.GameLoopTime: Current game time in milliseconds (existing)
    ///
    /// Reference: design.md "CombatStatusSensor - Reads Body.InCombat, Body.IsCasting, etc."
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates)
    /// Code Reuse: MimicNPC.Body combat status properties (existing)
    /// </remarks>
    public class CombatStatusSensor : MimicSensor
    {
        /// <summary>
        /// Tracks the last time combat was active (in GameLoop time milliseconds)
        /// Used to calculate OUT_OF_COMBAT_TIME for defensive goal priority
        /// </summary>
        private long _lastCombatTime = 0;

        /// <summary>
        /// Updates world state with current combat status from Body properties
        /// Direct property reads with minimal time tracking logic
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads plus one time calculation
        /// Thread Safety: Body properties and GameLoop.GameLoopTime are thread-safe for reads
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to calculate priority:
        /// - DefensiveGoal: Checks OUT_OF_COMBAT_TIME > 10s to activate buff maintenance (×3 priority)
        /// - HealerGoal: Uses IN_COMBAT to determine if combat healing or maintenance mode
        /// - PullerGoal: Checks IN_COMBAT to determine if ready to pull (priority 1.0 if false, 0.0 if true)
        ///
        /// Actions use these values for preconditions:
        /// - CastSpellAction: Precondition requires CAN_CAST = true (not casting, stunned, or mezzed)
        /// - MeleeAttackAction: Checks IS_ATTACKING to avoid redundant attack commands
        ///
        /// Error Handling (from design.md "Scenario 1: Spell Cast Interrupted"):
        /// - CombatStatusSensor updates world state (canCast = false when stunned/mezzed)
        /// - Action precondition check fails
        /// - Trigger replanning with updated state
        /// - Mimic adapts to control effects naturally
        ///
        /// Out-of-Combat Time Usage:
        /// - DefensiveGoal uses outOfCombatTime > 10.0f to trigger buff maintenance
        /// - Prevents constant buff spam during combat
        /// - Enables natural buff renewal between pulls (organic behavior)
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before reading properties
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetBool(MimicWorldStateKeys.IN_COMBAT, false);
                SetBool(MimicWorldStateKeys.IS_CASTING, false);
                SetBool(MimicWorldStateKeys.IS_ATTACKING, false);
                SetBool(MimicWorldStateKeys.IS_STUNNED, false);
                SetBool(MimicWorldStateKeys.IS_MEZZED, false);
                SetBool(MimicWorldStateKeys.CAN_CAST, false);
                SetFloat(MimicWorldStateKeys.OUT_OF_COMBAT_TIME, 0f);
                return;
            }

            // Direct property reads from existing game state - zero duplication
            // Body.InCombat: Boolean property indicating active combat state
            bool inCombat = _body.InCombat;
            SetBool(MimicWorldStateKeys.IN_COMBAT, inCombat);

            // Body.IsCasting: Boolean property indicating active spell casting
            SetBool(MimicWorldStateKeys.IS_CASTING, _body.IsCasting);

            // Body.IsAttacking: Boolean property indicating active melee attack
            SetBool(MimicWorldStateKeys.IS_ATTACKING, _body.IsAttacking);

            // Body.IsStunned: Boolean property indicating stunned control effect
            SetBool(MimicWorldStateKeys.IS_STUNNED, _body.IsStunned);

            // Body.IsMezzed: Boolean property indicating mezzed control effect
            SetBool(MimicWorldStateKeys.IS_MEZZED, _body.IsMezzed);

            // CAN_CAST: Derived state - can cast if not already casting and not under control effects
            // This is a simple boolean logic combination, not complex calculation
            bool canCast = !_body.IsCasting && !_body.IsStunned && !_body.IsMezzed;
            SetBool(MimicWorldStateKeys.CAN_CAST, canCast);

            // Track out of combat time for defensive goal priority
            // Update _lastCombatTime when combat is active
            if (inCombat)
            {
                _lastCombatTime = GameLoop.GameLoopTime;
            }

            // Calculate seconds since last combat (convert from milliseconds)
            // GameLoop.GameLoopTime is in milliseconds, divide by 1000 for seconds
            float outOfCombatTime = (_lastCombatTime > 0)
                ? (GameLoop.GameLoopTime - _lastCombatTime) / 1000f
                : 0f;

            SetFloat(MimicWorldStateKeys.OUT_OF_COMBAT_TIME, outOfCombatTime);
        }

        /// <summary>
        /// Gets debug information showing current combat status
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid())
                return $"{GetType().Name} (Body Invalid)";

            float outOfCombatTime = (_lastCombatTime > 0)
                ? (GameLoop.GameLoopTime - _lastCombatTime) / 1000f
                : 0f;

            return $"{GetType().Name} (Combat: {_body.InCombat}, Casting: {_body.IsCasting}, " +
                   $"Stunned: {_body.IsStunned}, Mezzed: {_body.IsMezzed}, " +
                   $"OutOfCombat: {outOfCombatTime:F1}s)";
        }
    }
}
