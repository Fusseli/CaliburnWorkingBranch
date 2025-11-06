using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using System.Collections.Generic;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads group coordination flags from MimicGroup
    /// Tracks which spells/abilities are already being cast by group members to prevent duplicates
    /// Thin wrapper - performs zero logic, only reads existing coordination flag properties
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "Sensors are thin wrappers that read existing game state from MimicNPC.Body and MimicBrain properties"
    ///
    /// World State Keys Populated:
    /// - ALREADY_CAST_INSTANT_HEAL: Group member already cast instant heal this tick (prevent duplicate instant heals)
    /// - ALREADY_CASTING_HOT: Group member already casting heal over time (prevent HoT spam)
    /// - ALREADY_CASTING_REGEN: Group member already casting regen buff (prevent regen spam)
    /// - ALREADY_CASTING_CURE_MEZZ: Group member already casting cure mezz (prevent duplicate cure mezz)
    /// - ALREADY_CASTING_CURE_DISEASE: Group member already casting cure disease (prevent duplicate cure disease)
    /// - ALREADY_CASTING_CURE_POISON: Group member already casting cure poison (prevent duplicate cure poison)
    /// - CC_TARGETS: Shared list of crowd controlled enemies (prevent breaking mezz)
    ///
    /// Integration Points:
    /// - MimicGroup.AlreadyCastInstantHeal: Instant heal coordination flag (MimicGroup.cs:154)
    /// - MimicGroup.AlreadyCastingHoT: HoT coordination flag (MimicGroup.cs:156)
    /// - MimicGroup.AlreadyCastingRegen: Regen coordination flag (MimicGroup.cs:158)
    /// - MimicGroup.AlreadyCastingCureMezz: Cure mezz coordination flag (MimicGroup.cs:160)
    /// - MimicGroup.AlreadyCastingCureDisease: Cure disease coordination flag (MimicGroup.cs:162)
    /// - MimicGroup.AlreadyCastingCurePoison: Cure poison coordination flag (MimicGroup.cs:164)
    /// - MimicGroup.CCTargets: Shared CC target list (MimicGroup.cs:23)
    ///
    /// DAoC-Specific Coordination Context:
    /// Group coordination is critical in DAoC to prevent:
    /// 1. **Heal Spam**: Multiple healers casting same spell on same target (wastes mana/time)
    /// 2. **Mezz Breaking**: Damaging already-mezzed targets (breaks 60s mezz instantly)
    /// 3. **Cure Spam**: Multiple mimics curing same condition (waste of casting time)
    ///
    /// These flags are set by MimicBrain.CheckGroupHealth() which runs once per think tick
    /// and analyzes what group members are currently casting.
    ///
    /// Action Usage Examples:
    /// - HealAction: Precondition checks ALREADY_CAST_INSTANT_HEAL to prevent duplicate instant heals
    ///   if (worldState.Get("alreadyCastInstantHeal") == true) return false; // Someone already healing
    ///
    /// - HoTAction: Precondition checks ALREADY_CASTING_HOT to prevent HoT spam
    ///   if (worldState.Get("alreadyCastingHoT") == true) return false; // HoT already active
    ///
    /// - MezzAction: Precondition checks CC_TARGETS to avoid mezzing already-controlled enemies
    ///   var ccTargets = worldState.Get<List<GameLiving>>("ccTargets");
    ///   if (ccTargets.Contains(target)) return false; // Already controlled
    ///
    /// - DamageAction: Precondition checks CC_TARGETS to avoid breaking mezz
    ///   var ccTargets = worldState.Get<List<GameLiving>>("ccTargets");
    ///   if (ccTargets.Contains(target)) return false; // Don't break mezz!
    ///
    /// Reference: design.md "Component 3: Sensor Framework - GroupCoordinationSensor"
    /// Requirements: 2.2 (Shared Sensor Framework), 2.5 (Cache reuse), 8.1 (Heal coordination), 8.2 (CC coordination)
    /// Code Reuse: MimicGroup coordination properties (existing, see MimicGroup.cs:154-164, 23)
    /// </remarks>
    public class GroupCoordinationSensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with current group coordination flags from MimicGroup
        /// Direct property reads from existing coordination flags - zero logic or calculation
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads from existing flags set by CheckGroupHealth()
        /// Thread Safety: Uses MimicGroup.HealLock for thread-safe access to coordination flags
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Actions use coordination flags to prevent conflicts:
        ///
        /// - HealAction: If ALREADY_CAST_INSTANT_HEAL is true, skip instant heal
        ///   (another healer already cast instant heal this tick)
        ///
        /// - HoTAction: If ALREADY_CASTING_HOT is true, skip heal over time
        ///   (another healer already casting HoT, prevent spam)
        ///
        /// - CureMezzAction: If ALREADY_CASTING_CURE_MEZZ is true, skip cure mezz
        ///   (another group member already curing mezz)
        ///
        /// - DamageAction: If target is in CC_TARGETS, skip damage
        ///   (target is mezzed/rooted, don't break it!)
        ///
        /// Error Handling (from design.md):
        /// - Multiple healers coordinate via flags (Requirement 8.1)
        ///   Only first healer to check casts instant heal, others skip
        ///
        /// - CC coordination prevents mezz breaking (Requirement 8.2)
        ///   All DPS mimics check CC_TARGETS before attacking
        ///   Mezz breaks on ANY damage in DAoC (critical mechanic)
        ///
        /// - If not in group, all flags set to false (safe defaults - no coordination needed)
        ///
        /// Cache Reuse (Requirement 2.5):
        /// - CheckGroupHealth() runs in MimicBrain, sets all coordination flags
        /// - This sensor simply reads the flags, no recalculation
        /// - Prevents duplicate iteration over group members
        ///
        /// DAoC Heal Coordination Mechanic (Requirement 8.1):
        /// In DAoC group content, multiple healers must coordinate to avoid:
        /// - Overhealing: Two healers cast big heal on same target (one wasted)
        /// - Heal sniping: Instant heals land while big heal is casting (waste cast time)
        /// - HoT spam: Multiple HoTs on same target (doesn't stack in DAoC)
        ///
        /// This sensor enables organic coordination:
        /// - First healer to check casts instant heal, sets flag
        /// - Other healers see flag, skip instant heal, choose different action
        /// - HoT coordination prevents stacking (only one HoT per target)
        ///
        /// DAoC CC Coordination Mechanic (Requirement 8.2):
        /// In DAoC, mezz (crowd control) breaks on ANY damage:
        /// - Mezz duration: Up to 60 seconds (very powerful)
        /// - Breaks instantly: Any damage, any source breaks mezz
        /// - AOE danger: Area damage can break multiple mezzes
        ///
        /// This sensor enables mezz protection:
        /// - MainCC adds mezzed enemies to CCTargets list
        /// - All DPS mimics check list before attacking
        /// - Cost calculator adds 100x penalty for damaging mezzed targets
        /// - Prevents accidental mezz breaking (catastrophic in DAoC)
        ///
        /// See daoc-role-analysis.md "Mezz Breaking Prevention" for full context.
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before accessing group
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetDefaultCoordinationValues();
                return;
            }

            // Check if mimic is in a valid MimicGroup
            if (!IsInGroup())
            {
                // Not in group - set safe default values (no coordination needed)
                SetDefaultCoordinationValues();
                return;
            }

            // Get MimicGroup instance
            var mimicGroup = GetMimicGroup();
            if (mimicGroup == null)
            {
                // Group exists but not a MimicGroup - set safe defaults
                SetDefaultCoordinationValues();
                return;
            }

            // Thread-safe access to coordination flags
            // MimicGroup.HealLock protects CheckGroupHealth() coordination flags
            lock (mimicGroup.HealLock)
            {
                // Direct property reads from existing coordination flags
                // CheckGroupHealth() already ran in MimicBrain - we just read the results

                // Heal coordination flags (Requirement 8.1: Group Coordination Preservation)
                // Prevent duplicate healing casts from multiple healers
                SetBool(MimicWorldStateKeys.ALREADY_CAST_INSTANT_HEAL, mimicGroup.AlreadyCastInstantHeal);
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_HOT, mimicGroup.AlreadyCastingHoT);
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_REGEN, mimicGroup.AlreadyCastingRegen);

                // Cure coordination flags (Requirement 8.1: Group Coordination Preservation)
                // Prevent duplicate cure casts from multiple healers
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_MEZZ, mimicGroup.AlreadyCastingCureMezz);
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_DISEASE, mimicGroup.AlreadyCastingCureDisease);
                SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_POISON, mimicGroup.AlreadyCastingCurePoison);
            }

            // CC coordination (Requirement 8.2: CC coordination)
            // Read shared CC targets list (prevent mezz breaking)
            // Note: CCTargets list is publicly accessible, no lock needed for read-only access
            // List is modified by MainCC when applying crowd control
            var ccTargets = new List<GameLiving>(mimicGroup.CCTargets);
            SetObject("ccTargets", ccTargets);

            // Calculate number of controlled adds for CC goal priority
            int numControlledAdds = ccTargets.Count;
            SetInt("numControlledAdds", numControlledAdds);
        }

        /// <summary>
        /// Sets safe default values when not in a valid group
        /// All coordination flags set to false, CC targets empty
        /// </summary>
        private void SetDefaultCoordinationValues()
        {
            // Heal coordination flags
            SetBool(MimicWorldStateKeys.ALREADY_CAST_INSTANT_HEAL, false);
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_HOT, false);
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_REGEN, false);

            // Cure coordination flags
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_MEZZ, false);
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_DISEASE, false);
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_CURE_POISON, false);

            // CC coordination
            SetObject("ccTargets", new List<GameLiving>());
            SetInt("numControlledAdds", 0);
        }

        /// <summary>
        /// Gets debug information showing current group coordination state
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

            // Read current coordination flags
            bool instantHeal = mimicGroup.AlreadyCastInstantHeal;
            bool hot = mimicGroup.AlreadyCastingHoT;
            bool regen = mimicGroup.AlreadyCastingRegen;
            bool cureMezz = mimicGroup.AlreadyCastingCureMezz;
            bool cureDisease = mimicGroup.AlreadyCastingCureDisease;
            bool curePoison = mimicGroup.AlreadyCastingCurePoison;
            int ccCount = mimicGroup.CCTargets.Count;

            // Build flag string (show only active flags)
            var activeFlags = new List<string>();
            if (instantHeal) activeFlags.Add("InstantHeal");
            if (hot) activeFlags.Add("HoT");
            if (regen) activeFlags.Add("Regen");
            if (cureMezz) activeFlags.Add("CureMezz");
            if (cureDisease) activeFlags.Add("CureDisease");
            if (curePoison) activeFlags.Add("CurePoison");

            string flagString = activeFlags.Count > 0 ? string.Join(", ", activeFlags) : "None";

            return $"{GetType().Name} (Active: {flagString}, CCTargets: {ccCount})";
        }
    }
}
