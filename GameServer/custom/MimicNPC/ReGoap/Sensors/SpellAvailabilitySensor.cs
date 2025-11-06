using DOL.GS.Scripts.ReGoap;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads spell availability directly from MimicNPC.Body properties
    /// Tracks which spell types are available for casting based on class/spec
    /// Thin wrapper - performs zero logic, reads existing spell lists from MimicBrain.CheckSpells
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "Sensors are thin wrappers that read existing game state from MimicNPC.Body and MimicBrain properties"
    ///
    /// Code Reuse (from task requirements):
    /// Leverages existing MimicBrain.CheckSpells logic by reading the same spell properties:
    /// - Body.CanCastCrowdControlSpells: Boolean check for CC spell availability
    /// - Body.CrowdControlSpells: List of available crowd control spells
    /// - Body.CanCastBolts: Boolean check for bolt spell availability
    /// - Body.BoltSpells: List of available bolt spells
    /// - Body.CanCastInstantCrowdControlSpells: Boolean check for instant CC availability
    /// - Body.InstantCrowdControlSpells: List of instant CC spells
    /// - Body.HealBig, HealEfficient, HealGroup, etc.: Individual healing spell properties
    /// - Body.CureMezz, CureDisease, CurePoison: Individual cure spell properties
    ///
    /// World State Keys Populated:
    /// - CAN_CAST_CROWD_CONTROL: Body.CanCastCrowdControlSpells
    /// - CAN_CAST_INSTANT_CROWD_CONTROL: Body.CanCastInstantCrowdControlSpells
    /// - CAN_CAST_BOLTS: Body.CanCastBolts
    /// - CAN_CAST_HEALING: Has any healing spell (HealBig/HealEfficient/HealGroup/etc.)
    /// - CAN_CAST_CURE_MEZZ: Body.CureMezz != null
    /// - CAN_CAST_CURE_DISEASE: Body.CureDisease != null
    /// - CAN_CAST_CURE_POISON: Body.CurePoison != null
    /// - AVAILABLE_CROWD_CONTROL_SPELLS: Body.CrowdControlSpells (list)
    /// - AVAILABLE_BOLT_SPELLS: Body.BoltSpells (list)
    /// - AVAILABLE_HEALING_SPELLS: List of all available healing spells
    /// - NUM_CROWD_CONTROL_SPELLS: Count of CC spells
    /// - NUM_BOLT_SPELLS: Count of bolt spells
    /// - NUM_HEALING_SPELLS: Count of healing spells
    ///
    /// Integration Points (from MimicBrain.CheckSpells at line 1444):
    /// - MimicBrain.CheckSpells uses these same properties to determine spell casting
    /// - Defensive spells checked via Body.CanCastMiscSpells (line 1481)
    /// - Offensive spells checked via Body.CanCastHarmfulSpells (line 1566)
    /// - CC spells checked via MimicBody.CanCastCrowdControlSpells (line 1458, 1531)
    /// - Bolt spells checked via MimicBody.CanCastBolts (line 1555)
    /// - Instant spells checked via Body.CanCastInstantHarmfulSpells (line 1504)
    ///
    /// Usage by Goals/Actions:
    /// - HealerGoal: Checks CAN_CAST_HEALING to determine if healing is possible
    /// - CCGoal: Checks CAN_CAST_CROWD_CONTROL to determine if CC actions available
    /// - DPSGoal: Checks CAN_CAST_BOLTS to determine if bolt damage available
    /// - ActionFactory: Uses AVAILABLE_*_SPELLS lists to generate class-specific actions
    ///
    /// Reference: design.md "Component 2: MimicReGoapAgent - Sensor management"
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates), 4.2 (Action preconditions)
    /// Code Reuse: MimicNPC.Body spell availability properties (existing at MimicNPC.cs:62-85, 1429-1442)
    /// Code Reuse: MimicBrain.CheckSpells logic (existing at MimicBrain.cs:1444-1593)
    /// </remarks>
    public class SpellAvailabilitySensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with spell availability from Body spell properties
        /// Direct property reads - zero calculation or duplication of CheckSpells logic
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads and list access - no iteration or filtering
        /// Thread Safety: Body properties are thread-safe for reads
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to determine action availability:
        /// - HealerGoal: If CAN_CAST_HEALING = false, goal priority becomes 0 (cannot satisfy)
        /// - CCGoal: If NUM_CROWD_CONTROL_SPELLS = 0, goal cannot be activated
        /// - DPSGoal: Prefers bolts if CAN_CAST_BOLTS = true
        ///
        /// Actions use these values for preconditions:
        /// - CastSpellAction: Checks if specific spell exists in AVAILABLE_*_SPELLS lists
        /// - ActionFactory: Enumerates AVAILABLE_HEALING_SPELLS to generate CastSpellAction instances
        ///
        /// Code Reuse from MimicBrain.CheckSpells (line 1444-1593):
        /// - Line 1458: if (MimicBody.CanCastCrowdControlSpells) - same property read
        /// - Line 1462: foreach (Spell spell in MimicBody.CrowdControlSpells) - same list access
        /// - Line 1504: if (Body.CanCastInstantHarmfulSpells) - same pattern for instant spells
        /// - Line 1555: if (MimicBody.CanCastBolts && spellsToCast.Count < 1) - same bolt check
        /// - Line 1566: if (Body.CanCastHarmfulSpells) - same harmful spell check
        ///
        /// This sensor provides the same data CheckSpells uses, but in ReGoap world state format
        /// for goal/action decision-making instead of imperative spell selection.
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before reading properties
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetBool(MimicWorldStateKeys.CAN_CAST_CROWD_CONTROL, false);
                SetBool(MimicWorldStateKeys.CAN_CAST_INSTANT_CROWD_CONTROL, false);
                SetBool(MimicWorldStateKeys.CAN_CAST_BOLTS, false);
                SetBool(MimicWorldStateKeys.CAN_CAST_HEALING, false);
                SetBool(MimicWorldStateKeys.CAN_CAST_CURE_MEZZ, false);
                SetBool(MimicWorldStateKeys.CAN_CAST_CURE_DISEASE, false);
                SetBool(MimicWorldStateKeys.CAN_CAST_CURE_POISON, false);
                SetInt(MimicWorldStateKeys.NUM_CROWD_CONTROL_SPELLS, 0);
                SetInt(MimicWorldStateKeys.NUM_BOLT_SPELLS, 0);
                SetInt(MimicWorldStateKeys.NUM_HEALING_SPELLS, 0);
                SetObject(MimicWorldStateKeys.AVAILABLE_CROWD_CONTROL_SPELLS, null);
                SetObject(MimicWorldStateKeys.AVAILABLE_BOLT_SPELLS, null);
                SetObject(MimicWorldStateKeys.AVAILABLE_HEALING_SPELLS, null);
                return;
            }

            // Direct property reads from existing game state - zero duplication
            // Same properties used by MimicBrain.CheckSpells (line 1458, 1504, 1531, 1555)

            #region Crowd Control Spell Availability

            // Body.CanCastCrowdControlSpells: Boolean property (MimicNPC.cs:63)
            // Used by CheckSpells at line 1458: if (MimicBody.CanCastCrowdControlSpells)
            bool canCastCC = _body.CanCastCrowdControlSpells;
            SetBool(MimicWorldStateKeys.CAN_CAST_CROWD_CONTROL, canCastCC);

            // Body.CanCastInstantCrowdControlSpells: Boolean property (MimicNPC.cs:62)
            // Used by CheckSpells for instant CC filtering
            bool canCastInstantCC = _body.CanCastInstantCrowdControlSpells;
            SetBool(MimicWorldStateKeys.CAN_CAST_INSTANT_CROWD_CONTROL, canCastInstantCC);

            // Body.CrowdControlSpells: List<Spell> property (MimicNPC.cs:80)
            // Used by CheckSpells at line 1462: foreach (Spell spell in MimicBody.CrowdControlSpells)
            // and at line 1545: foreach (Spell spell in MimicBody.CrowdControlSpells)
            var ccSpells = _body.CrowdControlSpells;
            int numCCSpells = ccSpells?.Count ?? 0;
            SetInt(MimicWorldStateKeys.NUM_CROWD_CONTROL_SPELLS, numCCSpells);
            SetObject(MimicWorldStateKeys.AVAILABLE_CROWD_CONTROL_SPELLS, ccSpells);

            #endregion

            #region Bolt Spell Availability

            // Body.CanCastBolts: Boolean property (MimicNPC.cs:64)
            // Used by CheckSpells at line 1555: if (MimicBody.CanCastBolts && spellsToCast.Count < 1)
            bool canCastBolts = _body.CanCastBolts;
            SetBool(MimicWorldStateKeys.CAN_CAST_BOLTS, canCastBolts);

            // Body.BoltSpells: List<Spell> property (MimicNPC.cs:85)
            // Used by CheckSpells at line 1557: foreach (Spell spell in MimicBody.BoltSpells)
            var boltSpells = _body.BoltSpells;
            int numBoltSpells = boltSpells?.Count ?? 0;
            SetInt(MimicWorldStateKeys.NUM_BOLT_SPELLS, numBoltSpells);
            SetObject(MimicWorldStateKeys.AVAILABLE_BOLT_SPELLS, boltSpells);

            #endregion

            #region Healing Spell Availability

            // Collect all available healing spells from Body properties
            // These properties are populated during MimicNPC initialization (MimicNPC.cs:1429-1437)
            // Used by CheckHeals() method which CheckSpells calls at line 1453
            var healingSpells = new List<Spell>();

            // Add all non-null healing spells to the list
            // Property reads from MimicNPC.cs:1429-1437
            if (_body.HealBig != null) healingSpells.Add(_body.HealBig);
            if (_body.HealEfficient != null) healingSpells.Add(_body.HealEfficient);
            if (_body.HealGroup != null) healingSpells.Add(_body.HealGroup);
            if (_body.HealInstant != null) healingSpells.Add(_body.HealInstant);
            if (_body.HealInstantGroup != null) healingSpells.Add(_body.HealInstantGroup);
            if (_body.HealOverTime != null) healingSpells.Add(_body.HealOverTime);
            if (_body.HealOverTimeGroup != null) healingSpells.Add(_body.HealOverTimeGroup);
            if (_body.HealOverTimeInstant != null) healingSpells.Add(_body.HealOverTimeInstant);
            if (_body.HealOverTimeInstantGroup != null) healingSpells.Add(_body.HealOverTimeInstantGroup);

            bool canCastHealing = healingSpells.Count > 0;
            SetBool(MimicWorldStateKeys.CAN_CAST_HEALING, canCastHealing);
            SetInt(MimicWorldStateKeys.NUM_HEALING_SPELLS, healingSpells.Count);
            SetObject(MimicWorldStateKeys.AVAILABLE_HEALING_SPELLS, healingSpells);

            #endregion

            #region Cure Spell Availability

            // Body.CureMezz: Spell property (MimicNPC.cs:1438)
            // Used by CheckHeals for cure mezz logic
            bool canCureMezz = _body.CureMezz != null;
            SetBool(MimicWorldStateKeys.CAN_CAST_CURE_MEZZ, canCureMezz);

            // Body.CureDisease: Spell property (MimicNPC.cs:1439)
            // Body.CureDiseaseGroup: Spell property (MimicNPC.cs:1440)
            // Used by CheckHeals at lines 1997-2010 for disease cure logic
            bool canCureDisease = _body.CureDisease != null || _body.CureDiseaseGroup != null;
            SetBool(MimicWorldStateKeys.CAN_CAST_CURE_DISEASE, canCureDisease);

            // Body.CurePoison: Spell property (MimicNPC.cs:1441)
            // Body.CurePoisonGroup: Spell property (MimicNPC.cs:1442)
            // Used by CheckHeals at lines 2011-2026 for poison cure logic
            bool canCurePoison = _body.CurePoison != null || _body.CurePoisonGroup != null;
            SetBool(MimicWorldStateKeys.CAN_CAST_CURE_POISON, canCurePoison);

            #endregion

            #region Offensive and Defensive Spell Availability

            // These use the generic spell lists from GameNPC base class
            // Body.CanCastHarmfulSpells: GameNPC property used at CheckSpells line 1566
            // Body.CanCastMiscSpells: GameNPC property used at CheckSpells line 1481
            // Body.CanCastInstantHarmfulSpells: GameNPC property used at CheckSpells line 1504
            // Body.CanCastInstantMiscSpells: GameNPC property used at CheckSpells line 1513

            bool canCastOffensive = _body.CanCastHarmfulSpells || _body.CanCastInstantHarmfulSpells || canCastBolts;
            SetBool(MimicWorldStateKeys.CAN_CAST_OFFENSIVE_SPELLS, canCastOffensive);

            bool canCastDefensive = _body.CanCastMiscSpells || _body.CanCastInstantMiscSpells;
            SetBool(MimicWorldStateKeys.CAN_CAST_DEFENSIVE_SPELLS, canCastDefensive);

            #endregion
        }

        /// <summary>
        /// Gets debug information showing spell availability
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid())
                return $"{GetType().Name} (Body Invalid)";

            int numCC = _body.CrowdControlSpells?.Count ?? 0;
            int numBolts = _body.BoltSpells?.Count ?? 0;
            int numHeals = 0;

            // Count healing spells
            if (_body.HealBig != null) numHeals++;
            if (_body.HealEfficient != null) numHeals++;
            if (_body.HealGroup != null) numHeals++;
            if (_body.HealInstant != null) numHeals++;
            if (_body.HealInstantGroup != null) numHeals++;
            if (_body.HealOverTime != null) numHeals++;
            if (_body.HealOverTimeGroup != null) numHeals++;
            if (_body.HealOverTimeInstant != null) numHeals++;
            if (_body.HealOverTimeInstantGroup != null) numHeals++;

            return $"{GetType().Name} (CC: {numCC}, Bolts: {numBolts}, Heals: {numHeals}, " +
                   $"CureMezz: {_body.CureMezz != null}, CureDisease: {_body.CureDisease != null || _body.CureDiseaseGroup != null}, " +
                   $"CurePoison: {_body.CurePoison != null || _body.CurePoisonGroup != null})";
        }
    }
}
