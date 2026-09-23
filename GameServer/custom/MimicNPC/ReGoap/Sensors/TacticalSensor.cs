using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.RealmAbilities;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using DOL.GS.ServerProperties;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>Complete per-decision facts for the registered tactical goals.
    /// Reads health directly so planning never depends on CheckHeals having run first.</summary>
    public sealed class TacticalSensor : MimicSensor
    {
        public const string SupportAvailable = "supportAvailable";
        public const string BuffsAvailable = "buffsAvailable";
        public const string ControlAvailable = "controlAvailable";
        public const string OffenseAvailable = "offenseAvailable";
        public const string AttackAvailable = "attackAvailable";
        public const string CureNeeded = "cureNeeded";
        public const string EnemyCasting = "enemyCasting";
        public const string InterruptAvailable = "interruptAvailable";
        public const string AssistAvailable = "assistAvailable";
        public const string PeelAvailable = "peelAvailable";
        public const string GuardAvailable = "guardAvailable";
        public const string KiteAvailable = "kiteAvailable";
        public const string QuickcastReady = "quickcastReady";
        public const string RezAvailable = "rezAvailable";
        public const string RezOutsideAvailable = "rezOutsideAvailable";
        public const string PurgeAvailable = "purgeAvailable";
        public const string CallAvailable = "callAvailable";
        public const string DebuffAvailable = "debuffAvailable";
        public const string FlankAvailable = "flankAvailable";
        public const string ArrowTypeAvailable = "arrowTypeAvailable";

        private static readonly eEffect[] DebuffEffects =
        {
            eEffect.StrengthDebuff, eEffect.DexterityDebuff, eEffect.ConstitutionDebuff,
            eEffect.StrConDebuff, eEffect.DexQuiDebuff, eEffect.ArmorFactorDebuff,
            eEffect.ArmorAbsorptionDebuff, eEffect.BodyResistDebuff, eEffect.SpiritResistDebuff,
            eEffect.EnergyResistDebuff, eEffect.HeatResistDebuff, eEffect.ColdResistDebuff,
            eEffect.MatterResistDebuff, eEffect.MeleeDamageDebuff, eEffect.AllStatDebuff,
            eEffect.AllStatPercentDebuff,
        };

        public override void UpdateSensor()
        {
            var context = ((MimicReGoapAgent)agent).Context;
            bool canAct = IsBodyValid() && !_body.IsStunned && !_body.IsMezzed;
            bool combat = context == MimicDecisionContext.Combat && !_brain.PreventCombat && !_brain.IsHealer;
            var targetLiving = _body.TargetObject as GameLiving;
            bool targetValid = combat && targetLiving != null && targetLiving.IsAlive
                && targetLiving.ObjectState == GameObject.eObjectState.Active && targetLiving.CurrentRegion == _body.CurrentRegion
                && !targetLiving.IsMezzed && !targetLiving.IsRooted && _brain.CanAggroTarget(targetLiving);

            // HasAggro is sufficient before the first attack: Body.InCombat is not yet set.
            SetBool(MimicWorldStateKeys.IN_COMBAT, _body.InCombat || (combat && _brain.HasAggro));
            SetBool(MimicWorldStateKeys.HAS_AGGRO, _brain.HasAggro);
            SetBool(MimicWorldStateKeys.HAS_TARGET, targetValid);
            SetBool(MimicWorldStateKeys.TARGET_MATCHES_MAIN_ASSIST,
                _body.Group == null || _body.Group.MimicGroup.CurrentTarget == _body.TargetObject);
            SetBool(MimicWorldStateKeys.IS_MAIN_ASSIST, _brain.IsMainAssist);

            var members = _body.Group == null ? new GameLiving[] { _body }
                : _body.Group.GetMembersInTheGroup().Cast<GameLiving>().ToArray();
            var reachable = members.Where(m => m.IsAlive && m.ObjectState == GameObject.eObjectState.Active
                && m.CurrentRegion == _body.CurrentRegion && _body.IsWithinRadius(m, WorldMgr.VISIBILITY_DISTANCE)).ToArray();
            int injured = reachable.Count(m => m.HealthPercent < (_brain.IsHealer ? 100 : MimicGroup.HealThreshold));
            SetInt(MimicWorldStateKeys.GROUP_SIZE, reachable.Length);
            SetInt(MimicWorldStateKeys.NUM_NEED_HEALING, injured);
            SetInt(MimicWorldStateKeys.NUM_EMERGENCY_HEALING, reachable.Count(m => m.HealthPercent < MimicGroup.EmergencyThreshold));
            SetInt(MimicWorldStateKeys.NUM_CRITICAL_HEALTH, reachable.Count(m => m.HealthPercent < 25));
            SetFloat(MimicWorldStateKeys.AVG_HEALTH_DEFICIT_PERCENT,
                reachable.Length == 0 ? 0 : (float)reachable.Average(m => 100 - m.HealthPercent));
            SetBool(CureNeeded, reachable.Any(m =>
                (m.IsMezzed && _body.CureMezz != null) ||
                (m.IsDiseased && (_body.CureDisease != null || _body.CureDiseaseGroup != null)) ||
                (m.IsPoisoned && (_body.CurePoison != null || _body.CurePoisonGroup != null))));

            var ccTargets = _body.Group?.MimicGroup.CCTargets;
            int adds = ccTargets?.Count(m => m != null && m.IsAlive && !m.IsMezzed && !m.IsStunned
                && !m.IsRooted && m != _body.Group.MimicGroup.CurrentTarget
                && m.CurrentRegion == _body.CurrentRegion && _brain.CanAggroTarget(m)) ?? 0;
            SetInt(MimicWorldStateKeys.NUM_CONTROLLABLE_ADDS, adds);
            SetInt(MimicWorldStateKeys.NUM_ENEMIES, _brain.GetOrderedAggroList().Count);
            SetBool(MimicWorldStateKeys.IS_MAIN_CC, _brain.IsMainCC);
            SetBool(MimicWorldStateKeys.BUFFS_MAINTAINED, false);
            SetBool(SupportAvailable, canAct && !_body.IsSilenced && _body.CanCastHealSpells);
            SetBool(BuffsAvailable, canAct && context == MimicDecisionContext.Support && !_body.InCombat
                && !_body.IsMoving && !_body.IsSilenced);
            SetBool(ControlAvailable, canAct && context != MimicDecisionContext.Healing && !_brain.PreventCombat
                && _brain.IsMainCC && adds > 0 && _body.CanCastCrowdControlSpells && !_body.IsSilenced && !_body.IsCasting);
            SetBool(OffenseAvailable, canAct && targetValid && !_body.IsSilenced && !_brain.IsFleeing);
            SetBool(AttackAvailable, canAct && targetValid && !_body.IsCasting);
            bool enemyCasting = targetValid && targetLiving.IsCasting;
            SetBool(EnemyCasting, enemyCasting);
            SetBool(InterruptAvailable, canAct && enemyCasting && !_body.IsCasting);
            var assistTarget = _body.Group?.MimicGroup.CurrentTarget as GameLiving;
            bool assistValid = combat && !_brain.IsMainAssist && assistTarget != null && assistTarget.IsAlive
                && assistTarget.ObjectState == GameObject.eObjectState.Active && assistTarget.CurrentRegion == _body.CurrentRegion
                && _brain.CanAggroTarget(assistTarget);
            SetBool(AssistAvailable, canAct && assistValid && _body.TargetObject != (GameObject)assistTarget);
            // Peel scan: aggroed enemies hitting another group member. CC'd enemies are
            // skipped so the tank never breaks its own group's mezz/stun/root.
            int notOnTank = 0;
            bool healerUnderAttack = false;
            if (combat && _brain.IsMainTank && _body.Group != null)
            {
                var memberSet = new HashSet<GameLiving>(members);
                foreach (var entry in _brain.GetOrderedAggroList())
                {
                    var enemy = entry.Item1;
                    if (enemy == null || !enemy.IsAlive || enemy.ObjectState != GameObject.eObjectState.Active
                        || enemy.CurrentRegion != _body.CurrentRegion || enemy.IsMezzed || enemy.IsStunned || enemy.IsRooted)
                        continue;
                    var enemyTarget = enemy.TargetObject as GameLiving;
                    if (enemyTarget == null || enemyTarget == _body || !enemyTarget.IsAlive || !memberSet.Contains(enemyTarget))
                        continue;
                    if (!_brain.CanAggroTarget(enemy))
                        continue;
                    notOnTank++;
                    if (enemyTarget is MimicNPC memberNpc && memberNpc.MimicBrain != null && memberNpc.MimicBrain.IsHealer)
                        healerUnderAttack = true;
                }
            }
            SetInt(MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, notOnTank);
            SetBool(MimicWorldStateKeys.HEALER_UNDER_ATTACK, healerUnderAttack);
            SetBool(PeelAvailable, canAct && combat && _brain.IsMainTank && notOnTank > 0 && !_body.IsCasting);
            // Guard is a maintained engine effect: assign once, it holds until death/leave.
            // Candidate scoring lives in the brain so sensor and action cannot disagree.
            // Manual /mguard (or whisper) locks the agent out while its target stands.
            SetBool(MimicWorldStateKeys.GUARD_ACTIVE, _brain.GetOwnGuardee() != null);
            SetBool(GuardAvailable, canAct && !_body.IsCasting && _brain.FindGuardCandidate() != null);
            // Group rez (PR16 corpses): single shared selection in the brain so
            // sensor and action cannot disagree. PR17 only when calm; PR18 lifts
            // the combat gate for battle rez. Realm pre-check lives in
            // FindRezTarget; the engine re-validates at cast time.
            var rezTarget = _brain.FindRezTarget();
            SetObject(MimicWorldStateKeys.MEMBER_TO_REZ, rezTarget);
            int numDead = 0;
            if (_body.Group != null)
            {
                foreach (GameLiving m in _body.Group.GetMembersInTheGroup())
                {
                    if (m != null && !m.IsAlive && m.CurrentRegion == _body.CurrentRegion)
                        numDead++;
                }
            }
            SetInt(MimicWorldStateKeys.NUM_DEAD, numDead);
            var rezGroup = _body.Group?.MimicGroup;
            bool alreadyRezzing = rezGroup != null && rezGroup.AlreadyCastingRez;
            SetBool(MimicWorldStateKeys.ALREADY_CASTING_REZ, alreadyRezzing);
            bool canRez = canAct && !_body.IsSilenced && _brain.HasRezSpell();
            SetBool(MimicWorldStateKeys.CAN_CAST_REZ, canRez);
            // Battle rez (PR18): mid-fight casting is only sane when the caster is
            // clear (nobody slapping them, not being interrupted) and the corpse is
            // nearly alone (at most 2 live enemies near it). No suicide runs: the
            // corpse must already be in range, the caster never chases it.
            bool inCombat = _body.InCombat || _brain.HasAggro;
            bool combatSafe = false;
            if (canRez && rezTarget != null && !alreadyRezzing && inCombat && !_body.IsBeingInterrupted)
            {
                bool casterClear = true;
                int nearCorpse = 0;
                foreach (var entry in _brain.GetOrderedAggroList())
                {
                    var enemy = entry.Item1;
                    if (enemy == null || !enemy.IsAlive || enemy.CurrentRegion != _body.CurrentRegion)
                        continue;
                    if (_body.IsWithinRadius(enemy, 350)) { casterClear = false; break; }
                    if (rezTarget.CurrentRegion == enemy.CurrentRegion
                        && enemy.GetDistanceTo(rezTarget) <= 1000)
                        nearCorpse++;
                }
                combatSafe = casterClear && nearCorpse <= 2;
            }
            SetBool(MimicWorldStateKeys.REZ_COMBAT_SAFE, combatSafe);
            SetBool(RezAvailable, canRez && rezTarget != null && !_body.IsCasting
                && !alreadyRezzing && _body.IsWithinRadius(rezTarget, DOL.AI.Brain.MimicBrain.REZ_RANGE)
                && (!inCombat || combatSafe));
            // Stranger rez (PR17b, opt-in): only when there is really nothing else
            // to do — own group has no dead, nobody needs emergency care, no combat
            // and no aggro. The area scan runs last so it costs nothing otherwise.
            GameLiving outsiderToRez = null;
            bool calm = canRez && !_body.IsCasting && !_body.InCombat && !_brain.HasAggro
                && numDead == 0 && !alreadyRezzing && _brain.RezOutside;
            if (calm)
            {
                int emergency = 0, critical = 0;
                foreach (var m in reachable)
                {
                    if (m.HealthPercent < MimicGroup.EmergencyThreshold) emergency++;
                    if (m.HealthPercent < 25) critical++;
                }
                if (emergency == 0 && critical == 0)
                    outsiderToRez = _brain.FindOutsiderRezTarget();
            }
            SetObject(MimicWorldStateKeys.OUTSIDER_TO_REZ, outsiderToRez);
            SetBool(RezOutsideAvailable, outsiderToRez != null);
            // Self-purge (PR19b): hard CC with a ready Purge RA. Yield while
            // interrupted (Quickcast/kite own that tick).
            var purge = _brain.GetPurgeAbility();
            SetBool(PurgeAvailable, IsBodyValid() && purge != null
                && (_body.IsMezzed || _body.IsStunned || _body.IsRooted)
                && _body.GetSkillDisabledDuration(purge) <= 0);
            // Caster survival: interrupted while casting, or a live enemy sitting on us.
            // Melee/tanks never kite: without ranged casts, running only sheds aggro control.
            // Solo casters mirror the FSM (long flight, hold ring); grouped casters fall
            // back to a kite mate (tank first) instead of fleeing across the map.
            // Main tanks hold the line in groups and never kite.
            bool isCaster = _body.CanCastHarmfulSpells || _brain.IsHealer;
            bool solo = _body.Group == null;
            bool meleed = false;
            if (combat && isCaster)
            {
                foreach (var entry in _brain.GetOrderedAggroList())
                {
                    var enemy = entry.Item1;
                    if (enemy == null || !enemy.IsAlive || enemy.TargetObject != _body)
                        continue;
                    if (_body.IsWithinRadius(enemy, 200)) { meleed = true; break; }
                }
            }
            bool pressured = combat && isCaster && canAct && (_body.IsBeingInterrupted || meleed);
            bool holdRing = false;
            if (solo && combat && isCaster && canAct && _brain.IsFleeing)
            {
                foreach (var entry in _brain.GetOrderedAggroList())
                {
                    var enemy = entry.Item1;
                    if (enemy == null || !enemy.IsAlive || enemy.TargetObject != _body)
                        continue;
                    if (enemy.CurrentRegion != _body.CurrentRegion)
                        continue;
                    if (_body.IsWithinRadius(enemy, 700)) { holdRing = true; break; }
                }
            }
            bool soloKite = solo && DOL.AI.Brain.MimicBrain.ShouldKeepKiting(pressured, _brain.IsFleeing, holdRing);
            bool groupKite = !solo && !_brain.IsMainTank && pressured && _brain.FindKiteMate() != null;
            SetBool(KiteAvailable, soloKite || groupKite);
            var quickCast = _body.GetAbility(Abilities.Quickcast);
            bool quickcastReady = pressured && _body.IsBeingInterrupted && quickCast != null
                && _body.GetSkillDisabledDuration(quickCast) <= 0
                && EffectListService.GetAbilityEffectOnTarget(_body, eEffect.QuickCast) == null;
            SetBool(QuickcastReady, quickcastReady);
            // Caller side of the assist train: selection already put the assist on the
            // focus target before sensing, so this flag is normally false (satisfied).
            // It fires when a better focus target appeared after selection.
            var focus = combat && _brain.IsMainAssist ? _brain.FindFocusTarget() : null;
            SetBool(CallAvailable, canAct && focus != null && _body.TargetObject != (GameObject)focus);
            // Debuff before damage: target missing any major debuff while we hold debuff
            // spells in the harmful pool. Present debuffs count regardless of source so
            // grouped debuffers do not stack redundantly. Instant debuffs already flow
            // through the normal offensive path with recast guards.
            bool canDebuff = _body.HarmfulSpells?.Any(s => s != null && s.IsDebuff) == true;
            bool targetDebuffed = false;
            if (targetValid && canDebuff)
            {
                foreach (var fx in DebuffEffects)
                    if (EffectListService.GetEffectOnTarget(targetLiving, fx) != null) { targetDebuffed = true; break; }
            }
            SetBool(DebuffAvailable, canAct && targetValid && canDebuff && !targetDebuffed
                && !_body.IsSilenced && !_brain.IsFleeing);
            // Positional styles: same gates as the FSM flank loop (melee weapon, attack
            // range, positional styles, not the tank), plus a victim standing still and
            // busy elsewhere. Dancing around an active duelist never pays.
            bool meleeArmed = _body.ActiveWeapon != null
                && _body.ActiveWeapon.Item_Type != (int)eInventorySlot.DistanceWeapon;
            bool victimOpen = targetLiving != null && !targetLiving.IsMoving && targetLiving.TargetObject != _body;
            SetBool(FlankAvailable, canAct && combat && targetValid && meleeArmed && victimOpen
                && _body.IsWithinRadius(targetLiving, _body.attackComponent.AttackRange)
                && _body.CanUsePositionalStyles && !_brain.IsMainTank && !_brain.IsFlanking
                && !_body.IsCasting);
            // New-archery damage type: bow drawn, valid target, best resist-beating type
            // differs from the applied chooser buff. Slash needs no buff (game default).
            // Old-flag shots take type from ammo and ignore the buff: flag stays false.
            bool bowDrawn = _body.ActiveWeapon != null
                && _body.ActiveWeapon.Item_Type == (int)eInventorySlot.DistanceWeapon;
            bool arrowWanted = false;
            if (canAct && combat && targetValid && bowDrawn && !_body.IsCasting
                && Properties.ALLOW_OLD_ARCHERY == false)
            {
                var best = _brain.FindBestArrowType(targetLiving);
                arrowWanted = best != eDamageType.Natural && best != _brain.GetActiveArrowType();
            }
            SetBool(ArrowTypeAvailable, arrowWanted);
        }
    }
}
