//Author: BluRaven Date 1/27/2010
//Notes: The following things from the database are required: Akil's walking path,
//The spell for summoning salamanders that the priests use.  The cold DD spell the salamanders use.
//The NPC Template for the summoned salamanders.
//TODO: Give the salamanders a damage shield.

#region Using Statements

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using log4net;
using System.Reflection;
using DOL.GS.Atlantis;
using DOL.Database;
using DOL.Language;
using DOL.GS.Spells;

#endregion Using Statements

#region GameNPCs

namespace DOL.GS.Atlantis
{
    #region GameAkil

    public class GameAkil : TetheredEncounterMob
    {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		[ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            SpawnEncounter(Albion);
            SpawnAkil(Albion);
            SpawnEncounter(Midgard);
            SpawnAkil(Midgard);
            SpawnEncounter(Hibernia);
            SpawnAkil(Hibernia);
            Spell summonSpell = SkillBase.GetSpellByID(8200);
            Spell lifeTap = SkillBase.GetSpellByID(8202);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, summonSpell);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, lifeTap);
            log.Info("Spawning Artifact Encounter: Tartaros' Gift.");
        }

        public static int Albion = 73;
        public static int Midgard = 30;
        public static int Hibernia = 130;

        public static int[,] PortalPriestPosition = {
            // 0-X     1-Y    2-Z   3-H
            {471089, 574188, 11104, 723}, //0---Priest 1
            {470880, 573938, 11104, 381}, //1---Priest 2
            {470417, 573857, 11104, 3978}, //2---Priest 3
            {470145, 574060, 11104, 3601}, //3---Priest 4
            {469914, 574588, 11104, 2944}, //4---Priest 5
            {470183, 574959, 11104, 2448}, //5---Priest 6
            {470642, 575069, 11104, 1927}, //6---Priest 7
            {471036, 574819, 11104, 1421}, //7---Priest 8
        };

        public override bool Interact(GamePlayer player)
        {
            return false;
        }
        public override bool WhisperReceive(GameLiving source, string str)
        {
            return false;
        }
        public override void Die(GameObject killer)
        {
            DeSpawnEncounter(this);
            EncounterMgr.GrantEncounterCredit(killer, true, true, "Tartaros' Gift");
            base.Die(killer);
        }

        public static void SpawnAkil(int region)
        {
            //Create and spawn a High Priest Akil
            GameAkil Akil = new GameAkil();
            Akil.Model = 1047;
            Akil.Size = 75;
            Akil.Level = 73;
            Akil.Name = "High Priest Akil";
            Akil.CurrentRegionID = (ushort)region;
            Akil.Heading = 748;
            Akil.Realm = 0;
            Akil.CurrentSpeed = 0;
            Akil.MaxSpeedBase = 200;
            Akil.GuildName = "";
            Akil.X = 470530;
            Akil.Y = 574463;
            Akil.Z = 11117;
            Akil.RoamingRange = 0;
            Akil.RespawnInterval = 1200000;// Twenty Minutes
            Akil.TetherRange = 700;
            Akil.BodyType = 0;
            Akil.PathID = "TartarosEncounterPath";
            Akil.Spells.Add(SkillBase.GetSpellByID(8202));
            AkilBrain brain = new AkilBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 500;
            Akil.SetOwnBrain(brain);
			Akil.AutoSetStats();
            Akil.AddToWorld();
            return;
        }

        public static void SpawnEncounter(int region)
        {
            //Spawn Portal Priests and their fire effect mobs around the perimeter of the pad.
            for (int i = 0; i < 8; i++)
            {
                SpawnPriest(PortalPriestPosition[i, 0], PortalPriestPosition[i, 1], PortalPriestPosition[i, 2], PortalPriestPosition[i, 3], region);
                SpawnFireEffectMob(PortalPriestPosition[i, 0], PortalPriestPosition[i, 1], PortalPriestPosition[i, 2], PortalPriestPosition[i, 3], region);
            }
        }

        public static void SpawnPriest(int priestX, int priestY, int priestZ, int priestH, int region)
        {
            GamePriest priest = new GamePriest();
            priest.Model = 1688;
            priest.Size = 50;
            priest.Level = 65;
            priest.Name = "siam-he portal priest";
            priest.CurrentRegionID = (ushort)region;
            priest.Heading = (ushort)priestH;
            priest.Realm = 0;
            priest.CurrentSpeed = 0;
            priest.MaxSpeedBase = 0;
            priest.GuildName = "";
            priest.X = priestX;
            priest.Y = priestY;
            priest.Z = priestZ;
            priest.RoamingRange = 0;
            priest.RespawnInterval = 0;
            priest.BodyType = 0;
            PriestBrain brain = new PriestBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 1500;
            priest.SetOwnBrain(brain);
            priest.Flags |= GameNPC.eFlags.PEACE;
			priest.AutoSetStats();
            priest.AddToWorld();
            return;
        }

        public static void SpawnFireEffectMob(int fireX, int fireY, int fireZ, int fireH, int region)
        {
            GameNPC fire = new GameNPC();
            fire.Model = 1686;
            fire.Size = 50;
            fire.Level = 67;
            fire.Name = "flaming shield";
            fire.CurrentRegionID = (ushort)region;
            fire.Heading = (ushort)fireH;
            fire.Realm = 0;
            fire.CurrentSpeed = 0;
            fire.MaxSpeedBase = 0;
            fire.GuildName = "";
            fire.X = fireX;
            fire.Y = fireY;
            fire.Z = fireZ;
            fire.RoamingRange = 0;
            fire.RespawnInterval = 0;
            fire.BodyType = 0;
            fire.Flags ^= GameNPC.eFlags.PEACE;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 0;
            brain.AggroRange = 0;
            fire.SetOwnBrain(brain);
            fire.AddToWorld();
            return;
        }

        public static void DeSpawnEncounter(GameAkil akil)
        {
            foreach (GameNPC npc in akil.GetNPCsInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                if (npc.Name == "summoned salamander")
                {
                    npc.Health = 0;
                    npc.Delete();
                }
            }
        }

    }
    #endregion GameAkil

    #region GamePriest
    class GamePriest : BasicEncounterMob
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
    #endregion

	#region GameSalamander

	public class GameSalamander : GameNPC
	{
		public GameSalamander(INpcTemplate template)
			: base(template)
		{
		}

		public override long ExperienceValue
		{
			get
			{
				// no XP
				return 0;
			}
		}

		public override void DropLoot(GameObject killer)
		{
			// no loot
		}
	}


	#endregion GameSalamander
}

#endregion GameNPCs

#region Brains

namespace DOL.AI.Brain
{

    #region PriestBrain
    public class PriestBrain : StandardMobBrain
    {
        Spell summonSpell = SkillBase.GetSpellByID(8200);
        public PriestBrain()
            : base()
        {
        }

        public override void Think()
        {
           GamePriest body = Body as GamePriest;
           int playercount = 0;
           foreach (GamePlayer p in body.GetPlayersInRadius((ushort)AggroRange))
           {
               playercount++;
           }
           if (playercount > 0)
           {
               int sallycount = 0;
               foreach (GameNPC sally in body.GetNPCsInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
               {
                   if (sally.Name == "summoned salamander")
                   {
                       sallycount++;
                   }
               }
               if (sallycount < 60)
               {
                    body.CastSpell(summonSpell, m_mobSpellLine, false);
               }
           }
            base.Think();
        }

    }
    #endregion PriestBrain

    #region AkilBrain

    public class AkilBrain : StandardMobBrain
    {
        public AkilBrain()
            : base()
        {
            AggroLevel = 100;
            AggroRange = 0;
            ThinkInterval = 3000;

        }
        public override void Think()
        {
            GameAkil body = Body as GameAkil;
            if (!body.IsMovingOnPath && !body.InCombat && !body.AttackState && !body.IsReturningToSpawnPoint && body.IsAlive) 
            { 
                body.MoveOnPath(50); 
            }

            base.Think();
        }
    }

    #endregion AkilBrain

    #region SallyBrain

    public class SallyBrain : StandardMobBrain
    {
        public SallyBrain()
            : base()
        {
        }

        public override int ThinkInterval
        {
            get
            {
                //slow down thinking to reduce stress on the server.  There can be as many as 40 salamanders active at once.
                return 3000;
            }

        }

    }

    #endregion SallyBrain
}

#endregion Brains

#region SpellHandlers

namespace DOL.GS.Spells
{
	/// <summary>
	/// NPC summon spell handler
	/// 
	/// Spell.LifeDrainReturn is used for npc Template ID.
	///
	/// Spell.Value is used for hard level cap of NPC
	/// Spell.Damage is used to set npc level:
	/// less than zero is considered as a percent (0 .. 100+) of target level;
	/// higher than zero is considered as level value.
	/// Resulting value is limited by the Byte field type.
	/// </summary>
    [SpellHandlerAttribute("SummonSalamander")]
	public class SummonSalamander : SpellHandler
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		protected GameNPC sally = null;

		public SummonSalamander(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

		public override void FinishSpellCast(GameLiving target)
		{
			foreach (GamePlayer player in m_caster.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
			{
				if (player != m_caster)
					player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameObject.Casting.CastsASpell", m_caster.GetName(0, true)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
			}

			base.FinishSpellCast(target);

		}

		protected virtual void GetNPCLocation(out int x, out int y, out int z, out ushort heading, out Region region)
		{
			Point2D point = Caster.GetPointFromHeading( Caster.Heading, 64 );
            x = 470535;
            y = 574469;
            z = 11104;
			heading = Caster.Heading;
			region = Caster.CurrentRegion;
		}

		protected virtual GameSalamander CreateSalamander(INpcTemplate template)
		{
			return new GameSalamander(template);
		}

		protected virtual byte GetNPCLevel()
		{
			byte level = (byte)Spell.Damage;
			return Math.Max((byte)1, level);
		}

		protected virtual void AddHandlers()
		{
		}


        public override void ApplyEffectOnTarget(GameLiving target)
		{
			INpcTemplate template = NpcTemplateMgr.GetTemplate(Spell.LifeDrainReturn);
			if (template == null)
			{
				if (log.IsWarnEnabled)
					log.WarnFormat("NPC template {0} not found! Spell: {1}", Spell.LifeDrainReturn, Spell.ToString());
				MessageToCaster("NPC template " + Spell.LifeDrainReturn + " not found!", eChatType.CT_System);
				return;
			}

			GameSpellEffect effect = CreateSpellEffect(target, CasterEffectiveness);

			SallyBrain brain = new SallyBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 900;
			sally = CreateSalamander(template);
			//brain.WalkState = eWalkState.Stay;
            sally.SetOwnBrain(brain); // as AI.ABrain
			int x, y, z;
			ushort heading;
			Region region;
			GetNPCLocation(out x, out y, out z, out heading, out region);
			sally.X = x;
			sally.Y = y;
			sally.Z = z;
			sally.Heading = heading;
			sally.CurrentRegion = region;
			sally.CurrentSpeed = 0;
			sally.Realm = Caster.Realm;
			sally.Level = GetNPCLevel();
            sally.RoamingRange = 1000;
			sally.AutoSetStats();
			sally.AddToWorld();
			AddHandlers();
			effect.Start(sally);
		}

		public override int CalculateSpellResistChance(GameLiving target)
		{
			return 0;
		}

		public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
		{
			RemoveHandlers();
			effect.Owner.Health = 0;
			effect.Owner.Delete();
			return 0;
		}

		protected virtual void RemoveHandlers()
		{
			GameEventMgr.RemoveAllHandlersForObject(sally);
		}

		protected virtual void OnNpcReleaseCommand(DOLEvent e, object sender, EventArgs arguments)
		{
		}

    }

}

#endregion SpellHandlers
