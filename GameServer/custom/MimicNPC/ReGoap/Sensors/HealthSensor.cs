using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads health state directly from MimicNPC.Body properties
    /// Thin wrapper - performs zero logic, only reads existing health properties
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "HealthSensor reads directly from Body properties - no calculation, no duplication"
    ///
    /// World State Keys Populated:
    /// - SELF_HEALTH_PERCENT: Body.HealthPercent (0-100)
    /// - SELF_HEALTH: Body.Health (current HP)
    /// - SELF_MAX_HEALTH: Body.MaxHealth (maximum HP)
    ///
    /// Example from design.md:
    /// ```csharp
    /// // HealthSensor reads directly from Body properties
    /// public override void UpdateSensor()
    /// {
    ///     // Direct property reads from existing game state
    ///     worldState.Set("selfHealthPercent", _body.HealthPercent);
    ///     worldState.Set("selfHealth", _body.Health);
    ///     worldState.Set("selfMaxHealth", _body.MaxHealth);
    /// }
    /// ```
    ///
    /// Integration Points:
    /// - Body.Health: Current health points (existing property)
    /// - Body.MaxHealth: Maximum health points (existing property)
    /// - Body.HealthPercent: Health percentage 0-100 (existing property)
    ///
    /// Reference: design.md "HealthSensor - Reads Body.Health* properties"
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates)
    /// Code Reuse: MimicNPC.Body health properties (existing)
    /// </remarks>
    public class HealthSensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with current health values from Body properties
        /// Direct property reads only - no calculations or logic
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads, no computation overhead
        /// Thread Safety: Body properties are thread-safe for reads
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to calculate priority:
        /// - HealerGoal: Checks SELF_HEALTH_PERCENT to prioritize emergency heals (<50%)
        /// - DefensiveGoal: Checks SELF_HEALTH to determine if defensive abilities needed
        /// - TankGoal: Monitors SELF_HEALTH_PERCENT for defensive stance switching
        ///
        /// Actions use these values for preconditions:
        /// - HealSelfAction: Precondition requires SELF_HEALTH_PERCENT < 75
        /// - DefensiveAbilityAction: Precondition requires SELF_HEALTH_PERCENT < 60
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before reading properties
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetFloat(MimicWorldStateKeys.SELF_HEALTH_PERCENT, 0f);
                SetInt(MimicWorldStateKeys.SELF_HEALTH, 0);
                SetInt(MimicWorldStateKeys.SELF_MAX_HEALTH, 0);
                return;
            }

            // Direct property reads from existing game state - zero logic
            // Body.HealthPercent: Returns byte (0-100), Body maintains this calculation
            SetFloat(MimicWorldStateKeys.SELF_HEALTH_PERCENT, _body.HealthPercent);

            // Body.Health: Returns int, current health points
            SetInt(MimicWorldStateKeys.SELF_HEALTH, _body.Health);

            // Body.MaxHealth: Returns int, maximum health points (level + buffs)
            SetInt(MimicWorldStateKeys.SELF_MAX_HEALTH, _body.MaxHealth);
        }

        /// <summary>
        /// Gets debug information showing current health state
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid())
                return $"{GetType().Name} (Body Invalid)";

            return $"{GetType().Name} (HP: {_body.Health}/{_body.MaxHealth} = {_body.HealthPercent}%)";
        }
    }
}
