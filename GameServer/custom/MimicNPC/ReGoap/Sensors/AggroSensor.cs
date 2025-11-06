using DOL.GS.Scripts.ReGoap;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads aggro state directly from MimicBrain.AggroList property
    /// Tracks active threats, threat levels, and target priority information
    /// Thin wrapper - performs minimal logic (LINQ queries on existing collection), reads existing AggroList
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "AggroSensor reads directly from Brain.AggroList - existing concurrent dictionary"
    ///
    /// World State Keys Populated:
    /// - HAS_AGGRO: Boolean (AggroList.Count > 0)
    /// - NUM_ENEMIES: AggroList.Count
    /// - HIGHEST_THREAT_ENEMY: GameLiving with highest EffectiveAggro (LINQ query)
    /// - AGGRO_TARGETS: List of all enemies (AggroList.Keys)
    /// - NUM_UNCONTROLLED_ADDS: Count of unmezzed adds (for CC priority)
    /// - NUM_ENEMIES_NOT_ON_TANK: Count of enemies not targeting tank (for tank threat)
    /// - THREAT_PERCENT_OF_HIGHEST: Tank's threat as percentage (for tank priority)
    ///
    /// Example from design.md:
    /// ```csharp
    /// // AggroSensor reads directly from Brain's aggro list
    /// public override void UpdateSensor()
    /// {
    ///     // Direct property access - existing concurrent dictionary
    ///     var aggroList = _brain.AggroList;
    ///     worldState.Set("hasAggro", aggroList.Count > 0);
    ///     worldState.Set("numEnemies", aggroList.Count);
    ///     worldState.Set("highestThreatEnemy", GetHighestThreatEnemy(aggroList));
    ///     worldState.Set("aggroTargets", aggroList.Keys.ToList());
    /// }
    /// ```
    ///
    /// Integration Points:
    /// - Brain.AggroList: ConcurrentDictionary&lt;GameLiving, AggroAmount&gt; (existing property)
    /// - Brain.HasAggro: Boolean property (existing, derived from AggroList.Count)
    /// - AggroAmount.Effective: Long, effective threat value (existing property)
    /// - Body.Group.MimicGroup.MainTank: Tank reference for threat calculations (existing)
    ///
    /// Reference: design.md "AggroSensor - Reads Brain.AggroList"
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates)
    /// Code Reuse: MimicBrain.AggroList (existing concurrent collection)
    /// DAoC Mechanics: Aggro management, threat generation, tank priority (see daoc-role-analysis.md)
    /// </remarks>
    public class AggroSensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with current aggro information from Brain.AggroList
        /// Direct property reads with minimal LINQ queries on existing collection
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads plus LINQ queries on existing concurrent dictionary
        /// Thread Safety: Brain.AggroList is ConcurrentDictionary, safe for concurrent reads
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to calculate priority:
        /// - TankGoal: Uses NUM_ENEMIES_NOT_ON_TANK to determine threat management priority
        /// - CCGoal: Uses NUM_UNCONTROLLED_ADDS to determine crowd control priority
        /// - DPSGoal: Uses HAS_AGGRO to determine if combat is active (priority 0.0 if no aggro)
        /// - PullerGoal: Uses HAS_AGGRO to determine if already in combat (don't pull during combat)
        ///
        /// Actions use these values for preconditions:
        /// - CastSpellAction: May check AGGRO_TARGETS for valid targets
        /// - MeleeAttackAction: Uses AGGRO_TARGETS to select melee targets
        /// - CCAction: Uses NUM_UNCONTROLLED_ADDS to determine if CC needed
        ///
        /// Error Handling (from design.md "Scenario 3: Target Dies Mid-Plan"):
        /// - AggroSensor updates to reflect removed target from AggroList
        /// - NUM_ENEMIES decreases, HAS_AGGRO may become false
        /// - Trigger replanning with updated aggro state
        /// - DPS goal selects new target from remaining AGGRO_TARGETS
        ///
        /// DAoC Mechanics (from daoc-role-analysis.md):
        /// - Threat Generation: Tank must maintain highest threat to hold aggro
        /// - Add Management: CC must control additional enemies beyond main target
        /// - Assist Train: All DPS focuses Main Assist's target from AGGRO_TARGETS
        /// - Aggro Range: MAX_AGGRO_DISTANCE = 3600 units (DAoC 1.65 standard)
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Brain reference before reading AggroList
            if (!IsBrainValid())
            {
                // Set safe default values if Brain is invalid
                SetBool(MimicWorldStateKeys.HAS_AGGRO, false);
                SetInt(MimicWorldStateKeys.NUM_ENEMIES, 0);
                SetObject(MimicWorldStateKeys.HIGHEST_THREAT_ENEMY, null);
                SetObject(MimicWorldStateKeys.AGGRO_TARGETS, new List<GameLiving>());
                SetInt(MimicWorldStateKeys.NUM_UNCONTROLLED_ADDS, 0);
                SetInt(MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);
                SetFloat(MimicWorldStateKeys.THREAT_PERCENT_OF_HIGHEST, 0f);
                return;
            }

            // Direct property access to existing concurrent dictionary
            // Brain.AggroList is thread-safe ConcurrentDictionary<GameLiving, AggroAmount>
            // This is a simple property read - zero logic duplication
            bool hasAggro = _brain.HasAggro;
            SetBool(MimicWorldStateKeys.HAS_AGGRO, hasAggro);

            if (!hasAggro)
            {
                // No enemies - set safe default values for aggro-specific properties
                SetInt(MimicWorldStateKeys.NUM_ENEMIES, 0);
                SetObject(MimicWorldStateKeys.HIGHEST_THREAT_ENEMY, null);
                SetObject(MimicWorldStateKeys.AGGRO_TARGETS, new List<GameLiving>());
                SetInt(MimicWorldStateKeys.NUM_UNCONTROLLED_ADDS, 0);
                SetInt(MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);
                SetFloat(MimicWorldStateKeys.THREAT_PERCENT_OF_HIGHEST, 0f);
                return;
            }

            // Get aggro list - this is a direct property read of existing collection
            // AggroList is maintained by Brain's existing aggro logic
            var aggroList = GetAggroListSnapshot();

            // Simple count from existing collection
            int numEnemies = aggroList.Count;
            SetInt(MimicWorldStateKeys.NUM_ENEMIES, numEnemies);

            // Simple LINQ query to find highest threat enemy
            // AggroAmount.Effective is calculated by existing Brain aggro logic
            var highestThreatEnemy = GetHighestThreatEnemy(aggroList);
            SetObject(MimicWorldStateKeys.HIGHEST_THREAT_ENEMY, highestThreatEnemy);

            // Extract list of enemies from dictionary keys
            // This is a simple collection transformation
            var aggroTargets = aggroList.Keys.ToList();
            SetObject(MimicWorldStateKeys.AGGRO_TARGETS, aggroTargets);

            // Calculate uncontrolled adds (for CC priority)
            // Uses existing IsMezzed property from GameLiving
            // Current target is not considered an "add" - only additional enemies
            int numUncontrolledAdds = CountUncontrolledAdds(aggroTargets);
            SetInt(MimicWorldStateKeys.NUM_UNCONTROLLED_ADDS, numUncontrolledAdds);

            // Calculate enemies not on tank (for tank threat management)
            // Uses existing TargetObject property from GameLiving
            int numEnemiesNotOnTank = CountEnemiesNotOnTank(aggroTargets);
            SetInt(MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, numEnemiesNotOnTank);

            // Calculate tank's threat as percentage of highest (for tank priority)
            // Uses existing AggroAmount.Effective values from Brain's aggro calculations
            float threatPercentOfHighest = CalculateTankThreatPercent(aggroList);
            SetFloat(MimicWorldStateKeys.THREAT_PERCENT_OF_HIGHEST, threatPercentOfHighest);
        }

        /// <summary>
        /// Gets a snapshot of the current aggro list for thread-safe iteration
        /// AggroList is ConcurrentDictionary, safe to iterate but snapshot ensures consistency
        /// </summary>
        private Dictionary<GameLiving, long> GetAggroListSnapshot()
        {
            var snapshot = new Dictionary<GameLiving, long>();

            // Access Brain's AggroList via reflection or public property
            // Since AggroList is protected, we need to access it through Brain's public interface
            // Brain.HasAggro already confirms the list is not empty

            // Use reflection to access protected AggroList
            var aggroListField = _brain.GetType().GetProperty("AggroList",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);

            if (aggroListField != null)
            {
                var aggroList = aggroListField.GetValue(_brain) as System.Collections.Concurrent.ConcurrentDictionary<GameLiving, object>;

                if (aggroList != null)
                {
                    foreach (var kvp in aggroList)
                    {
                        // Access AggroAmount.Effective property
                        var aggroAmount = kvp.Value;
                        var effectiveProperty = aggroAmount?.GetType().GetProperty("Effective");

                        if (effectiveProperty != null)
                        {
                            var effectiveValue = (long)(effectiveProperty.GetValue(aggroAmount) ?? 0L);
                            snapshot[kvp.Key] = effectiveValue;
                        }
                    }
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Gets the enemy with highest effective threat from aggro list
        /// Simple LINQ query on existing threat calculations
        /// </summary>
        private GameLiving GetHighestThreatEnemy(Dictionary<GameLiving, long> aggroList)
        {
            if (aggroList == null || aggroList.Count == 0)
                return null;

            // Simple LINQ query - finds max by Effective threat value
            // AggroAmount.Effective is calculated by Brain's existing aggro logic
            return aggroList.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
        }

        /// <summary>
        /// Counts uncontrolled adds (not mezzed, not current target)
        /// Used for CC goal priority calculation
        /// </summary>
        private int CountUncontrolledAdds(List<GameLiving> aggroTargets)
        {
            if (!IsBodyValid() || aggroTargets == null)
                return 0;

            var currentTarget = _body.TargetObject as GameLiving;

            // Count enemies that are:
            // 1. Not the current target (adds only)
            // 2. Not mezzed (uncontrolled)
            // Uses existing IsMezzed property from GameLiving
            return aggroTargets.Count(enemy =>
                enemy != currentTarget &&
                enemy != null &&
                enemy.IsAlive &&
                !enemy.IsMezzed);
        }

        /// <summary>
        /// Counts enemies not targeting the tank
        /// Used for tank threat goal priority calculation
        /// </summary>
        private int CountEnemiesNotOnTank(List<GameLiving> aggroTargets)
        {
            if (!IsBodyValid() || aggroTargets == null)
                return 0;

            // Get Main Tank from group
            var mainTank = _body.Group?.MimicGroup?.MainTank;
            if (mainTank == null)
                return 0; // No tank, can't calculate

            // Count enemies not targeting the tank
            // Uses existing TargetObject property from GameLiving
            return aggroTargets.Count(enemy =>
                enemy != null &&
                enemy.IsAlive &&
                enemy.TargetObject != mainTank);
        }

        /// <summary>
        /// Calculates tank's threat as percentage of highest threat
        /// Used for tank threat goal priority (low % = losing aggro)
        /// </summary>
        private float CalculateTankThreatPercent(Dictionary<GameLiving, long> aggroList)
        {
            if (!IsBodyValid() || aggroList == null || aggroList.Count == 0)
                return 0f;

            // Get Main Tank from group
            var mainTank = _body.Group?.MimicGroup?.MainTank;
            if (mainTank == null)
                return 0f; // No tank, can't calculate

            // Find highest threat value in aggro list
            long highestThreat = aggroList.Values.Max();
            if (highestThreat <= 0)
                return 0f;

            // Find tank's current threat on each enemy
            // This is simplified - in real implementation, would need per-enemy threat tracking
            // For now, we use the tank's highest threat value across all enemies
            long tankThreat = 0;

            // Check if tank is even in combat
            if (mainTank.InCombat && mainTank.TargetObject is GameLiving tankTarget)
            {
                // If tank's target is in our aggro list, that's the threat we care about
                if (aggroList.ContainsKey(tankTarget))
                {
                    tankThreat = aggroList[tankTarget];
                }
            }

            // Calculate percentage
            return (float)tankThreat / (float)highestThreat;
        }

        /// <summary>
        /// Gets debug information showing current aggro state
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBrainValid())
                return $"{GetType().Name} (Brain Invalid)";

            bool hasAggro = _brain.HasAggro;

            if (!hasAggro)
                return $"{GetType().Name} (No Aggro)";

            var aggroList = GetAggroListSnapshot();
            int numEnemies = aggroList.Count;
            var highestThreat = GetHighestThreatEnemy(aggroList);

            return $"{GetType().Name} (Enemies: {numEnemies}, " +
                   $"Highest Threat: {highestThreat?.Name ?? "None"})";
        }
    }
}
