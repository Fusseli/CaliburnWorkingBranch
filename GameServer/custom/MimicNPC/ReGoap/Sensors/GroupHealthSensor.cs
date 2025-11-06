using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using System.Linq;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads group health state from MimicGroup cached results
    /// Tracks group injury levels, healing needs, cure needs, and coordination flags
    /// Thin wrapper - performs zero logic, only reads existing CheckGroupHealth() cached results
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "GroupHealthSensor reads directly from MimicGroup cached results - no calculation, no duplication"
    ///
    /// World State Keys Populated:
    /// - GROUP_SIZE: Group.MemberCount
    /// - GROUP_HEALTH_DEFICIT: MimicGroup.AmountToHeal (total HP missing)
    /// - NUM_EMERGENCY_HEALING: MimicGroup.NumNeedEmergencyHealing (<50% HP)
    /// - NUM_NEED_HEALING: MimicGroup.NumNeedHealing (<75% HP)
    /// - NUM_CRITICAL_HEALTH: Count of members <25% HP (calculated from group members)
    /// - AVG_HEALTH_DEFICIT_PERCENT: Average deficit % across injured members
    /// - MEMBER_TO_HEAL: MimicGroup.MemberToHeal (most injured member)
    /// - MEMBER_TO_CURE_MEZZ: MimicGroup.MemberToCureMezz (mezzed member)
    /// - NUM_NEED_CURE_DISEASE: MimicGroup.NumNeedCureDisease
    /// - NUM_NEED_CURE_POISON: MimicGroup.NumNeedCurePoison
    /// - ALREADY_CAST_INSTANT_HEAL: MimicGroup.AlreadyCastInstantHeal (coordination flag)
    /// - ALREADY_CASTING_HOT: MimicGroup.AlreadyCastingHoT (coordination flag)
    /// - ALREADY_CASTING_REGEN: MimicGroup.AlreadyCastingRegen (coordination flag)
    /// - ALREADY_CASTING_CURE_MEZZ: MimicGroup.AlreadyCastingCureMezz (coordination flag)
    ///
    /// Example from design.md:
    /// ```csharp
    /// // GroupHealthSensor reads directly from MimicGroup cached results
    /// public override void UpdateSensor()
    /// {
    ///     var group = _body.Group?.MimicGroup;
    ///     if (group != null)
    ///     {
    ///         // Direct property reads from existing cached results
    ///         // CheckGroupHealth() already ran - we just read the results
    ///         worldState.Set("groupHealthDeficit", group.AmountToHeal);
    ///         worldState.Set("numEmergencyHealing", group.NumNeedEmergencyHealing);
    ///         worldState.Set("numNeedHealing", group.NumNeedHealing);
    ///         worldState.Set("memberToHeal", group.MemberToHeal);
    ///     }
    /// }
    /// ```
    ///
    /// Integration Points:
    /// - Body.Group.MimicGroup: Group coordination instance (existing property)
    /// - MimicGroup.CheckGroupHealth(): Method that calculates and caches all health data (existing)
    /// - MimicGroup.AmountToHeal, NumNeedEmergencyHealing, etc.: Cached results (existing properties)
    /// - MimicGroup.AlreadyCastInstantHeal, etc.: Coordination flags (existing properties)
    ///
    /// Reference: design.md "GroupHealthSensor - Reads MimicGroup cached results"
    /// Requirements: 2.2 (Group coordination), 2.5 (Cache reuse), 8.5 (Group coordination preservation)
    /// Code Reuse: MimicGroup.CheckGroupHealth() cached results (existing)
    /// DAoC Mechanics: Healer coordination, emergency response, cure priorities (see daoc-role-analysis.md)
    /// </remarks>
    public class GroupHealthSensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with current group health information from MimicGroup cached results
        /// Direct property reads from existing cached data - zero logic or calculation
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads from already-calculated cache (CheckGroupHealth runs separately)
        /// Thread Safety: Uses MimicGroup.HealLock for thread-safe access to cached results
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to calculate priority:
        /// - HealerGoal: Uses NUM_EMERGENCY_HEALING and AVG_HEALTH_DEFICIT_PERCENT for priority
        ///   Priority = 5.0 * (numInjured / groupSize) * avgDeficit
        ///   ×10 if any member <50% HP (emergency)
        ///   ×50 if any member <25% HP (critical)
        /// - DefensiveGoal: Checks GROUP_HEALTH_DEFICIT to determine if healing needed
        /// - CureGoal: Uses NUM_NEED_CURE_DISEASE and NUM_NEED_CURE_POISON for priority
        ///
        /// Actions use these values for preconditions:
        /// - HealAction: Precondition requires MEMBER_TO_HEAL != null
        /// - EmergencyHealAction: Precondition requires NUM_EMERGENCY_HEALING > 0
        /// - CureDiseaseAction: Precondition requires NUM_NEED_CURE_DISEASE > 0
        ///
        /// Coordination flags prevent duplicate casts (Requirement 8.5):
        /// - ALREADY_CAST_INSTANT_HEAL: Prevents multiple instant heals on same target
        /// - ALREADY_CASTING_HOT: Prevents HoT spam
        /// - ALREADY_CASTING_CURE_MEZZ: Prevents multiple cure mezz casts
        ///
        /// Error Handling (from design.md):
        /// - Healer emergency response: NUM_CRITICAL_HEALTH > 0 triggers 50x priority
        /// - Multiple healers coordinate via flags (only one instant heal per tick)
        /// - If not in group, all values set to safe defaults (no healing needed)
        ///
        /// Cache Reuse (Requirement 2.5):
        /// - CheckGroupHealth() runs separately in MimicBrain, calculates once per think tick
        /// - This sensor simply reads the cached results, no recalculation
        /// - Prevents duplicate expensive calculations (iterating group members, checking HP)
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before accessing group
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetDefaultGroupValues();
                return;
            }

            // Check if mimic is in a valid MimicGroup
            if (!IsInGroup())
            {
                // Not in group - set safe default values (no healing needed)
                SetDefaultGroupValues();
                return;
            }

            // Get MimicGroup instance
            var mimicGroup = GetMimicGroup();
            if (mimicGroup == null)
            {
                // Group exists but not a MimicGroup - set safe defaults
                SetDefaultGroupValues();
                return;
            }

            // Thread-safe access to cached group health data
            // MimicGroup.HealLock protects CheckGroupHealth() results
            lock (mimicGroup.HealLock)
            {
                // Direct property reads from existing cached results
                // CheckGroupHealth() already ran - we just read the results

                // Basic group info
                int groupSize = _body.Group?.MemberCount ?? 0;
                SetInt(MimicWorldStateKeys.GROUP_SIZE, groupSize);

                // Health deficit and injury counts (from cached CheckGroupHealth results)
                // These are calculated once per think tick by CheckGroupHealth()
                SetInt(MimicWorldStateKeys.GROUP_HEALTH_DEFICIT, mimicGroup.AmountToHeal);
                SetInt(MimicWorldStateKeys.NUM_EMERGENCY_HEALING, mimicGroup.NumNeedEmergencyHealing);
                SetInt(MimicWorldStateKeys.NUM_NEED_HEALING, mimicGroup.NumNeedHealing);

                // Calculate critical health count (<25% HP) from current group members
                // This is a lightweight calculation using existing group member references
                int numCriticalHealth = CountCriticalHealthMembers();
                SetInt(MimicWorldStateKeys.NUM_CRITICAL_HEALTH, numCriticalHealth);

                // Calculate average health deficit percentage
                // Uses cached NumInjured and AmountToHeal from CheckGroupHealth
                float avgHealthDeficitPercent = CalculateAvgHealthDeficitPercent(mimicGroup, groupSize);
                SetFloat(MimicWorldStateKeys.AVG_HEALTH_DEFICIT_PERCENT, avgHealthDeficitPercent);

                // Most injured member (from cached CheckGroupHealth results)
                SetObject(MimicWorldStateKeys.MEMBER_TO_HEAL, mimicGroup.MemberToHeal);

                // Cure targets (from cached CheckGroupHealth results)
                SetObject(MimicWorldStateKeys.MEMBER_TO_CURE_MEZZ, mimicGroup.MemberToCureMezz);
                SetInt(MimicWorldStateKeys.NUM_NEED_CURE_DISEASE, mimicGroup.NumNeedCureDisease);
                SetInt(MimicWorldStateKeys.NUM_NEED_CURE_POISON, mimicGroup.NumNeedCurePoison);

                // Coordination flags (Requirement 8.5: Group Coordination Preservation)
                // These flags prevent duplicate casts from multiple healers
                SetBool(MimicWorldStateKeys.ALREADY_CAST_INSTANT_HEAL, mimicGroup.AlreadyCastInstantHeal);
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_HOT, mimicGroup.AlreadyCastingHoT);
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_REGEN, mimicGroup.AlreadyCastingRegen);
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_MEZZ, mimicGroup.AlreadyCastingCureMezz);
            }
        }

        /// <summary>
        /// Sets safe default values when not in a valid group
        /// All healing needs set to zero, coordination flags set to false
        /// </summary>
        private void SetDefaultGroupValues()
        {
            SetInt(MimicWorldStateKeys.GROUP_SIZE, 0);
            SetInt(MimicWorldStateKeys.GROUP_HEALTH_DEFICIT, 0);
            SetInt(MimicWorldStateKeys.NUM_EMERGENCY_HEALING, 0);
            SetInt(MimicWorldStateKeys.NUM_NEED_HEALING, 0);
            SetInt(MimicWorldStateKeys.NUM_CRITICAL_HEALTH, 0);
            SetFloat(MimicWorldStateKeys.AVG_HEALTH_DEFICIT_PERCENT, 0f);
            SetObject(MimicWorldStateKeys.MEMBER_TO_HEAL, null);
            SetObject(MimicWorldStateKeys.MEMBER_TO_CURE_MEZZ, null);
            SetInt(MimicWorldStateKeys.NUM_NEED_CURE_DISEASE, 0);
            SetInt(MimicWorldStateKeys.NUM_NEED_CURE_POISON, 0);
            SetBool(MimicWorldStateKeys.ALREADY_CAST_INSTANT_HEAL, false);
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_HOT, false);
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_REGEN, false);
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_MEZZ, false);
        }

        /// <summary>
        /// Counts group members below 25% health (critical emergency)
        /// Lightweight calculation using existing group member references
        /// </summary>
        private int CountCriticalHealthMembers()
        {
            if (_body.Group == null)
                return 0;

            // Simple LINQ query on existing group members
            // Uses existing HealthPercent property from GameLiving
            return _body.Group.GetMembersInTheGroup()
                .Count(member => member is GameLiving living &&
                                 living.IsAlive &&
                                 living.HealthPercent < 25);
        }

        /// <summary>
        /// Calculates average health deficit percentage across injured members
        /// Uses cached NumInjured and AmountToHeal from CheckGroupHealth results
        /// </summary>
        /// <remarks>
        /// Example: If 2 members are injured with deficits of 500 HP and 300 HP,
        /// and their max healths are 2000 HP and 1000 HP:
        /// - Total deficit = 800 HP
        /// - Average deficit % = ((500/2000 + 300/1000) / 2) * 100 = 27.5%
        ///
        /// This is used for healer goal priority calculation:
        /// Priority = 5.0 * (numInjured / groupSize) * avgDeficit
        /// </remarks>
        private float CalculateAvgHealthDeficitPercent(MimicGroup mimicGroup, int groupSize)
        {
            if (mimicGroup.NumInjured <= 0 || groupSize <= 0)
                return 0f;

            // Calculate average deficit percentage from injured members only
            // This is more accurate than total deficit / total max HP
            float totalDeficitPercent = 0f;
            int injuredCount = 0;

            foreach (var member in _body.Group.GetMembersInTheGroup())
            {
                if (member is GameLiving living && living.IsAlive)
                {
                    // Calculate deficit percentage for this member
                    if (living.Health < living.MaxHealth)
                    {
                        float deficit = living.MaxHealth - living.Health;
                        float deficitPercent = (deficit / (float)living.MaxHealth) * 100f;
                        totalDeficitPercent += deficitPercent;
                        injuredCount++;
                    }
                }
            }

            if (injuredCount <= 0)
                return 0f;

            return totalDeficitPercent / injuredCount;
        }

        /// <summary>
        /// Gets debug information showing current group health state
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

            int groupSize = _body.Group?.MemberCount ?? 0;
            int deficit = mimicGroup.AmountToHeal;
            int emergency = mimicGroup.NumNeedEmergencyHealing;
            int needHealing = mimicGroup.NumNeedHealing;
            var memberToHeal = mimicGroup.MemberToHeal;

            return $"{GetType().Name} (Size: {groupSize}, Deficit: {deficit}, " +
                   $"Emergency: {emergency}, NeedHeal: {needHealing}, " +
                   $"Target: {memberToHeal?.Name ?? "None"})";
        }
    }
}
