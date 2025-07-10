using System;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    public class Pellinistra : GameNPC
    {
        private bool clonesSpawned = false;

        public override bool AddToWorld()
        {
            // Set NPC properties
            Model = 395;
            Name = "Pellinistra";
            Level = 105;
            Size = 100;
            MaxSpeedBase = 250;
            Realm = eRealm.None;

            // Load template if needed
            // INpcTemplate npcTemplate = NpcTemplateMgr.GetTemplate(XXXXX);
            // LoadTemplate(npcTemplate);

            Faction = FactionMgr.GetFactionByID(150);
            MaxHealth = 100000;

            // Assign custom AI
            SetOwnBrain(new PellinistraBrain(this));

            // Save to database
            return base.AddToWorld();
        }

        public void SpawnClones()
        {
            RemoveClones();

            // Clone 1: Gardien de Pellinistra
            var clone1 = new PellinistraClone("Gardien de Pellinistra");
            clone1.X = this.X - 250;
            clone1.Y = this.Y - 10;
            clone1.Z = this.Z;
            clone1.CurrentRegion = this.CurrentRegion;
            clone1.Heading = this.Heading;
            clone1.Level = 65;
            clone1.Size = 99;
            clone1.MaxSpeedBase = 250;
            clone1.Realm = 0;
            clone1.SetOwnBrain(new PellinistraCloneBrain(clone1));
            clone1.AddToWorld();

            // Clone 2: Guerrier de Pellinistra
            var clone2 = new PellinistraClone("Guerrier de Pellinistra");
            clone2.X = this.X + 250;
            clone2.Y = this.Y + 10;
            clone2.Z = this.Z;
            clone2.CurrentRegion = this.CurrentRegion;
            clone2.Heading = this.Heading;
            clone2.Level = 75;
            clone2.Size = 99;
            clone2.MaxSpeedBase = 250;
            clone2.Realm = 0;
            clone2.SetOwnBrain(new PellinistraCloneBrain(clone2));
            clone2.AddToWorld();

            // Clone 3: Aide de Camps de Pellinistra
            var clone3 = new PellinistraClone("Aide de Camps de Pellinistra");
            clone3.X = this.X + 100;
            clone3.Y = this.Y - 100;
            clone3.Z = this.Z;
            clone3.CurrentRegion = this.CurrentRegion;
            clone3.Heading = this.Heading;
            clone3.Level = 80;
            clone3.Size = 99;
            clone3.MaxSpeedBase = 250;
            clone3.Realm = 0;
            clone3.SetOwnBrain(new PellinistraCloneBrain(clone3));
            clone3.AddToWorld();
        }

        public void RemoveClones()
        {
            foreach (var npc in this.GetNPCsInRadius(5000))
            {
                if (npc != null && npc.Name.Contains("de Pellinistra"))
                {
                    npc.DeleteFromDatabase();
                    npc.Delete();
                }
            }
        }

        public override void Die(GameObject killer)
        {
            // Remove clones on death
            RemoveClones();

            // Reward players
            var player = killer as GamePlayer;
            if (player != null && IsWorthReward)
            {
                if (player.Group != null)
                {
                    foreach (var member in player.Group.GetPlayersInTheGroup())
                    {
                        member.GainRealmPoints(Level * 20);
                    }
                }
                else
                {
                    player.GainRealmPoints(Level * 20);
                }
            }

            base.Die(killer);
        }
    }

    // Clone class
    public class PellinistraClone : GameNPC
    {
        public PellinistraClone(string name)
        {
            Name = name;
            Size = 99;
            MaxHealth = 5000;
            Realm = eRealm.None;
        }

        public override bool AddToWorld()
        {
            // Additional setup if needed
            return base.AddToWorld();
        }

        public override void Die(GameObject killer)
        {
            // Optional: add special effects or messages
            base.Die(killer);
        }
    }

    // Main boss AI class
    public class PellinistraBrain : StandardMobBrain
    {
        private Pellinistra owner;
        private bool clonesSpawned = false;

        public PellinistraBrain(Pellinistra ownerNPC)
        {
            owner = ownerNPC;
            AggroLevel = 100;
            AggroRange = 600;
            ThinkInterval = 1500;
        }

        public override void Think()
        {
            if (owner == null || !owner.IsAlive)
                return;

            double healthPercent = owner.HealthPercent;

            // Spawn clones at 6-7% health if not stunned
            if (healthPercent >= 6 && healthPercent <= 7 && !owner.IsStunned && !clonesSpawned)
            {
                owner.SpawnClones();
                clonesSpawned = true;
            }

            // Remove clones when boss health is above 95% (respawn)
            if (healthPercent > 95)
            {
                owner.RemoveClones();
                clonesSpawned = false;
            }

            base.Think();
        }
    }

    // Clone AI class
    public class PellinistraCloneBrain : StandardMobBrain
    {
        public PellinistraCloneBrain(GameNPC owner) : base()
        {
            OwnerNPC = owner;
            AggroRange = 600;
            AggroLevel = 100;
            ThinkInterval = 1500;
        }

        public GameNPC OwnerNPC { get; private set; }

        public override void Think()
        {
            // Optional: add clone-specific behaviors
            base.Think();
        }
    }
}