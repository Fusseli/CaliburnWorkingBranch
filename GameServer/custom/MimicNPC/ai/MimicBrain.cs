using DOL.Database;
using DOL.GS;
using DOL.GS.API;
using DOL.GS.Effects;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using DOL.GS.RealmAbilities;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.GS.SkillHandler;
using DOL.GS.Spells;
using DOL.GS.Styles;
using DOL.Language;
using DOL.GS.ReGoap.Mimic;
using log4net;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using static DOL.AI.Brain.StandardMobBrain;
using static DOL.GS.Styles.Style;

namespace DOL.AI.Brain
{
    public class MimicBrain : ABrain, IOldAggressiveBrain
    {
        public static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override bool IsActive => Body != null && Body.IsAlive && Body.ObjectState == GameObject.eObjectState.Active;

        public bool IsHealer = false;

        /// <summary>Set by /mheal. Election auto-flags untouched heal-capable bots only.</summary>
        public bool HealerManual = false;

        public MimicReGoapAgent GoapAgent { get; private set; }
        public volatile bool GoapEnabled = true;
        private bool _goapSelectingSpell;
        private string _roleSnapshot;

        public virtual bool TryGoap(MimicDecisionContext context)
        {
            if (!IsActive || MimicBody == null)
                return false;
            MaybeElectRoles();
            GoapAgent ??= new MimicReGoapAgent(MimicBody, this);
            // Commands only change the flag; all agent mutations stay on this thread.
            if (GoapEnabled != GoapAgent.IsEnabled())
            {
                if (GoapEnabled) GoapAgent.Enable();
                else GoapAgent.Disable();
            }
            return GoapAgent.TryThink(context);
        }

        /// <summary>Class-aware role defaults, evaluated only when group membership
        /// actually changes (cheap snapshot string). Runs on the owning AI thread,
        /// same as every other agent mutation.</summary>
        private void MaybeElectRoles()
        {
            var group = Body.Group;
            var mg = group?.MimicGroup;
            if (mg == null)
                return;
            var members = group.GetMembersInTheGroup();
            string snap = members.Count + "|" + string.Join(",", members.Select(m => m?.Name ?? "?").OrderBy(n => n));
            if (snap == _roleSnapshot)
                return;
            _roleSnapshot = snap;
            mg.ElectRoles(members);
        }

        public virtual void SelectGoapAttackTarget()
        {
            // A finished point flight leaves stale flee flags behind when GOAP handled
            // combat: clear them on arrival so spell selection sees a free caster again.
            // Follow-driven kite runs (mate converges) carry no point: clear those once
            // standing still, and stop trailing the mate.
            if (IsFleeing && TargetFleePosition != null && !Body.IsDestinationValid)
            {
                IsFleeing = false;
                TargetFleePosition = null;
            }
            else if (IsFleeing && TargetFleePosition == null && !Body.IsMoving)
            {
                IsFleeing = false;
                Body.StopFollowing();
            }
            var previousTarget = Body.TargetObject;
            if (!CheckMainTankTarget() && !CheckGoapAssistTarget() && !CheckGoapCallTarget())
                Body.TargetObject = CalculateNextAttackTarget();
            if (Body.TargetObject != previousTarget)
                ResetFlanking();
        }

        private bool CheckGoapAssistTarget()
        {
            // Restores the intent of the legacy CheckAssist helper (now dead code):
            // grouped non-assist members open on the main assist target when it is valid.
            // Tanks keep CheckMainTankTarget priority; the assist itself keeps its own selection.
            if (IsMainAssist)
                return false;
            var group = Body.Group?.MimicGroup;
            var mainAssist = group?.MainAssist;
            if (mainAssist == null || !mainAssist.InCombat)
                return false;
            var assistTarget = group.CurrentTarget as GameLiving;
            if (assistTarget == null || !CanAggroTarget(assistTarget))
                return false;
            Body.TargetObject = assistTarget;
            return true;
        }

        private bool CheckGoapCallTarget()
        {
            // Caller side of the assist train: the main assist opens on the highest-value
            // valid enemy instead of the highest-threat one, so the train focuses fire.
            // Everyone else follows via CheckGoapAssistTarget; tanks keep threat duty.
            if (!IsMainAssist)
                return false;
            var focus = FindFocusTarget();
            if (focus == null)
                return false;
            Body.TargetObject = focus;
            return true;
        }

        /// <summary>Highest-value valid enemy: actively casting enemies first (shut down
        /// the cast by focus), then weakest valid targets (secure kills, snowball the
        /// fight). CC'd, invalid and unattackable enemies are never called. A small
        /// stickiness bonus keeps the train from flip-flopping between equal targets.</summary>
        public GameLiving FindFocusTarget()
        {
            GameLiving best = null;
            int bestScore = int.MinValue;
            foreach (var entry in GetOrderedAggroList())
            {
                var enemy = entry.Item1;
                if (enemy == null || !enemy.IsAlive || enemy.ObjectState != GameObject.eObjectState.Active
                    || enemy.CurrentRegion != Body.CurrentRegion || enemy.IsMezzed || enemy.IsStunned || enemy.IsRooted)
                    continue;
                if (!CanAggroTarget(enemy))
                    continue;
                int score = 100 - enemy.HealthPercent;
                if (enemy.IsCasting)
                    score += 50;
                if (enemy == Body.TargetObject)
                    score += 5;
                if (score > bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        public bool ExecuteGoapCallTarget()
        {
            // Planner-visible safety net for focus switches that selection missed.
            // No FSM calls, no recursive GOAP entry; fails closed to let damage handle it.
            var focus = FindFocusTarget();
            if (focus == null)
                return false;
            Body.TargetObject = focus;
            return true;
        }

        public bool ExecuteGoapFlank()
        {
            // Mirrors the FSM flank loop one tick at a time: compute the style point,
            // walk it, latch arrival. A running walk is left alone so movement does
            // not stutter; a victim that turned or moved resets the flank instead of
            // dancing for nothing. Fails closed to let damage handle it.
            var victim = Body.TargetObject as GameLiving;
            if (victim == null || victim.IsMoving || victim.TargetObject == Body)
            {
                ResetFlanking();
                return false;
            }
            if (IsFlanking)
                return true;
            if (TargetFlankPosition != null)
            {
                if (Body.IsDestinationValid)
                    return true;
                if (Body.GetDistance(TargetFlankPosition) < 5)
                {
                    IsFlanking = true;
                    TargetFlankPosition = null;
                    return true;
                }
            }
            IsFlanking = false;
            var point = GetStylePositionPoint(victim, GetPositional());
            if (point == null)
                return false;
            TargetFlankPosition = point;
            Body.StopFollowing();
            Body.StopAttack();
            Body.WalkTo(new Point3D(point.X, point.Y, victim.Z), Body.MaxSpeed);
            return true;
        }

        public bool ExecuteGoapDebuff()
        {
            // Cast the first applicable debuff from the harmful pool, skipping targets
            // that already carry it. Instant debuffs already flow through the normal
            // offensive path with recast guards, so only cast-time spells are picked here.
            // No FSM calls, no recursive GOAP entry; fails closed to let damage handle it.
            _goapSelectingSpell = true;
            try
            {
                var spells = Body.HarmfulSpells;
                if (spells != null)
                {
                    foreach (var spell in spells)
                    {
                        if (spell == null || !spell.IsDebuff)
                            continue;
                        if (!CanCastOffensiveSpell(spell))
                            continue;
                        var target = Body.TargetObject as GameLiving;
                        if (target == null || LivingHasEffect(target, spell))
                            continue;
                        if (CheckOffensiveSpells(spell))
                            return true;
                    }
                }
                return Body.IsCasting;
            }
            finally { _goapSelectingSpell = false; }
        }

        /// <summary>Rank melee damage types by total target mitigation (resist +
        /// worn-armor resist). Pure logic for unit tests; Slash wins ties as the
        /// game default, current type kept within a small margin (no flapping).</summary>
        public static eDamageType BestArrowType(int crushR, int slashR, int thrustR, eDamageType current)
        {
            eDamageType best = eDamageType.Slash;
            int bestScore = slashR;
            if (crushR < bestScore) { bestScore = crushR; best = eDamageType.Crush; }
            if (thrustR < bestScore) { bestScore = thrustR; best = eDamageType.Thrust; }
            int currentScore = current == eDamageType.Crush ? crushR
                : current == eDamageType.Thrust ? thrustR : slashR;
            if (currentScore <= bestScore + 5)
                return current;
            return best;
        }

        /// <summary>Best arrow type for a live target, or Natural when no choice applies.</summary>
        public eDamageType FindBestArrowType(GameLiving target)
        {
            if (target == null || !target.IsAlive)
                return eDamageType.Natural;
            int crushR = target.GetResist(eDamageType.Crush);
            int slashR = target.GetResist(eDamageType.Slash);
            int thrustR = target.GetResist(eDamageType.Thrust);
            var armor = target.Inventory?.GetItem(eInventorySlot.TorsoArmor);
            if (armor != null)
            {
                crushR += SkillBase.GetArmorResist(armor, eDamageType.Crush);
                slashR += SkillBase.GetArmorResist(armor, eDamageType.Slash);
                thrustR += SkillBase.GetArmorResist(armor, eDamageType.Thrust);
            }
            return BestArrowType(crushR, slashR, thrustR, GetActiveArrowType());
        }

        /// <summary>Currently applied chooser buff type, or Natural when none.
        /// Slash is the game default and needs no buff.</summary>
        public eDamageType GetActiveArrowType()
        {
            return AttackComponent.GetMimicArrowType(Body);
        }

        public bool ExecuteGoapArrowType()
        {
            // Apply the chosen damage-type buff directly by spell ID (Blunt 7397,
            // Thrusting 7398, Slashing 7399). Plain handler, no NPC gate, stealth-safe,
            // near-permanent until replaced. Fails closed to let damage handle it.
            if (Body.IsCasting)
                return true;
            var best = FindBestArrowType(Body.TargetObject as GameLiving);
            if (best == eDamageType.Natural || best == GetActiveArrowType())
                return false;
            int spellId = best == eDamageType.Crush ? 7397 : best == eDamageType.Thrust ? 7398 : 7399;
            var spell = SkillBase.GetSpellByID(spellId);
            var line = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
            if (spell == null || line == null)
                return false;
            Body.CastSpell(spell, line);
            return true;
        }

        public bool ExecuteGoapAssist()
        {
            // Planner-visible safety net for mid-fight assist switches that selection missed.
            // No FSM calls, no recursive GOAP entry; fails closed to let damage handle it.
            var assistTarget = Body.Group?.MimicGroup.CurrentTarget as GameLiving;
            if (assistTarget == null || !assistTarget.IsAlive)
                return false;
            Body.TargetObject = assistTarget;
            return true;
        }

        public bool ExecuteGoapPeel()
        {
            // Take the first aggroed enemy off an attacked group member. CC'd enemies
            // are skipped so a peel can never break the group's own control.
            // No FSM calls, no recursive GOAP entry; fails closed to let damage handle it.
            if (Body.Group == null)
                return false;
            foreach (var entry in GetOrderedAggroList())
            {
                var enemy = entry.Item1;
                if (enemy == null || !enemy.IsAlive || enemy.ObjectState != GameObject.eObjectState.Active
                    || enemy.CurrentRegion != Body.CurrentRegion || enemy.IsMezzed || enemy.IsStunned || enemy.IsRooted)
                    continue;
                var enemyTarget = enemy.TargetObject as GameLiving;
                if (enemyTarget == null || enemyTarget == Body || !enemyTarget.IsAlive)
                    continue;
                if (!Body.Group.IsInTheGroup(enemyTarget))
                    continue;
                if (!CanAggroTarget(enemy))
                    continue;
                Body.TargetObject = enemy;
                AttackSelectedTarget(false);
                return Body.IsAttacking || Body.IsMoving || Body.IsCasting;
            }
            return false;
        }

        /// <summary>Manual guard target set via /mguard or whisper. While it stands
        /// (alive, same region, still grouped) the agent leaves guard alone.</summary>
        public GameLiving ManualGuardTarget { get; private set; }
        public void SetManualGuard(GameLiving target) { ManualGuardTarget = target; }
        public void ClearManualGuard() { ManualGuardTarget = null; }

        public GameLiving GetOwnGuardee()
        {
            var group = Body.Group;
            if (group == null) return null;
            foreach (GameLiving m in group.GetMembersInTheGroup())
            {
                if (m == null || m == Body || !m.IsAlive || m.CurrentRegion != Body.CurrentRegion)
                    continue;
                var fx = EffectListService.GetAbilityEffectOnTarget(m, eEffect.Guard) as GuardECSGameEffect;
                if (fx != null && fx.Source == Body)
                    return m;
            }
            return null;
        }

        private static int ScoreGuardCandidate(GameLiving m)
        {
            bool isHealer = m is MimicNPC mn && mn.MimicBrain != null && mn.MimicBrain.IsHealer;
            bool isCaster = (m as GamePlayer)?.CharacterClass?.ClassType == eClassType.ListCaster
                || (m as MimicNPC)?.CharacterClass?.ClassType == eClassType.ListCaster;
            int score = isHealer ? 6 : isCaster ? 4 : 2;
            if (m.attackComponent?.Attackers?.Count > 0) score += 3;
            if (m.HealthPercent < 50) score += 2;
            return score;
        }

        /// <summary>Neediest group member for guard, or null when no action is wanted:
        /// no Guard ability, solo, manual lock standing, or current assignment optimal
        /// (switches only to a clearly needier target, no flapping).</summary>
        public GameLiving FindGuardCandidate()
        {
            if (Body == null || !Body.HasAbility(Abilities.Guard) || Body.Group == null)
                return null;
            if (ManualGuardTarget != null)
            {
                if (ManualGuardTarget.IsAlive && ManualGuardTarget.CurrentRegion == Body.CurrentRegion
                    && Body.Group.IsInTheGroup(ManualGuardTarget))
                    return null;
                ManualGuardTarget = null;
            }
            GameLiving current = GetOwnGuardee();
            GameLiving best = null;
            int bestScore = 0;
            foreach (GameLiving m in Body.Group.GetMembersInTheGroup())
            {
                if (m == null || m == Body || !m.IsAlive || m.CurrentRegion != Body.CurrentRegion)
                    continue;
                int score = ScoreGuardCandidate(m);
                if (score > bestScore) { bestScore = score; best = m; }
            }
            if (best == null || best == current)
                return null;
            if (current != null && bestScore <= ScoreGuardCandidate(current) + 2)
                return null;
            return best;
        }

        public bool ExecuteGoapGuard()
        {
            // One tick of guard assignment; the engine holds the effect afterwards.
            // No FSM calls, no recursive GOAP entry; fails closed to let other work run.
            var candidate = FindGuardCandidate();
            if (candidate == null)
                return false;
            return SetGuard(candidate, out _);
        }

        /// <summary>Manual rez opt-out via /mrez. While set, neither GOAP nor the
        /// legacy defensive path will cast resurrection spells on this bot.</summary>
        public bool RezDisabled;

        /// <summary>Opt-in via "/mrez outside": rez nearby same-realm strangers,
        /// but only when the own group is fully calm (PR17b). Default off.</summary>
        public bool RezOutside;

        /// <summary>Standard rez range: all realm rez spells use 1500 units.</summary>
        public const int REZ_RANGE = 1500;

        /// <summary>PR26/PR27: does this bot fight at range - a bow, crossbow or thrown
        /// weapon equipped in the distance slot?
        ///
        /// PR27: this MUST read the inventory, never Body.ActiveWeaponSlot. The active
        /// slot is a momentary action state, not a loadout fact: this class switches to
        /// Distance immediately before every archery cast, and back to Standard/TwoHanded
        /// on the melee and puller-reset paths. So an archer's slot flips several times
        /// inside one fight, and a sensor sampling it got a coin flip - which is why
        /// archers still kited while in melee but correctly stopped when out of it.
        ///
        /// This is the same expression PerformPull already uses to choose a ranged pull,
        /// so the two now share one definition of "ranged attacker" rather than three
        /// ad-hoc tests. Stable for the whole fight, and semantically the right question
        /// for both gates: a ranged attacker wants to hold its range, so it should not
        /// kite and should not spend decisions debuffing.</summary>
        public bool IsBowUser
            => Body != null
            && Body.Inventory != null
            && Body.Inventory.GetItem(eInventorySlot.DistanceWeapon) != null;

        /// <summary>PR36: is this bot holding a melee weapon? Copied from the expression
        /// TacticalSensor already uses for FlankAvailable, but promoted here so flank and
        /// kite are the same question and cannot disagree.
        ///
        /// Item_Type is the item's slot type, so comparing it against the distance slot
        /// answers "is the active weapon something you swing at" rather than "does it have
        /// a bow somewhere in the pack".</summary>
        public bool IsMeleeArmed
            => Body?.ActiveWeapon != null
            && Body.ActiveWeapon.Item_Type != (int)eInventorySlot.DistanceWeapon;

        /// <summary>PR36: shortest offensive range that makes running away worthwhile.</summary>
        public const int KITE_MIN_OFFENSIVE_RANGE = 1500;

        /// <summary>PR36: does this bot have an attack that actually reaches past melee?
        ///
        /// This is the honest test for "is it a kiter", and it replaces CanCastHarmfulSpells
        /// which only asked "does it have any harmful spell at all". Mimsies are built from
        /// their class specs, so every one of them has *something* harmful - a weapon spec
        /// contributes a short-range harm - which is why pure melee classes were kiting.
        /// A Thane or Valewalker also trains a caster spec, and that nuke reaches well past
        /// melee, so the same test correctly keeps them able to kite at range.
        ///
        /// Tunable rather than derived, because the exact range of a weapon spec's harm is
        /// data, and a const can be adjusted live without touching the database.</summary>
        public bool HasRangedOffensive
            => Body?.HarmfulSpells != null
            && Body.HarmfulSpells.Any(s => s != null && s.Range >= KITE_MIN_OFFENSIVE_RANGE);

        #region PR32 - bard / minstrel melee policy

        /// <summary>PR32: is this a Bard? Bards are their realm's support class -
        /// healer, buffer, demezzer and speed - so a grouped Bard stays off the frontline
        /// and works the instrument.</summary>
        public bool IsBard
            => MimicBody?.CharacterClass != null
            && MimicBody.CharacterClass.ID == (int)eCharacterClass.Bard;

        /// <summary>PR32: is this a Minstrel? Unlike a Bard a Minstrel is a legitimate
        /// melee attacker as well as a singer, so the frontline restriction does not apply
        /// to it.</summary>
        public bool IsMinstrel
            => MimicBody?.CharacterClass != null
            && MimicBody.CharacterClass.ID == (int)eCharacterClass.Minstrel;

        /// <summary>PR32: the frontline rule, as a pure decision so it is unit-testable.
        ///
        /// Only a Bard in a group is held back:
        ///  - grouped Bard  - stays in support; its melee is poor and its heal, CC, cleanse
        ///    and speed are what the group is actually missing
        ///  - solo Bard     - no one to support, so it fights for itself
        ///  - Minstrel      - never held back; it is the better melee class of the two and
        ///    is meant to trade blows while it songs
        ///  - anyone else   - unchanged
        ///
        /// Deliberately not applied to interrupts: ExecuteGoapInterrupt melee-pressures an
        /// enemy caster before falling back to an instant, and that rupt is precisely the one
        /// melee a Bard is still allowed to make.
        /// </summary>
        public static bool MayMeleeAttack(bool isBard, bool isMinstrel, bool hasGroup)
            => !isBard || isMinstrel || !hasGroup;

        /// <summary>PR32: should this bot be allowed to swing at things? See
        /// <see cref="MayMeleeAttack"/> for the policy. Used by both the GOAP sensor and
        /// the FSM so the two paths cannot disagree.</summary>
        public bool MayAttackInMelee
            => Body != null && MayMeleeAttack(IsBard, IsMinstrel, Body.Group != null);

        /// <summary>PR32: why attacking is withheld, or null when it is not. Null exactly
        /// when attacking is permitted, which makes it directly testable.</summary>
        public string MeleeRestrictionReason()
            => MayAttackInMelee ? null : "grouped Bard keeps to support";

        #endregion

        #region PR31 - songs

        /// <summary>PR33: the song chosen on the tick the bot stepped onto the instrument,
        /// cast on the next one. Cleared whenever the decision is abandoned.</summary>
        private Spell _pendingSong;

        /// <summary>Every song this bot can play: spells with NeedInstrument, taken from the
        /// same usable skill list everything else uses. Spell.NeedInstrument is exactly
        /// InstrumentRequirement != 0, so this is a pure data test - Bard/Minstrel songs,
        /// the five Bard_Speed_Song tiers, everything else the DB gives an instrument
        /// requirement all arrive here without hard-coding a single song name.
        ///
        /// PR33: this reads GetAllUsableSkills(), NOT GetAllUsableListSpells(). Those are two
        /// different lists on MimicNPC and only the first one holds songs: the "// Add Songs"
        /// block lives inside GetAllUsableSkills and writes into m_usableSkills as
        /// Tuple&lt;Skill, Skill&gt; where Item1 is the spell. GetAllUsableListSpells returns
        /// m_usableListSpells - the list-caster spell set - which has no songs in it at all.
        /// Reading the wrong one silently returned an empty list, so CanPlaySongs was always
        /// false, SongAvailable was never set, and SongGoal never fired.</summary>
        public List<Spell> GetSongs()
        {
            var songs = new List<Spell>();
            if (MimicBody == null)
                return songs;
            foreach (var entry in MimicBody.GetAllUsableSkills())
            {
                if (entry?.Item1 is Spell spell && spell.NeedInstrument)
                    songs.Add(spell);
            }
            return songs;
        }

        /// <summary>Which slot holds an instrument we could play from. Bards get one in
        /// TwoHandWeapon and one in DistanceWeapon (MimicNPC.SetWeapons), so prefer the
        /// distance slot - that is also the stance the old dead code assumed.</summary>
        public eActiveWeaponSlot GetInstrumentStance()
        {
            if (Body?.Inventory == null)
                return eActiveWeaponSlot.Standard;
            if (Body.Inventory.GetItem(eInventorySlot.DistanceWeapon) != null)
                return eActiveWeaponSlot.Distance;
            if (Body.Inventory.GetItem(eInventorySlot.TwoHandWeapon) != null)
                return eActiveWeaponSlot.TwoHanded;
            return eActiveWeaponSlot.Standard;
        }

        /// <summary>Can this bot actually play a song right now: does it know any, and does
        /// it hold something to play them with?</summary>
        public bool CanPlaySongs
        {
            get
            {
                if (Body == null || Body.Inventory == null)
                    return false;
                return GetSongs().Count > 0 && GetInstrumentStance() != eActiveWeaponSlot.Standard;
            }
        }

        /// <summary>PR31: is a song already pulsing on this bot?
        ///
        /// This matters more than it looks. MimicNPC.SwitchWeapon cancels every active
        /// pulse whose InstrumentRequirement is set, so a bot that flips weapons each tick
        /// cancels its own song the moment it starts. That is why the old song code was
        /// switched off: nothing held the stance, so songs could never survive a decision.</summary>
        public bool IsSongPulsing
        {
            get
            {
                if (Body == null)
                    return false;
                // ActivePulseSpells is a Dictionary<eSpellType, Spell> - the values are the
                // pulsing spells themselves, not effect objects.
                foreach (var spell in Body.ActivePulseSpells.Values)
                {
                    if (spell != null && spell.NeedInstrument)
                        return true;
                }
                return false;
            }
        }

        /// <summary>PR31: pick the song worth playing right now, or null.
        ///
        /// Ordered by how much the bot's realm depends on it, which is the opposite order
        /// to how urgent each individual song is:
        ///   - Speed first. Bards and Minstrels are their realm's speed class, and the
        ///     speed song is a group-wide buff, so it is wanted before every fight and
        ///     re-applied through the post-fight regen window.
        ///   - Then endurance and mana, which are group resources that run down.
        ///   - Then healing, and only as a supplement: a health regen song is a slow
        ///     over-time, never a substitute for a real heal, so it must never outrank
        ///     HealGroupGoal. It is ranked below every real heal on purpose.
        ///
        /// PR39: a song that is on cooldown is not a candidate. The goal used to pick the
        /// best song by value and never look at the recast, so a Bard whose long speed song
        /// had a multi-minute recast was offered that same song on every single tick. The
        /// action then failed on the cooldown check, the goal fell back to the FSM, the FSM
        /// put the instrument away, and the next tick stepped onto it again - an endless
        /// weapon flip that cancelled the in-flight cast and the pulse every time.
        ///
        /// Pure decision, so it is unit-testable without a live body.</summary>
        public Spell GetBestSong(bool canBuffUpGroup, bool groupNeedsMana,
            bool groupNeedsEndurance, bool groupNeedsHealth)
        {
            Spell speed = null, endur = null, mana = null, heal = null;
            foreach (var song in GetSongs())
            {
                if (song == null || IsSongOnCooldown(song))
                    continue;

                switch (song.SpellType)
                {
                    case eSpellType.SpeedEnhancement:
                    case eSpellType.SpeedOfTheRealm:
                    case eSpellType.CombatSpeedBuff:
                    case eSpellType.CelerityBuff:
                        // PR35: the only gated song, and gated on the honest signal. Speed is
                        // a group-wide buff for between fights, so it waits for a safe moment.
                        if (canBuffUpGroup)
                            speed = PickHigherTier(speed, song);
                        break;
                    case eSpellType.EnduranceRegenBuff:
                    case eSpellType.AllRegenBuff:
                    case eSpellType.PowerHealthEnduranceRegenBuff:
                        // PR35: deliberately NOT gated. Endurance is what the group burns
                        // through in a fight, and a singer exists to put it back - taking
                        // this away mid-fight would make bards and minstrels worse exactly
                        // when they are needed.
                        if (groupNeedsEndurance)
                            endur = PickHigherTier(endur, song);
                        break;
                    case eSpellType.PowerRegenBuff:
                    case eSpellType.HeroismBuff:
                        // Same reasoning: mid-fight mana regen is wanted, not withheld.
                        if (groupNeedsMana)
                            mana = PickHigherTier(mana, song);
                        break;
                    case eSpellType.HealthRegenBuff:
                        if (groupNeedsHealth)
                            heal = PickHigherTier(heal, song);
                        break;
                }
            }
            return speed ?? endur ?? mana ?? heal;
        }

        /// <summary>
        /// PR39: is this song still on cooldown? A song with a multi-minute recast was
        /// offered on every tick, the cast failed on every tick, and the fallback to the
        /// FSM flipped the instrument away and back in a loop.
        /// </summary>
        public bool IsSongOnCooldown(Spell song)
        {
            if (song == null || Body == null)
                return false;
            return Body.GetSkillDisabledDuration(song) > 0;
        }

        /// <summary>Songs come in five tiers (Bard_Speed_Song runs Clear Path 144% through
        /// Clear Horizon 204%). Higher Value is the better song, so take the strongest tier
        /// the bot owns rather than whichever the list happened to yield first.</summary>
        private static Spell PickHigherTier(Spell current, Spell candidate)
            => current == null || candidate.Value > current.Value ? candidate : current;

        /// <summary>PR34/PR35: how good a song is. Lower is better, matching the order
        /// GetBestSong has always preferred: speed, then endurance, then mana, then health.
        /// Exposed as a pure function so the "is this song worth interrupting for?"
        /// decision can be unit-tested without a live pulse.
        ///
        /// The health regen song is last on purpose - it is a slow over-time and never a
        /// substitute for a real heal.</summary>
        public static int SongRank(eSpellType type)
        {
            switch (type)
            {
                case eSpellType.SpeedEnhancement:
                case eSpellType.SpeedOfTheRealm:
                case eSpellType.CombatSpeedBuff:
                case eSpellType.CelerityBuff:
                    return 0;
                case eSpellType.EnduranceRegenBuff:
                case eSpellType.AllRegenBuff:
                case eSpellType.PowerHealthEnduranceRegenBuff:
                    return 1;
                case eSpellType.PowerRegenBuff:
                case eSpellType.HeroismBuff:
                    return 2;
                case eSpellType.HealthRegenBuff:
                    return 3;
                default:
                    return int.MaxValue;
            }
        }

        /// <summary>PR34: should a running song be cancelled in favour of a better one?
        /// Strictly better only - an equal-rank song is left alone so the bot does not
        /// churn a perfectly good buff back and forth.</summary>
        public static bool ShouldYieldSong(int runningRank, int wantedRank)
            => wantedRank < runningRank;

        /// <summary>PR34: the song currently pulsing on this bot, or null. Found from
        /// ActivePulseSpells, which is keyed by eSpellType, so the rank is right there in
        /// the dictionary key.</summary>
        private Spell GetPulsingSong(out int rank)
        {
            rank = int.MaxValue;
            if (Body == null)
                return null;
            foreach (var entry in Body.ActivePulseSpells)
            {
                if (entry.Value == null || !entry.Value.NeedInstrument)
                    continue;
                int candidate = SongRank(entry.Key);
                if (candidate < rank)
                {
                    rank = candidate;
                    // Keep going only if this is still the best we have seen.
                }
                if (rank == 0)
                    break;
            }
            foreach (var entry in Body.ActivePulseSpells)
            {
                if (entry.Value != null && entry.Value.NeedInstrument && SongRank(entry.Key) == rank)
                    return entry.Value;
            }
            return null;
        }

        /// <summary>PR34: stop the running song so the next tick can play a better one.
        /// Same call MimicNPC.SwitchWeapon uses when it has to break a pulse.</summary>
        private void StopPulsingSong(Spell song)
        {
            if (Body == null || song == null)
                return;
            var effect = EffectListService.GetPulseEffectOnTarget(Body, song);
            if (effect != null)
                EffectService.RequestImmediateCancelEffect(effect);
            log.Debug($"Song: {MimicBody.Name} stopping {song.Name} to switch to a better song");
        }


        ///
        /// Playing from the instrument slot matters because SpellHandler.CheckInstrument()
        /// tests the caster's ACTIVE weapon, and MimicNPC.SwitchWeapon cancels any pulse
        /// already running - so the stance has to be committed and held.
        ///
        /// PR33: the switch and the cast are deliberately separate ticks. Doing both in one
        /// call means casting in the same breath that changed the active weapon, and
        /// CheckInstrument() can reject the cast against the outgoing weapon - which looks
        /// exactly like "the song animation played and then nothing happened". One tick to
        /// step into position, the next to play.
        ///
        /// Fails closed so the planner simply tries something else next decision.</summary>
        public bool ExecuteGoapSong()
        {
            if (Body == null || !CanPlaySongs)
            {
                _pendingSong = null;
                _stanceCommitted = false;
                return false;
            }

            // PR35: one honest gate replaces PR34's "!inCombat || regenWindow".
            var canBuffUpGroup = CanBuffUpGroup();

            // PR34: a pulsing song runs on its own power, so without this a bot that
            // started the wrong one held it indefinitely and never upgraded - a minstrel
            // that took health regen after a wipe could keep it through the whole lull.
            // Yield only for a strictly better song, so a good buff is never churned.
            if (IsSongPulsing)
            {
                var running = GetPulsingSong(out int runningRank);
                var wanted = GetBestSong(canBuffUpGroup, GroupNeedsMana(),
                    GroupNeedsEndurance(), GroupNeedsHealth());
                if (running != null && wanted != null
                    && ShouldYieldSong(runningRank, SongRank(wanted.SpellType)))
                {
                    StopPulsingSong(running);
                    _pendingSong = wanted;
                    // PR39: the new song is committed too - the instrument must survive the
                    // tick between stopping the old song and starting the new one.
                    _stanceCommitted = true;
                    return true;
                }
                _pendingSong = null;
                _stanceCommitted = false;
                return false;
            }

            // Tick one: step onto the instrument and stop. The next tick casts.
            var stance = GetInstrumentStance();
            if (Body.ActiveWeaponSlot != stance)
            {
                // PR39: the song that is about to be played is chosen HERE and remembered,
                // not nulled. It used to be nulled, so the song was re-derived on the cast
                // tick from whatever the group needed at that moment - and the two ticks
                // were not atomic, so a different goal could step in between and the
                // instrument would be put away again. Remembering it also means the melee
                // stance knows a cast is pending and leaves the instrument alone.
                _pendingSong = GetBestSong(canBuffUpGroup, GroupNeedsMana(),
                    GroupNeedsEndurance(), GroupNeedsHealth());
                if (_pendingSong == null)
                    return false;

                _stanceCommitted = true;
                Body.SwitchWeapon(stance);
                log.Debug($"Song: switching to {stance} to play {MimicBody.Name}'s next song");
                return true;
            }

            var song = _pendingSong;
            _pendingSong = null;
            _stanceCommitted = false;
            song ??= GetBestSong(canBuffUpGroup, GroupNeedsMana(),
                GroupNeedsEndurance(), GroupNeedsHealth());
            if (song == null)
                return false;
            if (IsSongOnCooldown(song))
            {
                log.Debug($"Song: {song.Name} still on cooldown for {MimicBody.Name}");
                return false;
            }
            _goapSelectingSpell = true;
            try
            {
                Body.TargetObject = Body;
                bool cast = Body.CastSpell(song, m_mobSpellLine);
                // PR33: log it. Songs failed silently for a long time - the only visible
                // sign was an animation with no effect, and nothing said why.
                log.Debug($"Song: {MimicBody.Name} cast {song.Name} ({song.SpellType}) = {cast}"
                    + (cast ? "" : $" [active weapon {Body.ActiveWeapon}:{(Body.ActiveWeapon == null ? "none" : Body.ActiveWeapon.Object_Type.ToString())}]"));
                return cast;
            }
            finally { _goapSelectingSpell = false; }
        }

        /// <summary>PR33: melee wants the sword, not the drum. A singer that just put the
        /// instrument away is holding it, and hitting with it is both weak and wrong-looking.
        /// Bards and Minstrels keep their melee weapon precisely for rupts and for playing
        /// solo, so they switch to it before swinging and back to the instrument to sing.
        ///
        /// PR39: a committed song cast keeps the instrument. The song is two ticks - step
        /// onto the instrument, then cast - and anything that ran in between would put the
        /// instrument away again. Every flip cancels the in-flight cast AND the running
        /// pulse (MimicNPC.SwitchWeapon), so a Bard that was flipped back and forth could
        /// never finish a song: the weapon flip and the song fought each other.
        /// </summary>
        private bool PrepareMeleeStance()
        {
            if (Body?.Inventory == null || IsSongPulsing)
                return false;
            // PR39: a song cast is already committed - the instrument stays.
            if (_stanceCommitted)
                return false;
            if (Body.ActiveWeaponSlot == eActiveWeaponSlot.Standard)
                return false;
            bool hasMelee = Body.Inventory.GetItem(eInventorySlot.RightHandWeapon) != null
                || Body.Inventory.GetItem(eInventorySlot.TwoHandWeapon) != null;
            if (!hasMelee)
                return false;
            Body.SwitchWeapon(eActiveWeaponSlot.Standard);
            log.Debug($"Melee stance: {MimicBody.Name} putting the instrument away");
            return true;
        }

        /// <summary>PR39: is a song cast committed? Set when the bot steps onto the
        /// instrument, cleared when the song is cast or abandoned. While it is set, the
        /// melee stance leaves the instrument alone.</summary>
        private bool _stanceCommitted;

        /// <summary>PR31: while a song is pulsing the bot must stay on the instrument.
        /// Every caller that would switch to a melee weapon has to ask this first.</summary>
        public bool ShouldHoldSongStance
            => IsSongPulsing && Body != null && Body.ActiveWeaponSlot == GetInstrumentStance();

        /// <summary>PR31/PR35: is this bot in a safe moment to put the group speed buff up?
        ///
        /// PR35: PR34 answered this with "!InCombat || regenWindow", and both halves were
        /// wrong. Body.InCombat is a sticky flag that outlives a fight - and the minstrel in
        /// the test had gone to *melee*, so its speed song stayed blocked after the battle
        /// while the ungated mana song kept being picked. It sang PowerRegenBuff and never
        /// came back to speed. The health condition on top was wrong in principle too: a
        /// hurt singer should heal, and HealGroupGoal (50) and EmergencyHealGoal (100)
        /// already outrank the song (45), so the song never needed to second-guess them -
        /// and demanding 70% health blocked the speed song exactly when the bot wanted a
        /// group buff most.
        ///
        /// Aggro is the honest signal: HasAggro is !AggroList.IsEmpty, and that table
        /// empties when the enemies actually die. No health term on purpose.
        ///
        /// This gates the SPEED song only. Endurance, mana and health songs stay ungated -
        /// a singer is needed for those mid-fight, which is the whole point of the class.</summary>
        public bool CanBuffUpGroup()
        {
            if (Body == null || HasAggro)
                return false;                       // something is on me right now
            if (Body.Group == null)
                return true;                        // solo and calm
            foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
            {
                // MimicBrain is not on GameLiving, so this needs the cast. A group-mate
                // still being beaten on means the group is not ready to be buffed.
                if (member is MimicNPC mate && mate.MimicBrain != null && mate.MimicBrain.HasAggro)
                    return false;
            }
            return true;
        }

        /// <summary>PR31: does anybody in the group need mana topped up? Mirrors the
        /// threshold the old dead song code used (ManaPercent &lt; 80 on a mana class).</summary>
        public bool GroupNeedsMana()
        {
            if (Body == null)
                return false;
            if (Body.Group == null)
                return Body.MaxMana > 0 && Body.ManaPercent < 80;
            foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
            {
                if (member != null && member.MaxMana > 0 && member.ManaPercent < 80)
                    return true;
            }
            return false;
        }

        /// <summary>PR31: group endurance running down, which is what a solo endurance song
        /// exists for.</summary>
        public bool GroupNeedsEndurance()
        {
            if (Body == null)
                return false;
            if (Body.Group == null)
                return Body.EndurancePercent < 60;
            foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
            {
                if (member != null && member.EndurancePercent < 60)
                    return true;
            }
            return false;
        }

        /// <summary>PR31: someone is hurt enough to be worth topping up. Deliberately a low
        /// bar - the health song is a supplement that must never delay a real heal, so it
        /// only gets played when something is genuinely low.</summary>
        public bool GroupNeedsHealth()
        {
            if (Body == null)
                return false;
            if (Body.Group == null)
                return Body.HealthPercent < Properties.NPC_HEAL_THRESHOLD;
            foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
            {
                if (member != null && member.HealthPercent < Properties.NPC_HEAL_THRESHOLD)
                    return true;
            }
            return false;
        }

        #endregion

        /// <summary>How far this bot can actually reach a corpse with the rez spells it
        /// holds. The sensor used to gate on the flat REZ_RANGE while the action gated on
        /// the spell's own range, so any corpse in between was planned every tick and then
        /// refused - and each refusal costs a replan plus a full resense. The sensor now
        /// asks this instead, so the two can never disagree. Never above REZ_RANGE, and 0
        /// when the bot cannot rezz at all.</summary>
        public int GetRezSpellRange()
        {
            if (Body == null)
                return 0;
            int best = 0;
            ConsiderPool(Body.MiscSpells);
            ConsiderPool(Body.InstantMiscSpells);
            return Math.Min(REZ_RANGE, best);

            void ConsiderPool(IEnumerable<Spell> pool)
            {
                if (pool == null)
                    return;
                foreach (var spell in pool)
                    if (spell != null && spell.SpellType == eSpellType.Resurrect && spell.Range > best)
                        best = spell.Range;
            }
        }

        /// <summary>True when any misc spell is a resurrection spell.</summary>
        public bool HasRezSpell()
        {
            if (Body == null)
                return false;
            if (Body.MiscSpells != null)
            {
                foreach (var spell in Body.MiscSpells)
                {
                    if (spell != null && spell.SpellType == eSpellType.Resurrect)
                        return true;
                }
            }
            if (Body.InstantMiscSpells != null)
            {
                foreach (var spell in Body.InstantMiscSpells)
                {
                    if (spell != null && spell.SpellType == eSpellType.Resurrect)
                        return true;
                }
            }
            return false;
        }

        /// <summary>Nearest dead group member that may be rezzed (PR16 corpse):
        /// grouped, same region, same realm per server rules (engine re-validates
        /// at cast). Shared by the sensor and both cast paths so they agree.</summary>
        public GameLiving FindRezTarget()
        {
            if (Body == null || Body.Group == null || RezDisabled || !HasRezSpell())
                return null;
            GameLiving best = null;
            int bestDist = int.MaxValue;
            foreach (GameLiving m in Body.Group.GetMembersInTheGroup())
            {
                if (m == null || m == Body || m.IsAlive)
                    continue;
                if (m.ObjectState != GameObject.eObjectState.Active || m.CurrentRegion != Body.CurrentRegion)
                    continue;
                if (!GameServer.ServerRules.IsSameRealm(Body, m, true))
                    continue;
                int d = Body.GetDistanceTo(m);
                if (d > REZ_RANGE || d >= bestDist)
                    continue;
                best = m;
                bestDist = d;
            }
            return best;
        }

        public bool ExecuteGoapRez()
        {
            // One tick of rez casting through the existing defensive-spell path;
            // the ResurrectSpellHandler enforces range, LoS and realm at cast.
            // An interrupted caster yields (Quickcast/kite handle pressure).
            // No FSM calls, no recursive GOAP entry; fails closed to let healing run.
            if (Body.IsBeingInterrupted)
                return false;
            var corpse = FindRezTarget();
            if (corpse == null)
                return false;
            var group = Body.Group?.MimicGroup;
            // PR43: ask the LIVE cast state, not the cached flag. The cached flag was set
            // when a rez was REQUESTED and was only ever cleared by CheckGroupHealth -
            // which is reached only through CheckHeals, which only runs on a living bot
            // that can heal. So if the group's only heal-capable member died, the flag
            // stuck true and no corpse was ever rezzed again.
            if (IsGroupCastingRez())
                return false;
            _goapSelectingSpell = true;
            try
            {
                if (Body.MiscSpells != null)
                {
                    foreach (var spell in Body.MiscSpells)
                    {
                        if (spell == null || spell.SpellType != eSpellType.Resurrect)
                            continue;
                        if (!CanCastDefensiveSpell(spell))
                            continue;
                        if (!Body.IsWithinRadius(corpse, spell.Range))
                            continue;
                    Body.TargetObject = corpse;
                    if (Body.CastSpell(spell, m_mobSpellLine))
                    {
                        // PR43: the cached flag is no longer set here. It was set when the
                        // REQUEST was made, and the only thing that ever cleared it was
                        // CheckGroupHealth - which is reached only through CheckHeals, and
                        // that only runs on a living bot that can heal. So it could stick
                        // true forever and no corpse would ever be rezzed again. The live
                        // cast state is the truth, and it cannot get stuck.
                        return true;
                    }
                    // One spell refusing (mana, GCD, a cast still finishing) must not
                    // abandon the rest of the pool: keep looking for another rez.
                    continue;
                }
            }
            if (Body.InstantMiscSpells != null)
            {
                foreach (var spell in Body.InstantMiscSpells)
                {
                    if (spell == null || spell.SpellType != eSpellType.Resurrect)
                        continue;
                    if (!CanCastDefensiveSpell(spell))
                        continue;
                    if (!Body.IsWithinRadius(corpse, spell.Range))
                        continue;
                    Body.TargetObject = corpse;
                    if (Body.CastSpell(spell, m_mobSpellLine))
                    {
                        // PR43: the cached flag is no longer set here. It was set when the
                        // REQUEST was made, and the only thing that ever cleared it was
                        // CheckGroupHealth - which is reached only through CheckHeals, and
                        // that only runs on a living bot that can heal. So it could stick
                        // true forever and no corpse would ever be rezzed again. The live
                        // cast state is the truth, and it cannot get stuck.
                        return true;
                    }
                    continue;
                }
            }
            }
            finally { _goapSelectingSpell = false; }
            return false;
        }

        /// <summary>Nearest dead stranger (non-group player or foreign mimic corpse)
        /// that may be rezzed, or null. Only when fully calm: no combat, no aggro,
        /// own group has no dead. Never chases: the corpse must already be in rez
        /// range. Opt-in via RezOutside; the engine re-validates realm at cast.</summary>
        public GameLiving FindOutsiderRezTarget()
        {
            if (Body == null || RezDisabled || !RezOutside || !HasRezSpell())
                return null;
            if (Body.InCombat || HasAggro)
                return null;
            // Scan radius is what this bot can actually reach, not the flat REZ_RANGE:
            // otherwise the scan happily returns a stranger the action then refuses for
            // being out of spell range, and the goal is planned and dropped every tick.
            int reach = GetRezSpellRange();
            if (reach <= 0)
                return null;
            GameLiving best = null;
            int bestDist = int.MaxValue;
            // Dead players linger Active and targetable like our own corpses.
            foreach (GamePlayer p in Body.GetPlayersInRadius((ushort)reach))
            {
                if (p == null || ReferenceEquals(p, Body) || p.IsAlive)
                    continue;
                if (p.CurrentRegion != Body.CurrentRegion)
                    continue;
                if (Body.Group != null && Body.Group.IsInTheGroup(p))
                    continue;
                if (!GameServer.ServerRules.IsSameRealm(Body, p, true))
                    continue;
                int d = Body.GetDistanceTo(p);
                if (d >= bestDist)
                    continue;
                best = p;
                bestDist = d;
            }
            // Dead mimics of other groups linger Active too (PR16 corpses).
            foreach (GameNPC n in Body.GetNPCsInRadius((ushort)reach))
            {
                if (n is not MimicNPC m || m == Body || m.IsAlive)
                    continue;
                if (m.CurrentRegion != Body.CurrentRegion)
                    continue;
                if (Body.Group != null && Body.Group.IsInTheGroup(m))
                    continue;
                if (!GameServer.ServerRules.IsSameRealm(Body, m, true))
                    continue;
                int d = Body.GetDistanceTo(m);
                if (d >= bestDist)
                    continue;
                best = m;
                bestDist = d;
            }
            return best;
        }

        /// <summary>Read an object the sensors already put in the live world state, or null.
        /// Lets an action act on exactly what the goal was planned against instead of
        /// re-deriving it. Reads the same state object the sensors write
        /// (ReGoapAgent.memory.GetWorldState()).</summary>
        public object GetGoapObject(string key)
        {
            var memory = GoapAgent?.GetMemory();
            if (memory == null || string.IsNullOrEmpty(key))
                return null;
            try
            {
                return memory.GetWorldState()?.Get(key);
            }
            catch
            {
                return null;
            }
        }

        public bool ExecuteGoapRezOutside()
        {
            // One tick of stranger rez through the existing defensive-spell path.
            // No group coordination flag: other groups may race us, the first
            // completed cast wins and late casts fizzle safe on a living target.
            // No FSM calls, no recursive GOAP entry; fails closed to let other work run.
            // The corpse comes from the world state the sensor already chose. Re-scanning
            // here meant a second radius-wide player+NPC sweep every decision and let the
            // action rez a different stranger than the goal was planned for.
            GameLiving corpse = GetGoapObject(DOL.GS.Scripts.ReGoap.MimicWorldStateKeys.OUTSIDER_TO_REZ) as GameLiving;
            if (corpse == null || !corpse.IsAlive)
                corpse = FindOutsiderRezTarget();
            if (corpse == null)
                return false;
            _goapSelectingSpell = true;
            try
            {
                if (Body.MiscSpells != null)
                {
                    foreach (var spell in Body.MiscSpells)
                    {
                        if (spell == null || spell.SpellType != eSpellType.Resurrect)
                            continue;
                        if (!CanCastDefensiveSpell(spell))
                            continue;
                        if (!Body.IsWithinRadius(corpse, spell.Range))
                            continue;
                        Body.TargetObject = corpse;
                        if (Body.CastSpell(spell, m_mobSpellLine))
                            return true;
                        continue;
                    }
                }
                if (Body.InstantMiscSpells != null)
                {
                    foreach (var spell in Body.InstantMiscSpells)
                    {
                        if (spell == null || spell.SpellType != eSpellType.Resurrect)
                            continue;
                        if (!CanCastDefensiveSpell(spell))
                            continue;
                        if (!Body.IsWithinRadius(corpse, spell.Range))
                            continue;
                        Body.TargetObject = corpse;
                        if (Body.CastSpell(spell, m_mobSpellLine))
                            return true;
                        continue;
                    }
                }
            }
            finally { _goapSelectingSpell = false; }
            return false;
        }

        /// <summary>Owned Purge RA, or null. Purge is bought by the auto-buyer
        /// (PR19a) like any class RA.</summary>
        public PurgeAbility GetPurgeAbility()
        {
            if (MimicBody == null)
                return null;
            foreach (var ab in MimicBody.GetRealmAbilities())
            {
                if (ab is PurgeAbility purge)
                    return purge;
            }
            return null;
        }

        public bool ExecuteGoapPurge()
        {
            // One tick of self-purge through the RA's own Execute (reuse timing
            // included). No FSM calls, no recursive GOAP entry; fails closed.
            var purge = GetPurgeAbility();
            if (purge == null)
                return false;
            if (Body.GetSkillDisabledDuration(purge) > 0)
                return false;
            purge.Execute(Body);
            return true;
        }

        public bool ExecuteGoapKite()
        {
            // Solo: FSM-parity long flight. A finished flight is judged, never blindly
            // restarted: still inside the hold range -> run again, far enough -> stand
            // still, face the enemy and let the sensor drop KiteAvailable so the caster
            // can actually cast. That terminal state is what makes this kiting instead of
            // a 400-unit hop loop (run, turn, run, turn).
            // Grouped (non-tank): fall back to a kite mate so peel/guard take over
            // instead of fleeing across the map; hold there while pressured.
            // No FSM calls, no recursive GOAP entry; fails closed to let damage handle it.
            if (Body.Group == null)
            {
                if (IsFleeing && Body.IsDestinationValid)
                    return true;
                int distanceToTarget = Body.TargetObject != null
                    ? Body.GetDistance(Body.TargetObject)
                    : 0;
                if (Body.TargetObject != null && IsFleeing && TargetFleePosition != null
                    && !ShouldKeepFleeing(distanceToTarget, KITE_HOLD_RANGE))
                {
                    // Flight finished and the pursuer is outside the hold range.
                    IsFleeing = false;
                    TargetFleePosition = null;
                    Body.StopFollowing();
                    Body.TurnTo(Body.TargetObject);
                    return true;
                }
                IsFleeing = false;
                TargetFleePosition = null;
                Flee(Body.TargetObject != null
                    ? Math.Max(KITE_MIN_HOP, KITE_FLIGHT_DISTANCE - distanceToTarget)
                    : KITE_NO_TARGET_FLIGHT);
                return IsFleeing || Body.IsMoving;
            }
            if (IsMainTank)
                return false;
            var mate = FindKiteMate();
            if (mate == null)
            {
                if (IsFleeing && Body.IsDestinationValid)
                    return true;
                IsFleeing = false;
                TargetFleePosition = null;
                Flee(600);
                return IsFleeing || Body.IsMoving;
            }
            IsFleeing = true;
            TargetFleePosition = null;
            Body.Follow(mate, 150, 5000);
            return true;
        }

        /// <summary>Keep fleeing while pressured, or while a pursuer stays inside the
        /// hold ring. Pure logic for unit tests; arrival alone never ends a kite that
        /// something is still glued to.</summary>
        public static bool ShouldKeepKiting(bool pressured, bool fleeing, bool pursuerInsideRing)
            => pressured || (fleeing && pursuerInsideRing);

        /// <summary>Ring the caster tries to reach, measured from the pursuer.</summary>
        public const int KITE_FLIGHT_DISTANCE = 2000;
        /// <summary>Inside this range a finished flight continues; outside it the caster
        /// holds and turns to face. Same 400-unit arrival test the legacy FSM used, and
        /// the sensor reads the same constant so the two can never disagree again.</summary>
        public const int KITE_HOLD_RANGE = 400;
        /// <summary>Never issue a flight shorter than this, even when already on the ring,
        /// so a fresh kite start is a decisive break rather than a shuffle.</summary>
        public const int KITE_MIN_HOP = 400;
        /// <summary>Fallback flight when the target was lost mid-decision: no target means
        /// no ring to measure from, so break away a fixed distance like the legacy FSM.</summary>
        public const int KITE_NO_TARGET_FLIGHT = 1200;

        /// <summary>Did a completed flight get the caster out of reach? Pure for unit
        /// tests; ExecuteGoapKite owns the hold, this only answers "run again?".</summary>
        public static bool ShouldKeepFleeing(int distanceToTarget, int holdRange)
            => distanceToTarget < holdRange;

        /// <summary>Nearest converge point for a grouped kite: the main tank first
        /// (peel/guard take over on arrival), else the nearest living member in region.
        /// Nobody within 1500, nobody to run to.</summary>
        public GameLiving FindKiteMate()
        {
            var group = Body.Group;
            if (group == null)
                return null;
            GameLiving tank = null, near = null;
            int nearDist = int.MaxValue;
            foreach (GameLiving m in group.GetMembersInTheGroup())
            {
                if (m == null || m == Body || !m.IsAlive || m.CurrentRegion != Body.CurrentRegion)
                    continue;
                int d = Body.GetDistance(m);
                if (d > 1500)
                    continue;
                if (group.MimicGroup != null && group.MimicGroup.MainTank == m)
                    tank = m;
                if (d < nearDist) { nearDist = d; near = m; }
            }
            return tank ?? near;
        }

        public bool ExecuteGoapQuickcast()
        {
            // Pop Quickcast so the pressured cast goes through uninterruptible.
            // Mirrors the FSM's own quickcast handling; the next tick casts normally.
            // No FSM calls, no recursive GOAP entry; fails closed to let kite handle it.
            Ability quickCast = Body.GetAbility(Abilities.Quickcast);
            if (quickCast == null || Body.GetSkillDisabledDuration(quickCast) > 0)
                return false;
            if (EffectListService.GetAbilityEffectOnTarget(Body, eEffect.QuickCast) != null)
                return true;
            new QuickCastECSGameEffect(new ECSGameEffectInitParams(Body, QuickCastECSGameEffect.DURATION + 1000, 1));
            Body.DisableSkill(quickCast, 180000);
            return true;
        }

        public bool ExecuteGoapSpell(eCheckSpellType type)
        {
            // Preserve derived class overrides, but let GOAP choose when to heal.
            _goapSelectingSpell = true;
            try
            {
                if (type == eCheckSpellType.CrowdControl)
                {
                    // PR38: prune the claim list, then ask for a live candidate. The old
                    // test was "does CCTargets still hold something", which was false for a
                    // bot group from the moment it pulled, and unreachable for an ungrouped
                    // bot - the branch returned false on a null group before testing
                    // anything. Both meant this action could never run.
                    PruneCcClaims();
                    if (GetCcCandidates().Count == 0)
                        return false;
                }
                if (type == eCheckSpellType.Offensive)
                    Body.ControlledBrain?.Attack(Body.TargetObject);
                bool result = CheckSpells(type);
                if (result && type == eCheckSpellType.Offensive)
                    Body.StopAttack();
                return result || Body.IsCasting;
            }
            finally { _goapSelectingSpell = false; }
        }

        public bool ExecuteGoapEngagement()
        {
            AttackSelectedTarget(false);
            return Body.IsAttacking || Body.IsMoving || Body.IsCasting;
        }

        public bool ExecuteGoapInterrupt()
        {
            // Damage interrupts: melee pressure first, else an instant offensive tick.
            // No FSM calls, no recursive GOAP entry; fails closed to let the planner try damage.
            AttackSelectedTarget(false);
            if (Body.IsAttacking || Body.IsMoving)
                return true;
            return ExecuteGoapSpell(eCheckSpellType.Offensive);
        }

        public bool IsMainPuller { get { return Body.Group?.MimicGroup.MainPuller == Body; } }

        public bool IsMainTank { get { return Body.Group?.MimicGroup.MainTank == Body; } }

        public bool IsMainLeader { get { return Body.Group?.MimicGroup.MainLeader == Body; } }

        public bool IsMainCC { get { return Body.Group?.MimicGroup.MainCC == Body; } }

        public bool IsMainAssist { get { return Body.Group?.MimicGroup.MainAssist == Body; } }

        private MimicNPC _mimicBody;

        public MimicNPC MimicBody
        {
            get { return _mimicBody; }
            set { _mimicBody = value; }
        }

        public const int MAX_AGGRO_DISTANCE = 3600;
        public const int MAX_AGGRO_LIST_DISTANCE = 6000;
        private const int EFFECTIVE_AGGRO_AMOUNT_CALCULATION_DISTANCE_THRESHOLD = 500;

        public bool PreventCombat;
        public bool PvPMode;
        public bool Defend;
        public bool Roam;
        public bool IsFleeing;
        public bool IsPulling;
        public bool Debug;

        public GameObject LastTargetObject;
        public bool IsFlanking;
        public Point2D TargetFlankPosition;

        public Point3D TargetFleePosition;

        // Used for AmbientBehaviour "Seeing" - maintains a list of GamePlayer in range
        public List<GamePlayer> PlayersSeen = new();

        /// <summary>
        /// Constructs a new MimicBrain
        /// </summary>
        public MimicBrain() : base()
        {
            FSM = new FSM();
            FSM.Add(new MimicState_Idle(this));
            FSM.Add(new MimicState_WakingUp(this));
            FSM.Add(new MimicState_Aggro(this));
            FSM.Add(new MimicState_ReturnToSpawn(this));
            FSM.Add(new MimicState_Patrolling(this));
            FSM.Add(new MimicState_Roaming(this));
            FSM.Add(new MimicState_FollowLeader(this));
            FSM.Add(new MimicState_Camp(this));
            FSM.Add(new MimicState_Duel(this));
            FSM.Add(new MimicState_Dead(this));

            FSM.SetCurrentState(eFSMStateType.WAKING_UP);
        }

        /// <summary>
        /// Returns the string representation of the MimicBrain
        /// </summary>
        public override string ToString()
        {
            return base.ToString() + ", AggroLevel=" + AggroLevel.ToString() + ", AggroRange=" + AggroRange.ToString();
        }

        public override bool Stop()
        {
            // tolakram - when the brain stops, due to either death or no players in the vicinity, clear the aggro list
            if (base.Stop())
            {
                GoapAgent?.ClearPlan();
                ClearAggroList();
                return true;
            }

            return false;
        }

        public override void KillFSM()
        {
            GoapAgent?.Disable();
            FSM.KillFSM();
        }

        #region AI

        public override void Think()
        {
            CheckRealmAbilitiesThrottled();
            // PR43: prune the aggro list every decision, whoever this bot is.
            //
            // The only pruner used to be SelectGoapAttackTarget -> CalculateNextAttackTarget
            // -> CleanUpAggroListAndGetHighestModifiedThreat, and the agent SKIPS target
            // selection for healers (MimicReGoapAgent: "if (context == Combat && !PreventCombat
            // && !IsHealer) SelectGoapAttackTarget()"). So a healer's aggro list was never
            // pruned at all - and a stale entry keeps HasAggro true, and HasAggro is:
            //
            //   - MimicNPC.InCombat, which is what MimicNPC.Interact checks. So the status
            //     window was refused SILENTLY, for as long as the stale entry survived.
            //   - CanBuffUpGroup, which returns false if ANY group member has aggro. So one
            //     stale entry blocked the whole group's speed song.
            //
            // The cleanup is the same one a fighter already runs every combat tick, and it
            // does not order the list - it only drops the entries that no longer apply.
            PruneAggroList();
            FSM.Think();
        }

        /// <summary>
        /// PR43: drop the aggro entries that no longer apply, without picking a target.
        ///
        /// The full cleanup also returns the highest-threat enemy, which is only wanted by
        /// a bot that is going to attack something. This is the pruning half on its own, so
        /// that a bot which never attacks - a healer - still gets a truthful aggro list.
        /// </summary>
        public void PruneAggroList()
        {
            // The cleanup reads Body.attackComponent, and a brain can exist without a body
            // (the test doubles do exactly that), so this has to be safe without one.
            if (Body == null)
                return;
            CleanUpAggroListAndGetHighestModifiedThreat();
        }

        private long _nextRACheck;
        private int _lastRACheckedRealmLevel = -1;

        /// <summary>Spends earned realm ranks on class RAs (PR19a): immediately
        /// at spawn, then only when the rank changed or every 10s. One int
        /// compare per think otherwise.</summary>
        private void CheckRealmAbilitiesThrottled()
        {
            var body = MimicBody;
            if (body == null || !body.IsAlive)
                return;
            long now = GameLoop.GameLoopTime;
            if (body.RealmLevel == _lastRACheckedRealmLevel && now < _nextRACheck)
                return;
            _nextRACheck = now + 10000;
            _lastRACheckedRealmLevel = body.RealmLevel;
            body.CheckRealmAbilities();
        }

        public virtual void OnLeaderAggro()
        { }
        public virtual void OnEnterAggro()
        { }

        public virtual void OnExitAggro()
        { }

        public virtual void OnEnterRoam()
        { }

        public virtual void OnExitRoam()
        { }

        public virtual void OnLevelUp()
        { }

        public virtual void OnRefreshSpecDependantSkills()
        { }

        public void OnGroupMemberAttacked(AttackData ad)
        {
            if (FSM.GetState(eFSMStateType.CAMP) == FSM.GetCurrentState())
            {
                if (!Body.IsWithinRadius(ad.Attacker, AggroRange))
                    return;
            }

            switch (ad.AttackResult)
            {
                case eAttackResult.Blocked:
                case eAttackResult.Evaded:
                case eAttackResult.Fumbled:
                case eAttackResult.HitStyle:
                case eAttackResult.HitUnstyled:
                case eAttackResult.Missed:
                case eAttackResult.Parried:
                AddToAggroList(ad.Attacker, 1);
                break;
            }

            if (FSM.GetState(eFSMStateType.AGGRO) != FSM.GetCurrentState() && !IsHealer)
                FSM.SetCurrentState(eFSMStateType.AGGRO);
        }

        public virtual bool CheckProximityAggro(int aggroRange)
        {
            //FireAmbientSentence();

            if (PvPMode || AggroLevel > 0 && AggroRange > 0 && !HasAggro && Body.CurrentSpellHandler == null)
            {
                CheckPlayerAggro();
                CheckNPCAggro(aggroRange);
            }

            // Some calls rely on this method to return if there's something in the aggro list, not necessarily to perform a proximity aggro check.
            // But this doesn't necessarily return whether or not the check was positive, only the current state (LoS checks take time).
            return HasAggro;
        }

        public virtual bool HasPatrolPath()
        {
            return Body.MaxSpeedBase > 0 &&
                Body.CurrentSpellHandler == null &&
                !Body.IsMoving &&
                !Body.attackComponent.AttackState &&
                !Body.InCombat &&
                !Body.IsMovingOnPath &&
                !string.IsNullOrEmpty(Body.PathID);
        }

        /// <summary>
        /// Check for aggro against players
        /// </summary>
        protected virtual void CheckPlayerAggro()
        {
            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)AggroRange))
            {
                if (!CanAggroTarget(player))
                    continue;

                if (player.IsStealthed || player.Steed != null)
                    continue;

                if (player.effectListComponent.ContainsEffectForEffectType(eEffect.Shade))
                    continue;

                if (Properties.CHECK_LOS_BEFORE_AGGRO)
                    // We don't know if the LoS check will be positive, so we have to ask other players
                    player.Out.SendCheckLos(Body, player, new CheckLosResponse(LosCheckForAggroCallback));
                else
                {
                    AddToAggroList(player, 1);
                    return;
                }
            }
        }

        /// <summary>
        /// Check for aggro against close NPCs
        /// </summary>
        protected virtual void CheckNPCAggro(int aggroRange)
        {
            List<GameNPC> npcsInRadius = Body.GetNPCsInRadius((ushort)aggroRange);

            if (npcsInRadius.Count > 1)
            {
                int startIndex = Util.Random(0, npcsInRadius.Count - 1);

                for (int i = 0; i < npcsInRadius.Count; i++)
                {
                    int index = startIndex + i;

                    if (index >= npcsInRadius.Count)
                        index = i - (npcsInRadius.Count - startIndex);

                    if (!CanAggroTarget(npcsInRadius[index]))
                        continue;

                    if (npcsInRadius[index] is IGamePlayer player && player.IsStealthed && !MimicBody.CanDetect(player))
                        continue;

                    if (npcsInRadius[index] is GameTaxi or GameTrainingDummy)
                        continue;

                    if (Properties.CHECK_LOS_BEFORE_AGGRO)
                    {
                        // Check LoS if either the target or the current mob is a pet
                        if (npcsInRadius[index].Brain is ControlledMobBrain theirControlledMobBrain && theirControlledMobBrain.GetPlayerOwner() is GamePlayer theirOwner)
                        {
                            theirOwner.Out.SendCheckLos(Body, npcsInRadius[index], new CheckLosResponse(LosCheckForAggroCallback));
                            continue;
                        }
                    }

                    AddToAggroList(npcsInRadius[index], 1);

                    return;
                }
            }

            //foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)aggroRange))
            //{
            //    if (!CanAggroTarget(npc))
            //        continue;

            //    if (npc is IGamePlayer player && player.IsStealthed && !MimicBody.CanDetect(player))
            //        continue;

            //    if (npc is GameTaxi or GameTrainingDummy)
            //        continue;

            //    if (Properties.CHECK_LOS_BEFORE_AGGRO)
            //    {
            //        // Check LoS if either the target or the current mob is a pet
            //        if (npc.Brain is ControlledMobBrain theirControlledMobBrain && theirControlledMobBrain.GetPlayerOwner() is GamePlayer theirOwner)
            //        {
            //            theirOwner.Out.SendCheckLos(Body, npc, new CheckLosResponse(LosCheckForAggroCallback));
            //            continue;
            //        }
            //    }

            //    AddToAggroList(npc, 1);

            //    return;
            //}
        }

        public virtual void FireAmbientSentence()
        {
            if (Body.ambientTexts != null && Body.ambientTexts.Any(item => item.Trigger == "seeing"))
            {
                // Check if we can "see" players and fire off ambient text
                List<GamePlayer> currentPlayersSeen = new();

                foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)AggroRange))
                {
                    if (!PlayersSeen.Contains(player))
                    {
                        Body.FireAmbientSentence(GameNPC.eAmbientTrigger.seeing, player);
                        PlayersSeen.Add(player);
                    }

                    currentPlayersSeen.Add(player);
                }

                for (int i = 0; i < PlayersSeen.Count; i++)
                {
                    if (!currentPlayersSeen.Contains(PlayersSeen[i]))
                        PlayersSeen.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The interval for thinking, min 1.5 seconds
        /// 10 seconds for 0 aggro mobs
        /// </summary>
        public override int ThinkInterval
        {
            get
            {
                return 500;
            }
        }

        /// <summary>
        /// If this brain is part of a formation, it edits it's values accordingly.
        /// </summary>
        /// <param name="x">The x-coordinate to refer to and change</param>
        /// <param name="y">The x-coordinate to refer to and change</param>
        /// <param name="z">The x-coordinate to refer to and change</param>
        public virtual bool CheckFormation(ref int x, ref int y, ref int z)
        {
            return false;
        }

        /// <summary>
        /// Checks the Abilities
        /// </summary>
        public virtual void CheckDefensiveAbilities()
        {
            if (Body.Abilities == null || Body.Abilities.Count <= 0 || Body.Group == null)
                return;

            foreach (Ability ab in Body.GetAllAbilities())
            {
                switch (ab.KeyName)
                {
                    case Abilities.Intercept:
                    {
                        //if (Body.Group != null)
                        //{
                        //    GameLiving interceptTarget;
                        //    List<GameLiving> interceptTargets = new List<GameLiving>();

                        //    foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                        //    {
                        //        if (groupMember is MimicNPC mimic)
                        //        {
                        //            if (mimic.CharacterClass.ID == (int)eCharacterClass.Cleric ||
                        //                mimic.CharacterClass.ID == (int)eCharacterClass.Druid ||
                        //                mimic.CharacterClass.ID == (int)eCharacterClass.Healer ||
                        //                mimic.CharacterClass.ID == (int)eCharacterClass.Friar ||
                        //                mimic.CharacterClass.ID == (int)eCharacterClass.Bard ||
                        //                mimic.CharacterClass.ID == (int)eCharacterClass.Shaman)
                        //            {
                        //                interceptTargets.Add(groupMember);
                        //            }
                        //        }
                        //    }
                        //}
                        break;
                    }

                    case Abilities.Guard:
                    {
                        break;
                    }
                        
                    case Abilities.Protect:
                    {
                        break;
                    }
                }
            }
        }

        public void CheckOffensiveAbilities()
        {
            if (Body.Abilities == null || Body.Abilities.Count <= 0)
                return;

            if (CanUseAbility())
            {
                foreach (Ability ab in Body.GetAllAbilities())
                {
                    if (Body.GetSkillDisabledDuration(ab) == 0)
                    {
                        switch (ab.KeyName)
                        {
                            case Abilities.Berserk:
                            {
                                if (Body.TargetObject is GameLiving target)
                                {
                                    if (Body.IsWithinRadius(Body.TargetObject, Body.MeleeAttackRange) &&
                                        GameServer.ServerRules.IsAllowedToAttack(Body, target, true))
                                    {
                                        new BerserkECSGameEffect(new ECSGameEffectInitParams(Body, 20000, 1));
                                        Body.DisableSkill(ab, 420000);
                                    }
                                }

                                break;
                            }

                            case Abilities.Stag:
                            {
                                if (Body.TargetObject is GameLiving target)
                                {
                                    if (Body.IsWithinRadius(Body.TargetObject, Body.MeleeAttackRange) &&
                                        GameServer.ServerRules.IsAllowedToAttack(Body, target, true) || Body.HealthPercent < 75)
                                    {
                                        new StagECSGameEffect(new ECSGameEffectInitParams(Body, 30000, 1), ab.Level);
                                        Body.DisableSkill(ab, 900000);
                                    }
                                }

                                break;
                            }

                            case Abilities.Triple_Wield:
                            {
                                if (Body.TargetObject is GameLiving target)
                                {
                                    if (Body.IsWithinRadius(Body.TargetObject, Body.MeleeAttackRange) &&
                                        GameServer.ServerRules.IsAllowedToAttack(Body, target, true))
                                    {
                                        new TripleWieldECSGameEffect(new ECSGameEffectInitParams(Body, 30000, 1));
                                        Body.DisableSkill(ab, 420000);
                                    }
                                }

                                break;
                            }

                            case Abilities.DirtyTricks:
                            {
                                if (Body.TargetObject is GameLiving target)
                                {
                                    IGamePlayer gamePlayer = target as IGamePlayer;

                                    if (gamePlayer != null && gamePlayer.CharacterClass.ClassType == eClassType.ListCaster)
                                        break;

                                    if (Body.IsWithinRadius(Body.TargetObject, Body.MeleeAttackRange) &&
                                        GameServer.ServerRules.IsAllowedToAttack(Body, target, true))
                                    {
                                        new DirtyTricksECSGameEffect(new ECSGameEffectInitParams(Body, 30000, 1));
                                        Body.DisableSkill(ab, 420000);
                                    }
                                }

                                break;
                            }

                            case Abilities.ChargeAbility:
                            {
                                if (Body.TargetObject is GameLiving target &&
                                    GameServer.ServerRules.IsAllowedToAttack(Body, target, true) &&
                                    !Body.IsWithinRadius(target, 500))
                                {
                                    ChargeAbility charge = Body.GetAbility<ChargeAbility>();

                                    if (charge != null && Body.GetSkillDisabledDuration(charge) <= 0)
                                        charge.Execute(Body);
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }

        private bool CanUseAbility()
        {
            if (!Body.IsAlive ||
                Body.IsMezzed ||
                Body.IsStunned ||
                Body.IsSitting)
                return false;

            return true;
        }

        public bool SetGuard(GameLiving target, out bool ourEffect)
        {
            if (target != null && target != Body)
            {
                GuardAbilityHandler.CheckExistingEffectsOnTarget(Body, target, true, out bool foundOurEffect, out GuardECSGameEffect existingEffectFromAnotherSource);

                ourEffect = foundOurEffect;

                if (foundOurEffect)
                    return false;

                if (existingEffectFromAnotherSource != null)
                    return false;

                GuardAbilityHandler.CancelOurEffectThenAddOnTarget(Body, target);

                return true;
            }

            ourEffect = false;
            return false;
        }

        public bool SetProtect(GameLiving target, out bool ourEffect)
        {
            if (target != null && target != Body)
            {
                ProtectAbilityHandler.CheckExistingEffectsOnTarget(Body, target, true, out bool foundOurEffect, out ProtectECSGameEffect existingEffectFromAnotherSource);

                ourEffect = foundOurEffect;

                if (foundOurEffect)
                    return false;

                if (existingEffectFromAnotherSource != null)
                    return false;

                ProtectAbilityHandler.CancelOurEffectThenAddOnTarget(Body, target);

                return true;
            }

            ourEffect = false;
            return false;
        }

        public bool SetIntercept(GameLiving target, out bool ourEffect)
        {
            if (target != null && target != Body)
            {
                InterceptAbilityHandler.CheckExistingEffectsOnTarget(Body, target, true, out bool foundOurEffect, out InterceptECSGameEffect existingEffectFromAnotherSource);

                ourEffect = foundOurEffect;

                if (foundOurEffect)
                    return false;

                if (existingEffectFromAnotherSource != null)
                    return false;

                InterceptAbilityHandler.CancelOurEffectThenAddOnTarget(Body, target);

                return true;
            }

            ourEffect = false;
            return false;
        }

        #endregion AI

        #region MimicGroup AI

        #region MainPuller

        public void CheckPuller()
        {
            if (IsPulling && Body.TargetObject != null && Body.TargetObject.ObjectState == GameObject.eObjectState.Active)
            {
                if (CheckResetPuller())
                {
                    Body.ReturnToSpawnPoint(Body.MaxSpeed);

                    // PR27: these were joined with ||, so the condition was always true -
                    // no class can fail all three "not an archer" comparisons at once, so
                    // the archer exclusion never applied. That put archer pullers into a
                    // melee slot on pull reset, and they had to switch back to Distance
                    // before they could shoot: the churn that made ActiveWeaponSlot flap
                    // and briefly defeated the kiting/debuff exclusion.
                    if (MimicBody.CharacterClass.ID != (int)eCharacterClass.Hunter &&
                        MimicBody.CharacterClass.ID != (int)eCharacterClass.Ranger &&
                        MimicBody.CharacterClass.ID != (int)eCharacterClass.Scout &&
                        MimicBody.CharacterClass.ClassType != eClassType.ListCaster)
                    {
                        if (MimicBody.MimicSpec.Is2H)
                            Body.SwitchWeapon(eActiveWeaponSlot.TwoHanded);
                        else
                            Body.SwitchWeapon(eActiveWeaponSlot.Standard);
                    }

                    return;
                }
            }

            if (!Body.InCombat)
            {
                if (CheckDelayPull())
                {
                    Body.StopAttack();
                    Body.StopFollowing();
                }
                else
                {
                    GameLiving pullTarget = GetPullTarget();
                    PerformPull(pullTarget);
                }
            }
        }

        public bool CheckDelayPull()
        {
            if (LastTargetObject != null && LastTargetObject.ObjectState == GameObject.eObjectState.Active)
                return true;

            if (TryGoap(MimicDecisionContext.Support) || CheckSpells(eCheckSpellType.Defensive) || MimicBody.Sit(CheckStats(75)))
                return true;

            if (Body.Group != null &&
                Body.Group.GetMembersInTheGroup().Any(groupMember => groupMember.IsCasting || groupMember.IsSitting))
                return true;

            return false;
        }

        public GameLiving GetPullTarget()
        {
            if (!Body.IsAttacking && !Body.IsCasting && !Body.IsSitting)
            {
                if (Body.Group.MimicGroup.CCTargets.Count > 0)
                    return Body.Group.MimicGroup.CCTargets[Util.Random(Body.Group.MimicGroup.CCTargets.Count - 1)];

                CheckProximityAggro(3600);

                if (AggroList.Count > 0)
                {
                    GameLiving closestTarget;

                    if (Body.Group.MimicGroup.PullFromPoint != null)
                        closestTarget = AggroList.Where(pair => Body.GetConLevel(pair.Key) >= Body.Group.MimicGroup.ConLevelFilter).
                                                   OrderBy(pair => pair.Key.GetDistance(Body.Group.MimicGroup.PullFromPoint)).
                                                   ThenBy(pair => Body.GetDistanceTo(pair.Key)).First().Key;
                    else
                        closestTarget = AggroList.Where(pair => Body.GetConLevel(pair.Key) > Body.Group.MimicGroup.ConLevelFilter).
                                                   OrderBy(pair => Body.GetDistanceTo(pair.Key)).First().Key;

                    return closestTarget;
                }
            }

            return null;
        }

        private bool CheckResetPuller()
        {
            if (Body.TargetObject is GameNPC npcTarget && npcTarget.Brain is StandardMobBrain mobBrain && mobBrain.HasAggro)
            {
                LastTargetObject = Body.TargetObject;
                IsPulling = false;
                Body.StopAttack();
                ClearAggroList();

                return true;
            }

            return false;
        }

        public void PerformPull(GameLiving target)
        {
            if (target == null)
                return;

            IsPulling = true;

            // PR27: IsBowUser is this same inventory test, so "ranged attacker" is
            // defined once in this class rather than duplicated here.
            if (IsBowUser)
            {
                Body.SwitchWeapon(eActiveWeaponSlot.Distance);
                Body.StartAttack(target);
            }
            else
            {
                //if (Body.CanCastInstantHarmfulSpells)
                //{
                //    foreach(Spell spell in Body.InstantHarmfulSpells)

                //}
                //if (!Body.IsWithinRadius(Body.TargetObject, spell.Range))
                //{
                //    Body.Follow(Body.TargetObject, spell.Range - 100, 5000);
                //    QueuedOffensiveSpell = spell;
                //    return false;
                //}
            }
        }

        #endregion MainPuller

        #region MainLeader

        public bool CheckDelayRoam()
        {
            if (Body.IsCasting || TryGoap(MimicDecisionContext.Support) || CheckSpells(eCheckSpellType.Defensive) || MimicBody.Sit(CheckStats(75)))
                return true;

            if (Body.Group != null &&
                Body.Group.GetMembersInTheGroup().Any(groupMember => groupMember.IsCasting || groupMember.IsSitting || (groupMember is MimicNPC mimic &&
                                                      mimic.MimicBrain.FSM.GetCurrentState() == mimic.MimicBrain.FSM.GetState(eFSMStateType.FOLLOW_THE_LEADER) &&
                                                      !Body.IsWithinRadius(groupMember, 1000))))
                return true;

            return false;
        }

        #endregion MainLeader

        #region MainCC

        public void CheckMainCC()
        {
            var group = Body.Group?.MimicGroup;

            // PR38: the guard was Body.Group.MimicGroup.CCTargets.Count - an unguarded
            // dereference on a body that may have no group at all, and a count of a list
            // that only a player's BAF ever filled.
            if (group != null && group.CCTargets.Count > 0)
            {
                if (CheckSpells(eCheckSpellType.CrowdControl))
                    return;
            }

            if (!Body.InCombat && group != null && group.CCTargets.Count > 0)
            {
                group.CCTargets = ValidateCCList(group.CCTargets);
            }
        }

        #region CcCandidates

        /// <summary>
        /// PR38: the rule for "may I put control on this enemy right now", with every input
        /// already resolved, so it can be pinned by a test without a body.
        ///
        /// <paramref name="isPrimaryTarget"/> is the group's shared target. Controlling the
        /// enemy everybody is already hitting buys nothing, so it is excluded - that was the
        /// intent of the old filter and it is kept. With no group there is no primary target,
        /// which is what lets a solo bot control at all.
        ///
        /// <paramref name="claimedByGroup"/> is a sibling that already started a cast at this
        /// enemy. Without it, every mezz-capable bot in a group fires at the same add. The
        /// caster's own claim is not included - see ClaimCcTarget.
        /// </summary>
        public static bool IsControllableEnemy(bool alive, bool active, bool sameRegion, bool aggroable,
            bool alreadyControlled, bool isPrimaryTarget, bool claimedByGroup)
            => alive && active && sameRegion && aggroable && !alreadyControlled && !isPrimaryTarget && !claimedByGroup;

        /// <summary>
        /// PR38: the first control spell in this bot's pool that it can actually land on
        /// this enemy right now, or false. This is the one place "can I control that enemy"
        /// is answered, and both the candidate count and the cast read it, so the number the
        /// sensor reports and the casts the brain makes can never disagree.
        ///
        /// Three separate ways a spell is refused, each of which used to be invisible:
        ///  - not castable now (cooldown, or the target type)
        ///  - the effect is already on the enemy. LivingHasEffect also recognises the spell
        ///    we are mid-cast with, so a queued cast is not repeated
        ///  - out of the spell's own reach. The cast path had this filter but the count did
        ///    not, so an unreachable add was reported as work the bot could do.
        /// </summary>
        public bool TryPickControlSpell(GameLiving enemy, out Spell spell)
        {
            spell = null;
            var pool = MimicBody?.CrowdControlSpells;
            if (enemy == null || Body == null || pool == null)
                return false;

            foreach (Spell candidate in pool)
            {
                if (!CanUseControlSpell(candidate) || LivingHasEffect(enemy, candidate))
                    continue;

                if (candidate.Range > 0 && !Body.IsWithinRadius(enemy, candidate.Range))
                    continue;

                spell = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// PR38: can this control spell be cast right now? The same data test as
        /// CanCastOffensiveSpell, except that it accepts instant casts.
        ///
        /// CanCastOffensiveSpell requires CastTime > 0, which is right for damage - an
        /// instant nuke is handled by the instant path. Crowd control has no such second
        /// path: instant mezzes are routed into CrowdControlSpells by SortSpells and were
        /// then refused here by the cast-time test, and CheckInstantOffensiveSpells never
        /// saw them because that iterates InstantHarmfulSpells. Dead on both paths.
        /// </summary>
        protected bool CanUseControlSpell(Spell spell)
        {
            if (spell == null || Body == null)
                return false;
            if (Body.GetSkillDisabledDuration(spell) > 0)
                return false;
            return spell.Target is eSpellTarget.ENEMY or eSpellTarget.AREA or eSpellTarget.CONE;
        }

        /// <summary>
        /// PR38: is control this bot's job? Only a mezzer qualifies.
        ///
        /// PR37 put roots and snares into the same pool as mezzes, and every one of them
        /// wakes CanCastCrowdControlSpells. Without this narrowing a Druid carrying a single
        /// root would take the group's control duty and spend the fight casting it.
        ///
        /// Capability-derived, not a class table: it reads the pool SortSpells already built.
        /// </summary>
        public bool IsCcPrimary
        {
            get
            {
                var pool = MimicBody?.CrowdControlSpells;
                if (pool == null)
                    return false;
                foreach (Spell spell in pool)
                {
                    if (CcClassifier.IsMezzer(spell))
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// PR38: enemies this bot has real control work for, highest effective aggro first.
        ///
        /// This is the aggro-list candidate set, narrowed to the ACTIONABLE ones. The
        /// difference is the whole point: the first filter asked "is this enemy still
        /// controllable", and that test reads IsMezzed/IsStunned/IsRooted - but a snare sets
        /// none of them (SpeedDecreaseSpellHandler only sets IsRooted at Value == 99). A
        /// snared enemy therefore stayed a candidate forever, the count never drained, and
        /// the bot never went back to damage. Now a candidate is one a cast would actually
        /// change something for, so the count falls to zero the moment control is on.
        ///
        /// This replaces MimicGroup.CCTargets as the source of CC work. CCTargets had
        /// exactly one producer in the entire server - StandardMobBrain's
        /// battlegroup-add-finder, and only for a mob that walked past a *player*. A bot
        /// group fighting another bot group therefore always saw zero, the sensor's
        /// NUM_CONTROLLABLE_ADDS was always 0, ControlAvailable was always false, and the
        /// CC goal could never fire.
        /// </summary>
        public List<GameLiving> GetCcCandidates()
        {
            List<GameLiving> candidates = new List<GameLiving>();
            if (Body == null)
                return candidates;

            var group = Body.Group?.MimicGroup;
            var claimed = group?.CCTargets;
            GameLiving primary = group?.CurrentTarget as GameLiving;

            foreach (var entry in GetOrderedAggroList())
            {
                GameLiving enemy = entry.Item1;
                if (enemy == null || enemy == Body)
                    continue;

                if (!IsControllableEnemy(
                        enemy.IsAlive,
                        enemy.ObjectState == GameObject.eObjectState.Active,
                        enemy.CurrentRegion == Body.CurrentRegion,
                        CanAggroTarget(enemy),
                        enemy.IsMezzed || enemy.IsStunned || enemy.IsRooted,
                        enemy == primary,
                        claimed != null && claimed.Contains(enemy) && enemy != _myCcClaim))
                    continue;

                if (!TryPickControlSpell(enemy, out _))
                    continue;

                candidates.Add(enemy);
            }

            return candidates;
        }

        /// <summary>
        /// Claim an enemy for this group so no sibling re-casts at it.
        ///
        /// PR38: the claim is written when the cast STARTS, and it used to hide the target
        /// from the caster as well as from its siblings - so a control that was resisted
        /// dropped that add from the caster's own candidate list and was never retried.
        /// The caster's own claim is therefore remembered separately and does not block it.
        /// </summary>
        public void ClaimCcTarget(GameLiving enemy)
        {
            _myCcClaim = enemy;
            var group = Body.Group?.MimicGroup;
            if (group != null && enemy != null && !group.CCTargets.Contains(enemy))
                group.CCTargets.Add(enemy);
        }

        /// <summary>
        /// Drop claims that no longer mean anything: dead, gone, out of range or no longer
        /// something we may attack. A claim is not removed when a cast starts, so a resisted
        /// control is retried rather than forgotten - but PR44 is where it stops being
        /// retried forever.
        /// </summary>
        public void PruneCcClaims()
        {
            var group = Body.Group?.MimicGroup;
            if (group != null)
            {
                group.CCTargets.RemoveAll(t => t == null || !t.IsAlive || t.IsMezzed || t.IsStunned
                    || t.IsRooted || t.CurrentRegion != Body.CurrentRegion || !CanAggroTarget(t));
            }

            // PR38: the caster's own claim must not outlive the enemy it was made for,
            // or it blocks that enemy from ever being controlled again.
            if (_myCcClaim != null && (group == null || !group.CCTargets.Contains(_myCcClaim)))
                _myCcClaim = null;
        }

        /// <summary>How close an enemy has to be to count as sitting on us.</summary>
        public const int PRESSURE_MELEE_RANGE = 200;

        /// <summary>
        /// PR38: is an enemy sitting on us? One definition, shared by the control gate and
        /// the kite gate - they used to ask the same question in two places.
        /// </summary>
        public bool IsEngagedInMelee()
        {
            if (Body == null)
                return false;

            foreach (var entry in GetOrderedAggroList())
            {
                var enemy = entry.Item1;
                if (enemy == null || !enemy.IsAlive || enemy.TargetObject != Body)
                    continue;
                if (Body.IsWithinRadius(enemy, PRESSURE_MELEE_RANGE))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// PR38: are we in a hurry? Being interrupted, or an enemy on us, means starting a
        /// multi-second control now is how you get interrupted instead of how you win.
        /// </summary>
        public bool IsUnderPressure() => Body != null && (Body.IsBeingInterrupted || IsEngagedInMelee());

        /// <summary>
        /// PR38: should this bot hold off on control? Pure, so it can be pinned without a
        /// body. The control gate is deliberately NOT gated on being a caster or in combat,
        /// so a pac healer is pressured for the first time.
        /// </summary>
        public static bool ShouldWithholdControl(bool underPressure, bool silenced, bool casting)
            => underPressure || silenced || casting;

        /// <summary>
        /// PR38: may this bot start a new offensive cast?
        ///
        /// The missing "!IsCasting" here was the queued-cast chain. OffenseAvailable was the
        /// only one of the three action gates that did not test it, so while a six second
        /// control was running the damage goal was still planned, the damage action queued
        /// another spell, and CastingComponent promotes a queued spell to the active cast
        /// when the first one ends - which means IsCasting never became false and the bot
        /// could do nothing but cast, forever.
        /// </summary>
        public static bool CanStartOffensiveCast(bool isCasting, bool isSilenced, bool isFleeing)
            => !isCasting && !isSilenced && !isFleeing;

        /// <summary>
        /// PR40: may this bot start a new defensive cast?
        ///
        /// A buff in flight must not be re-requested. <c>CheckDefensiveSpells</c> picks a
        /// RANDOM missing buff and <c>CastSpell</c> QUEUES it while the previous one is
        /// still casting - and <c>CastingComponent.StartCastSpell</c> does
        /// <c>QueuedSpellHandler = newSpellHandler</c>, which silently DISCARDS the spell
        /// that was queued before it. The queue is refreshed every think tick, so the bot
        /// replaced its own queued buff every tick with a different random one, and the
        /// buff it had actually chosen never happened.
        ///
        /// Pure, so the decision can be pinned without a body.
        /// </summary>
        public static bool CanStartDefensiveCast(bool isCasting, bool isSilenced, bool isMoving, bool inCombat)
            => !isCasting && !isSilenced && !isMoving && !inCombat;

        /// <summary>The enemy this bot last started a control cast on.</summary>
        private GameLiving _myCcClaim;

        #endregion CcCandidates

        // Test for bad lists. Might not be needed.
        private List<GameLiving> ValidateCCList(List<GameLiving> ccList)
        {
            List<GameLiving> validatedList = new List<GameLiving>();

            // PR38: this hard-cast to StandardMobBrain. MimicBrain and StandardMobBrain are
            // siblings - both implement IOldAggressiveBrain, neither derives from the other -
            // so any MimicNPC in this list threw InvalidCastException. That was unreachable
            // while only BAF mobs went in. CC candidates are enemies, and when two bot groups
            // fight each other the enemies are MimicNPCs, so it is now the ordinary case.
            foreach (GameLiving cc in ccList)
            {
                // Brain hangs off GameNPC, not GameLiving - the same reason the old body
                // began with `cc is GameNPC npc`.
                if (!(cc is GameNPC npc) || !npc.IsAlive)
                    continue;

                bool hasAggro = npc.Brain is StandardMobBrain mob ? mob.HasAggro
                    : npc.Brain is MimicBrain mimic ? mimic.HasAggro
                    : false;

                if (hasAggro)
                    validatedList.Add(cc);
            }

            return validatedList;
        }

        #endregion MainCC

        #region MainTank

        public bool CheckMainTankTarget()
        {
            if (!IsMainTank)
                return false;

            GameLiving target = null;
            List<GameLiving> listOfTargets = null;

            if (AggroList.Count > 0)
            {
                listOfTargets = (AggroList.Keys.Where(key => key.TargetObject is GameLiving livingTarget && livingTarget != Body &&
                                                             !livingTarget.IsMezzed && !livingTarget.IsRooted).ToList());
            }

            if (listOfTargets != null && listOfTargets.Count > 0)
                target = listOfTargets[Util.Random(listOfTargets.Count - 1)];

            if (target != null)
            {
                Body.TargetObject = target;

                return true;
            }

            return false;
        }

        #endregion MainTank

        public bool CheckStats(short threshold)
        {
            if (Body.HealthPercent < threshold || (Body.MaxMana > 0 && Body.ManaPercent < threshold) || Body.EndurancePercent < threshold)
                return true;

            return false;
        }

        #endregion MimicGroup AI

        #region Aggro

        protected int _aggroRange;

        /// <summary>
        /// Max Aggro range in that this npc searches for enemies
        /// </summary>
        public virtual int AggroRange
        {
            get => Math.Min(_aggroRange, MAX_AGGRO_DISTANCE);
            set => _aggroRange = value;
        }

        /// <summary>
        /// Aggressive Level in % 0..100, 0 means not Aggressive
        /// </summary>
        public virtual int AggroLevel { get; set; }

        protected ConcurrentDictionary<GameLiving, AggroAmount> AggroList { get; } = new();
        protected List<(GameLiving, long)> OrderedAggroList { get; private set; } = [];
        public GameLiving LastHighestThreatInAttackRange { get; private set; }

        public class AggroAmount(long @base = 0)
        {
            public long Base { get; set; } = @base;
            public long Effective { get; set; }
            public long Temporary { get; set; }
        }

        /// <summary>
        /// Checks whether living has someone on its aggrolist
        /// </summary>
        public virtual bool HasAggro => !AggroList.IsEmpty;

        /// <summary>
        /// Add aggro table of this brain to that of another living.
        /// </summary>
        public void AddAggroListTo(StandardMobBrain brain)
        {
            if (!brain.Body.IsAlive)
                return;

            foreach (var pair in AggroList)
                brain.AddToAggroList(pair.Key, pair.Value.Base);
        }

        public virtual void AddToAggroList(GameLiving living, long aggroAmount)
        {
            if (Body.IsConfused || !Body.IsAlive || living == null)
                return;

            AggroList.AddOrUpdate(living, Add, Update, aggroAmount);

            if (living is IGamePlayer player)
            {
                // Add the whole group to the aggro list.
                if (player.Group != null)
                {
                    foreach (IGamePlayer playerInGroup in player.Group.GetIPlayersInTheGroup())
                    {
                        if (playerInGroup != living)
                            AggroList.TryAdd((GameLiving)playerInGroup, new());
                    }
                }

            }

            // Change state and reschedule the next think tick to improve responsiveness.
            if (FSM.GetCurrentState() != FSM.GetState(eFSMStateType.AGGRO) && HasAggro && !IsHealer)
            {
                FSM.SetCurrentState(eFSMStateType.AGGRO);
                NextThinkTick = GameLoop.GameLoopTime;
            }

            static AggroAmount Add(GameLiving key, long arg)
            {
                return new(Math.Max(0, arg));
            }

            static AggroAmount Update(GameLiving key, AggroAmount oldValue, long arg)
            {
                oldValue.Base = Math.Max(0, oldValue.Base + arg);
                return oldValue;
            }
        }

        public virtual void RemoveFromAggroList(GameLiving living)
        {
            AggroList.TryRemove(living, out _);
        }

        public long GetMaxAggro()
        {
            return AggroList.IsEmpty
                ? 0
                : AggroList.OrderByDescending(x => x.Value.Effective).Select(x => (x.Key, x.Value.Effective)).ToList().First().Effective;
        }

        public List<(GameLiving, long)> GetOrderedAggroList()
        {
            // Potentially slow, so we cache the result.
            lock (((ICollection)OrderedAggroList).SyncRoot)
            {
                if (OrderedAggroList.Count == 0)
                    OrderedAggroList = AggroList.OrderByDescending(x => x.Value.Effective).Select(x => (x.Key, x.Value.Effective)).ToList();

                return OrderedAggroList.ToList();
            }
        }
        public long GetBaseAggroAmount(GameLiving living)
        {
            return AggroList.TryGetValue(living, out AggroAmount aggroAmount) ? aggroAmount.Base : 0;
        }

        /// <summary>
        /// Remove all livings from the aggrolist.
        /// </summary>
        public virtual void ClearAggroList()
        {
            AggroList.Clear();

            lock (((ICollection)OrderedAggroList).SyncRoot)
            {
                OrderedAggroList.Clear();
            }

            LastHighestThreatInAttackRange = null;
        }

        /// <summary>
        /// Selects and attacks the next target or does nothing.
        /// </summary>
            public virtual void AttackMostWanted()
            {
                if (!IsActive)
                    return;

                //if (PvPMode || CheckAssist == null)

                if (!CheckMainTankTarget())
                    Body.TargetObject = CalculateNextAttackTarget();

                // PR32: same frontline restriction the sensor applies to AttackAvailable.
                // Without this a grouped Bard still charged in through the FSM even though
                // GOAP would never have picked DealDamageGoal for it - the two paths have to
                // agree or the policy is half a policy. Self-defence is exempt: if
                // something is already on us, backing off to a song stance is how we die.
                if (!MayAttackInMelee && !IsUnderAttack())
                    return;

                AttackSelectedTarget(true);
            }

            /// <summary>PR32: is something already hitting us? Used so the Bard frontline
            /// restriction never becomes "stand there and get killed".</summary>
            private bool IsUnderAttack()
            {
                if (Body == null)
                    return false;
                foreach (var entry in GetOrderedAggroList())
                {
                    var enemy = entry.Item1;
                    if (enemy != null && enemy.IsAlive && enemy.TargetObject == Body)
                        return true;
                }
                return false;
            }

        private void AttackSelectedTarget(bool allowSpellSelection)
        {
            // PR33: a bard or minstrel that just sang is holding the instrument. Swinging
            // with it is weak and looks wrong, and both keep a melee weapon precisely for
            // rupting and for playing solo - so step onto it first and attack next tick.
            if (PrepareMeleeStance())
                return;
            if (Body.TargetObject != null)
            {
                if (Body.ControlledBrain != null)
                    Body.ControlledBrain.Attack(Body.TargetObject);

                if (allowSpellSelection && !IsFleeing && CheckSpells(eCheckSpellType.Offensive))
                {
                    Body.StopAttack();
                }
                else
                {
                    CheckOffensiveAbilities();

                    if (MimicBody.CharacterClass.ClassType == eClassType.ListCaster && MimicBody.CharacterClass.ID != (int)eCharacterClass.Valewalker)
                    {
                        ECSGameAbilityEffect quickCast = EffectListService.GetAbilityEffectOnTarget(Body, eEffect.QuickCast);

                        if (quickCast != null)
                        {
                            CheckSpells(eCheckSpellType.Offensive);
                            return;
                        }

                        // Don't flee if in a group for now. Need better control over when and where and how.
                        if (Body.Group == null)
                        {
                            if ((TargetFleePosition == null && !IsFleeing && Body.IsBeingInterrupted && quickCast == null))
                            {
                                //TODO: Get dynamic distances based on circumstances. Maybe rethink the whole thing.
                                int fleeDistance = 2000 - Body.GetDistance(Body.TargetObject);

                                Flee(fleeDistance);

                                return;
                            }

                            if (Body.IsDestinationValid)
                                return;
                            else if (TargetFleePosition != null)
                            {
                                if (Body.GetDistance(TargetFleePosition) < 5)
                                {
                                    IsFleeing = false;
                                    TargetFleePosition = null;

                                    if (Body.IsWithinRadius(Body.TargetObject, 400))
                                    {
                                        Flee(1800);
                                        return;
                                    }

                                    if (Body.TargetObject != Body)
                                        Body.TurnTo(Body.TargetObject);
                                }
                            }
                        }

                        return;
                    }

                    if (Body.TargetObject != LastTargetObject)
                        ResetFlanking();

                    if (Body.ActiveWeapon?.Item_Type != (int)eInventorySlot.DistanceWeapon && Body.IsWithinRadius(Body.TargetObject, Body.attackComponent.AttackRange))
                    {
                        if (MimicBody.CanUsePositionalStyles && !IsMainTank && Body.ActiveWeapon != null)
                        {
                            if (Body.TargetObject is GameLiving livingTarget)
                            {
                                if (livingTarget.IsMoving || livingTarget.TargetObject == Body)
                                    ResetFlanking();

                                if (TargetFlankPosition == null && !IsFlanking && !livingTarget.IsMoving && livingTarget.TargetObject != Body)
                                {
                                    LastTargetObject = Body.TargetObject;
                                    TargetFlankPosition = GetStylePositionPoint(livingTarget, GetPositional());
                                    Body.StopFollowing();
                                    Body.StopAttack();
                                    Body.WalkTo(new Point3D(TargetFlankPosition.X, TargetFlankPosition.Y, livingTarget.Z), Body.MaxSpeed);
                                    return;
                                }

                                if (Body.IsDestinationValid)
                                {
                                    if (TargetFlankPosition == null)
                                        Body.Follow(Body.TargetObject, 75, 5000);
                                    else
                                        return;
                                }
                                else if (TargetFlankPosition != null)
                                {
                                    if (Body.GetDistance(TargetFlankPosition) < 5)
                                    {
                                        IsFlanking = true;
                                        TargetFlankPosition = null;
                                    }
                                }
                            }
                        }
                    }

                    // PR31: never switch off the instrument while a song is pulsing -
                    // MimicNPC.SwitchWeapon cancels every active pulse that needs an
                    // instrument, so doing this mid-song killed the song every tick and is
                    // why songs were unusable before.
                    //
                    // PR39: and never switch off the instrument while a song cast is
                    // COMMITTED but not yet started - the song is two ticks, and this
                    // block is what used to undo the first one.
                    if ((MimicBody.CharacterClass.ID == (int)eCharacterClass.Minstrel ||
                        (MimicBody.CharacterClass.ID == (int)eCharacterClass.Bard && Body.Group == null)) &&
                        Body.ActiveWeaponSlot != eActiveWeaponSlot.Standard &&
                        !ShouldHoldSongStance && !_stanceCommitted)
                        Body.SwitchWeapon(eActiveWeaponSlot.Standard);

                    Body.StartAttack(Body.TargetObject);

                    LastTargetObject = Body.TargetObject;
                }
            }
        }

        private void Flee(int distance)
        {
            TargetFleePosition = GetFleePoint(distance);

            if (TargetFleePosition != null)
            {
                IsFleeing = true;
                MimicBody.Sprint(true);

                Body.PathTo(TargetFleePosition, Body.MaxSpeed);
            }
            else
            {
                IsFleeing = false;
            }
        }

        public void ResetFlanking()
        {
            IsFlanking = false;
            TargetFlankPosition = null;
        }

        private GameObject CheckAssist()
        {
            if (Body.Group != null && Body.Group.MimicGroup.MainAssist.InCombat)
            {
                GameObject assistTarget = Body.Group.MimicGroup.CurrentTarget;
                GameObject target = null;

                if (assistTarget != null && CanAggroTarget((GameLiving)assistTarget))
                    target = assistTarget;

                return target;
            }

            return null;

            //if (Body.Group != null)
            //{
            //    foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
            //    {
            //        if (groupMember is GameLiving living)
            //            foreach (var attacker in living.attackComponent.Attackers)
            //                AddToAggroList(attacker.Key, 1);
            //    }
            //}
        }

        private Point3D GetFleePoint(int fleeDistance)
        {
            ushort heading;

            if (Body.IsObjectInFront(Body.TargetObject, 120))
                heading = (ushort)(Body.Heading - 2048);
            else
                heading = Body.Heading;

            if (heading < 0)
                heading += 4096;

            if (heading > 4096)
                heading -= 4096;

            Point2D point = Body.GetPointFromHeading(heading, fleeDistance);

            if (Body.CurrentRegion.GetZone(point.X, point.Y) == null)
            {
                for (int i = 0; i < 8; i++)
                {
                    heading += 512;

                    if (heading > 4096)
                        heading -= 4096;

                    Point2D validPoint = Body.GetPointFromHeading(heading, fleeDistance);

                    if (Body.CurrentRegion.GetZone(validPoint.X, validPoint.Y) != null)
                    {
                        point = validPoint;
                        break;
                    }
                }

                // Surrounded or walled in: every candidate sits outside any zone, so
                // refuse the flight instead of pathing at nothing. The old guard tested
                // `point`, which was already non-null here and could never fire.
                if (Body.CurrentRegion.GetZone(point.X, point.Y) == null)
                    return null;
            }

            if (PathingMgr.Instance.HasNavmesh(Body.CurrentZone))
            {
                Vector3? target = PathingMgr.Instance.GetClosestPointAsync(Body.CurrentZone, new Vector3(point.X, point.Y, Body.Z));

                if (target.HasValue)
                    return new Point3D(target.Value.X, target.Value.Y, target.Value.Z);
            }

            return new Point3D(point.X, point.Y, Body.Z);
        }

        private eOpeningPosition GetPositional()
        {
            eOpeningPosition positional = 0;

            if (MimicBody.CanUseSideStyles && MimicBody.CanUseBackStyles)
            {
                if (Util.RandomBool())
                    positional = eOpeningPosition.Back;
                else
                    positional = eOpeningPosition.Side;
            }
            else if (MimicBody.CanUseSideStyles)
                positional = eOpeningPosition.Side;
            else if (MimicBody.CanUseBackStyles)
                positional = eOpeningPosition.Back;

            return positional;
        }

        private Point2D GetStylePositionPoint(GameLiving living, eOpeningPosition positional)
        {
            ushort heading = 0;

            switch (positional)
            {
                case eOpeningPosition.Side:
                if (Util.RandomBool())
                    heading = (ushort)(living.Heading - 1024);
                else
                    heading = (ushort)(living.Heading + 1024);
                break;

                case eOpeningPosition.Back:
                heading = (ushort)(living.Heading - 2048);
                break;

                case eOpeningPosition.Front:
                heading = living.Heading;
                break;
            }

            if (heading < 0)
                heading += 4096;

            if (heading > 4096)
                heading -= 4096;

            Point2D point = living.GetPointFromHeading(heading, 75);

            return point;
        }

        private long _isHandlingAdditionToAggroListFromLosCheck;
        private bool StartAddToAggroListFromLosCheck => Interlocked.Exchange(ref _isHandlingAdditionToAggroListFromLosCheck, 1) == 0; // Returns true the first time it's called.


        protected virtual void LosCheckForAggroCallback(GamePlayer player, eLosCheckResponse response, ushort sourceOID, ushort targetOID)
        {
            // Make sure only one thread can enter this block to prevent multiple entities from being added to the aggro list.
            // Otherwise mobs could kill one player and immediately go for another one.
            if (response is eLosCheckResponse.TRUE && StartAddToAggroListFromLosCheck)
            {
                if (!HasAggro)
                {
                    GameObject gameObject = Body.CurrentRegion.GetObject(targetOID);

                    if (gameObject is GameLiving gameLiving)
                        AddToAggroList(gameLiving, 1);
                }

                _isHandlingAdditionToAggroListFromLosCheck = 0;
            }
        }

        protected virtual bool ShouldBeRemovedFromAggroList(GameLiving living)
        {
            // Keep Necromancer shades so that we can attack them if their pets die.
            return !living.IsAlive ||
                   living.ObjectState != GameObject.eObjectState.Active ||
                   living.IsStealthed ||
                   living.CurrentRegion != Body.CurrentRegion ||
                   !Body.IsWithinRadius(living, MAX_AGGRO_LIST_DISTANCE) ||
                   (!GameServer.ServerRules.IsAllowedToAttack(Body, living, true) && !living.effectListComponent.ContainsEffectForEffectType(eEffect.Shade));

        }

        protected virtual bool ShouldBeIgnoredFromAggroList(GameLiving living)
        {
            // We're keeping shades in the aggro list so that mobs attack them after their pet dies, so they need to be filtered out here.
            return living.effectListComponent.ContainsEffectForEffectType(eEffect.Shade);
        }

        protected virtual GameLiving CleanUpAggroListAndGetHighestModifiedThreat()

        {
            // Clear cached ordered aggro list.
            // It isn't built here because ordering all entities in the aggro list can be expensive, and we typically don't need it.
            // It's built on demand, when `GetOrderedAggroList` is called.
            OrderedAggroList.Clear();

            int attackRange = Body.attackComponent.AttackRange;
            GameLiving highestThreat = null;
            KeyValuePair<GameLiving, AggroAmount> currentTarget = default;
            long highestEffectiveAggro = -1; // Assumes that negative aggro amounts aren't allowed in the list.
            long highestEffectiveAggroInAttackRange = -1; // Assumes that negative aggro amounts aren't allowed in the list.

            foreach (var pair in AggroList)
            {
                GameLiving living = pair.Key;

                if (Body.TargetObject == living)
                    currentTarget = pair;

                if (ShouldBeRemovedFromAggroList(living))
                {
                    AggroList.TryRemove(living, out _);
                    continue;
                }

                if (ShouldBeIgnoredFromAggroList(living))
                    continue;

                // Livings further than `EFFECTIVE_AGGRO_AMOUNT_CALCULATION_DISTANCE_THRESHOLD` units away have a reduced effective aggro amount.
                // Using `Math.Ceiling` helps differentiate between 0 and 1 base aggro amount.
                AggroAmount aggroAmount = pair.Value;
                double distance = Body.GetDistanceTo(living);
                aggroAmount.Effective = distance > EFFECTIVE_AGGRO_AMOUNT_CALCULATION_DISTANCE_THRESHOLD ?
                                        (long)Math.Ceiling(aggroAmount.Base * (EFFECTIVE_AGGRO_AMOUNT_CALCULATION_DISTANCE_THRESHOLD / distance)) :
                                        aggroAmount.Base;

                if (aggroAmount.Effective > highestEffectiveAggroInAttackRange)
                {
                    if (distance <= attackRange)
                    {
                        highestEffectiveAggroInAttackRange = aggroAmount.Effective;
                        LastHighestThreatInAttackRange = living;
                    }

                    if (aggroAmount.Effective > highestEffectiveAggro)
                    {
                        highestEffectiveAggro = aggroAmount.Effective;
                        highestThreat = living;
                    }
                }
            }

            if (highestThreat != null)
            {
                // Don't change target if our new found highest threat has the same effective aggro.
                // This helps with BAF code to make mobs actually go to their intended target.
                if (currentTarget.Key != null && currentTarget.Key != highestThreat && currentTarget.Value.Effective >= highestEffectiveAggro)
                    highestThreat = currentTarget.Key;
            }
            else
            {
                // The list seems to be full of shades. It could mean we added a shade to the aggro list instead of its pet.
                // Ideally, this should never happen, but it currently can be caused by the way `AddToAggroList` propagates aggro to group members.
                // When that happens, don't bother checking aggro amount and simply return the first pet in the list.
                return AggroList.FirstOrDefault().Key?.ControlledBrain?.Body;
            }

            return highestThreat;
        }

        /// <summary>
        /// Returns the best target to attack from the current aggro list.
        /// </summary>
        protected virtual GameLiving CalculateNextAttackTarget()
        {
            return CleanUpAggroListAndGetHighestModifiedThreat();
        }

        public virtual bool CanAggroTarget(GameLiving target)
        {
            if (!GameServer.ServerRules.IsAllowedToAttack(Body, target, true))
                return false;

            // Get owner if target is pet or subpet
            GameLiving realTarget = target;

            if (realTarget is GameNPC npcTarget && npcTarget.Brain is IControlledBrain npcTargetBrain)
                realTarget = npcTargetBrain.GetLivingOwner();

            // Only attack if target is green+
            if (Body.IsObjectGreyCon(realTarget))
                return false;

            if (!PvPMode && FSM.GetCurrentState() == FSM.GetState(eFSMStateType.ROAMING))
            {
                ConColor conLimit = (ConColor)Body.GetConLevel(realTarget);

                if (conLimit >= ConColor.PURPLE)
                    return false;

                if (Body.Group == null && conLimit >= ConColor.ORANGE)
                    return false;

                if (realTarget is GameNPC npc && npc.Brain is StandardMobBrain brain && brain.HasAggro)
                    return false;
            }

            if (realTarget is IGamePlayer && realTarget.Realm != Body.Realm)
                return true;

            // Renegade mimics leave renegade (realm 0) keep guards alone
            if (Body.Realm == eRealm.Renegade && realTarget is GameKeepGuard && realTarget.Realm == eRealm.None)
                return false;

            if (realTarget is GameKeepGuard && realTarget.Realm != Body.Realm)
                return true;

            if (realTarget is GameNPC && realTarget is not MimicNPC && realTarget is not GameKeepGuard && PvPMode)
                return false;

            // We put this here to prevent aggroing non-factions npcs
            return (Body.Realm != eRealm.None || realTarget is not GameNPC) && AggroLevel > 0;
        }

        public virtual void OnAttackedByEnemy(AttackData ad)
        {
            if (!Body.IsAlive || Body.ObjectState != GameObject.eObjectState.Active)
                return;

            if (FSM.GetCurrentState() == FSM.GetState(eFSMStateType.PASSIVE))
                return;

            ConvertDamageToAggroAmount(ad.Attacker, Math.Max(1, ad.Damage + ad.CriticalDamage));

            if (!Body.attackComponent.AttackState && FSM.GetCurrentState() != FSM.GetState(eFSMStateType.AGGRO))
            {
                FSM.SetCurrentState(eFSMStateType.AGGRO);
                Think();
            }

            if (Body.Group != null)
            {
                foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                {
                    if (groupMember is MimicNPC mimic && groupMember != Body)
                        ((MimicBrain)mimic.Brain).OnGroupMemberAttacked(ad);
                }
            }
        }

        /// <summary>
        /// Converts a damage amount into an aggro amount, and splits it between the pet and its owner if necessary.
        /// Assumes damage to be superior than 0.
        /// </summary>
        protected virtual void ConvertDamageToAggroAmount(GameLiving attacker, int damage)
        {
            if (attacker is GameNPC NpcAttacker && NpcAttacker.Brain is ControlledMobBrain controlledBrain)
            {
                damage = controlledBrain.ModifyDamageWithTaunt(damage);

                // Aggro is split between the owner (15%) and their pet (85%).
                int aggroForOwner = (int)(damage * 0.15);

                // We must ensure that the same amount of aggro isn't added for both, otherwise an out-of-combat mob could attack the owner when their pet engages it.
                // The owner must also always generate at least 1 aggro.
                // This works as long as the split isn't 50 / 50.
                if (aggroForOwner == 0)
                {
                    AddToAggroList(controlledBrain.Owner, 1);
                    AddToAggroList(NpcAttacker, Math.Max(2, damage));
                }
                else
                {
                    AddToAggroList(controlledBrain.Owner, aggroForOwner);
                    AddToAggroList(NpcAttacker, damage - aggroForOwner);
                }
            }
            else
                AddToAggroList(attacker, damage);
        }

        #endregion Aggro

        #region Spells

        public enum eCheckSpellType
        {
            Offensive,
            Defensive,
            CrowdControl
        }

        /// <summary>
        /// Checks if any spells need casting
        /// </summary>
        /// <param name="type">Which type should we go through and check for?</param>
        public virtual bool CheckSpells(eCheckSpellType type)
        {
            if (Body == null || Body.Spells == null || Body.Spells.Count <= 0)
                return false;

            bool casted = false;
            List<Spell> spellsToCast = new();

            // Healers should heal whether in combat or out of it.
            if (!_goapSelectingSpell && CheckHeals())
                return true;

            if (!casted && type == eCheckSpellType.CrowdControl)
            {
                // PR38: only a mezzer takes the control duty, and only when it is not
                // pressured, silenced or already casting. A Druid with one root is not a
                // controller, and a bot with a mob on it should not start a six second mez.
                if (MimicBody.CanCastCrowdControlSpells && IsCcPrimary
                    && !ShouldWithholdControl(IsUnderPressure(), Body.IsSilenced, Body.IsCasting))
                {
                    // PR38: the candidate set is the aggro list. This used to read
                    // MimicBody.Group.MimicGroup.CCTargets - an unguarded dereference that
                    // threw for an ungrouped bot, and a list that only ever received mobs
                    // which walked past a player. Bot against bot, the list was always
                    // empty, so this whole block never ran. That is the reason there was
                    // so little crowd control to see.
                    //
                    // Both the target and the spell come from the same gate
                    // (TryPickControlSpell), so the count the sensor reports and the cast
                    // the brain makes can never disagree. Both picks were Util.Random
                    // before: two mez-capable bots in one group could pick the same add and
                    // neither could answer "which enemy does this class control".
                    foreach (GameLiving candidate in GetCcCandidates())
                    {
                        if (!TryPickControlSpell(candidate, out Spell spell))
                            continue;

                        Body.TargetObject = candidate;
                        casted = Body.CastSpell(spell, m_mobSpellLine);

                        if (casted)
                        {
                            // PR38: claim it on cast START. The old code removed the target
                            // on a successful cast, which meant a mezz that landed vanished
                            // from the list while one that was resisted stayed in it being
                            // retried - and the caster's own claim hid it from the caster,
                            // so it was never retried at all.
                            ClaimCcTarget(candidate);
                        }

                        break;
                    }
                }
            }
            else if (!casted && type == eCheckSpellType.Defensive)
            {
                if (Body.CanCastMiscSpells)
                    casted = CheckDefensiveSpells(Body.MiscSpells);
                //if (Body.CanCastMiscSpells)
                //{
                //    foreach (Spell spell in Body.MiscSpells)
                //    {
                //        if (CheckDefensiveSpells(spell))
                //        {
                //            casted = true;
                //            break;
                //        }
                //    }
                //}
            }
            else if (!casted && type == eCheckSpellType.Offensive)
            {
                if (MimicBody.CharacterClass.ID == (int)eCharacterClass.Cleric)
                {
                    if (!Util.Chance(Math.Max(5, Body.ManaPercent - 50)))
                        return false;
                }

                // Check instant spells, but only cast one to prevent spamming
                if (Body.CanCastInstantHarmfulSpells)
                {
                    foreach (Spell spell in Body.InstantHarmfulSpells)
                    {
                        if (CheckInstantOffensiveSpells(spell))
                            break;
                    }
                }

                if (Body.CanCastInstantMiscSpells)
                {
                    foreach (Spell spell in Body.InstantMiscSpells)
                    {
                        if (CheckInstantDefensiveSpells(spell))
                            break;
                    }
                }

                // TODO: Better nightshade casting logic. For now just make them melee but still use instants.
                if (MimicBody.CharacterClass.ID == (int)eCharacterClass.Nightshade)
                    return false;

                // TODO: This makes Thane and Valewalker use melee when in range rather than cast in all situations.
                //        but still use instants. Need to include other exceptions like maybe low health or endurance.
                if ((MimicBody.CanUsePositionalStyles || MimicBody.CanUseAnytimeStyles) && (Body.IsWithinRadius(Body.TargetObject, 550) || Body.ManaPercent <= 10))
                    return false;

                // PR38: the opportunistic crowd-control roll that used to live here is gone.
                //
                // It was a Util.Chance(50) - 95 when the target was already on us, 0 when the
                // target was the group's shared one - that pushed a control spell into
                // spellsToCast BEFORE the damage pool below, so a successful roll meant the
                // damage spells were never even considered. The "offensive" action was a
                // control action half the time.
                //
                // That is what built the queued-cast chain. A six second control starts, the
                // sensor's OffenseAvailable did not test IsCasting, so the damage goal was
                // planned again on the next tick, the roll produced another control, and
                // CastingComponent promotes a queued spell to the active cast when the first
                // one ends - so IsCasting never became false. The bot could do nothing but
                // cast control, for the whole fight.
                //
                // Control now has its own planned action (ControlAdd -> ControlAddsGoal),
                // which is what a decision should look like. A random roll inside the damage
                // path is not a decision.

                if (MimicBody.CanCastBolts && spellsToCast.Count < 1)
                {
                    foreach (Spell spell in MimicBody.BoltSpells)
                    {
                        if (CanCastOffensiveSpell(spell))
                            spellsToCast.Add(spell);
                    }
                }

                if (spellsToCast.Count < 1)
                {
                    if (Body.CanCastHarmfulSpells)
                    {
                        foreach (Spell spell in Body.HarmfulSpells)
                        {
                            if (spell.SpellType == eSpellType.Charm ||
                                spell.SpellType == eSpellType.Amnesia ||
                                spell.SpellType == eSpellType.Confusion ||
                                spell.SpellType == eSpellType.Taunt)
                                continue;

                            if (CanCastOffensiveSpell(spell))
                                spellsToCast.Add(spell);
                        }
                    }
                }

                if (spellsToCast.Count > 0)
                {
                    Spell spellToCast = spellsToCast[Util.Random(spellsToCast.Count - 1)];

                    if (spellToCast.Uninterruptible || !Body.IsBeingInterrupted)
                        casted = CheckOffensiveSpells(spellToCast);
                    else if (!spellToCast.Uninterruptible && Body.IsBeingInterrupted)
                    {
                        if (MimicBody.CharacterClass.ClassType == eClassType.ListCaster)
                        {
                            Ability quickCast = Body.GetAbility(Abilities.Quickcast);

                            if (quickCast != null)
                            {
                                if (Body.GetSkillDisabledDuration(quickCast) <= 0)
                                {
                                    // Give mimics a small bump in duration, they don't use it as well as humans.
                                    new QuickCastECSGameEffect(new ECSGameEffectInitParams(Body, QuickCastECSGameEffect.DURATION + 1000, 1));
                                    Body.DisableSkill(quickCast, 180000);

                                    casted = CheckOffensiveSpells(spellToCast);
                                }
                            }
                        }
                    }
                }
            }

            return casted || Body.IsCasting;
        }

        protected bool CanCastOffensiveSpell(Spell spell)
        {
            if (spell.SpellType == eSpellType.Archery &&
                spell.LifeDrainReturn == (int)DOL.GS.Spells.Archery.eShotType.Critical &&
                !Body.IsStealthed)
                return false;

            if (Body.GetSkillDisabledDuration(spell) <= 0)
            {
                if (spell.CastTime > 0)
                {
                    if (spell.Target is eSpellTarget.ENEMY or eSpellTarget.AREA or eSpellTarget.CONE)
                        return true;
                }
            }

            return false;
        }

        protected bool CanCastDefensiveSpell(Spell spell)
        {
            if (spell == null || spell.IsHarmful)
                return false;

            // Make sure we're currently able to cast the spell.
            if (spell.CastTime > 0 && Body.IsBeingInterrupted && !spell.Uninterruptible)
                return false;

            // Make sure the spell isn't disabled.
            if (Body.GetSkillDisabledDuration(spell) > 0)
                return false;

            return true;
        }

        /// <summary>Have we already checked heals this loop?</summary>
        public bool AlreadyCheckedHeals;
        private long nextCureTime = 0;

        /// <summary>PR42: will the engine let this cast start?
        ///
        /// <c>SpellHandler.CheckBeginCast</c> refuses a non-instant cast while the interrupt
        /// timer is live, and that timer is <c>InterruptRemainingDuration</c> - which is
        /// <c>max(InterruptTime, SelfInterruptTime)</c>, so it covers the SELF interrupt too
        /// and not just damage. A melee swing sets a three second self-interrupt, so a
        /// healer that had just swung was asking for casts the engine would refuse.
        ///
        /// <c>CheckHeals</c> used <c>IsBeingInterruptedIgnoreSelfInterrupt</c>, which is the
        /// damage-only half, so it asked for casts the engine would refuse - and then
        /// reported success, because <c>CastSpell</c> returns true as soon as the request is
        /// QUEUED.
        ///
        /// An instant cast is exempt from the interrupt gate, and the sitting gate is handled
        /// separately by standing up first.
        ///
        /// Pure, so the rule can be pinned without a body.</summary>
        public static bool CanEngineStartCast(bool isInterruptLocked, bool isInstantCast)
            => isInstantCast || !isInterruptLocked;

        /// <summary>
        /// PR43: is this member a better heal target than the one we have?
        ///
        /// Most injured first, nearest as the tie-break. Pure, so the rule can be pinned
        /// without a body. The reach test is separate - see IsWithinHealReach.
        /// </summary>
        public static bool IsBetterHealTarget(int newHealthPercent, int newDistance,
            int currentHealthPercent, int currentDistance)
            => newHealthPercent < currentHealthPercent
            || (newHealthPercent == currentHealthPercent && newDistance < currentDistance);

        /// <summary>
        /// PR43: can the healer reach this member? A reach of 0 means the healer has no
        /// heal spells at all, in which case the reach test is skipped rather than
        /// excluding everybody.
        /// </summary>
        public static bool IsWithinHealReach(int distance, int reach)
            => reach <= 0 || distance <= reach;

        /// <summary>
        /// PR43: the widest reach this bot has with any of its heals.
        ///
        /// The same shape as GetRezSpellRange: the widest range over the heal pools, so the
        /// reach test is data-driven and not a magic number. The widest is the right
        /// conservative answer - if a member is out of reach of every heal the bot owns,
        /// then no heal it will pick can reach it either.
        /// </summary>
        public int GetHealSpellRange()
        {
            if (Body == null)
                return 0;
            int best = 0;
            ConsiderPool(Body.HealSpells);
            ConsiderPool(Body.InstantHealSpells);
            return best;

            void ConsiderPool(IEnumerable<Spell> pool)
            {
                if (pool == null)
                    return;
                foreach (var spell in pool)
                    if (spell != null && spell.Range > best)
                        best = spell.Range;
            }
        }

        /// <summary>
        /// PR43: is any group member already casting - or about to cast - a resurrection?
        ///
        /// PR43: this used to read MimicGroup.AlreadyCastingRez, a CACHED flag. It was set
        /// when a rez was REQUESTED, and the only thing that ever cleared it was
        /// MimicGroup.CheckGroupHealth - which is reached from exactly one place,
        /// MimicBrain.CheckHeals - and that only runs on a living bot that can HEAL. So if
        /// the group's only heal-capable member died, the flag stayed true forever: one rez
        /// was requested, and no corpse was ever rezzed again.
        ///
        /// The live cast state cannot get stuck, so ask it directly. The QUEUED handler is
        /// included, because a request that has not started yet is still a rez in flight.
        /// </summary>
        public bool IsGroupCastingRez()
        {
            var group = Body?.Group;
            if (group == null)
                return false;

            foreach (GameLiving member in group.GetMembersInTheGroup())
            {
                if (member == null || !member.IsAlive)
                    continue;

                var casting = member.castingComponent;
                var spell = casting?.SpellHandler?.Spell ?? casting?.QueuedSpellHandler?.Spell;
                if (spell != null && spell.SpellType == eSpellType.Resurrect)
                    return true;
            }
            return false;
        }

        /// <summary>Check for healing and cure spells</summary>
        /// <returns>True if trying to heal, including moving to get into range</returns>
        public bool CheckHeals()
        {
            /* Summary of priorities:
                Below emergency threshold, heal as fast as possible:
                    Instant heal
                    Interrupt casting non healing spells
                    Group heal if doing so heals the group more than single target would
                    Cast our biggest heal
                    Cast our most efficient heal

                Cure Mez
                Cure disease on most injured
                Cure poison on most injured

                Below healing threshold, prioritize efficiency:
                    Let the current spell finish casting before healing        
                    Apply HoT
                    Group heal if doing so is more mana efficient than single target
                    Cast our biggest heal if we're above mana threshold and they've lost enough health to merit it
                    Cast our most efficient heal

            Notes:
                Dedicated healers will heal group members over threshold, and are more likely to use group heals efficiently
                Spread heals are not being considered
                The following spell types will only be cast by a single group member at a time:
                    Instant heal, HoT, health regen, cure mezz, cure disease, cure poison
                Cure disease and poison are on a shared timer so cure spam doesn't stop non-emergency healing,
                    and gives secondary healers a chance to cure
                Local functions are used extensively to minimize unnecessary spell checks and move complexity out of spell selection
            */

            const byte ManaThreshold = 90;
            const long CureDelay = 5000;

            if (AlreadyCheckedHeals || !Body.CanCastHealSpells || Body.IsStunned || Body.IsMezzed || Body.IsSilenced)
                return false;

            AlreadyCheckedHeals = true;

            #region Instant Spell Local Functions

            bool? m_canCastInstantHeal = null;
            bool CanCastInstantHeal() => m_canCastInstantHeal ??= CheckHealSpell(MimicBody.HealInstant);

            bool? m_canCastInstantGroupHeal = null;
            bool CanCastInstantGroupHeal() => m_canCastInstantGroupHeal ??= CheckHealSpell(MimicBody.HealInstantGroup);

            bool? m_canCastInstantHot = null;
            bool CanCastInstantHot() => m_canCastInstantHot ??= CheckHealSpell(MimicBody.HealOverTimeInstant);

            bool? m_canCastInstantGroupHot = null;
            bool CanCastInstantGroupHot() => m_canCastInstantGroupHot ??= CheckHealSpell(MimicBody.HealOverTimeInstantGroup);

            // Instant cure spells are incredibly rare, so it's faster to check if instant before the general spell check
            bool? m_canCastCureDisease = null;
            bool CanCastCureDisease() => m_canCastCureDisease ??= CheckHealSpell(MimicBody.CureDisease)
                && (!MimicBody.IsBeingInterruptedIgnoreSelfInterrupt || MimicBody.CureDisease.IsInstantCast);
            bool CanCastCureDiseaseInstant() => MimicBody.CureDisease != null && MimicBody.CureDisease.IsInstantCast
                && CanCastCureDisease();

            bool? m_canCastCureDiseaseGroup = null;
            bool CanCastCureDiseaseGroup() => m_canCastCureDiseaseGroup ??= CheckHealSpell(MimicBody.CureDiseaseGroup)
                && (!MimicBody.IsBeingInterruptedIgnoreSelfInterrupt || MimicBody.CureDiseaseGroup.IsInstantCast);
            bool CanCastCureDiseaseGroupInstant() => MimicBody.CureDiseaseGroup != null && MimicBody.CureDiseaseGroup.IsInstantCast
                && CanCastCureDiseaseGroup();

            bool? m_canCastCurePoison = null;
            bool CanCastCurePoison() => m_canCastCurePoison ??= CheckHealSpell(MimicBody.CurePoison)
                && (!MimicBody.IsBeingInterruptedIgnoreSelfInterrupt || MimicBody.CurePoison.IsInstantCast);
            bool CanCastCurePoisonInstant() => MimicBody.CurePoison != null && MimicBody.CurePoison.IsInstantCast
                && CanCastCurePoison();

            bool? m_canCastCurePoisonGroup = null;
            bool CanCastCurePoisonGroup() => m_canCastCurePoisonGroup ??= CheckHealSpell(MimicBody.CurePoisonGroup)
                && (!MimicBody.IsBeingInterruptedIgnoreSelfInterrupt || MimicBody.CurePoisonGroup.IsInstantCast);
            bool CanCastCurePoisonGroupInstant() => MimicBody.CurePoisonGroup != null && MimicBody.CurePoisonGroup.IsInstantCast
                && CanCastCurePoisonGroup();

            bool CanCastInstant() => CanCastInstantHeal() || CanCastInstantGroupHeal()
                || CanCastInstantHot() || CanCastInstantGroupHot()
                || CanCastCureDiseaseInstant() || CanCastCureDiseaseGroupInstant()
                || CanCastCurePoisonInstant() || CanCastCurePoisonGroupInstant();

            #endregion

            if (MimicBody.IsBeingInterruptedIgnoreSelfInterrupt && !CanCastInstant())
                return false;

            bool isCastingHeal = MimicBody.IsCasting && MimicBody.castingComponent.SpellHandler.Spell.IsHealing;

            if (isCastingHeal && !CanCastInstant())
                return true;

            // Working variables
            int amountToHeal;
            int numEmergency = 0;
            int numNeedHealing = 0;
            Spell spellToCast = null;
            GameLiving spellTarget = null;
            GameObject oldTarget;
            bool startedCasting = false;

            #region Local Functions

            bool? m_canCastGroupHeal = null;
            bool CanCastGroupHeal() => m_canCastGroupHeal ??= CheckHealSpell(MimicBody.HealGroup);

            bool? m_canCastBigHeal = null;
            bool CanCastBigHeal() => m_canCastBigHeal ??= CheckHealSpell(MimicBody.HealBig);

            bool? m_canCastEfficientHeal = null;
            bool CanCastEfficientHeal() => m_canCastEfficientHeal ??= CheckHealSpell(MimicBody.HealEfficient);

            bool? m_canCastHot = null;
            bool CanCastHot() => m_canCastHot ??= CheckHealSpell(MimicBody.HealOverTime);

            bool? m_canCastHotGroup = null;
            bool CanCastHotGroup() => m_canCastHotGroup ??= CheckHealSpell(MimicBody.HealOverTimeGroup);

            bool CheckHealSpell(Spell spell, bool checkGroup = true)
            {
                return spell != null
                    && (!MimicBody.IsBeingInterruptedIgnoreSelfInterrupt || spell.IsInstantCast)
                    && (!spell.HasRecastDelay || MimicBody.GetSkillDisabledDuration(spell) <= 0)
                    && MimicBody.Mana >= MimicBody.PowerCost(spell);
            }

            double m_groupHealVal = double.MinValue;
            double GetGroupHealVal()
            {
                if (m_groupHealVal < 0)
                {
                    m_groupHealVal = MimicBody.HealGroup.Value >= 0
                        ? numNeedHealing * MimicBody.HealGroup.Value
                        : amountToHeal * MimicBody.HealGroup.Value * -0.01d;
                }
                return m_groupHealVal;
            }

            double m_effectHoT = double.MinValue;
            double m_effectRegen = double.MinValue;
            double GetHotEffect(Spell spell)
            {
                switch (spell.SpellType)
                {
                    case eSpellType.HealOverTime:
                        if (m_effectHoT < 0d)
                        {
                            List<ECSGameEffect> effects;
                            lock (spellTarget.effectListComponent.EffectsLock)
                            {
                                spellTarget.effectListComponent.Effects.TryGetValue(eEffect.HealOverTime, out effects);

                                if (effects != null)
                                {
                                    foreach (ECSGameEffect effect in effects)
                                        if (effect is ECSGameSpellEffect)
                                        {
                                            double newHoT = MimicNPC.HealAmount(effect.SpellHandler.Spell, spellTarget);
                                            if (newHoT > m_effectHoT)
                                                m_effectHoT = newHoT;
                                        }
                                }
                                else
                                    m_effectHoT = 0d;
                            }
                        }
                        return m_effectHoT;
                    case eSpellType.HealthRegenBuff:
                        if (m_effectRegen < 0d)
                        {
                            List<ECSGameEffect> effects;
                            lock (spellTarget.effectListComponent.EffectsLock)
                                spellTarget.effectListComponent.Effects.TryGetValue(eEffect.HealthRegenBuff, out effects);

                            if (effects != null)
                            {
                                foreach (ECSGameEffect effect in effects)
                                    if (effect is ECSGameSpellEffect)
                                    {
                                        double newRegen = MimicNPC.HealAmount(effect.SpellHandler.Spell, spellTarget);
                                        if (newRegen > m_effectRegen)
                                            m_effectRegen = newRegen;
                                    }
                            }
                            else
                                m_effectRegen = 0d;
                        }
                        return m_effectRegen;
                }

                return 0d;
            }

            #endregion

            MimicGroup mGroup = MimicBody.Group?.MimicGroup;

            lock (mGroup?.HealLock ?? new object())
            {
                #region Check Health

                if (mGroup == null)
                {
                    amountToHeal = MimicBody.MaxHealth - MimicBody.Health;

                    if (amountToHeal > 0)
                    {
                        spellTarget = MimicBody;

                        if (MimicBody.HealthPercent < MimicGroup.HealThreshold)
                        {
                            numNeedHealing = 1;

                            if (MimicBody.HealthPercent < MimicGroup.EmergencyThreshold)
                                numEmergency = 1;
                        }
                    }
                }
                else
                {
                    mGroup.CheckGroupHealth(MimicBody);

                    amountToHeal = mGroup.AmountToHeal;
                    numEmergency = mGroup.NumNeedEmergencyHealing;
                    numNeedHealing = IsHealer
                        ? mGroup.NumInjured
                        : mGroup.NumNeedHealing;
                    spellTarget = mGroup.MemberToHeal;

                    if (mGroup.AlreadyCastInstantHeal)
                        m_canCastInstantHeal = m_canCastInstantGroupHeal = false;

                    if (mGroup.AlreadyCastingHoT)
                    {
                        if (MimicBody.HealOverTimeInstant == null || MimicBody.HealOverTimeInstant.SpellType == eSpellType.HealOverTime)
                            m_canCastInstantHot = false;
                        if (MimicBody.HealOverTimeInstantGroup == null || MimicBody.HealOverTimeInstantGroup.SpellType == eSpellType.HealOverTime)
                            m_canCastInstantGroupHot = false;
                        if (MimicBody.HealOverTime == null || MimicBody.HealOverTime.SpellType == eSpellType.HealOverTime)
                            m_canCastHot = false;
                        if (MimicBody.HealOverTimeGroup == null || MimicBody.HealOverTimeGroup.SpellType == eSpellType.HealOverTime)
                            m_canCastHotGroup = false;
                    }

                    if (mGroup.AlreadyCastingRegen)
                    {
                        if (MimicBody.HealOverTimeInstant == null || MimicBody.HealOverTimeInstant.SpellType == eSpellType.HealthRegenBuff)
                            m_canCastInstantHot = false;
                        if (MimicBody.HealOverTimeInstantGroup == null || MimicBody.HealOverTimeInstantGroup.SpellType == eSpellType.HealthRegenBuff)
                            m_canCastInstantGroupHot = false;
                        if (MimicBody.HealOverTime == null || MimicBody.HealOverTime.SpellType == eSpellType.HealthRegenBuff)
                            m_canCastHot = false;
                        if (MimicBody.HealOverTimeGroup == null || MimicBody.HealOverTimeGroup.SpellType == eSpellType.HealthRegenBuff)
                            m_canCastHotGroup = false;
                    }

                    if (mGroup.AlreadyCastingCureDisease)
                        m_canCastCureDisease = m_canCastCureDiseaseGroup = false;

                    if (mGroup.AlreadyCastingCurePoison)
                        m_canCastCurePoison = m_canCastCurePoisonGroup = false;
                }

                #endregion

                #region Emergency Heal

                if (numEmergency > 0)
                {
                    if (numEmergency > 1)
                    {
                        if (CanCastInstantGroupHeal())
                            spellToCast = MimicBody.HealInstantGroup;
                        else if (CanCastInstantHeal())
                            spellToCast = MimicBody.HealInstant;
                        else if (!isCastingHeal && CanCastGroupHeal())
                        {
                            if (MimicNPC.HealAmount(MimicBody.HealBig, spellTarget) > GetGroupHealVal() && CanCastBigHeal())
                                spellToCast = MimicBody.HealBig;
                            else if (MimicNPC.HealAmount(MimicBody.HealEfficient, spellTarget) > GetGroupHealVal() && CanCastEfficientHeal())
                                spellToCast = MimicBody.HealEfficient;
                            else
                                spellToCast = MimicBody.HealGroup;
                        }
                    }

                    if (spellToCast == null)
                    {
                        if (CanCastInstantHeal())
                            spellToCast = MimicBody.HealInstant;
                        else if (CanCastInstantGroupHeal())
                            spellToCast = MimicBody.HealInstantGroup;
                        else if (!isCastingHeal)
                        {
                            if (CanCastBigHeal())
                                spellToCast = MimicBody.HealBig;
                            else if (CanCastEfficientHeal())
                                spellToCast = MimicBody.HealEfficient;
                        }
                    }
                }

                #endregion

                #region Cure Mess/Disease/Poison

                if (spellToCast == null)
                {
                    if (mGroup != null && mGroup.MemberToCureMezz != null && !mGroup.AlreadyCastingCureMezz
                        && !MimicBody.IsCasting && CheckHealSpell(MimicBody.CureMezz))
                    {
                        spellToCast = MimicBody.CureMezz;
                        spellTarget = mGroup.MemberToCureMezz;
                    }
                    else if (mGroup == null)
                    {
                        if (MimicBody.IsDiseased && nextCureTime < GameLoop.GameLoopTime)
                        {
                            if (CanCastCureDisease() && (!MimicBody.IsCasting || CanCastCureDiseaseInstant()))
                            {
                                spellToCast = MimicBody.CureDisease;
                                spellTarget = MimicBody;
                            }
                            else if (CanCastCureDiseaseGroup() && (!MimicBody.IsCasting) || CanCastCureDiseaseGroupInstant())
                            {
                                spellToCast = MimicBody.CureDiseaseGroup;
                                spellTarget = MimicBody;
                            }
                        }
                        else if (MimicBody.IsPoisoned && nextCureTime < GameLoop.GameLoopTime)
                        {
                            if (CanCastCurePoison() && (!MimicBody.IsCasting || CanCastCurePoisonInstant()))
                            {
                                spellToCast = MimicBody.CurePoison;
                                spellTarget = MimicBody;
                            }
                            else if (CanCastCurePoisonGroup() && (!MimicBody.IsCasting || CanCastCurePoisonGroupInstant()))
                            {
                                spellToCast = MimicBody.CurePoisonGroup;
                                spellTarget = MimicBody;
                            }
                        }
                    }
                    else
                    {
                        if (mGroup.MemberToCureDisease != null && nextCureTime < GameLoop.GameLoopTime)
                        {
                            if (CanCastCureDiseaseGroup()
                                && (mGroup.NumNeedCureDisease > 1 || !CanCastCureDisease())
                                && (!MimicBody.IsCasting || CanCastCureDiseaseGroupInstant()))
                            {
                                spellToCast = MimicBody.CureDiseaseGroup;
                                spellTarget = mGroup.MemberToCureDisease;
                            }
                            else if (CanCastCureDisease()
                                && (!MimicBody.IsCasting || CanCastCureDiseaseInstant()))
                            {
                                spellToCast = MimicBody.CureDisease;
                                spellTarget = mGroup.MemberToCureDisease;
                            }
                        }
                        else if (mGroup.MemberToCurePoison != null && nextCureTime < GameLoop.GameLoopTime)
                        {
                            if (CanCastCurePoisonGroup()
                                && (mGroup.NumNeedCurePoison > 1 || !CanCastCurePoison())
                                && (!MimicBody.IsCasting || CanCastCurePoisonGroupInstant()))
                            {
                                spellToCast = MimicBody.CurePoisonGroup;
                                spellTarget = mGroup.MemberToCurePoison;
                            }
                            else if (CanCastCurePoison()
                                && (!MimicBody.IsCasting || CanCastCurePoisonInstant()))
                            {
                                spellToCast = MimicBody.CurePoison;
                                spellTarget = mGroup.MemberToCurePoison;
                            }
                        }
                    }
                }

                #endregion

                #region Non-Emergency Heal

                if (spellToCast == null && numNeedHealing > 0)
                {
                    if (numNeedHealing > 1)
                    {
                        // Instant HoTs usually have low cooldowns, so spam them whenever possible
                        if (CanCastInstantGroupHot()
                            && MimicNPC.HealAmount(MimicBody.HealOverTimeInstantGroup, spellTarget) > GetHotEffect(MimicBody.HealOverTimeInstantGroup))
                            spellToCast = MimicBody.HealOverTimeInstantGroup;
                        else if (!MimicBody.IsCasting || (numEmergency > 0 && !isCastingHeal))
                        {
                            if (CanCastHotGroup()
                                && MimicNPC.HealAmount(MimicBody.HealOverTimeGroup, spellTarget) > GetHotEffect(MimicBody.HealOverTimeGroup))
                                spellToCast = MimicBody.HealOverTimeGroup;
                            else if (CanCastGroupHeal())
                            {
                                if (!CanCastEfficientHeal()
                                    || (GetGroupHealVal() / MimicBody.PowerCost(MimicBody.HealGroup))
                                    > (MimicNPC.HealAmount(MimicBody.HealEfficient, spellTarget) / MimicBody.PowerCost(MimicBody.HealEfficient)))
                                    spellToCast = MimicBody.HealGroup;
                            }
                        }
                    }

                    if (spellToCast == null)
                    {
                        if (CanCastInstantHot()
                            && MimicNPC.HealAmount(MimicBody.HealOverTimeInstant, spellTarget) > GetHotEffect(MimicBody.HealOverTimeInstant))
                            spellToCast = MimicBody.HealOverTimeInstant;
                        else if (CanCastInstantGroupHot()
                            && MimicNPC.HealAmount(MimicBody.HealOverTimeInstantGroup, spellTarget) > GetHotEffect(MimicBody.HealOverTimeInstantGroup))
                            spellToCast = MimicBody.HealOverTimeInstantGroup;
                        else if (!MimicBody.IsCasting || (numEmergency > 0 && !isCastingHeal))
                        {
                            if (CanCastHot()
                                && MimicNPC.HealAmount(MimicBody.HealOverTime, spellTarget) > GetHotEffect(MimicBody.HealOverTime))
                                spellToCast = MimicBody.HealOverTime;
                            else if (CanCastHotGroup()
                                && MimicNPC.HealAmount(MimicBody.HealOverTimeGroup, spellTarget) > GetHotEffect(MimicBody.HealOverTimeGroup))
                                spellToCast = MimicBody.HealOverTimeGroup;
                            else if (MimicBody.ManaPercent >= ManaThreshold && CanCastBigHeal()
                                && (spellTarget.MaxHealth - spellTarget.Health) >= MimicNPC.HealAmount(MimicBody.HealBig, spellTarget))
                                spellToCast = MimicBody.HealBig;
                            else if (CanCastEfficientHeal())
                                spellToCast = MimicBody.HealEfficient;
                            else if (CanCastGroupHeal())
                                // We don't have a single target heal, but we might have a CL group heal
                                spellToCast = MimicBody.HealGroup;
                        }
                    }
                }

                #endregion

                #region Cast Spell

                if (spellToCast != null)
                {
                    if (!MimicBody.IsWithinRadius(spellTarget, new SpellHandler(MimicBody, spellToCast, m_mobSpellLine).CalculateSpellRange()))
                    {
                        // PR42: the approach has to beat the follow. NpcMovementComponent.Tick
                        // runs the movement REQUEST first and the FOLLOW second, and
                        // FollowTick() then overwrites the walk destination in the same tick -
                        // and WalkToInternal never clears the FOLLOW flag. So the healer
                        // walked toward the injured member and was instantly redirected to
                        // the leader, every 500 ms, and could never get into range. The heal
                        // target is picked by health percent with no range consideration, so
                        // being out of range is the normal case, not the exception.
                        //
                        // Dropping the follow is enough: MimicState_FollowLeader re-issues
                        // it at lines 202 and 219, and both are AFTER the
                        // "if (TryGoap(Healing) || CheckHeals()) return;" early return, so
                        // the FSM never reaches them while the healer is closing.
                        MimicBody.StopFollowing();
                        MimicBody.WalkTo(new(spellTarget.X, spellTarget.Y, spellTarget.Z), MimicBody.MaxSpeed);
                        return true;
                    }

                    if (!spellToCast.IsInstantCast)
                    {
                        if (MimicBody.IsCasting)
                            MimicBody.StopCurrentSpellcast();
                        else if (MimicBody.IsAttacking)
                            MimicBody.StopAttack();
                    }

                    // PR42: ask the engine the question it will actually ask, instead of
                    // asking a weaker one and then reporting success. SpellHandler's
                    // CheckBeginCast refuses a non-instant cast while the interrupt timer
                    // is live, and that timer is InterruptRemainingDuration - which covers
                    // the SELF interrupt too, not just damage. CheckHeals used
                    // IsBeingInterruptedIgnoreSelfInterrupt, which does not, so it asked for
                    // casts the engine would refuse.
                    //
                    // And it must not claim success when it does not get one: CastSpell
                    // returns true as soon as the request is QUEUED, so every later refusal
                    // was invisible - the FSM early-returned, the follow was never
                    // re-established, and the bot stood there "healing" forever.
                    if (!CanEngineStartCast(MimicBody.InterruptRemainingDuration > 0, spellToCast.IsInstantCast))
                        return false;

                    // PR42: stand up to heal. The engine refuses a cast from a sitting npc
                    // ("You can't cast while sitting!"), and MimicState_Camp sits the bot and
                    // has no stand-up branch - unlike FollowLeader, which stands up. So a
                    // healer that sat down could never cast again, and because the failed
                    // heal still reported success, the FSM never reached the sit/stand logic
                    // to undo it. Sitting made the heal fail, and the failed heal kept it
                    // sitting.
                    //
                    // Sit(false) is free: the IsSitting setter only acts on the false -> true
                    // transition, so standing up cannot cancel a cast.
                    if (MimicBody.IsSitting)
                        MimicBody.Sit(false);

                    oldTarget = MimicBody.TargetObject;
                    MimicBody.TargetObject = spellTarget;
                    startedCasting = MimicBody.CastSpell(spellToCast, m_mobSpellLine, false);

                    if (!startedCasting)
                        MimicBody.TargetObject = oldTarget;
                    else
                    {
                        if (spellToCast.IsInstantCast)
                        {
                            MimicBody.TargetObject = oldTarget;
                            startedCasting = false;
                        }
                        else if (spellToCast.SpellType == eSpellType.CureDisease || spellToCast.SpellType == eSpellType.CurePoison)
                            nextCureTime = GameLoop.GameLoopTime + CureDelay;

                        if (mGroup != null)
                            switch (spellToCast.SpellType)
                            {
                                case eSpellType.Heal:
                                    if (spellToCast.IsInstantCast)
                                        mGroup.AlreadyCastInstantHeal = true;
                                    break;
                                case eSpellType.HealOverTime: mGroup.AlreadyCastingHoT = true; break;
                                case eSpellType.HealthRegenBuff: mGroup.AlreadyCastingRegen = true; break;
                                case eSpellType.CureMezz: mGroup.AlreadyCastingCureMezz = true; break;
                                case eSpellType.CureDisease: mGroup.AlreadyCastingCureDisease = true; break;
                                case eSpellType.CurePoison: mGroup.AlreadyCastingCurePoison = true; break;
                            }
                    }
                }
            } // lock

            #endregion

            return startedCasting || isCastingHeal;
        }

        /// <summary>
        /// Checks defensive spells. Handles buffs, heals, etc.
        /// </summary>
        protected bool CheckDefensiveSpells(Spell spell)
        {
            if (!CanCastDefensiveSpell(spell))
                return false;

            bool casted = false;

            Body.TargetObject = null;

            // PR31: songs are not defensive spells and are deliberately not cast from
            // here. They need an instrument equipped and a held stance, so they have their
            // own path - SongGoal plus ExecuteGoapSong. The unreachable switch that used
            // to sit under this guard was written before that path existed.
            if (spell.NeedInstrument)
            {
                return false;
                switch (spell.SpellType)
                {
                    case eSpellType.PowerRegenBuff:
                    {
                        if (!Body.InCombat && !Body.IsMoving)
                        {
                            if (Body.Group != null)
                            {
                                if (Body.Group.GetMembersInTheGroup().Any(groupMember => groupMember.MaxMana > 0 && groupMember.ManaPercent < 80) && !LivingHasEffect(Body, spell))
                                {
                                    Body.SwitchWeapon(eActiveWeaponSlot.Distance);
                                    Body.TargetObject = Body;
                                }
                            }
                            else if (Body.ManaPercent < 75 && !LivingHasEffect(Body, spell))
                            {
                                Body.SwitchWeapon(eActiveWeaponSlot.Distance);
                                Body.TargetObject = Body;
                            }
                            else if (LivingHasEffect(Body, spell))
                            {
                                Body.TargetObject = Body;
                            }
                        }
                    }
                    break;

                    case eSpellType.HealthRegenBuff:

                    if (!Body.InCombat && !Body.IsMoving && !LivingHasEffect(Body, spell))
                    {
                        ECSGameEffect powerRegen = EffectListService.GetEffectOnTarget(Body, eEffect.Pulse, eSpellType.PowerRegenBuff);

                        if (powerRegen == null)
                        {
                            if (!Body.InCombat && !Body.IsMoving)
                            {
                                if (Body.Group != null)
                                {
                                    if (Body.Group.GetMembersInTheGroup().Any(groupMember => groupMember.HealthPercent < 80) && !LivingHasEffect(Body, spell))
                                    {
                                        Body.SwitchWeapon(eActiveWeaponSlot.Distance);
                                        Body.TargetObject = Body;
                                    }
                                }
                                else if (Body.HealthPercent < 80 && !LivingHasEffect(Body, spell))
                                {
                                    Body.SwitchWeapon(eActiveWeaponSlot.Distance);
                                    Body.TargetObject = Body;
                                }
                                else if (LivingHasEffect(Body, spell))
                                {
                                    Body.TargetObject = Body;
                                }
                            }
                        }
                    }
                    break;

                    case eSpellType.EnduranceRegenBuff:

                    if (Body.InCombat)
                    {
                        if (Body.Group != null)
                        {
                            if (Body.Group.GetMembersInTheGroup().Any(groupMember => groupMember.EndurancePercent < 95) && !LivingHasEffect(Body, spell))
                            {
                                Body.SwitchWeapon(eActiveWeaponSlot.Distance);
                                Body.TargetObject = Body;
                            }
                        }
                    }

                    break;

                    case eSpellType.SpeedEnhancement:
                    {
                        if (!Body.InCombat && Body.IsMoving && !LivingHasEffect(Body, spell))
                        {
                            Body.SwitchWeapon(eActiveWeaponSlot.Distance);
                            Body.TargetObject = Body;
                        }
                    }
                    break;
                }
            }
            //else
            //{
            switch (spell.SpellType)
            {
                #region Summon

                case eSpellType.SummonMinion:

                if (Body.ControlledBrain != null)
                {
                    IControlledBrain[] icb = Body.ControlledBrain.Body.ControlledNpcList;
                    int numberofpets = 0;

                    for (int i = 0; i < icb.Length; i++)
                    {
                        if (icb[i] != null)
                            numberofpets++;
                    }

                    if (numberofpets >= icb.Length)
                        break;

                    int cumulativeLevel = 0;

                    foreach (var petBrain in Body.ControlledBrain.Body.ControlledNpcList)
                    {
                        cumulativeLevel += petBrain != null && petBrain.Body != null ? petBrain.Body.Level : 0;
                    }

                    byte newpetlevel = (byte)(Body.Level * spell.Damage * -0.01);

                    if (newpetlevel > spell.Value)
                        newpetlevel = (byte)spell.Value;

                    if (cumulativeLevel + newpetlevel > 75)
                        break;

                    Body.TargetObject = Body;
                }

                break;

                case eSpellType.SummonCommander:
                case eSpellType.SummonUnderhill:
                case eSpellType.SummonDruidPet:
                case eSpellType.SummonSimulacrum:
                case eSpellType.SummonSpiritFighter:

                if (Body.ControlledBrain != null)
                    break;

                Body.TargetObject = Body;

                break;

                case eSpellType.PetSpell:
                break;

                #endregion Summon

                #region Pulse

                case eSpellType.SpeedEnhancement when spell.IsPulsing:

                if (!LivingHasEffect(Body, spell))
                    Body.TargetObject = Body;

                break;

                case eSpellType.Bladeturn when spell.IsPulsing:
                break;

                case eSpellType.MesmerizeDurationBuff when spell.IsPulsing:
                break;

                #endregion Pulse

                #region Buffs
                case eSpellType.WaterBreathing:
                break;

                case eSpellType.SpeedEnhancement when spell.IsInstantCast:
                break;

                case eSpellType.SpeedEnhancement when spell.Target == eSpellTarget.PET:
                case eSpellType.CombatSpeedBuff when spell.Duration > 20:
                case eSpellType.CombatSpeedBuff when spell.IsConcentration:
                case eSpellType.MesmerizeDurationBuff when !spell.IsPulsing:
                case eSpellType.Bladeturn when !spell.IsPulsing:
                case eSpellType.BodySpiritEnergyBuff:
                case eSpellType.HeatColdMatterBuff:
                case eSpellType.SpiritResistBuff:
                case eSpellType.EnergyResistBuff:
                case eSpellType.HeatResistBuff:
                case eSpellType.ColdResistBuff:
                case eSpellType.BodyResistBuff:
                case eSpellType.MatterResistBuff:
                case eSpellType.AllMagicResistBuff:
                case eSpellType.EnduranceRegenBuff:
                case eSpellType.PowerRegenBuff:
                case eSpellType.AblativeArmor:
                case eSpellType.AcuityBuff:
                case eSpellType.AFHitsBuff:
                case eSpellType.ArmorAbsorptionBuff:
                case eSpellType.ArmorFactorBuff:
                case eSpellType.Buff:
                case eSpellType.CelerityBuff:
                case eSpellType.ConstitutionBuff:
                case eSpellType.CourageBuff:
                case eSpellType.CrushSlashTrustBuff:
                case eSpellType.DexterityBuff:
                case eSpellType.DexterityQuicknessBuff:
                case eSpellType.EffectivenessBuff:
                case eSpellType.FatigueConsumptionBuff:
                case eSpellType.FlexibleSkillBuff:
                case eSpellType.HasteBuff:
                case eSpellType.HealthRegenBuff:
                case eSpellType.HeroismBuff:
                case eSpellType.KeepDamageBuff:
                case eSpellType.MagicResistBuff:
                case eSpellType.MeleeDamageBuff:
                case eSpellType.MLABSBuff:
                case eSpellType.PaladinArmorFactorBuff:
                case eSpellType.ParryBuff:
                case eSpellType.PowerHealthEnduranceRegenBuff:
                case eSpellType.StrengthBuff:
                case eSpellType.StrengthConstitutionBuff:
                case eSpellType.SuperiorCourageBuff:
                case eSpellType.ToHitBuff:
                case eSpellType.WeaponSkillBuff:
                case eSpellType.DamageAdd:
                case eSpellType.OffensiveProc:
                case eSpellType.DefensiveProc:
                case eSpellType.DamageShield:
                case eSpellType.OffensiveProcPvE:
                case eSpellType.BothAblativeArmor:
                {
                    if (spell.IsConcentration)
                    {
                        if (spell.Concentration > Body.Concentration)
                            break;

                        if (Body.effectListComponent.ConcentrationEffects.Count >= 20)
                            break;
                    }

                    if (spell.Target == eSpellTarget.PET)
                    {
                        // TODO: Add logic for damage shield use
                        if (spell.SpellType == eSpellType.DamageShield)
                            return false;

                        if (Body.ControlledBrain?.Body != null)
                        {
                            if (!LivingHasEffect(Body.ControlledBrain.Body, spell))
                                Body.TargetObject = Body.ControlledBrain.Body;
                        }

                        break;
                    }

                    // Buff self
                    if (!LivingHasEffect(Body, spell))
                    {
                        Body.TargetObject = Body;
                        break;
                    }

                    if (Body.Group != null)
                    {
                        if (spell.Target == eSpellTarget.REALM || spell.Target == eSpellTarget.GROUP)
                        {
                            foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                            {
                                if (groupMember != Body)
                                {
                                    if (!LivingHasEffect(groupMember, spell) && Body.IsWithinRadius(groupMember, spell.Range) && groupMember.IsAlive)
                                    {
                                        Body.TargetObject = groupMember;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                break;

                #endregion Buffs

                #region Cure Disease/Poison/Mezz

                case eSpellType.CureDisease:

                //Cure self
                if (Body.IsDiseased)
                {
                    Body.TargetObject = Body;
                    break;
                }

                // Cure group members
                if (Body.Group != null)
                {
                    foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                    {
                        if (groupMember != Body)
                        {
                            if (groupMember.IsDiseased && Body.IsWithinRadius(groupMember, spell.Range))
                            {
                                Body.TargetObject = groupMember;
                                break;
                            }
                        }
                    }
                }
                break;

                case eSpellType.CurePoison:
                //Cure self
                if (Body.IsPoisoned)
                {
                    Body.TargetObject = Body;
                    break;
                }

                // Cure group members
                if (Body.Group != null)
                {
                    foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                    {
                        if (groupMember != Body)
                        {
                            if (groupMember.IsPoisoned && Body.IsWithinRadius(groupMember, spell.Range))
                            {
                                Body.TargetObject = groupMember;
                                break;
                            }
                        }
                    }
                }
                break;

                case eSpellType.CureMezz:
                if (Body.Group != null)
                {
                    foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                    {
                        if (groupMember != Body)
                        {
                            if (groupMember.IsMezzed && Body.IsWithinRadius(groupMember, spell.Range))
                            {
                                Body.TargetObject = groupMember;
                                break;
                            }
                        }
                    }
                }
                break;

                case eSpellType.CureNearsightCustom:
                if (Body.Group != null)
                {
                    foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                    {
                        if (groupMember != Body)
                        {
                            if (LivingHasEffect(groupMember, spell) && Body.IsWithinRadius(groupMember, spell.Range))
                            {
                                Body.TargetObject = groupMember;
                                break;
                            }
                        }
                    }
                }
                break;

                #endregion Cure Disease/Poison/Mezz

                #region Charms

                case eSpellType.Charm:
                break;

                #endregion Charms

                case eSpellType.Resurrect:

                if (Body.Group != null && !RezDisabled
                    && (Body.Group.MimicGroup == null || !Body.Group.MimicGroup.AlreadyCastingRez))
                {
                    var corpse = FindRezTarget();
                    if (corpse != null && Body.IsWithinRadius(corpse, spell.Range))
                        Body.TargetObject = corpse;
                }
                break;

                case eSpellType.LifeTransfer:

                if (Body.Group != null)
                {
                    if (Body.HealthPercent > 50)
                    {
                        GameLiving livingToHeal = null;
                        int threshold = Properties.NPC_HEAL_THRESHOLD / 2;
                        int lowestHealth = 100;

                        foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                        {
                            if (groupMember.HealthPercent < threshold)
                            {
                                if (groupMember.HealthPercent < lowestHealth)
                                {
                                    livingToHeal = groupMember;
                                    lowestHealth = groupMember.HealthPercent;
                                }
                            }
                        }

                        if (livingToHeal != null && livingToHeal.IsAlive)
                            Body.TargetObject = livingToHeal;
                    }
                }

                break;

                case eSpellType.PetConversion:
                break;

                case eSpellType.FocusShell:
                break;

                default:
                log.Warn($"CheckDefensiveSpells() encountered an unknown spell type [{spell.SpellType}] for {Body?.Name}");
                break;
            }

            if (Body?.TargetObject != null)
            {
                //log.Info(Body.Name + " tried to cast " + spell.Name + " " + spell.SpellType.ToString() + " on " + Body.TargetObject.Name);
                //log.Info(Body.TargetObject.Name + " effect is " + LivingHasEffect((GameLiving)Body.TargetObject, spell));

                casted = Body.CastSpell(spell, m_mobSpellLine);
            }

            return casted;
        }

        bool CheckDefensiveSpells(List<Spell> spells)
        {
            // Contrary to offensive spells, we don't start with a valid target.
            // So the idea here is to find a target, switch before calling `CastDefensiveSpell`, then retrieve our previous target.
            List<(Spell, GameLiving)> spellsToCast = new(spells.Count);

            foreach (Spell spell in spells)
            {
                if (CanCastDefensiveSpell(spell, out GameLiving target))
                    spellsToCast.Add((spell, target));
            }

            if (spellsToCast.Count == 0)
                return false;

            GameObject oldTarget = Body.TargetObject;
            (Spell spell, GameLiving target) spellToCast = spellsToCast[Util.Random(spellsToCast.Count - 1)];
            Body.TargetObject = spellToCast.target;
            bool cast = Body.CastSpell(spellToCast.spell, m_mobSpellLine);

            if (Debug)
            {
                if (cast)
                    log.Info(Body.Name + " tried to cast " + spellToCast.spell.Name + " on " + spellToCast.target.Name + " and cast == true");
                else
                    log.Info(Body.Name + " tried to cast " + spellToCast.spell.Name + " on " + spellToCast.target.Name + " and cast == false");

                if (LivingHasEffect(spellToCast.target, spellToCast.spell))
                    log.Info(spellToCast.target.Name + " has the effect already.");
            }

            Body.TargetObject = oldTarget;
            return cast;
        }

        /// <summary>
        /// PR41: can this defensive spell be cast on a target right now?
        ///
        /// Hoisted out of CheckDefensiveSpells so the sensor can ask the SAME question
        /// the action asks. Before this, the sensor reported the buff state from a
        /// hard-coded false and the action worked it out for itself - so the two could
        /// not agree, and the goal could never be satisfied.
        /// </summary>
        protected bool CanCastDefensiveSpell(Spell spell, out GameLiving target)
        {
            target = null;

            // PR31: songs are excluded on purpose - see ExecuteGoapSong, which handles
            // the instrument stance. Everything else keeps the interrupt and recast gates.
            if (spell.NeedInstrument || (!spell.Uninterruptible && Body.IsBeingInterrupted) ||
                (spell.HasRecastDelay && Body.GetSkillDisabledDuration(spell) > 0))
            {
                return false;
            }

            target = FindTargetForDefensiveSpell(spell);
            return target != null;
        }

        /// <summary>
        /// PR41: how many of this bot's own buffs are still missing?
        ///
        /// The same question the defensive action asks, narrowed to the bot's own buffs.
        /// A buff that is already up is skipped - LivingHasEffect finds the effect on the
        /// body - so only the ones that genuinely need casting are counted. A buff on
        /// recast cooldown is not counted either, because casting it would fail anyway.
        ///
        /// Summons are excluded: FindTargetForDefensiveSpell returns the caster for
        /// those too, and a summon is not a buff.
        /// </summary>
        public int CountMissingSelfBuffs()
        {
            if (Body == null || !Body.CanCastMiscSpells)
                return 0;

            int missing = 0;
            foreach (Spell spell in Body.MiscSpells)
            {
                if (spell == null || spell.SpellType == eSpellType.Summon)
                    continue;
                if (CanCastDefensiveSpell(spell, out GameLiving target) && target == Body)
                    missing++;
            }

            return missing;
        }

        /// <summary>
        /// PR41: does this bot still have a buff it could apply?
        ///
        /// This is what the sensor reports as BUFFS_MAINTAINED (inverted). While that key
        /// was hard-coded false, BuffMaintenanceGoal.IsGoalSatisfied was always false, so
        /// the goal re-fired on every tick out of combat and nobody could tell from
        /// /mgoap status whether the buffs were actually up.
        /// </summary>
        public bool HasMissingSelfBuff() => CountMissingSelfBuffs() > 0;

        protected virtual GameLiving FindTargetForDefensiveSpell(Spell spell)
        {
            GameLiving target = null;

            switch (spell.SpellType)
            {
                #region Pulse

                case eSpellType.SpeedEnhancement when spell.IsPulsing:

                if (!LivingHasEffect(Body, spell))
                    target = Body;

                break;

                case eSpellType.Bladeturn when spell.IsPulsing:
                break;

                case eSpellType.MesmerizeDurationBuff when spell.IsPulsing:
                break;

                #endregion Pulse

                #region Buffs

                // TODO: WaterBreathing and Druid BothAblative and Healer Celerity
                case eSpellType.WaterBreathing:
                case eSpellType.BothAblativeArmor when spell.Duration <= 15:
                case eSpellType.CombatSpeedBuff when spell.Duration <= 20:
                break;

                case eSpellType.SpeedEnhancement when spell.IsInstantCast:
                break;

                case eSpellType.SpeedEnhancement when spell.IsPulsing:
                case eSpellType.SpeedEnhancement when spell.Target == eSpellTarget.PET:
                case eSpellType.CombatSpeedBuff when spell.Duration > 20:
                case eSpellType.CombatSpeedBuff when spell.IsConcentration:
                case eSpellType.MesmerizeDurationBuff when !spell.IsPulsing:
                case eSpellType.Bladeturn when !spell.IsPulsing:

                case eSpellType.AcuityBuff:
                case eSpellType.AFHitsBuff:
                case eSpellType.AllMagicResistBuff:
                case eSpellType.ArmorAbsorptionBuff:
                case eSpellType.ArmorFactorBuff:
                case eSpellType.PaladinArmorFactorBuff:
                case eSpellType.BodyResistBuff:
                case eSpellType.BodySpiritEnergyBuff:
                case eSpellType.Buff:
                case eSpellType.CelerityBuff:
                case eSpellType.ColdResistBuff:
                case eSpellType.CombatSpeedBuff:
                case eSpellType.ConstitutionBuff:
                case eSpellType.CourageBuff:
                case eSpellType.CrushSlashTrustBuff:
                case eSpellType.DexterityBuff:
                case eSpellType.DexterityQuicknessBuff:
                case eSpellType.EffectivenessBuff:
                case eSpellType.EnduranceRegenBuff:
                case eSpellType.EnergyResistBuff:
                case eSpellType.FatigueConsumptionBuff:
                case eSpellType.FlexibleSkillBuff:
                case eSpellType.HasteBuff:
                case eSpellType.HealthRegenBuff:
                case eSpellType.HeatColdMatterBuff:
                case eSpellType.HeatResistBuff:
                case eSpellType.HeroismBuff:
                case eSpellType.KeepDamageBuff:
                case eSpellType.MagicResistBuff:
                case eSpellType.MatterResistBuff:
                case eSpellType.MeleeDamageBuff:
                case eSpellType.MesmerizeDurationBuff:
                case eSpellType.MLABSBuff:
                case eSpellType.ParryBuff:
                case eSpellType.PowerHealthEnduranceRegenBuff:
                case eSpellType.PowerRegenBuff:
                case eSpellType.SavageCombatSpeedBuff:
                case eSpellType.SavageCrushResistanceBuff:
                case eSpellType.SavageDPSBuff:
                case eSpellType.SavageParryBuff:
                case eSpellType.SavageSlashResistanceBuff:
                case eSpellType.SavageThrustResistanceBuff:
                case eSpellType.SpiritResistBuff:
                case eSpellType.StrengthBuff:
                case eSpellType.StrengthConstitutionBuff:
                case eSpellType.SuperiorCourageBuff:
                case eSpellType.ToHitBuff:
                case eSpellType.WeaponSkillBuff:
                case eSpellType.DamageAdd:
                case eSpellType.OffensiveProc:
                case eSpellType.DefensiveProc:
                case eSpellType.DamageShield:
                case eSpellType.Bladeturn:
                case eSpellType.OffensiveProcPvE:
                {
                    // TODO: PBAoE Buffs
                    if (spell.IsPBAoE)
                        break;

                    if (spell.IsConcentration)
                    {
                        if (spell.Concentration > Body.Concentration)
                            break;

                        if (Body.effectListComponent.ConcentrationEffects.Count >= 20)
                            break;
                    }

                    if (!LivingHasEffect(Body, spell) && !Body.attackComponent.AttackState && spell.Target != eSpellTarget.PET)
                    {
                        target = Body;
                        break;
                    }

                    if (Body.ControlledBrain != null && Body.ControlledBrain.Body != null && Body.GetDistanceTo(Body.ControlledBrain.Body) <= spell.Range && !LivingHasEffect(Body.ControlledBrain.Body, spell) && spell.Target != eSpellTarget.SELF)
                    {
                        if (spell.SpellType == eSpellType.DamageShield)
                            break;

                        target = Body.ControlledBrain.Body;
                        break;
                    }

                    if (Body.Group != null)
                    {
                        if (spell.Target == eSpellTarget.REALM || spell.Target == eSpellTarget.GROUP)
                        {
                            foreach (GameLiving groupMember in Body.Group.GetMembersInTheGroup())
                            {
                                if (groupMember != Body)
                                {
                                    if (!LivingHasEffect(groupMember, spell) && !Body.attackComponent.AttackState && groupMember.IsAlive)
                                    {
                                        target = groupMember;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    break;
                }

                #endregion Buffs

                #region Summon

                case eSpellType.Summon:
                {
                    target = Body;
                    break;
                }

                case eSpellType.SummonMinion:
                {
                    if (Body.ControlledBrain != null)
                    {
                        IControlledBrain[] icb = Body.ControlledBrain.Body.ControlledNpcList;
                        int numberofpets = 0;

                        for (int i = 0; i < icb.Length; i++)
                        {
                            if (icb[i] != null)
                                numberofpets++;
                        }

                        if (numberofpets >= icb.Length)
                            break;

                        int cumulativeLevel = 0;

                        foreach (var petBrain in Body.ControlledBrain.Body.ControlledNpcList)
                        {
                            cumulativeLevel += petBrain != null && petBrain.Body != null ? petBrain.Body.Level : 0;
                        }

                        byte newpetlevel = (byte)(Body.Level * spell.Damage * -0.01);

                        if (newpetlevel > spell.Value)
                            newpetlevel = (byte)spell.Value;

                        if (cumulativeLevel + newpetlevel > 75)
                            break;

                        target = Body;
                    }

                    break;
                }

                #endregion Summon

                #region Heals

                case eSpellType.CombatHeal:
                case eSpellType.Heal:
                case eSpellType.HealOverTime:
                case eSpellType.MercHeal:
                case eSpellType.OmniHeal:
                case eSpellType.PBAoEHeal:
                case eSpellType.SpreadHeal:
                {
                    if (Body.ControlledBrain != null && Body.ControlledBrain.Body != null
                        && Body.GetDistanceTo(Body.ControlledBrain.Body) <= spell.Range
                        && Body.ControlledBrain.Body.HealthPercent < Properties.NPC_HEAL_THRESHOLD
                        && spell.Target != eSpellTarget.SELF)
                    {
                        target = Body.ControlledBrain.Body;
                        break;
                    }

                    break;
                }

                #endregion

                case eSpellType.SummonCommander:
                case eSpellType.SummonDruidPet:
                case eSpellType.SummonHunterPet:
                case eSpellType.SummonNecroPet:
                case eSpellType.SummonUnderhill:
                case eSpellType.SummonSimulacrum:
                case eSpellType.SummonSpiritFighter:
                {
                    if (Body.ControlledBrain != null)
                        break;

                    target = Body;
                    break;
                }

                case eSpellType.Resurrect:
                {
                    if (!RezDisabled && (Body.Group?.MimicGroup == null || !Body.Group.MimicGroup.AlreadyCastingRez))
                        target = FindRezTarget();

                    break;
                }

                default:
                break;
            }

            if (target != null)
                target = HandleExceptions(target, spell);

            return target;
        }

        private GameLiving HandleExceptions(GameLiving target, Spell spell)
        {
            if (target is IGamePlayer playerTarget)
            {
                switch (spell.SpellType)
                {
                    case eSpellType.AcuityBuff:
                    {
                        if (playerTarget.CharacterClass.ClassType != eClassType.ListCaster)
                            target = null;

                        break;
                    }

                    case eSpellType.StrengthBuff when playerTarget.CharacterClass.ID != (int)eCharacterClass.Valewalker:
                    case eSpellType.ArmorFactorBuff:
                    {
                        if (spell.IsConcentration && playerTarget.CharacterClass.ClassType == eClassType.ListCaster)
                            target = null;

                        break;
                    }

                    default:
                    break;
                }
            }

            return target;
        }

        /// <summary>
        /// Checks offensive spells.  Handles dds, debuffs, etc.
        /// </summary>
        protected virtual bool CheckOffensiveSpells(Spell spell, bool quickCast = false)
        {
            //if (spell.NeedInstrument && Body.ActiveWeaponSlot != eActiveWeaponSlot.Distance)
                //Body.SwitchWeapon(eActiveWeaponSlot.Distance);

            bool casted = false;

            if (spell.SpellType == eSpellType.Archery &&
                Body.ActiveWeaponSlot != eActiveWeaponSlot.Distance)
                Body.SwitchWeapon(eActiveWeaponSlot.Distance);

            if (Body.TargetObject is GameLiving living && (spell.Duration == 0 || !LivingHasEffect(living, spell) || spell.SpellType == eSpellType.DirectDamageWithDebuff || spell.SpellType == eSpellType.DamageSpeedDecrease))
            {
                casted = Body.CastSpell(spell, m_mobSpellLine);

                //log.Info(Body.Name + " tried to cast " + spell.Name + " " + spell.SpellType.ToString() + " on " + Body.TargetObject.Name);
                //log.Info(Body.TargetObject.Name + " effect is " + LivingHasEffect((GameLiving)Body.TargetObject, spell));
            }

            return casted;
        }

        protected virtual bool CheckInstantDefensiveSpells(Spell spell)
        {
            if (spell.HasRecastDelay && Body.GetSkillDisabledDuration(spell) > 0)
                return false;

            bool castSpell = false;

            switch (spell.SpellType)
            {
                // TODO: Stealth archer using speed to get away or attack
                //case eSpellType.SpeedEnhancement:

                case eSpellType.SavageCrushResistanceBuff:
                case eSpellType.SavageSlashResistanceBuff:
                case eSpellType.SavageThrustResistanceBuff:
                case eSpellType.SavageCombatSpeedBuff:
                case eSpellType.SavageDPSBuff:
                case eSpellType.SavageParryBuff:
                case eSpellType.SavageEvadeBuff:

                if (spell.SpellType == eSpellType.SavageCrushResistanceBuff ||
                    spell.SpellType == eSpellType.SavageSlashResistanceBuff ||
                    spell.SpellType == eSpellType.SavageThrustResistanceBuff &&
                    !CheckSavageResistSpell(spell.SpellType))
                    break;

                if (!LivingHasEffect(Body, spell))
                    castSpell = true;

                break;

                case eSpellType.BodySpiritEnergyBuff:
                case eSpellType.HeatColdMatterBuff:
                case eSpellType.SpiritResistBuff:
                case eSpellType.EnergyResistBuff:
                case eSpellType.HeatResistBuff:
                case eSpellType.ColdResistBuff:
                case eSpellType.BodyResistBuff:
                case eSpellType.MatterResistBuff:
                {
                    // Temp to stop Paladins/Skalds from spamming.
                    // TODO: Smarter use of resist chants.
                    if (spell.Pulse > 0)
                        break;

                    break;
                }

                case eSpellType.CombatHeal:
                case eSpellType.DamageAdd:
                case eSpellType.ArmorFactorBuff:
                case eSpellType.DexterityQuicknessBuff:
                case eSpellType.EnduranceRegenBuff:
                case eSpellType.CombatSpeedBuff:
                case eSpellType.AblativeArmor:
                case eSpellType.Bladeturn:
                case eSpellType.OffensiveProc:
                case eSpellType.SummonHunterPet:

                if (spell.SpellType == eSpellType.CombatSpeedBuff)
                {
                    if (Body.TargetObject != null && !Body.IsWithinRadius(Body.TargetObject, Body.MeleeAttackRange))
                        break;
                }

                if (!LivingHasEffect(Body, spell))
                    castSpell = true;

                break;
            }

            if (castSpell)
                Body.CastSpell(spell, m_mobSpellLine);

            return castSpell;
        }

        /// <summary>
        /// Checks Instant Spells.  Handles Taunts, shouts, stuns, etc.
        /// </summary>
        protected virtual bool CheckInstantOffensiveSpells(Spell spell)
        {
            if (spell.HasRecastDelay && Body.GetSkillDisabledDuration(spell) > 0)
                return false;

            bool castSpell = false;

            switch (spell.SpellType)
            {
                #region Enemy Spells

                case eSpellType.Taunt:

                if (Body.Group?.MimicGroup.MainTank == Body)
                    castSpell = true;

                break;

                case eSpellType.DirectDamage:
                case eSpellType.NightshadeNuke:
                case eSpellType.Lifedrain:
                case eSpellType.DexterityDebuff:
                case eSpellType.DexterityQuicknessDebuff:
                case eSpellType.StrengthDebuff:
                case eSpellType.StrengthConstitutionDebuff:
                case eSpellType.CombatSpeedDebuff:
                case eSpellType.DamageOverTime:
                case eSpellType.MeleeDamageDebuff:
                case eSpellType.AllStatsPercentDebuff:
                case eSpellType.CrushSlashThrustDebuff:
                case eSpellType.EffectivenessDebuff:
                case eSpellType.Disease:
                case eSpellType.Stun:
                case eSpellType.Mez:
                case eSpellType.Mesmerize:

                if (spell.IsPBAoE && !Body.IsWithinRadius(Body.TargetObject, spell.Radius))
                    break;

                // Try to limit the debuffs cast to save mana and time spent doing so.
                if (MimicBody.CharacterClass.ClassType == eClassType.ListCaster)
                {
                    if (!Util.Chance(Math.Max(5, Body.ManaPercent - 75)))
                        break;
                }

                if (!LivingHasEffect((GameLiving)Body.TargetObject, spell) && Body.IsWithinRadius(Body.TargetObject, spell.Range))
                    castSpell = true;

                break;

                #endregion Enemy Spells
            }

            ECSGameEffect pulseEffect = EffectListService.GetPulseEffectOnTarget(Body, spell);

            if (pulseEffect != null)
                return false;

            if (castSpell)
            {
                Body.CastSpell(spell, m_mobSpellLine);
                return true;
            }

            return false;
        }

        protected virtual bool CheckSavageResistSpell(eSpellType spellType)
        {
            eDamageType damageType = eDamageType.Natural;

            switch (spellType)
            {
                case eSpellType.SavageCrushResistanceBuff:
                damageType = eDamageType.Crush;
                break;

                case eSpellType.SavageSlashResistanceBuff:
                damageType = eDamageType.Slash;
                break;

                case eSpellType.SavageThrustResistanceBuff:
                damageType = eDamageType.Thrust;
                break;
            }

            if (Body.attackComponent.Attackers.Count > 0)
            {
                foreach (var attacker in Body.attackComponent.Attackers)
                {
                    if (attacker.Key.ActiveWeapon != null)
                    {
                        if (attacker.Key.ActiveWeapon.Type_Damage != 0 && (int)damageType == attacker.Key.ActiveWeapon.Type_Damage)
                            return true;
                    }
                    else if (attacker.Key is GameNPC npc)
                    {
                        if (npc.MeleeDamageType == damageType)
                            return true;
                    }
                }
            }

            return false;
        }

        protected static SpellLine m_mobSpellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
        //protected static SpellLine m_MimicSpellLine = SkillBase.GetSpellLine("MimicSpellLine");

        /// <summary>
        /// Checks if the living target has a spell effect.
        /// Only to be used for spell casting purposes.
        /// </summary>
        /// <returns>True if the living has the effect of will receive it by our current spell.</returns>
        public bool LivingHasEffect(GameLiving target, Spell spell)
        {
            if (target == null)
                return true;

            eEffect spellEffect = EffectService.GetEffectFromSpell(spell, m_mobSpellLine.IsBaseLine);

            // Ignore effects that aren't actually effects (may be incomplete).
            if (spellEffect is eEffect.DirectDamage or eEffect.Pet or eEffect.Unknown)
                return false;

            ISpellHandler spellHandler = Body.castingComponent.SpellHandler;

            // If we're currently casting 'spell' on 'target', assume it already has the effect.
            // This allows spell queuing while preventing casting on the same target more than once.
            if (spellHandler != null && spellHandler.Spell.ID == spell.ID && spellHandler.Target == target)
                return true;

            ISpellHandler queuedSpellHandler = Body.castingComponent.QueuedSpellHandler;

            // Do the same for our queued up spell.
            // This can happen on charmed pets having two buffs that they're trying to cast on their owner.
            if (queuedSpellHandler != null && queuedSpellHandler.Spell.ID == spell.ID && queuedSpellHandler.Target == target)
                return true;

            // May not be the right place for that, but without that check NPCs with more than one offensive or defensive proc will only buff themselves once.
            if (spell.SpellType is eSpellType.OffensiveProc or eSpellType.DefensiveProc)
            {
                if (target.effectListComponent.Effects.TryGetValue(EffectService.GetEffectFromSpell(spell, m_mobSpellLine.IsBaseLine), out List<ECSGameEffect> existingEffects))
                {
                    if (existingEffects.FirstOrDefault(e => e.SpellHandler.Spell.ID == spell.ID || (spell.EffectGroup > 0 && e.SpellHandler.Spell.EffectGroup == spell.EffectGroup)) != null)
                        return true;
                }

                return false;
            }

            ECSGameEffect pulseEffect = EffectListService.GetPulseEffectOnTarget(target, spell);

            if (pulseEffect != null)
                return true;

            // True if the target has the effect, or the immunity effect for this effect.
            // Treat NPC immunity effects as full immunity effects.
            return EffectListService.GetEffectOnTarget(target, spellEffect) != null || HasImmunityEffect(EffectService.GetImmunityEffectFromSpell(spell)) || HasImmunityEffect(EffectService.GetNpcImmunityEffectFromSpell(spell));

            bool HasImmunityEffect(eEffect immunityEffect)
            {
                return immunityEffect != eEffect.Unknown && EffectListService.GetEffectOnTarget(target, immunityEffect) != null;
            }
        }

        #endregion Spells

        #region DetectDoor

        public virtual void DetectDoor()
        {
            ushort range = (ushort)(ThinkInterval / 800 * Body.CurrentWaypoint.MaxSpeed);

            foreach (GameDoorBase door in Body.CurrentRegion.GetDoorsInRadius(Body, range))
            {
                if (door is GameKeepDoor)
                {
                    if (Body.Realm != door.Realm)
                        return;

                    door.Open();
                    //Body.Say("GameKeep Door is near by");
                    //somebody can insert here another action for GameKeep Doors
                    return;
                }
                else
                {
                    door.Open();
                    return;
                }
            }

            return;
        }

        #endregion DetectDoor
    }
}
