using DOL.GS.Scripts.ReGoap;
using DOL.GS.Styles;
using System.Collections.Generic;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Sensor that reads combat style availability directly from MimicNPC.Body properties
    /// Tracks which combat styles are available based on class/spec (positional, anytime, taunt, etc.)
    /// Thin wrapper - performs zero logic, reads existing style lists from MimicNPC and MimicAttackAction
    /// </summary>
    /// <remarks>
    /// Design Principle (from design.md Component 3: Sensor Framework):
    /// "Sensors are thin wrappers that read existing game state from MimicNPC.Body and MimicBrain properties"
    ///
    /// Code Reuse (from task requirements):
    /// Leverages existing style system by reading the same style properties:
    /// - Body.CanUseSideStyles: Boolean check for side positional styles
    /// - Body.CanUseBackStyles: Boolean check for back positional styles
    /// - Body.CanUseFrontStyle: Boolean check for front positional styles
    /// - Body.CanUseAnytimeStyles: Boolean check for anytime (non-positional) styles
    /// - Body.CanUsePositionalStyles: Combined check for any positional styles
    /// - Body.StylesSide: List of side positional styles (GameNPC.cs:4146)
    /// - Body.StylesBack: List of back positional styles (GameNPC.cs:4141)
    /// - Body.StylesFront: List of front positional styles (GameNPC.cs:4151)
    /// - Body.StylesAnytime: List of anytime styles (GameNPC.cs:4156)
    /// - Body.StylesChain: List of chain styles (GameNPC.cs:4131)
    /// - Body.StylesDefensive: List of defensive styles (GameNPC.cs:4136)
    /// - Body.StylesTaunt: List of taunt styles (MimicNPC.cs:66)
    /// - Body.StylesDetaunt: List of detaunt styles (MimicNPC.cs:67)
    /// - Body.StylesShield: List of shield styles (MimicNPC.cs:68)
    ///
    /// World State Keys Populated:
    /// - CAN_USE_SIDE_STYLES: Body.CanUseSideStyles
    /// - CAN_USE_BACK_STYLES: Body.CanUseBackStyles
    /// - CAN_USE_FRONT_STYLES: Body.CanUseFrontStyle
    /// - CAN_USE_ANYTIME_STYLES: Body.CanUseAnytimeStyles
    /// - CAN_USE_POSITIONAL_STYLES: Body.CanUsePositionalStyles
    /// - HAS_TAUNT_STYLES: Body.StylesTaunt != null && Count > 0
    /// - HAS_DETAUNT_STYLES: Body.StylesDetaunt != null && Count > 0
    /// - HAS_SHIELD_STYLES: Body.StylesShield != null && Count > 0
    /// - HAS_CHAIN_STYLES: Body.StylesChain != null && Count > 0
    /// - HAS_DEFENSIVE_STYLES: Body.StylesDefensive != null && Count > 0
    /// - AVAILABLE_SIDE_STYLES: Body.StylesSide (list)
    /// - AVAILABLE_BACK_STYLES: Body.StylesBack (list)
    /// - AVAILABLE_FRONT_STYLES: Body.StylesFront (list)
    /// - AVAILABLE_ANYTIME_STYLES: Body.StylesAnytime (list)
    /// - AVAILABLE_TAUNT_STYLES: Body.StylesTaunt (list)
    /// - AVAILABLE_DETAUNT_STYLES: Body.StylesDetaunt (list)
    /// - NUM_POSITIONAL_STYLES: Total count of positional styles
    /// - NUM_ANYTIME_STYLES: Count of anytime styles
    /// - NUM_TOTAL_STYLES: Total count of all combat styles
    ///
    /// Integration Points (from MimicAttackAction at line 35):
    /// - Line 34: if (StyleComponent.NextCombatStyle == null)
    /// - Line 35: _combatStyle = StyleComponent.MimicGetStyleToUse()
    /// - MimicAttackAction.PrepareMeleeAttack uses StyleComponent to select styles
    /// - StyleComponent.GetStyleToUse() determines which style to use based on position/requirements
    ///
    /// Integration Points (from MimicNPC.SortStyles at line 1710):
    /// - Line 1714: StylesBack?.Clear()
    /// - Line 1715: StylesSide?.Clear()
    /// - Line 1716: StylesFront?.Clear()
    /// - Line 1717: StylesAnytime?.Clear()
    /// - Line 1746-1749: Back style assignment (if opening position is Back)
    /// - Line 1753-1756: Side style assignment (if opening position is Side)
    /// - Line 1760-1763: Front style assignment (if opening position is Front)
    /// - Line 1812-1815: Anytime style assignment (if no position requirement)
    ///
    /// Usage by Goals/Actions:
    /// - MeleeAttackGoal: Checks CAN_USE_ANYTIME_STYLES or CAN_USE_POSITIONAL_STYLES
    /// - TankThreatGoal: Checks HAS_TAUNT_STYLES for threat generation options
    /// - StealtherGoal: Checks CAN_USE_BACK_STYLES for positional attack planning
    /// - ActionFactory: Uses AVAILABLE_*_STYLES lists to generate StyleAttackAction instances
    ///
    /// Reference: design.md "Component 2: MimicReGoapAgent - Sensor management"
    /// Requirements: 2.1 (Shared Sensor Framework), 2.4 (Sensor data updates), 4.2 (Action preconditions)
    /// Code Reuse: GameNPC style properties (existing at GameNPC.cs:4131-4156)
    /// Code Reuse: MimicNPC style properties (existing at MimicNPC.cs:66-68)
    /// Code Reuse: MimicNPC.SortStyles (existing at MimicNPC.cs:1710-1830)
    /// Code Reuse: MimicAttackAction.PrepareMeleeAttack (existing at MimicAttackAction.cs:32-58)
    /// Code Reuse: StyleComponent.GetStyleToUse (existing at StyleComponent.cs:119-150)
    /// </remarks>
    public class StyleAvailabilitySensor : MimicSensor
    {
        /// <summary>
        /// Updates world state with style availability from Body style properties
        /// Direct property reads - zero calculation or duplication of style selection logic
        /// </summary>
        /// <remarks>
        /// Performance: Simple property reads and list access - no iteration or filtering
        /// Thread Safety: Body.StyleComponent uses lockStyleList for thread-safe access
        /// Update Frequency: Every think tick (500ms) via MimicBrain
        ///
        /// Example Usage (from design.md):
        /// Goals use these values to determine action availability:
        /// - MeleeAttackGoal: If CAN_USE_ANYTIME_STYLES = false and CAN_USE_POSITIONAL_STYLES = false, goal cannot attack with styles
        /// - TankThreatGoal: Checks HAS_TAUNT_STYLES to determine if taunt styles can generate threat
        /// - StealtherGoal: Checks CAN_USE_BACK_STYLES for backstab/positional planning
        ///
        /// Actions use these values for preconditions:
        /// - StyleAttackAction: Checks if specific style exists in AVAILABLE_*_STYLES lists
        /// - ActionFactory: Enumerates AVAILABLE_ANYTIME_STYLES to generate attack actions
        ///
        /// Code Reuse from MimicAttackAction.PrepareMeleeAttack (line 32-58):
        /// - Line 34: if (StyleComponent.NextCombatStyle == null) - check if style queued
        /// - Line 35: _combatStyle = StyleComponent.MimicGetStyleToUse() - style selection
        /// - This sensor provides the same style data that StyleComponent uses, but in
        ///   ReGoap world state format for goal/action decision-making instead of imperative
        ///   style execution.
        ///
        /// Code Reuse from MimicNPC.SortStyles (line 1710-1830):
        /// - Line 1746-1749: Back style list population
        /// - Line 1753-1756: Side style list population
        /// - Line 1760-1763: Front style list population
        /// - Line 1812-1815: Anytime style list population
        /// - This sensor reads the same lists that SortStyles() populates
        ///
        /// Style Availability vs. Style Selection:
        /// - This sensor tracks which style TYPES the mimic HAS (permanent character abilities)
        /// - Style selection is handled by StyleComponent.GetStyleToUse() based on position/requirements
        /// - Actions check preconditions using availability data from this sensor
        /// </remarks>
        public override void UpdateSensor()
        {
            // Validate Body reference before reading properties
            if (!IsBodyValid())
            {
                // Set safe default values if Body is invalid
                SetBool(MimicWorldStateKeys.CAN_USE_SIDE_STYLES, false);
                SetBool(MimicWorldStateKeys.CAN_USE_BACK_STYLES, false);
                SetBool(MimicWorldStateKeys.CAN_USE_FRONT_STYLES, false);
                SetBool(MimicWorldStateKeys.CAN_USE_ANYTIME_STYLES, false);
                SetBool(MimicWorldStateKeys.CAN_USE_POSITIONAL_STYLES, false);
                SetBool(MimicWorldStateKeys.HAS_TAUNT_STYLES, false);
                SetBool(MimicWorldStateKeys.HAS_DETAUNT_STYLES, false);
                SetBool(MimicWorldStateKeys.HAS_SHIELD_STYLES, false);
                SetBool(MimicWorldStateKeys.HAS_CHAIN_STYLES, false);
                SetBool(MimicWorldStateKeys.HAS_DEFENSIVE_STYLES, false);
                SetInt(MimicWorldStateKeys.NUM_POSITIONAL_STYLES, 0);
                SetInt(MimicWorldStateKeys.NUM_ANYTIME_STYLES, 0);
                SetInt(MimicWorldStateKeys.NUM_TOTAL_STYLES, 0);
                SetObject(MimicWorldStateKeys.AVAILABLE_SIDE_STYLES, null);
                SetObject(MimicWorldStateKeys.AVAILABLE_BACK_STYLES, null);
                SetObject(MimicWorldStateKeys.AVAILABLE_FRONT_STYLES, null);
                SetObject(MimicWorldStateKeys.AVAILABLE_ANYTIME_STYLES, null);
                SetObject(MimicWorldStateKeys.AVAILABLE_TAUNT_STYLES, null);
                SetObject(MimicWorldStateKeys.AVAILABLE_DETAUNT_STYLES, null);
                return;
            }

            // Direct property reads from existing game state - zero duplication
            // Same properties used by MimicAttackAction (line 34-35) and MimicNPC.SortStyles (line 1710-1830)

            #region Positional Style Availability

            // Body.CanUseSideStyles: Boolean property (MimicNPC.cs:57)
            // Returns true if StylesSide list has items
            bool canUseSideStyles = _body.CanUseSideStyles;
            SetBool(MimicWorldStateKeys.CAN_USE_SIDE_STYLES, canUseSideStyles);

            // Body.CanUseBackStyles: Boolean property (MimicNPC.cs:58)
            // Returns true if StylesBack list has items
            bool canUseBackStyles = _body.CanUseBackStyles;
            SetBool(MimicWorldStateKeys.CAN_USE_BACK_STYLES, canUseBackStyles);

            // Body.CanUseFrontStyle: Boolean property (MimicNPC.cs:59)
            // Returns true if StylesFront list has items
            bool canUseFrontStyles = _body.CanUseFrontStyle;
            SetBool(MimicWorldStateKeys.CAN_USE_FRONT_STYLES, canUseFrontStyles);

            // Body.CanUsePositionalStyles: Boolean property (MimicNPC.cs:61)
            // Returns true if can use side OR back styles
            bool canUsePositionalStyles = _body.CanUsePositionalStyles;
            SetBool(MimicWorldStateKeys.CAN_USE_POSITIONAL_STYLES, canUsePositionalStyles);

            #endregion

            #region Anytime Style Availability

            // Body.CanUseAnytimeStyles: Boolean property (MimicNPC.cs:60)
            // Returns true if StylesAnytime list has items
            bool canUseAnytimeStyles = _body.CanUseAnytimeStyles;
            SetBool(MimicWorldStateKeys.CAN_USE_ANYTIME_STYLES, canUseAnytimeStyles);

            #endregion

            #region Style List Availability

            // Body.StylesSide: List<Style> property (GameNPC.cs:4146)
            // Populated by MimicNPC.SortStyles at line 1753-1756
            var sideStyles = _body.StylesSide;
            int numSideStyles = sideStyles?.Count ?? 0;
            SetObject(MimicWorldStateKeys.AVAILABLE_SIDE_STYLES, sideStyles);

            // Body.StylesBack: List<Style> property (GameNPC.cs:4141)
            // Populated by MimicNPC.SortStyles at line 1746-1749
            var backStyles = _body.StylesBack;
            int numBackStyles = backStyles?.Count ?? 0;
            SetObject(MimicWorldStateKeys.AVAILABLE_BACK_STYLES, backStyles);

            // Body.StylesFront: List<Style> property (GameNPC.cs:4151)
            // Populated by MimicNPC.SortStyles at line 1760-1763
            var frontStyles = _body.StylesFront;
            int numFrontStyles = frontStyles?.Count ?? 0;
            SetObject(MimicWorldStateKeys.AVAILABLE_FRONT_STYLES, frontStyles);

            // Body.StylesAnytime: List<Style> property (GameNPC.cs:4156)
            // Populated by MimicNPC.SortStyles at line 1812-1815
            var anytimeStyles = _body.StylesAnytime;
            int numAnytimeStyles = anytimeStyles?.Count ?? 0;
            SetInt(MimicWorldStateKeys.NUM_ANYTIME_STYLES, numAnytimeStyles);
            SetObject(MimicWorldStateKeys.AVAILABLE_ANYTIME_STYLES, anytimeStyles);

            // Calculate total positional styles count
            int numPositionalStyles = numSideStyles + numBackStyles + numFrontStyles;
            SetInt(MimicWorldStateKeys.NUM_POSITIONAL_STYLES, numPositionalStyles);

            #endregion

            #region Special Style Types

            // Body.StylesChain: List<Style> property (GameNPC.cs:4131)
            // Chain styles that require previous style to land
            bool hasChainStyles = _body.StylesChain != null && _body.StylesChain.Count > 0;
            SetBool(MimicWorldStateKeys.HAS_CHAIN_STYLES, hasChainStyles);

            // Body.StylesDefensive: List<Style> property (GameNPC.cs:4136)
            // Defensive styles (parry, block, evade followups)
            bool hasDefensiveStyles = _body.StylesDefensive != null && _body.StylesDefensive.Count > 0;
            SetBool(MimicWorldStateKeys.HAS_DEFENSIVE_STYLES, hasDefensiveStyles);

            // Body.StylesTaunt: List<Style> property (MimicNPC.cs:66)
            // Taunt styles for threat generation
            var tauntStyles = _body.StylesTaunt;
            bool hasTauntStyles = tauntStyles != null && tauntStyles.Count > 0;
            SetBool(MimicWorldStateKeys.HAS_TAUNT_STYLES, hasTauntStyles);
            SetObject(MimicWorldStateKeys.AVAILABLE_TAUNT_STYLES, tauntStyles);

            // Body.StylesDetaunt: List<Style> property (MimicNPC.cs:67)
            // Detaunt styles for threat reduction
            var detauntStyles = _body.StylesDetaunt;
            bool hasDetauntStyles = detauntStyles != null && detauntStyles.Count > 0;
            SetBool(MimicWorldStateKeys.HAS_DETAUNT_STYLES, hasDetauntStyles);
            SetObject(MimicWorldStateKeys.AVAILABLE_DETAUNT_STYLES, detauntStyles);

            // Body.StylesShield: List<Style> property (MimicNPC.cs:68)
            // Shield styles requiring shield equipped
            bool hasShieldStyles = _body.StylesShield != null && _body.StylesShield.Count > 0;
            SetBool(MimicWorldStateKeys.HAS_SHIELD_STYLES, hasShieldStyles);

            #endregion

            #region Total Style Count

            // Calculate total number of styles available
            int numTotalStyles = numPositionalStyles + numAnytimeStyles;
            if (_body.StylesChain != null) numTotalStyles += _body.StylesChain.Count;
            if (_body.StylesDefensive != null) numTotalStyles += _body.StylesDefensive.Count;
            if (_body.StylesTaunt != null) numTotalStyles += _body.StylesTaunt.Count;
            if (_body.StylesDetaunt != null) numTotalStyles += _body.StylesDetaunt.Count;
            if (_body.StylesShield != null) numTotalStyles += _body.StylesShield.Count;

            SetInt(MimicWorldStateKeys.NUM_TOTAL_STYLES, numTotalStyles);

            #endregion
        }

        /// <summary>
        /// Gets debug information showing style availability
        /// Used by /mimic debug command for troubleshooting
        /// </summary>
        public override string GetDebugInfo()
        {
            if (!IsBodyValid())
                return $"{GetType().Name} (Body Invalid)";

            // Count styles without re-enumerating
            int numSide = _body.StylesSide?.Count ?? 0;
            int numBack = _body.StylesBack?.Count ?? 0;
            int numFront = _body.StylesFront?.Count ?? 0;
            int numAnytime = _body.StylesAnytime?.Count ?? 0;
            int numTaunt = _body.StylesTaunt?.Count ?? 0;
            int numDetaunt = _body.StylesDetaunt?.Count ?? 0;

            int numPositional = numSide + numBack + numFront;
            int numTotal = numPositional + numAnytime + numTaunt + numDetaunt;

            return $"{GetType().Name} (Total: {numTotal}, Positional: {numPositional} [Side:{numSide} Back:{numBack} Front:{numFront}], " +
                   $"Anytime: {numAnytime}, Taunt: {numTaunt}, Detaunt: {numDetaunt})";
        }
    }
}
