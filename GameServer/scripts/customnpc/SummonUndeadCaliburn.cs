/*
 * Original script by ThatRickGuy 2004
 * Reworked by rdsandersjr 2012
 * tweaked by Bones to work with current svn 10/16/2012
 * SVN Revision 3129
 * Useage: ingame type /undead
 * TODO: tweak the marching orders, formations, add player following capabilities
*/

using System;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&undead", //command to handle
        ePrivLevel.GM, //minimum privelege level
        "summons undead", //command description
        "/undead - To summon the undead summoner")]

#region UndeadHandeler
    public class UndeadCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        const int cROWS = 4;
        const int cCOLS = 16;
        const int cMOBS = 64;
        public GameNPC[] mob;
        public UndeadCommanderNPC commanderMob;

        // This is the summoner that /undead will summon
        public class UndeadSummonerNPC : GameNPC
        {
            public UndeadCommandHandler UndeadCH;
            public UndeadSummonerNPC()
                : base()
            {
                //First, we set the position of this
                //npc in the constructor. You can set
                //the npc position, model etc. in the
                //StartEvent() method too if you want.
                Heading = 0x0;
                Name = "Ostego";
                GuildName = "Undead Horde";                
                Model = 107;
                Size = 50;
                Level = 10;
                Realm = eRealm.None;
                CurrentRegionID = CurrentRegionID;
            }
    #endregion UndeadHandeler

            //This function is the callback function that is called when
            //a player right clicks on the npc
#region Interact
            public override bool Interact(GamePlayer player)
            {
                if (!base.Interact(player)) return false;

                //Now we turn the npc into the direction of the person it is
                //speaking to.
                TurnTo(player.X, player.Y);

                //We send a message to player and make it appear in a popup
                //window. Text inside the [brackets] is clickable in popup
                //windows and will generate a &amp;whis text command!
                player.Out.SendMessage(
                    "Hello " + player.Name + " shale I [summon] the Horde, or [banish] them back to hell?",
                    eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return true;
            }
            //This function is the callback function that is called when
            //someone whispers something to this mob!
            public override bool WhisperReceive(GameLiving source, string str)
            {
                if (!base.WhisperReceive(source, str)) return false;

                //If the source is no player, we return false
                if (!(source is GamePlayer)) return false;

                //We cast our source to a GamePlayer object
                GamePlayer t = (GamePlayer)source;

                //Now we turn the npc into the direction of the person it is
                //speaking to.
                TurnTo(t.X, t.Y);

                //We test what the player whispered to the npc and
                //send a reply. The Method SendReply used here is
                //defined later in this class ... read on
                switch (str)
                {
                    case "summon":
                        SendReply(t, "Very well, Behold, the Undead Horde!");
                        UndeadCH.SummonHorde(this);
                        break;
                    case "banish":
                        SendReply(t, "Their purpose fullfilled, I shall return them to the grave!");
                        UndeadCH.BannishHorde();
                        break;
                    default: break;
                }
                return true;
            }

            //This function sends some text to a player and makes it appear
            //in a popup window. We just define it here so we can use it in
            //the WhisperToMe function instead of writing the long text
            //everytime we want to send some reply!
            private void SendReply(GamePlayer target, string msg)
            {
                target.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
        } // end undead summoner
#endregion Interact

        // This is the commander that Ostego will summon to order the troops
        public class UndeadCommanderNPC : GameNPC
        {
            public UndeadCommandHandler UndeadCH;
            public UndeadCommanderNPC()
                : base()
            {
                //First, we set the position of this
                //npc in the constructor. You can set
                //the npc position, model etc. in the
                //StartEvent() method too if you want.
                Heading = 0x0;
                Name = "Undead Commander";
                GuildName = "Undead Horde";
                Model = 106;
                Size = 60;
                Level = 10;
                Realm = eRealm.None;
                CurrentRegionID = 1;
            }

            //This function is the callback function that is called when
            //a player right clicks on the npc
            public override bool Interact(GamePlayer player)
            {
                if (!base.Interact(player)) return false;

                //Now we turn the npc into the direction of the person it is
                //speaking to.
                //TurnTo(player.X,player.Y);

                //We send a message to player and make it appear in a popup
                //window. Text inside the [brackets] is clickable in popup
                //windows and will generate a &amp;whis text command!
                player.Out.SendMessage(
                    "Undead Commander Reporting, " + player.Name + ". What is your command? [Prepare to move] [move out] [halt] [halt charge] [formation] [charge] [align]",
                    eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return true;
            }

            //This function is the callback function that is called when
            //someone whispers something to this mob!
            public override bool WhisperReceive(GameLiving source, string str)
            {
                if (!base.WhisperReceive(source, str)) return false;

                //If the source is no player, we return false
                if (!(source is GamePlayer)) return false;

                //We cast our source to a GamePlayer object
                GamePlayer t = (GamePlayer)source;

                //We test what the player whispered to the npc and
                //send a reply. The Method SendReply used here is
                //defined later in this class ... read on
                switch (str)
                {
                    case "Prepare to move":
                        SendReply(t, "Aye Sir! We will make ready!");
                        UndeadCH.MobRightFace();
                        break;
                    case "move out":
                        SendReply(t, "Aye Sir! We March!!");
                        UndeadCH.MobForwardMarch();
                        break;
                    case "halt":
                        SendReply(t, "Aye Sir! We Hold!");
                        UndeadCH.MobHalt();
                        break;
                    case "halt charge":
                        SendReply(t, "Aye Sir! We Hold!");
                        UndeadCH.MobHaltCharge();
                        break;
                    case "formation":
                        SendReply(t, "Aye Sir!");
                        UndeadCH.MobFallIn();
                        break;
                    case "charge":
                        SendReply(t, "Aye Sir! None shall be left standing!");
                        // perform charge animation
                        UndeadCH.MobCharge();
                        break;
                    case "align":
                        SendReply(t, "Aye Sir! Forming Up on you.");
                        TurnTo(t.X, t.Y);
                        UndeadCH.MobFallIn();
                        break;
                    default: break;
                }
                return true;
            }

            //This function sends some text to a player and makes it appear
            //in a popup window. We just define it here so we can use it in
            //the WhisperToMe function instead of writing the long text
            //everytime we want to send some reply!
            private void SendReply(GamePlayer target, string msg)
            {
                target.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
        } // end undead commander

        #region Marching Orders
        private const int cMARCH_DISTANCE = 1000;

        private Point3D GetForwardPoint(GameNPC npc, int distance)
        {
            return new Point3D(
                npc.X + (int)(Math.Sin(0.00153398078856 * npc.Heading) * distance),
                npc.Y - (int)(Math.Cos(0.00153398078856 * npc.Heading) * distance),
                npc.Z);
        }

        public void LeftFace(GameNPC toon)
        {
            int FaceHeading = Convert.ToInt32(toon.Heading + 1024);
            if (FaceHeading > 4096)
            {
                FaceHeading -= 4096;
            }
            toon.Heading = Convert.ToUInt16(FaceHeading);
        }

        public void RightFace(GameNPC toon)
        {
            int FaceHeading = Convert.ToInt32(toon.Heading - 1024);
            if (FaceHeading < 0)
            {
                FaceHeading += 4096;
            }
            toon.Heading = Convert.ToUInt16(FaceHeading);
        }

        public void AboutFace(GameNPC toon)
        {
            int FaceHeading = Convert.ToInt32(toon.Heading + 2048);
            if (FaceHeading > 4096)
            {
                FaceHeading -= 4096;
            }
            toon.Heading = Convert.ToUInt16(FaceHeading);
        }

        public void MobRightFace()
        {
            if (commanderMob != null)
            {
                AboutFace(commanderMob);
                commanderMob.Yell("Right-Face!");
                for (int i = 0; i < cMOBS; i++)
                {
                    try
                    {
                        RightFace(mob[i]);
                    }
                    catch
                    {

                    }
                }
            }
        }

        public void MobForwardMarch()
        {
            if (commanderMob != null)
            {
                commanderMob.Yell("Forward-March!");
                for (int i = 0; i < cMOBS; i++)
                {
                    try
                    {
                        mob[i].WalkTo(GetForwardPoint(mob[i], cMARCH_DISTANCE), 100);
                    }
                    catch
                    {

                    }
                }
                LeftFace(commanderMob);
                commanderMob.WalkTo(GetForwardPoint(commanderMob, cMARCH_DISTANCE), 100);
            }
        }
        
        public void MobHalt()
        {
            if (commanderMob != null)
            {
                commanderMob.Yell("Company!");
                commanderMob.Yell("Halt!");
                for (int i = 0; i < cMOBS; i++)
                {
                    try
                    {
                        mob[i].StopMoving();
                    }
                    catch
                    {

                    }
                }
                commanderMob.StopMoving();
                RightFace(commanderMob);
                commanderMob.Yell("Left-Face!");
                MobLeftFace();
            }
        }

        public void MobHaltCharge()
        {
            if (commanderMob != null)
            {
                commanderMob.Yell("Company!");
                commanderMob.Yell("Halt!");
                for (int i = 0; i < cMOBS; i++)
                {
                    try
                    {
                        mob[i].StopMoving();
                    }
                    catch
                    {

                    }
                }
                commanderMob.StopMoving();
            }
        }

        public void MobLeftFace()
        {
            if (commanderMob != null)
            {
                commanderMob.Yell("Right-Face!");
                for (int i = 0; i < cMOBS; i++)
                {
                    try
                    {
                        LeftFace(mob[i]);
                    }
                    catch
                    {

                    }
                }
                AboutFace(commanderMob);
            }
        }

        public void MobCharge()
        {
            if (commanderMob != null)
            {
                commanderMob.Yell("Charge!");
                for (int i = 0; i < cMOBS; i++)
                {
                    mob[i].WalkTo(GetForwardPoint(mob[i], cMARCH_DISTANCE), mob[i].MaxSpeed);
                }
                commanderMob.WalkTo(GetForwardPoint(commanderMob, cMARCH_DISTANCE), commanderMob.MaxSpeed);
            }
        }

        public void MobFallIn()
        {
            int BasePosX;
            int BasePosY;
            int MobPosX = 0;
            int MobPosY = 0;
            int MobNumber;
            double MidPoint = (cCOLS / 2) - .5;
            if (commanderMob != null)
            {
                int FormationHeading = commanderMob.Heading + 2048;
                if (FormationHeading >= 4096)
                {
                    FormationHeading -= 4096;
                }
                commanderMob.Yell("Company!");
                commanderMob.Yell("Fall-In!");
                for (int i = 0; i < cROWS; i++)
                {
                    BasePosX = Convert.ToInt32(commanderMob.X + Math.Sin(0.00153398078856 * commanderMob.Heading) * (150 + (50 * i)));
                    BasePosY = Convert.ToInt32(commanderMob.Y - Math.Cos(0.00153398078856 * commanderMob.Heading) * (150 + (50 * i)));

                    for (int j = 0; j < cCOLS; j++)
                    {
                        // Instantiate mob
                        MobNumber = Convert.ToUInt16(((i * cCOLS) + j + 1) - 1);

                        // Build the mob position
                        if (j > MidPoint)
                        {
                            FormationHeading += 1024;
                            if (FormationHeading >= 4096)
                            {
                                FormationHeading = FormationHeading - 4096;
                            }
                            MobPosX = Convert.ToInt32(BasePosX - Math.Sin(0.00153398078856 * FormationHeading) * ((j - MidPoint) * 50));
                            MobPosY = Convert.ToInt32(BasePosY + Math.Cos(0.00153398078856 * FormationHeading) * ((j - MidPoint) * 50));
                            FormationHeading -= 1024;
                            if (FormationHeading < 0)
                            {
                                FormationHeading = FormationHeading + 4096;
                            }
                        }

                        if (j < MidPoint)
                        {
                            FormationHeading -= 1024;
                            if (FormationHeading < 0)
                            {
                                FormationHeading = FormationHeading + 4096;
                            }
                            MobPosX = Convert.ToInt32(BasePosX - Math.Sin(0.00153398078856 * FormationHeading) * ((MidPoint - j) * 50));
                            MobPosY = Convert.ToInt32(BasePosY + Math.Cos(0.00153398078856 * FormationHeading) * ((MidPoint - j) * 50));
                            FormationHeading += 1024;
                            if (FormationHeading >= 4096)
                            {
                                FormationHeading = FormationHeading - 4096;
                            }
                        }

                        if (j == MidPoint)
                        {
                            MobPosX = BasePosX;
                            MobPosY = BasePosY;
                        }
                        mob[MobNumber].WalkTo(new Point3D(MobPosX, MobPosY, commanderMob.Z), mob[MobNumber].MaxSpeed);
                        mob[MobNumber].Heading = Convert.ToUInt16(commanderMob.Heading);
                        mob[MobNumber].Heading = Convert.ToUInt16(commanderMob.Heading);
                    }
                }
                commanderMob.Yell("Attention!");
            }
        }

    #endregion MArching Orders

        #region OnCommand /undead
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 1)
            {
                client.Out.SendMessage("/undead - To summon the undead summoner", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            

            UndeadSummonerNPC m_npc = new UndeadSummonerNPC();
            m_npc.UndeadCH = this;
            m_npc.SetOwnBrain(new AI.Brain.BlankBrain());
            m_npc.X = client.Player.X;
            m_npc.Y = client.Player.Y;
            m_npc.Z = client.Player.Z;
            m_npc.Heading = (ushort)((client.Player.Heading + 2048) % 4096);
            m_npc.CurrentRegion = client.Player.CurrentRegion;
            m_npc.AddToWorld();
            client.Out.SendMessage("Mob created: OID=" + m_npc.ObjectID + " at: " + m_npc.X + " " + m_npc.Y, eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return;
        } // End command /undead
        #endregion OnCommand /undead


        public void SummonHorde(UndeadSummonerNPC HordeMaster)
        {
            Random m_rnd;
            m_rnd = new Random();
            string theType = "DOL.GS.GameNPC";
            int Rows = cROWS;
            int Cols = cCOLS;
            int MobNumber = 0;
            mob = new GameNPC[cMOBS];
            int BasePosX = Convert.ToInt32(HordeMaster.X + Math.Sin(0.00153398078856 * HordeMaster.Heading) * 150);
            int BasePosY = Convert.ToInt32(HordeMaster.Y - Math.Cos(0.00153398078856 * HordeMaster.Heading) * 150);
            int BaseHeading = 1;
            int FormationHeading = 1;
            int MobPosX = 1;
            int MobPosY = 1;
            double MidPoint = (Cols / 2) - .5;
            if (HordeMaster.Heading >= 2048)
            {
                BaseHeading = Convert.ToInt32(HordeMaster.Heading - 2048);
            }
            else
            {
                BaseHeading = Convert.ToInt32(HordeMaster.Heading + 2048);
            }

            //****************************************************
            // Create Leader Mob
            //****************************************************
            commanderMob = new UndeadCommanderNPC();
            commanderMob.UndeadCH = this;
            //Fill the object variables
            commanderMob.X = BasePosX;
            commanderMob.Y = BasePosY;
            commanderMob.Z = HordeMaster.Z;
            commanderMob.CurrentRegion = HordeMaster.CurrentRegion;
            commanderMob.Heading = (ushort)BaseHeading;
            commanderMob.Level = 1;
            commanderMob.Realm = 0;
            commanderMob.Name = "Horde Commander";
            commanderMob.Model = 106;

            //Fill the living variables
            commanderMob.CurrentSpeed = 0;
            //commanderMob.MaxSpeed = 200;
            commanderMob.GuildName = "Undead Horde";
            commanderMob.Size = 60;
            commanderMob.AddToWorld();
            AboutFace(commanderMob);
            //****************************************************
            // Create Follower Mobs
            //****************************************************
            BaseHeading = HordeMaster.Heading;
            FormationHeading = BaseHeading;
            for (double i = 0; i < Rows; i++)
            {
                BasePosX = Convert.ToInt32(HordeMaster.X + Math.Sin(0.00153398078856 * HordeMaster.Heading) * (300 + (50 * i)));
                BasePosY = Convert.ToInt32(HordeMaster.Y - Math.Cos(0.00153398078856 * HordeMaster.Heading) * (300 + (50 * i)));
                for (double j = 0; j < Cols; j++)
                {
                    // Initiate mob
                    Console.Write("MobNumber: " + MobNumber, false);
                    Console.Write("/ti: " + i + "  j: " + j + "  cROWS: " + cROWS + "  Calc: " + (((i * cROWS) + j + 1) - 1), false);
                    MobNumber = Convert.ToInt32(((i * cCOLS) + j + 1) - 1);
                    try
                    {
                        mob[MobNumber] = new GameNPC();

                        // Build the mob position
                        if (j > MidPoint)
                        {
                            FormationHeading += 1024;
                            if (FormationHeading >= 4096)
                            {
                                FormationHeading = FormationHeading - 4096;
                            }
                            MobPosX = Convert.ToInt32(BasePosX - Math.Sin(0.00153398078856 * FormationHeading) * ((j - MidPoint) * 50));
                            MobPosY = Convert.ToInt32(BasePosY + Math.Cos(0.00153398078856 * FormationHeading) * ((j - MidPoint) * 50));
                            FormationHeading -= 1024;
                            if (FormationHeading < 0)
                            {
                                FormationHeading = FormationHeading + 4096;
                            }
                        }

                        if (j < MidPoint)
                        {
                            FormationHeading -= 1024;
                            if (FormationHeading < 0)
                            {
                                FormationHeading = FormationHeading + 4096;
                            }
                            MobPosX = Convert.ToInt32(BasePosX - Math.Sin(0.00153398078856 * FormationHeading) * ((MidPoint - j) * 50));
                            MobPosY = Convert.ToInt32(BasePosY + Math.Cos(0.00153398078856 * FormationHeading) * ((MidPoint - j) * 50));
                            FormationHeading += 1024;
                            if (FormationHeading >= 4096)
                            {
                                FormationHeading = FormationHeading - 4096;
                            }
                        }

                        if (j == MidPoint)
                        {
                            MobPosX = BasePosX;
                            MobPosY = BasePosY;
                        }
                        mob[MobNumber].X = MobPosX;
                        mob[MobNumber].Y = MobPosY;
                        mob[MobNumber].Z = HordeMaster.Z;
                        mob[MobNumber].CurrentRegion = HordeMaster.CurrentRegion;
                        mob[MobNumber].Heading = Convert.ToUInt16(BaseHeading);
                        mob[MobNumber].Level = 1;
                        mob[MobNumber].Realm = 0;
                        mob[MobNumber].Name = "Hordie " + MobNumber;
                        mob[MobNumber].Model = 108;

                        //Fill the living variables
                        mob[MobNumber].CurrentSpeed = 0;
                        //mob[MobNumber].MaxSpeed = Convert.ToByte(180 + m_rnd.Next(40));
                        mob[MobNumber].GuildName = "Undead Horde";
                        mob[MobNumber].Size = Convert.ToByte(48 + m_rnd.Next(7));
                        mob[MobNumber].AddToWorld();

                        mob[MobNumber].SaveIntoDatabase();
                    }
                    catch
                    {
                        Console.Write("Horde Mob [" + MobNumber + "]:", false);
                    }
                }
            }
            Console.Write("\tHorde Summoned:", true);
        } // End SummonHorde


        public void BannishHorde()
        {
            if (commanderMob != null)
            {
                try
                {
                    commanderMob.DeleteFromDatabase();
                    commanderMob.Delete();
                    commanderMob = null;
                    Console.Write("Horde Commander deleted:", true);
                }
                catch (Exception e)
                {
                    Console.Write("Horde Commander deleted:", false);
                    Console.Write("\t" + e.Message, false);
                }
            }
            for (int i = 0; i < cMOBS; i++)
            {
                //Thread.Sleep(100);
                if (mob[i] != null)
                {
                    try
                    {
                        mob[i].DeleteFromDatabase();
                        mob[i].Delete();
                        mob[i] = null;
                        Console.Write("Horde Mob[" + i + "] deleted:", true);
                    }
                    catch
                    {
                        Console.Write("Horde Mob[" + i + "] deleted:", false);
                    }
                }
                else
                {
                    Console.Write("Horde Mob[" + i + "] Is already Null", false);
                }
            }
        } // End BannishHorde
    } // End UndeadCommandHelper class
} // End Namespace
