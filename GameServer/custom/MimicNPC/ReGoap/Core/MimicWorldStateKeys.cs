namespace DOL.GS.Scripts.ReGoap
{
    /// <summary>
    /// World state key constants for MimicNPC ReGoap integration
    /// All values are populated by sensors reading directly from Body/Brain properties
    /// No data duplication - sensors are thin wrappers around existing game state
    /// </summary>
    /// <remarks>
    /// Design Principle: Sensors read existing game state from MimicNPC.Body and MimicBrain properties.
    /// No calculations or logic duplication occurs - sensors simply translate game state into ReGoap format.
    ///
    /// Reference: See design.md "Data Models - World State Keys" section for complete documentation.
    /// </remarks>
    public static class MimicWorldStateKeys
    {
        #region Self State (from Body.Health*, Body.Mana*)

        /// <summary>Health percentage (0-100) from Body.HealthPercent</summary>
        public const string SELF_HEALTH_PERCENT = "selfHealthPercent";

        /// <summary>Current health points from Body.Health</summary>
        public const string SELF_HEALTH = "selfHealth";

        /// <summary>Maximum health points from Body.MaxHealth</summary>
        public const string SELF_MAX_HEALTH = "selfMaxHealth";

        /// <summary>Mana percentage (0-100) from Body.ManaPercent</summary>
        public const string SELF_MANA_PERCENT = "selfManaPercent";

        /// <summary>Current mana points from Body.Mana</summary>
        public const string SELF_MANA = "selfMana";

        /// <summary>Maximum mana points from Body.MaxMana</summary>
        public const string SELF_MAX_MANA = "selfMaxMana";

        #endregion

        #region Combat State (from Body.InCombat, Body.IsCasting, Body.IsStunned, etc.)

        /// <summary>Currently in combat from Body.InCombat</summary>
        public const string IN_COMBAT = "inCombat";

        /// <summary>Currently casting a spell from Body.IsCasting</summary>
        public const string IS_CASTING = "isCasting";

        /// <summary>Currently performing melee attack from Body.IsAttacking</summary>
        public const string IS_ATTACKING = "isAttacking";

        /// <summary>Currently stunned (cannot act) from Body.IsStunned</summary>
        public const string IS_STUNNED = "isStunned";

        /// <summary>Currently mezzed (crowd controlled) from Body.IsMezzed</summary>
        public const string IS_MEZZED = "isMezzed";

        /// <summary>Can cast spells (not casting, stunned, or mezzed)</summary>
        public const string CAN_CAST = "canCast";

        /// <summary>Seconds since last combat (from GameLoop.GameLoopTime tracking)</summary>
        public const string OUT_OF_COMBAT_TIME = "outOfCombatTime";

        #endregion

        #region Target State (from Brain.CalculateNextAttackTarget, Body.TargetObject)

        /// <summary>Current target GameObject from Brain.CalculateNextAttackTarget()</summary>
        public const string CURRENT_TARGET = "currentTarget";

        /// <summary>Has valid target (currentTarget != null)</summary>
        public const string HAS_TARGET = "hasTarget";

        /// <summary>Target's health percentage from target.HealthPercent</summary>
        public const string TARGET_HEALTH_PERCENT = "targetHealthPercent";

        /// <summary>Distance to target from Body.GetDistanceTo(target)</summary>
        public const string TARGET_DISTANCE = "targetDistance";

        /// <summary>Target within melee range (200 units) from Body.IsWithinRadius(target, 200)</summary>
        public const string TARGET_IN_MELEE_RANGE = "targetInMeleeRange";

        /// <summary>Target within spell range (1500 units) from Body.IsWithinRadius(target, 1500)</summary>
        public const string TARGET_IN_SPELL_RANGE = "targetInSpellRange";

        /// <summary>Current target matches Main Assist's target (DAoC assist train)</summary>
        public const string TARGET_MATCHES_MAIN_ASSIST = "targetMatchesMainAssist";

        #endregion

        #region Aggro State (from Brain.AggroList)

        /// <summary>Has enemies on aggro list from Brain.AggroList.Count > 0</summary>
        public const string HAS_AGGRO = "hasAggro";

        /// <summary>Number of enemies on aggro list from Brain.AggroList.Count</summary>
        public const string NUM_ENEMIES = "numEnemies";

        /// <summary>Enemy with highest threat from Brain.AggroList (LINQ query)</summary>
        public const string HIGHEST_THREAT_ENEMY = "highestThreatEnemy";

        /// <summary>List of all aggroed enemies from Brain.AggroList.Keys</summary>
        public const string AGGRO_TARGETS = "aggroTargets";

        /// <summary>Number of adds not controlled (for CC priority)</summary>
        public const string NUM_UNCONTROLLED_ADDS = "numUncontrolledAdds";

        /// <summary>Number of enemies not targeting tank (for tank threat management)</summary>
        public const string NUM_ENEMIES_NOT_ON_TANK = "numEnemiesNotOnTank";

        /// <summary>Tank's threat as percentage of highest threat (for tank priority)</summary>
        public const string THREAT_PERCENT_OF_HIGHEST = "threatPercentOfHighest";

        /// <summary>Healer is currently under attack (enemies targeting healer)</summary>
        public const string HEALER_UNDER_ATTACK = "healerUnderAttack";

        #endregion

        #region Group State (from Body.Group.MimicGroup)

        /// <summary>Number of members in group from Group.MemberCount</summary>
        public const string GROUP_SIZE = "groupSize";

        /// <summary>Total health deficit in group from MimicGroup.AmountToHeal</summary>
        public const string GROUP_HEALTH_DEFICIT = "groupHealthDeficit";

        /// <summary>Number needing emergency healing (<50% HP) from MimicGroup.NumNeedEmergencyHealing</summary>
        public const string NUM_EMERGENCY_HEALING = "numEmergencyHealing";

        /// <summary>Number needing healing (<75% HP) from MimicGroup.NumNeedHealing</summary>
        public const string NUM_NEED_HEALING = "numNeedHealing";

        /// <summary>Number at critical health (<25% HP)</summary>
        public const string NUM_CRITICAL_HEALTH = "numCriticalHealth";

        /// <summary>Average health deficit percentage across injured members</summary>
        public const string AVG_HEALTH_DEFICIT_PERCENT = "avgHealthDeficitPercent";

        /// <summary>Group member needing heal from MimicGroup.MemberToHeal</summary>
        public const string MEMBER_TO_HEAL = "memberToHeal";

        /// <summary>Group member needing mezz cure from MimicGroup.MemberToCureMezz</summary>
        public const string MEMBER_TO_CURE_MEZZ = "memberToCureMezz";

        /// <summary>Number needing disease cure from MimicGroup.NumNeedCureDisease</summary>
        public const string NUM_NEED_CURE_DISEASE = "numNeedCureDisease";

        /// <summary>Number needing poison cure from MimicGroup.NumNeedCurePoison</summary>
        public const string NUM_NEED_CURE_POISON = "numNeedCurePoison";

        #endregion

        #region Group Coordination Flags (from Body.Group.MimicGroup)

        /// <summary>Instant heal already cast this tick from MimicGroup.AlreadyCastInstantHeal</summary>
        public const string ALREADY_CAST_INSTANT_HEAL = "alreadyCastInstantHeal";

        /// <summary>Heal over time already casting from MimicGroup.AlreadyCastingHoT</summary>
        public const string ALREADY_CASTING_HOT = "alreadyCastingHoT";

        /// <summary>Regen already casting from MimicGroup.AlreadyCastingRegen</summary>
        public const string ALREADY_CASTING_REGEN = "alreadyCastingRegen";

        /// <summary>Cure mezz already casting from MimicGroup.AlreadyCastingCureMezz</summary>
        public const string ALREADY_CASTING_CURE_MEZZ = "alreadyCastingCureMezz";

        /// <summary>Cure disease already casting from MimicGroup.AlreadyCastingCureDisease</summary>
        public const string ALREADY_CASTING_CURE_DISEASE = "alreadyCastingCureDisease";

        /// <summary>Cure poison already casting from MimicGroup.AlreadyCastingCurePoison</summary>
        public const string ALREADY_CASTING_CURE_POISON = "alreadyCastingCurePoison";

        #endregion

        #region Role State (from Body/Brain role assignments)

        /// <summary>Is Main Tank role from MimicGroup role assignment</summary>
        public const string IS_MAIN_TANK = "isMainTank";

        /// <summary>Is Main Assist role from MimicGroup role assignment</summary>
        public const string IS_MAIN_ASSIST = "isMainAssist";

        /// <summary>Is Main CC role from MimicGroup role assignment</summary>
        public const string IS_MAIN_CC = "isMainCC";

        /// <summary>Is Main Puller role from MimicGroup role assignment</summary>
        public const string IS_MAIN_PULLER = "isMainPuller";

        /// <summary>Is Healer role (any healing spec)</summary>
        public const string IS_HEALER = "isHealer";

        #endregion

        #region Goal Completion Flags (evaluated by goals against world state)

        /// <summary>Goal state: All group members at full health</summary>
        public const string GROUP_FULL_HEALTH = "groupFullHealth";

        /// <summary>Goal state: Tank has highest threat on all enemies</summary>
        public const string HAS_HIGHEST_THREAT = "hasHighestThreat";

        /// <summary>Goal state: All enemies targeting the tank</summary>
        public const string ENEMIES_ON_TANK = "enemiesOnTank";

        /// <summary>Goal state: Current target is dead</summary>
        public const string TARGET_DEAD = "targetDead";

        /// <summary>Goal state: All adds are crowd controlled</summary>
        public const string ADDS_CONTROLLED = "addsControlled";

        /// <summary>Goal state: Combat has been initiated (for puller)</summary>
        public const string COMBAT_INITIATED = "combatInitiated";

        /// <summary>Goal state: All defensive buffs are active</summary>
        public const string BUFFS_MAINTAINED = "buffsMaintained";

        #endregion

        #region DAoC-Specific Mechanics (from daoc-role-analysis.md)

        /// <summary>Was just interrupted while casting (for quickcast trigger)</summary>
        public const string WAS_JUST_INTERRUPTED = "wasJustInterrupted";

        /// <summary>Recent interrupt count (for quickcast threshold)</summary>
        public const string INTERRUPT_COUNT = "interruptCount";

        /// <summary>Should use quickcast ability (2+ interrupts)</summary>
        public const string SHOULD_USE_QUICKCAST = "shouldUseQuickcast";

        /// <summary>Enemy is currently casting from target.IsCasting</summary>
        public const string ENEMY_CASTING = "enemyCasting";

        /// <summary>Enemy's cast target from target.CastTarget</summary>
        public const string ENEMY_CAST_TARGET = "enemyCastTarget";

        /// <summary>Should interrupt enemy (in range and enemy casting)</summary>
        public const string SHOULD_INTERRUPT = "shouldInterrupt";

        /// <summary>Guard ability is active from Body.GuardTarget != null</summary>
        public const string GUARD_ACTIVE = "guardActive";

        /// <summary>Current guard target from Body.GuardTarget</summary>
        public const string GUARD_TARGET = "guardTarget";

        /// <summary>Healer needs guard (found healer, guard not active)</summary>
        public const string HEALER_NEEDS_GUARD = "healerNeedsGuard";

        /// <summary>Group has speed buff active (Speed 5/6)</summary>
        public const string GROUP_SPEED_ACTIVE = "groupSpeedActive";

        /// <summary>Group has melee classes present</summary>
        public const string GROUP_HAS_MELEE = "groupHasMelee";

        /// <summary>Speed buff critical (melee present, speed not active)</summary>
        public const string SPEED_CRITICAL = "speedCritical";

        /// <summary>Enemies currently mezzed (list)</summary>
        public const string MEZZED_ENEMIES = "mezzedEnemies";

        /// <summary>Adds not mezzed (excluding main target)</summary>
        public const string UNMEZZED_ADDS = "unmezzedAdds";

        /// <summary>Number of unmezzed adds</summary>
        public const string NUM_UNMEZZED_ADDS = "numUnmezzedAdds";

        /// <summary>Mezz needs refresh (expiring in <10s)</summary>
        public const string MEZZ_NEEDS_REFRESH = "mezzNeedsRefresh";

        /// <summary>Enemies immune to crowd control (list) - targets that returned explicit immunity messages</summary>
        public const string CC_IMMUNE_ENEMIES = "ccImmuneEnemies";

        /// <summary>Number of adds that are controllable (not already controlled AND not immune)</summary>
        public const string NUM_CONTROLLABLE_ADDS = "numControllableAdds";

        /// <summary>Main Assist player from MimicGroup.MainAssist</summary>
        public const string MAIN_ASSIST = "mainAssist";

        /// <summary>Main Assist's current target from MainAssist.TargetObject</summary>
        public const string MAIN_ASSIST_TARGET = "mainAssistTarget";

        /// <summary>Is the Main Assist (this mimic)</summary>
        public const string IS_MAIN_ASSIST_SELF = "isMainAssist";

        /// <summary>Quickcast ability available (not on cooldown)</summary>
        public const string QUICKCAST_AVAILABLE = "quickcastAvailable";

        #endregion

        #region Spell Availability (from Body spell properties)

        /// <summary>Can cast crowd control spells from Body.CanCastCrowdControlSpells</summary>
        public const string CAN_CAST_CROWD_CONTROL = "canCastCrowdControl";

        /// <summary>Can cast instant crowd control spells from Body.CanCastInstantCrowdControlSpells</summary>
        public const string CAN_CAST_INSTANT_CROWD_CONTROL = "canCastInstantCrowdControl";

        /// <summary>Can cast bolt spells from Body.CanCastBolts</summary>
        public const string CAN_CAST_BOLTS = "canCastBolts";

        /// <summary>Can cast healing spells (any healing spell available)</summary>
        public const string CAN_CAST_HEALING = "canCastHealing";

        /// <summary>Can cast cure mezz from Body.CureMezz</summary>
        public const string CAN_CAST_CURE_MEZZ = "canCastCureMezz";

        /// <summary>Can cast cure disease from Body.CureDisease/CureDiseaseGroup</summary>
        public const string CAN_CAST_CURE_DISEASE = "canCastCureDisease";

        /// <summary>Can cast cure poison from Body.CurePoison/CurePoisonGroup</summary>
        public const string CAN_CAST_CURE_POISON = "canCastCurePoison";

        /// <summary>Can cast offensive spells (harmful/instant harmful/bolts)</summary>
        public const string CAN_CAST_OFFENSIVE_SPELLS = "canCastOffensiveSpells";

        /// <summary>Can cast defensive spells (misc/instant misc)</summary>
        public const string CAN_CAST_DEFENSIVE_SPELLS = "canCastDefensiveSpells";

        /// <summary>List of available crowd control spells from Body.CrowdControlSpells</summary>
        public const string AVAILABLE_CROWD_CONTROL_SPELLS = "availableCrowdControlSpells";

        /// <summary>List of available bolt spells from Body.BoltSpells</summary>
        public const string AVAILABLE_BOLT_SPELLS = "availableBoltSpells";

        /// <summary>List of available healing spells (all heal types)</summary>
        public const string AVAILABLE_HEALING_SPELLS = "availableHealingSpells";

        /// <summary>Number of crowd control spells available</summary>
        public const string NUM_CROWD_CONTROL_SPELLS = "numCrowdControlSpells";

        /// <summary>Number of bolt spells available</summary>
        public const string NUM_BOLT_SPELLS = "numBoltSpells";

        /// <summary>Number of healing spells available</summary>
        public const string NUM_HEALING_SPELLS = "numHealingSpells";

        #endregion

        #region Ability Availability (from Body ability system)

        /// <summary>Has Quickcast ability from Body.HasAbility(Abilities.Quickcast)</summary>
        public const string HAS_QUICKCAST = "hasQuickcast";

        /// <summary>Has Intercept ability from Body.HasAbility(Abilities.Intercept)</summary>
        public const string HAS_INTERCEPT = "hasIntercept";

        /// <summary>Has Guard ability from Body.HasAbility(Abilities.Guard)</summary>
        public const string HAS_GUARD = "hasGuard";

        /// <summary>Has Protect ability from Body.HasAbility(Abilities.Protect)</summary>
        public const string HAS_PROTECT = "hasProtect";

        /// <summary>Has Berserk ability from Body.HasAbility(Abilities.Berserk)</summary>
        public const string HAS_BERSERK = "hasBerserk";

        /// <summary>Has Charge ability from Body.HasAbility(Abilities.ChargeAbility)</summary>
        public const string HAS_CHARGE = "hasCharge";

        /// <summary>Has Triple Wield ability from Body.HasAbility(Abilities.Triple_Wield)</summary>
        public const string HAS_TRIPLE_WIELD = "hasTripleWield";

        /// <summary>Has Dirty Tricks ability from Body.HasAbility(Abilities.DirtyTricks)</summary>
        public const string HAS_DIRTY_TRICKS = "hasDirtyTricks";

        /// <summary>Has Stag ability from Body.HasAbility(Abilities.Stag)</summary>
        public const string HAS_STAG = "hasStag";

        /// <summary>Has any defensive abilities (Intercept, Guard, Protect)</summary>
        public const string HAS_DEFENSIVE_ABILITIES = "hasDefensiveAbilities";

        /// <summary>Has any offensive abilities (Berserk, Charge, Triple Wield, Dirty Tricks, Stag)</summary>
        public const string HAS_OFFENSIVE_ABILITIES = "hasOffensiveAbilities";

        /// <summary>List of all available abilities from Body.GetAllAbilities()</summary>
        public const string AVAILABLE_ABILITIES = "availableAbilities";

        /// <summary>Number of abilities available</summary>
        public const string NUM_ABILITIES = "numAbilities";

        #endregion

        #region Style Availability (from Body style system)

        /// <summary>Can use side positional styles from Body.CanUseSideStyles</summary>
        public const string CAN_USE_SIDE_STYLES = "canUseSideStyles";

        /// <summary>Can use back positional styles from Body.CanUseBackStyles</summary>
        public const string CAN_USE_BACK_STYLES = "canUseBackStyles";

        /// <summary>Can use front positional styles from Body.CanUseFrontStyle</summary>
        public const string CAN_USE_FRONT_STYLES = "canUseFrontStyles";

        /// <summary>Can use anytime (non-positional) styles from Body.CanUseAnytimeStyles</summary>
        public const string CAN_USE_ANYTIME_STYLES = "canUseAnytimeStyles";

        /// <summary>Can use any positional styles (side or back) from Body.CanUsePositionalStyles</summary>
        public const string CAN_USE_POSITIONAL_STYLES = "canUsePositionalStyles";

        /// <summary>Has taunt styles for threat generation from Body.StylesTaunt</summary>
        public const string HAS_TAUNT_STYLES = "hasTauntStyles";

        /// <summary>Has detaunt styles for threat reduction from Body.StylesDetaunt</summary>
        public const string HAS_DETAUNT_STYLES = "hasDetauntStyles";

        /// <summary>Has shield styles (require shield) from Body.StylesShield</summary>
        public const string HAS_SHIELD_STYLES = "hasShieldStyles";

        /// <summary>Has chain styles (require previous style) from Body.StylesChain</summary>
        public const string HAS_CHAIN_STYLES = "hasChainStyles";

        /// <summary>Has defensive styles (parry/block/evade followups) from Body.StylesDefensive</summary>
        public const string HAS_DEFENSIVE_STYLES = "hasDefensiveStyles";

        /// <summary>List of available side positional styles from Body.StylesSide</summary>
        public const string AVAILABLE_SIDE_STYLES = "availableSideStyles";

        /// <summary>List of available back positional styles from Body.StylesBack</summary>
        public const string AVAILABLE_BACK_STYLES = "availableBackStyles";

        /// <summary>List of available front positional styles from Body.StylesFront</summary>
        public const string AVAILABLE_FRONT_STYLES = "availableFrontStyles";

        /// <summary>List of available anytime styles from Body.StylesAnytime</summary>
        public const string AVAILABLE_ANYTIME_STYLES = "availableAnytimeStyles";

        /// <summary>List of available taunt styles from Body.StylesTaunt</summary>
        public const string AVAILABLE_TAUNT_STYLES = "availableTauntStyles";

        /// <summary>List of available detaunt styles from Body.StylesDetaunt</summary>
        public const string AVAILABLE_DETAUNT_STYLES = "availableDetauntStyles";

        /// <summary>Number of positional styles (side + back + front)</summary>
        public const string NUM_POSITIONAL_STYLES = "numPositionalStyles";

        /// <summary>Number of anytime styles available</summary>
        public const string NUM_ANYTIME_STYLES = "numAnytimeStyles";

        /// <summary>Total number of all combat styles</summary>
        public const string NUM_TOTAL_STYLES = "numTotalStyles";

        #endregion
    }
}
