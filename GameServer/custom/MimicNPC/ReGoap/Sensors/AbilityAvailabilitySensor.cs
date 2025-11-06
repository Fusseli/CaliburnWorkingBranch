using DOL.GS.Scripts.ReGoap;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads realm ability availability directly from MimicNPC.Body properties
    /// Tracks which realm abilities and special abilities are available for use
    /// Thin wrapper - performs zero logic, reads existing ability system from GameLiving
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "Sensors are thin wrappers that read existing game state from MimicNPC.Body and MimicBrain properties"
    ///
    /// Code Reuse (from task requirements):
    /// Leverages existing ability system by reading the same ability properties:
    /// - Body.GetAllAbilities(): Returns list of all abilities the mimic has
    /// - Body.HasAbility(string keyName): Checks if specific ability exists
    /// - Body.GetAbility(string keyName): Retrieves specific ability instance
    /// - Body.GetAbility<T>(): Generic ability retrieval for typed access
    /// - Body.Abilities: Dictionary of all abilities keyed by KeyName
    ///
    /// World State Keys Populated:
    /// - HAS_QUICKCAST: Body.HasAbility(Abilities.Quickcast)
    /// - HAS_INTERCEPT: Body.HasAbility(Abilities.Intercept)
    /// - HAS_GUARD: Body.HasAbility(Abilities.Guard)
    /// - HAS_PROTECT: Body.HasAbility(Abilities.Protect)
    /// - HAS_BERSERK: Body.HasAbility(Abilities.Berserk)
    /// - HAS_CHARGE: Body.HasAbility(Abilities.ChargeAbility)
    /// - HAS_TRIPLE_WIELD: Body.HasAbility(Abilities.Triple_Wield)
    /// - HAS_DIRTY_TRICKS: Body.HasAbility(Abilities.DirtyTricks)
    /// - HAS_STAG: Body.HasAbility(Abilities.Stag)
    /// - AVAILABLE_ABILITIES: Body.GetAllAbilities() (list)
    /// - NUM_ABILITIES: Count of all abilities
    ///
    /// Integration Points (from MimicBrain.CheckDefensiveAbilities at line 348):
    /// - Line 350: if (Body.Abilities == null || Body.Abilities.Count <= 0) - same check
    /// - Line 353: foreach (Ability ab in Body.GetAllAbilities()) - same enumeration
    /// - Line 357: case Abilities.Intercept - same ability constant
    /// - Line 383: case Abilities.Guard - same ability constant
    /// - Line 388: case Abilities.Protect - same ability constant
    /// - Line 409: case Abilities.Berserk - same ability constant
    /// - Line 424: case Abilities.Stag - same ability constant
    /// - Line 439: case Abilities.Triple_Wield - same ability constant
    /// - Line 454: case Abilities.DirtyTricks - same ability constant
    /// - Line 474: case Abilities.ChargeAbility - same ability constant
    /// - Line 1592: Ability quickCast = Body.GetAbility(Abilities.Quickcast) - same retrieval
    ///
    /// Usage by Goals/Actions:
    /// - GuardHealerGoal: Checks HAS_GUARD to determine if tank can guard
    /// - QuickcastRecoveryGoal: Checks HAS_QUICKCAST to determine if quickcast available
    /// - TankThreatGoal: Checks HAS_INTERCEPT/HAS_PROTECT for defensive abilities
    /// - UseAbilityAction: Uses AVAILABLE_ABILITIES to generate ability actions
    ///
    /// Reference: design.md "Component 2: MimicReGoapAgent - Sensor management"
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates), 4.2 (Action preconditions)
    /// Code Reuse: GameLiving ability system (existing at GameLiving.cs:3788-3903)
    /// Code Reuse: MimicBrain.CheckDefensiveAbilities (existing at MimicBrain.cs:348-454)
    /// </remarks>
    public class AbilityAvailabilitySensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with ability availability from Body ability properties
        /// Direct property reads - zero calculation or duplication of CheckDefensiveAbilities logic
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads and dictionary lookups - no iteration for common checks
        /// Thread Safety: Body.Abilities uses lock (m_lockAbilities) internally for thread-safe access
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to determine action availability:
        /// - GuardHealerGoal: If HAS_GUARD = false, goal cannot be activated
        /// - QuickcastRecoveryGoal: If HAS_QUICKCAST = false, goal priority becomes 0
        /// - TankThreatGoal: Checks HAS_INTERCEPT/HAS_PROTECT for defensive options
        ///
        /// Actions use these values for preconditions:
        /// - UseAbilityAction: Checks if specific ability exists in AVAILABLE_ABILITIES list
        /// - ActionFactory: Enumerates AVAILABLE_ABILITIES to generate UseAbilityAction instances
        ///
        /// Code Reuse from MimicBrain.CheckDefensiveAbilities (line 348-454):
        /// - Line 350: if (Body.Abilities == null || Body.Abilities.Count <= 0) - same validation
        /// - Line 353: foreach (Ability ab in Body.GetAllAbilities()) - same enumeration pattern
        /// - This sensor provides the same ability data CheckDefensiveAbilities uses, but in
        ///   ReGoap world state format for goal/action decision-making instead of imperative
        ///   ability activation.
        ///
        /// Ability Availability vs. Cooldown Status:
        /// - This sensor tracks which abilities the mimic HAS (permanent character abilities)
        /// - Cooldown status is checked separately by actions during precondition validation
        /// - Body.IsAbilityOnCooldown() is called by actions, not sensors (dynamic per-use state)
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before reading properties
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetBool(MimicWorldStateKeys.HAS_QUICKCAST, false);
                SetBool(MimicWorldStateKeys.HAS_INTERCEPT, false);
                SetBool(MimicWorldStateKeys.HAS_GUARD, false);
                SetBool(MimicWorldStateKeys.HAS_PROTECT, false);
                SetBool(MimicWorldStateKeys.HAS_BERSERK, false);
                SetBool(MimicWorldStateKeys.HAS_CHARGE, false);
                SetBool(MimicWorldStateKeys.HAS_TRIPLE_WIELD, false);
                SetBool(MimicWorldStateKeys.HAS_DIRTY_TRICKS, false);
                SetBool(MimicWorldStateKeys.HAS_STAG, false);
                SetInt(MimicWorldStateKeys.NUM_ABILITIES, 0);
                SetObject(MimicWorldStateKeys.AVAILABLE_ABILITIES, null);
                return;
            }

            // Direct property reads from existing game state - zero duplication
            // Same properties used by MimicBrain.CheckDefensiveAbilities (line 350, 353)

            #region Common Realm Abilities

            // Body.HasAbility: GameLiving method (GameLiving.cs:3798)
            // Used by CheckDefensiveAbilities at line 357, 383, 388, etc.
            // Thread-safe via lock (m_lockAbilities) internally

            // Abilities.Quickcast: Constant from SkillConstants.cs:36
            // Used by MimicBrain at line 1592: Ability quickCast = Body.GetAbility(Abilities.Quickcast)
            bool hasQuickcast = _body.HasAbility(Abilities.Quickcast);
            SetBool(MimicWorldStateKeys.HAS_QUICKCAST, hasQuickcast);

            // Abilities.Intercept: Constant from SkillConstants.cs:196
            // Used by CheckDefensiveAbilities at line 357: case Abilities.Intercept
            bool hasIntercept = _body.HasAbility(Abilities.Intercept);
            SetBool(MimicWorldStateKeys.HAS_INTERCEPT, hasIntercept);

            // Abilities.Guard: Constant from SkillConstants.cs (implied, used at line 383)
            // Used by CheckDefensiveAbilities at line 383: case Abilities.Guard
            bool hasGuard = _body.HasAbility(Abilities.Guard);
            SetBool(MimicWorldStateKeys.HAS_GUARD, hasGuard);

            // Abilities.Protect: Constant from SkillConstants.cs (implied, used at line 388)
            // Used by CheckDefensiveAbilities at line 388: case Abilities.Protect
            bool hasProtect = _body.HasAbility(Abilities.Protect);
            SetBool(MimicWorldStateKeys.HAS_PROTECT, hasProtect);

            #endregion

            #region Offensive Abilities

            // Abilities.Berserk: Constant from SkillConstants.cs:192
            // Used by CheckDefensiveAbilities at line 409: case Abilities.Berserk
            bool hasBerserk = _body.HasAbility(Abilities.Berserk);
            SetBool(MimicWorldStateKeys.HAS_BERSERK, hasBerserk);

            // Abilities.ChargeAbility: Constant from SkillConstants.cs:200
            // Used by CheckDefensiveAbilities at line 474: case Abilities.ChargeAbility
            bool hasCharge = _body.HasAbility(Abilities.ChargeAbility);
            SetBool(MimicWorldStateKeys.HAS_CHARGE, hasCharge);

            // Abilities.Triple_Wield: Constant from SkillConstants.cs:224
            // Used by CheckDefensiveAbilities at line 439: case Abilities.Triple_Wield
            bool hasTripleWield = _body.HasAbility(Abilities.Triple_Wield);
            SetBool(MimicWorldStateKeys.HAS_TRIPLE_WIELD, hasTripleWield);

            // Abilities.DirtyTricks: Constant from SkillConstants.cs:220
            // Used by CheckDefensiveAbilities at line 454: case Abilities.DirtyTricks
            bool hasDirtyTricks = _body.HasAbility(Abilities.DirtyTricks);
            SetBool(MimicWorldStateKeys.HAS_DIRTY_TRICKS, hasDirtyTricks);

            // Abilities.Stag: Constant from SkillConstants.cs (implied, used at line 424)
            // Used by CheckDefensiveAbilities at line 424: case Abilities.Stag
            bool hasStag = _body.HasAbility(Abilities.Stag);
            SetBool(MimicWorldStateKeys.HAS_STAG, hasStag);

            #endregion

            #region All Abilities Enumeration

            // Body.GetAllAbilities(): GameLiving method (GameLiving.cs:3909+)
            // Used by CheckDefensiveAbilities at line 353: foreach (Ability ab in Body.GetAllAbilities())
            // Returns IEnumerable<Ability>, thread-safe via lock internally

            // Check if mimic has any abilities before enumeration
            // Same check as CheckDefensiveAbilities line 350: if (Body.Abilities == null || Body.Abilities.Count <= 0)
            if (_body.Abilities == null || _body.Abilities.Count <= 0)
            {
                SetInt(MimicWorldStateKeys.NUM_ABILITIES, 0);
                SetObject(MimicWorldStateKeys.AVAILABLE_ABILITIES, new List<Ability>());
                return; // No abilities available
            }

            // Enumerate all abilities the mimic has
            // Same enumeration as CheckDefensiveAbilities line 353
            var allAbilitiesCollection = _body.GetAllAbilities();
            var allAbilities = new List<Ability>();
            foreach (Ability ability in allAbilitiesCollection)
            {
                allAbilities.Add(ability);
            }
            int numAbilities = allAbilities.Count;

            SetInt(MimicWorldStateKeys.NUM_ABILITIES, numAbilities);
            SetObject(MimicWorldStateKeys.AVAILABLE_ABILITIES, allAbilities);

            #endregion

            #region Defensive vs Offensive Categorization

            // Categorize abilities for action factories
            // Defensive abilities: Intercept, Guard, Protect, Sprint, Quickcast
            bool hasDefensiveAbilities = hasIntercept || hasGuard || hasProtect;
            SetBool(MimicWorldStateKeys.HAS_DEFENSIVE_ABILITIES, hasDefensiveAbilities);

            // Offensive abilities: Berserk, Charge, Triple Wield, Dirty Tricks, Stag
            bool hasOffensiveAbilities = hasBerserk || hasCharge || hasTripleWield || hasDirtyTricks || hasStag;
            SetBool(MimicWorldStateKeys.HAS_OFFENSIVE_ABILITIES, hasOffensiveAbilities);

            #endregion
        }

        /// <summary>
        /// Gets debug information showing ability availability
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid())
                return $"{GetType().Name} (Body Invalid)";

            // Count abilities without re-enumerating
            int numAbilities = _body.Abilities?.Count ?? 0;

            // Check common abilities for debug display
            bool hasQuickcast = _body.HasAbility(Abilities.Quickcast);
            bool hasGuard = _body.HasAbility(Abilities.Guard);
            bool hasIntercept = _body.HasAbility(Abilities.Intercept);
            bool hasBerserk = _body.HasAbility(Abilities.Berserk);
            bool hasCharge = _body.HasAbility(Abilities.ChargeAbility);

            return $"{GetType().Name} (Total: {numAbilities}, " +
                   $"Quickcast: {hasQuickcast}, Guard: {hasGuard}, Intercept: {hasIntercept}, " +
                   $"Berserk: {hasBerserk}, Charge: {hasCharge})";
        }
    }
}
