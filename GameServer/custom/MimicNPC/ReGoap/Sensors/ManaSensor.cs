using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads mana state directly from MimicNPC.Body properties
    /// Thin wrapper - performs zero logic, only reads existing mana properties
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "ManaSensor reads directly from Body properties - no calculation, no duplication"
    ///
    /// World State Keys Populated:
    /// - SELF_MANA_PERCENT: Body.ManaPercent (0-100)
    /// - SELF_MANA: Body.Mana (current mana)
    /// - SELF_MAX_MANA: Body.MaxMana (maximum mana)
    ///
    /// Example from design.md:
    /// ```csharp
    /// // ManaSensor reads directly from Body properties
    /// public override void UpdateSensor()
    /// {
    ///     // Direct property reads - no calculation
    ///     worldState.Set("selfManaPercent", _body.ManaPercent);
    ///     worldState.Set("selfMana", _body.Mana);
    ///     worldState.Set("selfMaxMana", _body.MaxMana);
    /// }
    /// ```
    ///
    /// Integration Points:
    /// - Body.Mana: Current mana points (existing property)
    /// - Body.MaxMana: Maximum mana points (existing property)
    /// - Body.ManaPercent: Mana percentage 0-100 (existing property)
    ///
    /// Reference: design.md "ManaSensor - Reads Body.Mana* properties"
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates)
    /// Code Reuse: MimicNPC.Body mana properties (existing)
    /// </remarks>
    public class ManaSensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with current mana values from Body properties
        /// Direct property reads only - no calculations or logic
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads, no computation overhead
        /// Thread Safety: Body properties are thread-safe for reads
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to calculate priority:
        /// - DPSGoal: Checks SELF_MANA to determine if high-cost spells available
        /// - HealerGoal: Monitors SELF_MANA_PERCENT to avoid running out during emergencies
        /// - DefensiveGoal: Uses SELF_MANA to decide between mana-free and mana-costing abilities
        ///
        /// Actions use these values for preconditions:
        /// - CastSpellAction: Precondition requires SELF_MANA >= spell.PowerCost
        /// - UseAbilityAction: Some abilities require minimum mana threshold
        ///
        /// Error Handling (from design.md "Scenario 4: Out of Mana"):
        /// - ManaSensor reads Body.Mana, updates world state
        /// - Action precondition check fails (mana >= spell.PowerCost)
        /// - Trigger replanning
        /// - Planner excludes high-cost actions, prefers melee/free abilities
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before reading properties
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetFloat(MimicWorldStateKeys.SELF_MANA_PERCENT, 0f);
                SetInt(MimicWorldStateKeys.SELF_MANA, 0);
                SetInt(MimicWorldStateKeys.SELF_MAX_MANA, 0);
                return;
            }

            // Direct property reads from existing game state - zero logic
            // Body.ManaPercent: Returns byte (0-100), Body maintains this calculation
            SetFloat(MimicWorldStateKeys.SELF_MANA_PERCENT, _body.ManaPercent);

            // Body.Mana: Returns int, current mana points
            SetInt(MimicWorldStateKeys.SELF_MANA, _body.Mana);

            // Body.MaxMana: Returns int, maximum mana points (level + buffs + items)
            SetInt(MimicWorldStateKeys.SELF_MAX_MANA, _body.MaxMana);
        }

        /// <summary>
        /// Gets debug information showing current mana state
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid())
                return $"{GetType().Name} (Body Invalid)";

            return $"{GetType().Name} (Mana: {_body.Mana}/{_body.MaxMana} = {_body.ManaPercent}%)";
        }
    }
}
