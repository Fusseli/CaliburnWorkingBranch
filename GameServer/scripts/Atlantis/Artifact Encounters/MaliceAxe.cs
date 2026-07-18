//Author: BluRaven Date 2/14/2010 - Happy Valentines Day!
//Notes: The following things from the database are required: Malamis' DOT & Stun spells.
//Adjustable settings: see variables respawnmins, DeSpawnMins, and tokengoal below.

using System;
using System.Collections.Generic;
using System.Text;
using DOL.GS;
using log4net;
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.GS.Atlantis;
using DOL.Events;
using DOL.Database;
using System.Collections;
using DOL.AI.Brain;
using DOL.GS.Spells;

namespace DOL.GS.Atlantis
{
    public class BoundMaliceAxe : GameStaticItem
    {
        public static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static int Albion = 73;
        public static int Midgard = 30;
        public static int Hibernia = 130;
        public static int respawnmins = 20;
        public static int tokengoal = 100;
        public static int DeSpawnMins = 45;
        public long lastCompletedTime = 0;
        public bool encounterStarted = false;

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("Spawning Artifact Encounter: Malice Axe.");
            Spell DOT = SkillBase.GetSpellByID(8203);
            Spell Stun = SkillBase.GetSpellByID(8204);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, DOT);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, Stun);
            SpawnAxe(Albion);
            SpawnAxe(Midgard);
            SpawnAxe(Hibernia);
        }

        public override void SaveIntoDatabase()
        { }

        public override bool Interact(GamePlayer interacter)
        {
            if (!base.Interact(interacter))
                return false;
            if (!interacter.IsWithinRadius(this, WorldMgr.INTERACT_DISTANCE))
                return false;
            if (encounterStarted)
            {
                //encounter is in progress, give the player a token if he dosn't already have one.
                int MaliceTokenCount = interacter.TempProperties.GetProperty<int>("MaliceTokenCount");
                if (MaliceTokenCount >= 1)
                {
                    interacter.Out.SendMessage("The token is too powerful to carry more than one at a time.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    return false;
                }
                else
                {
                    DbItemTemplate tokentemplate = new DbItemTemplate();
                    tokentemplate.Model = 104;
                    tokentemplate.Name = "Malice Token";
                    tokentemplate.Id_nb = "malice_token";
                    tokentemplate.Weight = 20;
                    tokentemplate.Object_Type = 0;
                    tokentemplate.IsDropable = false;
                    tokentemplate.IsPickable = false;
                    tokentemplate.IsTradable = false;
                    tokentemplate.AllowAdd = false;
                    DbInventoryItem notSaved = new DbInventoryItem();
					notSaved.AllowAdd = false;
					notSaved.AllowDelete = false;
                    notSaved.Template = tokentemplate;
                    if (!interacter.Inventory.AddItem(interacter.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack), notSaved))
                    {
                        interacter.Out.SendMessage("You do not have enough inventory space to receive a token!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        interacter.TempProperties.SetProperty("MaliceTokenCount", 1);
                        interacter.Out.SendMessage("You have received one Malice Token!  This token belongs in a Malice chest.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    }
                }
                return true;
            }
            long timesince = interacter.CurrentRegion.Time - lastCompletedTime;
            long timer = ((60 * 1000) * respawnmins);
            if (lastCompletedTime > 0 && timesince < timer)
            {
                long timeleft = timer - timesince;
                if (timeleft < 1000 * 60)
                {
                    
                    interacter.Out.SendMessage("The Malice Axe encounter will be available again in " + (timeleft / 1000) + " seconds.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    return false;
                }
                else
                {
                    interacter.Out.SendMessage("The Malice Axe encounter will be available again in " + ((timeleft / (1000 * 60)) + 1) + " minutes.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    return false;
                }
            }
            if (!encounterStarted)
            {
                encounterStarted = true;
                SpawnEncounter(interacter, this);
                return true;
            }
            return true;
        }

        private static void SpawnAxe (int Region)
        {
            BoundMaliceAxe axe = new BoundMaliceAxe();
            axe.Model = 2109;
            axe.Name = "Bound Malice Axe";
            axe.X = 504830;
            axe.Y = 546820;
            axe.Z = 10698;
            axe.Heading = 1774;
            axe.CurrentRegionID = (ushort)Region;
            axe.AddToWorld();
            return;
        }

        private static void SpawnEncounter(GamePlayer player, BoundMaliceAxe axe)
        {
            //Spawn a Malamis and tell him to hold the player that he should aggro, and the axe that he came from.
            GameMalamis malamis = new GameMalamis();
            malamis.Firstaggroplayer = player;
            malamis.Axe = axe;
            malamis.Name = "Malamis";
            malamis.Flags |= GameNPC.eFlags.GHOST;
            malamis.Model = 1190;
            malamis.Size = 100;
            malamis.Level = 65;
            malamis.RoamingRange = 0;
            malamis.X = 504695;
            malamis.Y = 546682;
            malamis.Z = 10616;
            malamis.Heading = 1536;
            malamis.TetherRange = 500;
            malamis.CurrentRegionID = axe.CurrentRegionID;
            malamis.Strength = 275;
            malamis.Constitution = 520;
            malamis.Spells.Add(SkillBase.GetSpellByID(8203));
            MalamisBrain mbrain = new MalamisBrain();
            mbrain.AggroLevel = 100;
            mbrain.AggroRange = 600;
            malamis.SetOwnBrain(mbrain);
            malamis.AddToWorld();
        }

    }

    public class GameMalamis : TetheredEncounterMob
    {
        public GamePlayer Firstaggroplayer;
        public BoundMaliceAxe Axe;
        public DiscipleChest DChest;
        public WeakeningChest WChest;

        public static int[,] EffectPosition = {
            //  0       1      2
            {505511, 546801, 10616},
            {504876, 546207, 10616},
            {504856, 547394, 10629},
            {504245, 546781, 10616},
        };
        public static int[,] DisciplePosition = {
            //  0       1      2      3
            {504463, 548130, 10616, 2377},
            {505290, 548193, 10616, 1865},
            {504171, 546082, 10616, 3538},
            {505851, 547820, 10616, 1456},
            {506227, 547147, 10616, 1160},
            {503999, 546804, 10616, 3026},
            {506105, 546192, 10616, 534},
            {504814, 545888, 10616, 3993},
        };

        public override void StartRespawn()
        {
        }
        public override void Die(GameObject killer)
        {
            EncounterMgr.GrantEncounterCredit(killer, true, true, "Malice's Axe");
            Axe.lastCompletedTime = killer.CurrentRegion.Time;
            DespawnEncounter();
            base.Die(killer);
        }

        public void DespawnEncounter()
        {
            Axe.encounterStarted = false;
            //remove unused tokens from players inventories
            foreach (GamePlayer p in this.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                int MaliceTokenCount = p.TempProperties.GetProperty<int>("MaliceTokenCount");
                if (MaliceTokenCount >= 1)
                {
                    lock (p.Inventory)
                    {
                        DbInventoryItem item = p.Inventory.GetFirstItemByID("malice_token", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                        while (item != null)
                        {
                            p.Inventory.RemoveItem(item);
                            item = p.Inventory.GetFirstItemByID("malice_token", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                        }
                    }
                    p.TempProperties.SetProperty("MaliceTokenCount", 0);
                }
            }
            //clear and remove the chests
            if (DChest != null)
            {
                DChest.Tokens = 0;
                DChest.Delete();
                DChest.RemoveFromWorld();
                DChest = null;
            }
            if (WChest != null)
            {
                WChest.Tokens = 0;
                WChest.Delete();
                WChest.RemoveFromWorld();
                WChest = null;
            }
            //clear and remove the encounter mobs
            foreach (GameNPC npc in this.GetNPCsInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                if (npc.Name == "ring for Malamis region")
                {
                    npc.Brain.Stop();
                    npc.RemoveFromWorld();
                    npc.Delete();
                }
                if (npc.Name == "Disciple of Malamis")
                {
                    npc.Brain.Stop();
                    npc.Health = 0;
                    npc.Delete();
                }
                if (npc.Name == "Malamis")
                {
                    npc.Brain.Stop();
                    npc.Health = 0;
                    npc.Delete();
                }
            }
        }

        public override bool AddToWorld()
        {
            DiscipleChest dchest = new DiscipleChest();
            DChest = dchest;
            dchest.malamis = this;
            dchest.Name = "Malice Chest of the Disciples";
            dchest.Model = 1596;
            dchest.X = 504341;
            dchest.Y = 546002;
            dchest.Z = 10616;
            dchest.Heading = 1501;
            dchest.CurrentRegionID = this.CurrentRegionID;
            dchest.AddToWorld();

            WeakeningChest wchest = new WeakeningChest();
            WChest = wchest;
            wchest.malamis = this;
            wchest.Name = "Malice Chest of Weakening";
            wchest.Model = 1596;
            wchest.X = 504076;
            wchest.Y = 546307;
            wchest.Z = 10616;
            wchest.Heading = 1467;
            wchest.CurrentRegionID = this.CurrentRegionID;
            wchest.AddToWorld();

            for (int i = 0; i < 4; i++)
            {
                SpawnEffectMob(EffectPosition[i, 0], EffectPosition[i, 1], EffectPosition[i, 2], this.CurrentRegionID);
            }
            for (int i = 0; i < 8; i++)
            {
                SpawnDiscipleMob(DisciplePosition[i, 0], DisciplePosition[i, 1], DisciplePosition[i, 2], DisciplePosition[i, 3], this.CurrentRegionID);
            }
            if (!base.AddToWorld())
                return false;
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(DeactivateEncounter), BoundMaliceAxe.DeSpawnMins * 1000 * 60);
            return true;
        }

        //What happens when the encounter timer runs out.
        public int DeactivateEncounter(ECSGameTimer timer)
        {
            DespawnEncounter();
            return 0;
        }

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            int chestcount = 0;
            if (DChest != null) { chestcount++; }
            if (WChest != null) { chestcount++; }
            if (chestcount > 1 && this.HealthPercent <= 55)
            {
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    player.Out.SendSpellEffectAnimation(this, this, (ushort)2065, 0, false, 1);
                }
                this.ChangeHealth(this, eHealthChangeType.Spell, 150);
                return;
            }
            if (chestcount > 0 && this.HealthPercent <= 30)
            {
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    player.Out.SendSpellEffectAnimation(this, this, (ushort)2067, 0, false, 1);
                }
                this.ChangeHealth(this, eHealthChangeType.Spell, 275);
                return;
            }
                base.TakeDamage(source, damageType, damageAmount, criticalAmount);
        }
        public void SpawnEffectMob (int X, int Y, int Z, ushort region)
        {
            MaliceEffectMob effectmob = new MaliceEffectMob();
            effectmob.Name = "ring for Malamis region";
            effectmob.Model = 1;
            effectmob.Size = 25;
            effectmob.Level = 75;
            effectmob.RoamingRange = 0;
            effectmob.X = X;
            effectmob.Y = Y;
            effectmob.Z = Z;
            effectmob.Heading = (ushort)1183;
            effectmob.CurrentSpeed = 0;
            effectmob.MaxSpeedBase = 0;
            effectmob.CurrentRegionID = region;
            effectmob.Flags ^= GameNPC.eFlags.CANTTARGET;
            effectmob.Flags ^= GameNPC.eFlags.DONTSHOWNAME;
            effectmob.Flags |= GameNPC.eFlags.PEACE;
            MaliceEffectBrain brain = new MaliceEffectBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 1;
            effectmob.SetOwnBrain(brain);
            effectmob.AddToWorld();
        }
        public void SpawnDiscipleMob(int X, int Y, int Z, int H, ushort region)
        {
            MaliceDiscipleMob disciplemob = new MaliceDiscipleMob();
            disciplemob.Name = "Disciple of Malamis";
            disciplemob.Model = 1190;
            disciplemob.Size = (byte)Util.Random(35, 50);
            disciplemob.Level = (byte)Util.Random(45, 49);
            disciplemob.RoamingRange = 0;
            disciplemob.X = X;
            disciplemob.Y = Y;
            disciplemob.Z = Z;
            disciplemob.Heading = (ushort)H;
            disciplemob.CurrentSpeed = 0;
            disciplemob.MaxSpeedBase = 0;
            disciplemob.Flags |= GameNPC.eFlags.PEACE;
            disciplemob.CurrentRegionID = region;
            MaliceDiscipleBrain dbrain = new MaliceDiscipleBrain();
            dbrain.AggroLevel = 100;
            dbrain.AggroRange = 1500;
            disciplemob.SetOwnBrain(dbrain);
            disciplemob.Spells.Add(SkillBase.GetSpellByID(8204));
            disciplemob.AddToWorld();
        }
    }

    public class MaliceDiscipleMob : GameNPC
    {
        public override void StartRespawn()
        {
        }
        public override void SaveIntoDatabase()
        {
        }
        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
        }

    }

    public class MaliceDiscipleBrain : StandardMobBrain
    {
        public MaliceDiscipleBrain()
            : base()
        {
        }

        public override void Think()
        {
            if (Util.Random(5) == 5)
            {
                MaliceDiscipleMob body = Body as MaliceDiscipleMob;
                foreach (GamePlayer player in body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    player.Out.SendSpellEffectAnimation(body, body, (ushort)6120, 0, false, 1);
                }
            }
            //randomize targets a little bit.
            if (Util.Random(20) == 13)
            {
                Body.StopAttack();
                Body.StopCurrentSpellcast();
                ClearAggroList();
                CheckPlayerAggro();
                CheckNpcAggro();
                AttackMostWanted(); 
            }
            //stometimes stop attacking.
            if (Util.Random(10) == 9)
            {
                Body.StopAttack();
                Body.StopCurrentSpellcast();
                ClearAggroList();
            }

        }

    }

    public class MalamisBrain : StandardMobBrain
    {
        public MalamisBrain()
            : base()
        {
        }

        public override void Think()
        {
            GameMalamis body = Body as GameMalamis;
            if (body == null) { return; }

            //Sometimes teleport the players back to the axe if they are carrying a token.
                foreach (GamePlayer player in body.GetPlayersInRadius(1500))
                {
                    int pTokenCount = player.TempProperties.GetProperty<int>("MaliceTokenCount");
                    if (pTokenCount > 0)
                    {
                        if (Util.Random(8) == 7)
                        {
                            if (!player.IsStunned && player.IsAlive)
                            player.MoveTo(body.CurrentRegionID, 504881, 546724, 10626, 1147);
                        }
                    }
                }

                //This should increase the frequency of how often he casts his DOT.
                if (body.AttackState && Util.Random(20) == 13)
                {
                        Body.StopAttack();
                        Body.StopCurrentSpellcast();
                        CheckSpells(eCheckSpellType.Offensive);
                }
            
            base.Think();
        }

    }

    public class MaliceEffectMob : GameNPC
    {
        public override void StartRespawn()
        {
        }
        public override void SaveIntoDatabase()
        {
        }
    }
    public class MaliceEffectBrain : StandardMobBrain
    {
        public MaliceEffectBrain()
            : base()
        {
        }

        public override void Think()
        {
            MaliceEffectMob body = Body as MaliceEffectMob;
            foreach (GamePlayer player in body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSpellEffectAnimation(body, body, (ushort)9118, 0, false, 1);
            }
            base.Think();
        }

    }
   public class WeakeningChest : DiscipleChest
    {
    }

    public class DiscipleChest : GameStaticItem
    {
        public int Tokens = 0;
        private int TokensLeft = BoundMaliceAxe.tokengoal;
        public GameMalamis malamis;
        public override bool Interact(GamePlayer interacter)
        {
            if (!base.Interact(interacter))
                return false;
            if (!interacter.IsWithinRadius(this, WorldMgr.INTERACT_DISTANCE))
                return false;

            bool success = false;
            			if (interacter == null) return false;
                        lock (interacter.Inventory)
                        {
                            DbInventoryItem item = interacter.Inventory.GetFirstItemByID("malice_token", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                            while (item != null)
                            {
                                success = (interacter.Inventory.RemoveItem(item));
                                item = interacter.Inventory.GetFirstItemByID("malice_token", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                            }
                        }
                if (success)
                {
                    interacter.TempProperties.SetProperty("MaliceTokenCount", 0);
                    interacter.Out.SendMessage("You deposit one token!", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    Tokens++;
                    if (Tokens == TokensLeft)
                    {
                        DespawnChest(interacter);
                        return true;
                    }
                    interacter.Out.SendMessage("The " + this.Name + " requires " + (TokensLeft - Tokens) + " more tokens.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    return true;
                }
                return false;
            
        }

        private void DespawnChest(GamePlayer interacter)
        {
            EncounterMgr.BroadcastMsg(this, "The " + this.Name + " is filled to the brim with tokens and it dissappears!", WorldMgr.YELL_DISTANCE, eChatType.CT_Important, false);
            interacter.Out.SendMessage("The " + this.Name + " is filled and it dissappears!", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
            if (this.Name == "Malice Chest of the Disciples") 
            {
                foreach (GameNPC npc in this.GetNPCsInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (npc.Name == "Disciple of Malamis")
                    {
                        npc.Brain.Stop();
                        npc.Health = 0;
                        npc.Delete();
                    }
                }            
                malamis.DChest = null; 
            }
            if (this.Name == "Malice Chest of Weakening") { malamis.WChest = null; }
            this.Delete();
            this.RemoveFromWorld();
        }
    }
}
