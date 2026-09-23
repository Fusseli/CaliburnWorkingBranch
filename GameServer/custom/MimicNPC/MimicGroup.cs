using DOL.AI;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.RealmAbilities;
using DOL.GS.ServerProperties;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DOL.GS.Scripts
{
    public class MimicGroup
    {
        private Group _group;
        public GameLiving MainLeader { get; private set; }
        public GameLiving MainAssist { get; private set; }
        public GameLiving MainTank { get; private set; }
        public GameLiving MainCC { get; private set; }
        public GameLiving MainPuller { get; private set; }
        public Point3D CampPoint { get; private set; }
        public Point2D PullFromPoint {get; private set; }

        public List<GameLiving> CCTargets = new List<GameLiving>();

        public int ConLevelFilter = -2;

        public const string RoleTank = "tank";
        public const string RoleAssist = "assist";
        public const string RoleCC = "cc";
        public const string RolePuller = "puller";

        /// <summary>Roles assigned by hand (/mrole or whisper claim). Elections never touch them.</summary>
        public readonly HashSet<string> ManualRoles = new();

        public void LockRole(string role)
        {
            if (!string.IsNullOrEmpty(role))
                ManualRoles.Add(role.ToLowerInvariant());
        }

        public GameObject CurrentTarget
        {
            get { return MainAssist.TargetObject; }
        }

        public MimicGroup(GameLiving leader, Group group) 
        {
            MainLeader = leader;
            MainAssist = leader;
            MainTank = leader;
            MainCC = leader;
            MainPuller = leader;

            _group = group;
        }

        public bool SetLeader(GameLiving living)
        {
            if (living == null)
                return false;

            MainLeader = living;
            living.Group.SendMessageToGroupMembers(living, "Follow me! I will now lead the group. Not really though this isn't implemented.",
                PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);


            return true;
        }

        public bool SetMainAssist(GameLiving living)
        {
            if (living == null)
                return false;

            MainAssist = living;
            living.Group.SendMessageToGroupMembers(living, "Assist me! I will be the main assist. Not really though this isn't implemented.",
                PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);

            return true;
        }

        public bool SetMainTank(GameLiving living)
        {
            if (living == null)
                return false;

            MainTank = living;
            living.Group.SendMessageToGroupMembers(living, "I will tank.",
                PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);
            return true;
        }

        public bool SetMainCC(GameLiving living)
        {
            if (living == null)
                return false;

            MainCC = living;
            living.Group.SendMessageToGroupMembers(living, "I'll be the main CC.",
                PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);


            return true;
        }

        public bool SetMainPuller(GameLiving living)
        {
            if (living == null || living.Inventory.GetItem(eInventorySlot.DistanceWeapon) == null)
                return false;

            if (MainPuller == living)
            {
                MainPuller = MainLeader;
                living.Group.SendMessageToGroupMembers(living, "I'll stop pulling.",
                    PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);
            }
            else
            {
                MainPuller = living;
                living.Group.SendMessageToGroupMembers(living, "I'll be the puller.",
                    PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);
            }

            return true;
        }

        public void SetCampPoint(Point3D point)
        {
            if (point != null)
                CampPoint = new Point3D(point);
            else
                CampPoint = null;
        }

        #region RoleElection

        /// <summary>One member's capabilities for role election, read once from live
        /// state. Scoring is pure logic on snapshots, so elections are unit-testable
        /// without server singletons; only snapshot construction touches game objects.</summary>
        public sealed class RoleCapabilities
        {
            public GameLiving Member;
            public MimicBrain Brain;
            public bool IsHealer, HealerManual, CanHeal, CanCC, CanNuke, HasGuard, HasBow;

            public static RoleCapabilities Read(GameLiving m)
            {
                var c = new RoleCapabilities { Member = m };
                var mn = m as MimicNPC;
                c.Brain = mn?.MimicBrain;
                c.IsHealer = c.Brain != null && c.Brain.IsHealer;
                c.HealerManual = c.Brain != null && c.Brain.HealerManual;
                c.CanHeal = mn != null && (mn.CanCastHealSpells || mn.CanCastInstantHealSpells);
                c.CanCC = mn != null && mn.CanCastCrowdControlSpells;
                c.CanNuke = mn != null && mn.CanCastHarmfulSpells;
                c.HasGuard = mn != null && mn.HasAbility(Abilities.Guard);
                c.HasBow = mn?.Inventory?.GetItem(eInventorySlot.DistanceWeapon) != null;
                return c;
            }

            /// <summary>Guard holders first, melee over casters, healers never.</summary>
            public int TankScore() => IsHealer ? 0 : HasGuard ? 10 : CanNuke ? 2 : 5;

            /// <summary>Assist callers: nukers first, melee next, healers never.</summary>
            public int AssistScore() => IsHealer ? 0 : CanNuke ? 6 : 5;

            /// <summary>CC callers: only mez-capable bots score; ties keep the incumbent.</summary>
            public int CcScore() => CanCC ? 10 : 0;

            /// <summary>Pullers: ranged weapon required (same rule as SetMainPuller).</summary>
            public int PullerScore() => HasBow ? 10 : 0;
        }

        /// <summary>Class-aware role defaults for bot groups. A fresh group starts with
        /// every role on the leader; a healer-class leader would tank, pull, CC and call
        /// targets at once. Election moves each role to the most capable bot member.
        /// Players are never assigned or demoted, manually locked roles are skipped,
        /// and the incumbent stays on any tie, so elections never flap. Silent on purpose:
        /// no group spam on every join/leave.</summary>
        public void ElectRoles(System.Collections.Generic.IEnumerable<GameLiving> members)
            => ElectSnapshots(members
                ?.Where(m => m is MimicNPC && m.IsAlive && m.ObjectState == GameObject.eObjectState.Active)
                .Select(RoleCapabilities.Read).ToList()
                ?? new System.Collections.Generic.List<RoleCapabilities>());

        public void ElectSnapshots(System.Collections.Generic.List<RoleCapabilities> bots)
        {
            if (bots.Count == 0)
                return;
            ElectRole(RoleTank, c => c.TankScore(), bots, v => MainTank = v, () => MainTank);
            ElectRole(RoleAssist, c => c.AssistScore(), bots, v => MainAssist = v, () => MainAssist);
            ElectRole(RoleCC, c => c.CcScore(), bots, v => MainCC = v, () => MainCC);
            ElectRole(RolePuller, c => c.PullerScore(), bots, v => MainPuller = v, () => MainPuller);
            ElectHealers(bots);
        }

        private void ElectRole(string role, System.Func<RoleCapabilities, int> score,
            System.Collections.Generic.List<RoleCapabilities> bots,
            System.Action<GameLiving> assign, System.Func<GameLiving> current)
        {
            if (ManualRoles.Contains(role))
                return;
            var holder = current();
            if (holder is GamePlayer)
                return;
            RoleCapabilities best = null;
            int bestScore = int.MinValue;
            foreach (var c in bots)
            {
                int s = score(c);
                if (s > bestScore) { bestScore = s; best = c; }
            }
            if (best == null || best.Member == holder)
                return;
            var incumbent = bots.FirstOrDefault(c => c.Member == holder);
            int holderScore = incumbent != null ? score(incumbent) : int.MinValue;
            if (bestScore > holderScore)
                assign(best.Member);
        }

        /// <summary>Flag heal-capable bots as healers until the group has coverage
        /// (one per four members). Never touches manually /mheal-toggled bots,
        /// never unflags anyone: manual choice wins by staying put.</summary>
        private void ElectHealers(System.Collections.Generic.List<RoleCapabilities> bots)
        {
            int desired = System.Math.Max(1, bots.Count / 4);
            int flagged = bots.Count(c => c.IsHealer);
            if (flagged >= desired)
                return;
            foreach (var c in bots)
            {
                if (flagged >= desired)
                    break;
                if (!c.IsHealer && !c.HealerManual && c.CanHeal && c.Brain != null)
                {
                    c.Brain.IsHealer = true;
                    c.IsHealer = true;
                    flagged++;
                }
            }
        }

        #endregion

        public void SetPullPoint(Point2D point)
        {
            if (point != null)
                PullFromPoint = new Point2D(point);
            else
                PullFromPoint = null;
        }

        #region Healing

        /// <summary>Lock before accessing CheckGroupHealth() or related members</summary>
        public object HealLock = new();
        /// <summary>How injured is the group as a whole?</summary>
        public int AmountToHeal { get; private set; }
        /// <summary>How many group members are below emergency threshold</summary>
        public int NumNeedEmergencyHealing { get; private set; }
        /// <summary>How many group members are below healing threshold</summary>
        public int NumNeedHealing { get; private set; }
        /// <summary>How many group members are below max health</summary>
        public int NumInjured { get; private set; }
        /// <summary>Most injured group member</summary>
        public GameLiving MemberToHeal { get; private set; }
        /// <summary>Mezzed group member</summary>
        public GameLiving MemberToCureMezz { get; private set; }
        /// <summary>How many group members are diseased?</summary>
        public int NumNeedCureDisease { get; private set; }
        /// <summary>Most injured diseased group member</summary>
        public GameLiving MemberToCureDisease { get; private set; }
        /// <summary>How many group members are poisoned?</summary>
        public int NumNeedCurePoison { get; private set; }
        /// <summary>Most injured poisoned group member</summary>
        public GameLiving MemberToCurePoison { get; private set; }
        /// <summary>Is a group member already casting an instant heal spell?</summary>
        public bool AlreadyCastInstantHeal;
        /// <summary>Is a group member already casting a heal over time spell?  Set in MimicBrain.CheckHeals()</summary>
        public bool AlreadyCastingHoT;
        /// <summary>Is a group member already casting a health regen spell?</summary>
        public bool AlreadyCastingRegen;
        /// <summary>Is a group member already casting a cure mezz spell?</summary>
        public bool AlreadyCastingCureMezz;
        /// <summary>Is a group member already casting a cure disease spell?</summary>
        public bool AlreadyCastingCureDisease;
        /// <summary>Is a group member already casting a cure poison spell?</summary>
        public bool AlreadyCastingCurePoison;
        /// <summary>Is a group member already casting a resurrection spell? Set in CheckGroupHealth/ExecuteGoapRez.</summary>
        public bool AlreadyCastingRez;
        private int m_healthPercent;
        private int m_diseasePercent;
        private int m_poisonPercent;
        private int m_percentCurrent;

        static readonly public int HealThreshold = Properties.NPC_HEAL_THRESHOLD;
        static readonly public int EmergencyThreshold = HealThreshold / 2;

        private long nextCheckTime = 0;
        const long checkTimeOffset = 51; // Think() can be called slightly before interval

        /// <summary>Retrieve health and mezz/disease/poison status for the group</summary>
        /// <param name="checker">Healer checking group status</param>
        public void CheckGroupHealth(MimicNPC checker)
        {
            if (nextCheckTime < GameLoop.GameLoopTime)
            {
                nextCheckTime = GameLoop.GameLoopTime + checker.Brain.ThinkInterval - checkTimeOffset;

                AmountToHeal = 0;
                NumNeedEmergencyHealing = 0;
                NumNeedHealing = 0;
                NumInjured = 0;
                MemberToHeal = null;
                MemberToCureMezz = null;
                NumNeedCureDisease = 0;
                MemberToCureDisease = null;
                NumNeedCurePoison = 0;
                MemberToCurePoison = null;
                AlreadyCastInstantHeal = false;
                AlreadyCastingHoT = false;
                AlreadyCastingRegen = false;
                AlreadyCastingCureMezz = false;
                AlreadyCastingCureDisease = false;
                AlreadyCastingCurePoison = false;
                AlreadyCastingRez = false;

                m_healthPercent = 100;
                m_diseasePercent = 100;
                m_poisonPercent = 100;

                foreach (GameLiving groupMember in checker.Group.GetMembersInTheGroup())
                {
                    if (groupMember != checker && !groupMember.IsWithinRadius(checker, WorldMgr.VISIBILITY_DISTANCE))
                        // We can only reuse results if everybody is in the same region and reasonably close together
                        nextCheckTime = 0;
                    else
                    {
                        // PR16: corpses are rez jobs, not heal jobs. Dead members are
                        // counted by the rez scan (PR17), never by heal counters.
                        if (!groupMember.IsAlive)
                            continue;

                        m_percentCurrent = groupMember.HealthPercent;

                        if (m_percentCurrent < 100)
                        {
                            if (m_percentCurrent < EmergencyThreshold)
                                NumNeedEmergencyHealing++;
                            else if (m_percentCurrent < HealThreshold)
                                NumNeedHealing++;
                            else
                                NumInjured++;

                            AmountToHeal += groupMember.MaxHealth - groupMember.Health;
                        }

                        if (m_percentCurrent < m_healthPercent)
                        {
                            m_healthPercent = m_percentCurrent;
                            MemberToHeal = groupMember;
                        }

                        if (groupMember.IsMezzed && groupMember != null)
                            MemberToCureMezz = groupMember;

                        if (groupMember.IsDiseased)
                        {
                            NumNeedCureDisease++;
                            if (MemberToCureDisease == null || m_percentCurrent < m_diseasePercent)
                            {
                                MemberToCureDisease = groupMember;
                                m_diseasePercent = m_percentCurrent;
                            }
                        }

                        if (groupMember.IsPoisoned)
                        {
                            NumNeedCurePoison++;
                            if (MemberToCurePoison == null || m_percentCurrent < m_poisonPercent)
                            {
                                MemberToCurePoison = groupMember;
                                m_diseasePercent = m_poisonPercent;
                            }
                        }

                        if (groupMember.IsCasting)
                            switch (groupMember.CurrentSpellHandler.Spell.SpellType)
                            {
                                case eSpellType.HealOverTime: AlreadyCastingHoT = true; break;
                                case eSpellType.HealthRegenBuff: AlreadyCastingRegen = true; break;
                                case eSpellType.CureMezz: AlreadyCastingCureMezz = true; break;
                                case eSpellType.CureDisease: AlreadyCastingCureDisease = true; break;
                                case eSpellType.CurePoison: AlreadyCastingCurePoison = true; break;
                                case eSpellType.Resurrect: AlreadyCastingRez = true; break;
                            }
                    }
                }

                NumNeedHealing += NumNeedEmergencyHealing;
                NumInjured += NumNeedHealing;
            }
        }

        #endregion       
    }
}
