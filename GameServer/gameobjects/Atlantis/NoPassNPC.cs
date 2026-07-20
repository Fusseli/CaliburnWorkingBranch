using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;
using log4net;
using System.Reflection;
using DOL.Database;
using DOL.AI.Brain;
using DOL.GS.Movement;

namespace DOL.GS
{
    public class NoPass : GameNPC
    {
        public int RadiusNoPass = 400;

        public override bool AddToWorld()
        {
            this.SetOwnBrain(new NoPassBrain());
            Brain.Start();
            this.Model = 665;
            base.AddToWorld();
            return true;
        }
    }
    public class NoPassGuild : GameNPC
    {
        public string GuildNoPass = "";
        public int RadiusNoPass = 400;

        public override bool AddToWorld()
        {
            this.SetOwnBrain(new AntipassGuidBrain());
            Brain.Start();
            this.Model = 665;
            base.AddToWorld();
            return true;
        }
        public override void SaveIntoDatabase()
        {
        }
    }
    public class MountPass : GameNPC
    {
        public override bool AddToWorld()
        {
            this.SetOwnBrain(new MountPassBrain());
            Brain.Start();
            base.AddToWorld();
            return true;
        }
    }
    public class MyMount : GameNPC
    {
        public MyMount()
        {
            BlankBrain brain = new BlankBrain();
            SetOwnBrain(brain);
        }
        public MyMount(INpcTemplate templateid)
            : base(templateid)
        {
            BlankBrain brain = new BlankBrain();
            SetOwnBrain(brain);
        }

        public override int MAX_PASSENGERS
        {
            get
            {
                return 1;
            }
        }

        public override int SLOT_OFFSET
        {
            get
            {
                return 0;
            }
        }
    }
}

namespace DOL.AI.Brain
{
    public class NoPassBrain : StandardMobBrain
    {
        public NoPassBrain()
            : base()
        {
            ThinkInterval = 50;
            AggroLevel = 100;
            AggroRange = 400;
        }

        public override void Think()
        {
            NoPass BodyOfBrain = (NoPass)Body;
            int RadiusCheck = BodyOfBrain.RadiusNoPass;
            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)RadiusCheck))
            {
                if (player.Client.Account.PrivLevel < 5)
                {
                    double angle = 0.00153248422;
                    player.MoveTo(player.CurrentRegionID, (int)(Body.X - ((AggroRange + 10) * Math.Sin(angle * Body.Heading))), (int)(Body.Y + ((AggroRange + 10) * Math.Cos(angle * Body.Heading))), Body.Z, player.Heading);
                }
            }
        }
    }
    public class AntipassGuidBrain : StandardMobBrain
    {
        public AntipassGuidBrain()
            : base()
        {
            ThinkInterval = 50;
            AggroLevel = 100;
            AggroRange = 400;
        }

        public override void Think()
        {
            NoPassGuild BodyOfBrain = (NoPassGuild)Body;
            int RadiusCheck = BodyOfBrain.RadiusNoPass;
            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)RadiusCheck))
            {
                if ((player.Client.Account.PrivLevel < 5) && (player.GuildName == BodyOfBrain.GuildNoPass))
                {
                    double angle = 0.00153248422;
                    player.MoveTo(player.CurrentRegionID, (int)(Body.X - ((AggroRange + 10) * Math.Sin(angle * Body.Heading))), (int)(Body.Y + ((AggroRange + 10) * Math.Cos(angle * Body.Heading))), Body.Z, player.Heading);
                }
            }
        }
    }
    public class MountPassBrain : StandardMobBrain
    {
        public MountPassBrain()
            : base()
        {
            ThinkInterval = 100;
            AggroLevel = 100;
            AggroRange = 400;
        }

        public override void Think()
        {
            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)AggroRange))
            {
                if (player.IsOnHorse)
                    return;
                if (player.TempProperties.GetProperty<bool>("RIDING_MOUNT_PASS") == true)
                {
                    return;
                }

                PathPoint path = MovementMgr.LoadPath(this.Body.GuildName);
                if (path != null)
                {
                    MyMount mount = new MyMount();
                    mount.Size = 60;
                    mount.Realm = player.Realm;
                    mount.X = path.X;
                    mount.Y = path.Y;
                    mount.Z = path.Z;
                    mount.CurrentRegion = this.Body.CurrentRegion;
                    mount.Heading = path.GetHeading(path.Next);
                    mount.AddToWorld();
                    mount.CurrentWaypoint = path;
                    mount.MaxSpeedBase = this.Body.MaxSpeedBase;
                    mount.Flags = this.Body.Flags;
                    mount.Model = this.Body.Model;
                    GameEventMgr.AddHandler(mount, GameNPCEvent.PathMoveEnds, new DOLEventHandler(OnHorseAtPathEnd));
                    new MountHorseAction(player, mount).Start(400);
                    new HorseRideAction(mount).Start(4000);
                }
            }
        }

        public void OnHorseAtPathEnd(DOLEvent e, object o, EventArgs args)
        {
            if (!(o is GameNPC)) return;
            GameNPC npc = (GameNPC)o;

            GameEventMgr.RemoveHandler(npc, GameNPCEvent.PathMoveEnds, new DOLEventHandler(OnHorseAtPathEnd));
            npc.StopMoving();
            npc.RemoveFromWorld();
        }

        protected class MountHorseAction : ECSGameTimerWrapperBase
        {
            protected readonly GameNPC m_horse;

            public MountHorseAction(GamePlayer actionSource, GameNPC horse)
                : base(actionSource)
            {
                if (horse == null)
                    throw new ArgumentNullException("horse");
                m_horse = horse;
            }

            protected override int OnTick(ECSGameTimer timer)
            {
                GamePlayer player = (GamePlayer)Owner;
                if (player.IsOnHorse)
                    return 0;
                player.MountSteed(m_horse, true);
                player.TempProperties.SetProperty("RIDING_MOUNT_PASS", true);
                return 0;
            }
        }

        protected class HorseRideAction : ECSGameTimerWrapperBase
        {
            public HorseRideAction(GameNPC actionSource)
                : base(actionSource)
            {
            }

            protected override int OnTick(ECSGameTimer timer)
            {
                GameNPC horse = (GameNPC)Owner;
                horse.MoveOnPath(horse.MaxSpeed);
                return 0;
            }
        }
    }
}
