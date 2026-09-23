using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Scripts;
using NUnit.Framework;

namespace DOL.Tests.Unit
{
    /// <summary>Class-aware role defaults for bot groups. Scoring is pure logic on
    /// hand-built snapshots (no server singletons needed); only snapshot construction
    /// touches live game objects, and that path is covered by build + live play.</summary>
    [TestFixture]
    public class RoleElectionTests
    {
        private static MimicNPC Token() => (MimicNPC)RuntimeHelpers.GetUninitializedObject(typeof(MimicNPC));

        private static MimicBrain LooseBrain(bool healer = false, bool manual = false)
        {
            var brain = (MimicBrain)RuntimeHelpers.GetUninitializedObject(typeof(MimicBrain));
            brain.IsHealer = healer;
            brain.HealerManual = manual;
            return brain;
        }

        private static MimicGroup.RoleCapabilities Cap(MimicNPC member,
            bool healer = false, bool manual = false, bool canHeal = false,
            bool canCC = false, bool canNuke = false, bool guard = false, bool bow = false)
            => new MimicGroup.RoleCapabilities
            {
                Member = member,
                Brain = LooseBrain(healer, manual),
                IsHealer = healer,
                HealerManual = manual,
                CanHeal = canHeal,
                CanCC = canCC,
                CanNuke = canNuke,
                HasGuard = guard,
                HasBow = bow,
            };

        private static MimicGroup Elect(params MimicGroup.RoleCapabilities[] caps)
        {
            var group = new MimicGroup(caps[0].Member, null);
            group.ElectSnapshots(caps.ToList());
            return group;
        }

        [Test]
        public void SoloBotKeepsAllRoles()
        {
            var solo = Token();
            var group = Elect(Cap(solo));
            Assert.That(group.MainTank, Is.SameAs(solo));
            Assert.That(group.MainAssist, Is.SameAs(solo));
            Assert.That(group.MainCC, Is.SameAs(solo));
            Assert.That(group.MainPuller, Is.SameAs(solo));
        }

        [Test]
        public void HealerLeaderYieldsTankAndAssist()
        {
            var cleric = Token();
            var armsman = Token();
            var wizard = Token();
            var group = Elect(Cap(cleric, healer: true, canHeal: true), Cap(armsman), Cap(wizard, canNuke: true));
            Assert.That(group.MainTank, Is.SameAs(armsman), "Healer must never tank");
            Assert.That(group.MainAssist, Is.SameAs(wizard), "Nuker outranks melee for assist");
            Assert.That(group.MainCC, Is.SameAs(cleric), "Nobody mez-capable: incumbent stays");
            Assert.That(group.MainPuller, Is.SameAs(cleric), "Nobody bow-capable: incumbent stays");
        }

        [Test]
        public void GuardHolderTanksAndBowHolderPulls()
        {
            var leader = Token();
            var guard = Token();
            var archer = Token();
            var group = Elect(Cap(leader), Cap(guard, guard: true), Cap(archer, bow: true, canNuke: true));
            Assert.That(group.MainTank, Is.SameAs(guard));
            Assert.That(group.MainPuller, Is.SameAs(archer));
        }

        [Test]
        public void ManualLockWinsOverBetterCandidate()
        {
            var cleric = Token();
            var armsman = Token();
            var wizard = Token();
            var group = new MimicGroup(cleric, null);
            group.LockRole(MimicGroup.RoleAssist);
            group.LockRole(MimicGroup.RoleTank);
            group.ElectSnapshots(new List<MimicGroup.RoleCapabilities>
                { Cap(cleric, healer: true, canHeal: true), Cap(armsman), Cap(wizard, canNuke: true) });
            Assert.That(group.MainTank, Is.SameAs(cleric));
            Assert.That(group.MainAssist, Is.SameAs(cleric));
        }

        [Test]
        public void HealCapableBotGetsFlaggedUntilCovered()
        {
            var cleric = Token();
            var armsman = Token();
            var clericCap = Cap(cleric, canHeal: true);
            var armsCap = Cap(armsman);
            Elect(clericCap, armsCap);
            Assert.That(clericCap.Brain.IsHealer, Is.True, "Untouched heal-capable bot is flagged");
            Assert.That(armsCap.Brain.IsHealer, Is.False);
        }

        [Test]
        public void ManuallyUnflaggedHealerStaysOff()
        {
            var cleric = Token();
            var armsman = Token();
            var clericCap = Cap(cleric, canHeal: true, manual: true);
            Elect(clericCap, Cap(armsman));
            Assert.That(clericCap.Brain.IsHealer, Is.False, "Manual /mheal choice wins");
        }
    }
}
