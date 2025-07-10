/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

/*
 * Advanced LootGenerator for Realm and Bonuty Points.
 * Can use a simple in game command to set a Mobs name, Mobs Guild name or a region to give,
 * Realm Points, Bounty Points or both.
 * Also added a remove command too, so you can stop mobs giving rewards too.
 * 
 * Please note, mobs will only give rps/bps, or stop after you reboot the server, to allow the lootgen to load.
 * 
 * By deathwish Version 1.1 14/03/2012
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DOL.Events;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using log4net;
using DOL.GS.Commands;
using DOL.GS.Scripts;


#region command setup
namespace DOL.GS.Scripts
{
    [Cmd("&advgen", //command to handle
     ePrivLevel.GM, //minimum privelege level
     "Advance Generator commands", //command description
     "Types as followed: 1 = Rps, 2 = Bps, 3 = Both.", //command types
        // usage
     "'/advgen addname <mobs name> <type>", // use "" if mobs name contains a space, eg "New Mob", 
     "'/advgen addguild <mobs guild> <type>", // use "" if mobs guild contains a space, eg "New Guild", 
     "'/advgen addregion <region id> <type>", // will effect all mobs in that region, 
     "'/advgen removename <mobs name>",  // use "" if mobs name contains a space, eg "New Mob", 
     "'/advgen removeguild <mobs guild name>", // use "" if mobs guild contains a space, eg "New Guild", 
     "'/advgen removeregion <region id>" // will remove all mobs in that region
     )]


    public class AdvanceGen : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 3)
            {
                List<string> text = new List<string>();
                text.Add("Types as followed: 1 = Rps, 2 = Bps, 3 = Both.");
                text.Add("/advgen addname <mobs name> <type>");
                text.Add("/advgen addguild <mobs guild> <type>");
                text.Add("/advgen addregion <region id> <type>");
                text.Add("/advgen addregion <region id> <type>");
                text.Add("/advgen removename <mobs name>");
                text.Add("/advgen removeguild <mobs guild>");
                text.Add("/advgen removeregion <region id>");
                text.Add(" Advanced LootGenerator By Deathwish ");
                text.Add(" Version 1.1 - Open Script for Help.. ");
                text.Add(" Also to edit amounts ");
                client.Out.SendCustomTextWindow("Advanced Gen", text);
                return;
            }
            switch (args[1])
            {
                case "addname": AddName(client, args); break;
                case "addguild": AddGuild(client, args); break;
                case "addregion": AddRegion(client, args); break;
                case "removename": RemoveName(client, args); break;
                case "removeguild": RemoveGuild(client, args); break;
                case "removeregion": RemoveRegion(client, args); break;     
            }
        }



        private static int AddName(GameClient client, string[] args)
        {

            DbLootGenerator advgen = new DbLootGenerator();
            advgen.MobName = args[2];
            advgen.LootGeneratorClass = "DOL.GS.AdvancedGen" + args[3] + "";
            GameServer.Database.AddObject(advgen);
            client.Out.SendMessage("Mob name, " + args[2] + " has been added to advanced lootgen", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);
            return 1;
        }

        private static int AddGuild(GameClient client, string[] args)
        {
            DbLootGenerator advgen = new DbLootGenerator();
            advgen.MobGuild = args[2];
            advgen.LootGeneratorClass = "DOL.GS.AdvancedGen" + args[3] + "";
            client.Out.SendMessage("Mob Guild, " + args[2] + " has been added to advanced lootgen", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);

            return 1;
        }

        private static int AddRegion(GameClient client, string[] args)
        {

            DbLootGenerator advgen = new DbLootGenerator();
            advgen.RegionID = byte.Parse(args[2]);
            advgen.LootGeneratorClass = "DOL.GS.AdvancedGen" + args[3] + "";
            GameServer.Database.AddObject(advgen);
            client.Out.SendMessage("RegionID " + args[2] + " has been added to advanced lootgen", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);

            return 1;
        }

        private void RemoveName(GameClient client, string[] args)
        {
            DbLootGenerator advgen = (DbLootGenerator)GameServer.Database.SelectObject<DbLootGenerator>(DB.Column("MobName").IsEqualTo(args[2]));

            if (advgen == null)
            {
                client.Out.SendMessage("No Mobs name of the name of '" + args[2] + "' found", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);
                return;
            }
            else
            {
                GameServer.Database.DeleteObject(advgen);
                client.Out.SendMessage("You have deleted the following mobs name '" + args[2] + "' from Advanced Generator", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);
                return;
            }

        }

        private void RemoveGuild(GameClient client, string[] args)
        {
            DbLootGenerator advgen = (DbLootGenerator)GameServer.Database.SelectObject<DbLootGenerator>(DB.Column("MobGuild").IsEqualTo(args[2]));

            if (advgen == null)
            {
                client.Out.SendMessage("No Mobs Guild name of '" + args[2] + "' found", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);
                return;
            }
            else
            {

                GameServer.Database.DeleteObject(advgen);
                client.Out.SendMessage("You have deleted the following mobs guild name '" + args[2] + "' from Advanced Generator", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);
                return;
            }
        }

        private void RemoveRegion(GameClient client, string[] args)
        {
            DbLootGenerator advgen = (DbLootGenerator)GameServer.Database.SelectObject<DbLootGenerator>(DB.Column("RegionID").IsEqualTo(byte.Parse(args[2])));

            if (advgen == null)
            {
                client.Out.SendMessage("No RegionID '" + byte.Parse(args[2]) + "' found", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);
                return;
            }
            else
            {
                GameServer.Database.DeleteObject(advgen);
                client.Out.SendMessage("You have deleted the following RegionID '" + byte.Parse(args[2]) + "' from Advanced Generator", eChatType.CT_Advise, eChatLoc.CL_ChatWindow);
                return;
            }
        }
        public static object AllowAdd { get; set; }
    }
}
#endregion command setup

#region lootgenerator edit amounts here
namespace DOL.GS
{
    /// <summary>
    /// MoneyLootGenerator
    /// At the moment this generaotr only adds money to the loot
    /// </summary>
    public class AdvancedGen1 : LootGeneratorBase
    {
        public override LootList GenerateLoot(GameNPC mob, GameObject killer)
        {
            LootList loot = base.GenerateLoot(mob, killer);

            int Level = mob.Level;
            //amount of rp/bp gain based on the mob level

            int rewardrp = 0;
            if (Level >= 30) { rewardrp = 1; }
            if (Level >= 35) { rewardrp = 3; }
            if (Level >= 40) { rewardrp = 5; }
            if (Level >= 45) { rewardrp = 10; }
            if (Level >= 50) { rewardrp = 15; }
            if (Level >= 52) { rewardrp = 30; }
            if (Level >= 54) { rewardrp = 40; }
            if (Level >= 56) { rewardrp = 50; }
            if (Level >= 58) { rewardrp = 60; }
            if (Level >= 60) { rewardrp = 70; }
            if (Level >= 61) { rewardrp = 80; }
            if (Level >= 62) { rewardrp = 90; }
            if (Level >= 63) { rewardrp = 100; }
            if (Level >= 64) { rewardrp = 110; }
            if (Level >= 65) { rewardrp = 120; }
            if (Level >= 66) { rewardrp = 130; }
            if (Level >= 67) { rewardrp = 140; }
            if (Level >= 68) { rewardrp = 160; }
            if (Level >= 69) { rewardrp = 180; }
            if (Level >= 70) { rewardrp = 200; }
            if (Level >= 71) { rewardrp = 210; }
            if (Level >= 72) { rewardrp = 220; }
            if (Level >= 73) { rewardrp = 230; }
            if (Level >= 74) { rewardrp = 240; }
            if (Level >= 75) { rewardrp = 250; }
            if (Level >= 76) { rewardrp = 260; }
            if (Level >= 77) { rewardrp = 270; }
            if (Level >= 78) { rewardrp = 280; }
            if (Level >= 79) { rewardrp = 300; }
            if (Level >= 80) { rewardrp = 400; }

            IGamePlayer player = killer as IGamePlayer;
            player.Out.SendMessage("You Get " + rewardrp + " realm points!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.RealmPoints += rewardrp;

            return loot;
        }
    }

    public class AdvancedGen2 : LootGeneratorBase
    {
        public override LootList GenerateLoot(GameNPC mob, GameObject killer)
        {
            LootList loot = base.GenerateLoot(mob, killer);

            int Level = mob.Level;
            //amount of rp/bp gain based on the mob level
            int rewardbp = 0;

            if (Level >= 30) { rewardbp = 1; }
            if (Level >= 35) { rewardbp = 3; }
            if (Level >= 40) { rewardbp = 5; }
            if (Level >= 45) { rewardbp = 10; }
            if (Level >= 50) { rewardbp = 15; }
            if (Level >= 52) { rewardbp = 30; }
            if (Level >= 54) { rewardbp = 40; }
            if (Level >= 56) { rewardbp = 50; }
            if (Level >= 58) { rewardbp = 60; }
            if (Level >= 60) { rewardbp = 70; }
            if (Level >= 61) { rewardbp = 80; }
            if (Level >= 62) { rewardbp = 90; }
            if (Level >= 63) { rewardbp = 100; }
            if (Level >= 64) { rewardbp = 110; }
            if (Level >= 65) { rewardbp = 120; }
            if (Level >= 66) { rewardbp = 130; }
            if (Level >= 67) { rewardbp = 140; }
            if (Level >= 68) { rewardbp = 160; }
            if (Level >= 69) { rewardbp = 180; }
            if (Level >= 70) { rewardbp = 200; }
            if (Level >= 71) { rewardbp = 210; }
            if (Level >= 72) { rewardbp = 220; }
            if (Level >= 73) { rewardbp = 230; }
            if (Level >= 74) { rewardbp = 240; }
            if (Level >= 75) { rewardbp = 250; }
            if (Level >= 76) { rewardbp = 260; }
            if (Level >= 77) { rewardbp = 270; }
            if (Level >= 78) { rewardbp = 280; }
            if (Level >= 79) { rewardbp = 300; }
            if (Level >= 80) { rewardbp = 400; }

            IGamePlayer player = killer as IGamePlayer;
            player.Out.SendMessage("You Get " + rewardbp + " bounty points!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.BountyPoints += rewardbp;

            return loot;
        }
    }

    public class AdvancedGen3 : LootGeneratorBase
    {
        public override LootList GenerateLoot(GameNPC mob, GameObject killer)
        {
            LootList loot = base.GenerateLoot(mob, killer);

            int Level = mob.Level;
            //amount of rp/bp gain based on the mob level

            int rewardrp = 0;
            if (Level >= 30) { rewardrp = 1; }
            if (Level >= 35) { rewardrp = 3; }
            if (Level >= 40) { rewardrp = 5; }
            if (Level >= 45) { rewardrp = 10; }
            if (Level >= 50) { rewardrp = 15; }
            if (Level >= 52) { rewardrp = 30; }
            if (Level >= 54) { rewardrp = 40; }
            if (Level >= 56) { rewardrp = 50; }
            if (Level >= 58) { rewardrp = 60; }
            if (Level >= 60) { rewardrp = 70; }
            if (Level >= 61) { rewardrp = 80; }
            if (Level >= 62) { rewardrp = 90; }
            if (Level >= 63) { rewardrp = 100; }
            if (Level >= 64) { rewardrp = 110; }
            if (Level >= 65) { rewardrp = 120; }
            if (Level >= 66) { rewardrp = 130; }
            if (Level >= 67) { rewardrp = 140; }
            if (Level >= 68) { rewardrp = 160; }
            if (Level >= 69) { rewardrp = 180; }
            if (Level >= 70) { rewardrp = 200; }
            if (Level >= 71) { rewardrp = 210; }
            if (Level >= 72) { rewardrp = 220; }
            if (Level >= 73) { rewardrp = 230; }
            if (Level >= 74) { rewardrp = 240; }
            if (Level >= 75) { rewardrp = 250; }
            if (Level >= 76) { rewardrp = 260; }
            if (Level >= 77) { rewardrp = 270; }
            if (Level >= 78) { rewardrp = 280; }
            if (Level >= 79) { rewardrp = 300; }
            if (Level >= 80) { rewardrp = 400; }

            int rewardbp = 0;
            if (Level >= 30) { rewardbp = 1; }
            if (Level >= 35) { rewardbp = 3; }
            if (Level >= 40) { rewardbp = 5; }
            if (Level >= 45) { rewardbp = 10; }
            if (Level >= 50) { rewardbp = 15; }
            if (Level >= 52) { rewardbp = 30; }
            if (Level >= 54) { rewardbp = 40; }
            if (Level >= 56) { rewardbp = 50; }
            if (Level >= 58) { rewardbp = 60; }
            if (Level >= 60) { rewardbp = 70; }
            if (Level >= 61) { rewardbp = 80; }
            if (Level >= 62) { rewardbp = 90; }
            if (Level >= 63) { rewardbp = 100; }
            if (Level >= 64) { rewardbp = 110; }
            if (Level >= 65) { rewardbp = 120; }
            if (Level >= 66) { rewardbp = 130; }
            if (Level >= 67) { rewardbp = 140; }
            if (Level >= 68) { rewardbp = 160; }
            if (Level >= 69) { rewardbp = 180; }
            if (Level >= 70) { rewardbp = 200; }
            if (Level >= 71) { rewardbp = 210; }
            if (Level >= 72) { rewardbp = 220; }
            if (Level >= 73) { rewardbp = 230; }
            if (Level >= 74) { rewardbp = 240; }
            if (Level >= 75) { rewardbp = 250; }
            if (Level >= 76) { rewardbp = 260; }
            if (Level >= 77) { rewardbp = 270; }
            if (Level >= 78) { rewardbp = 280; }
            if (Level >= 79) { rewardbp = 300; }
            if (Level >= 80) { rewardbp = 400; }

            IGamePlayer player = killer as IGamePlayer;
            player.Out.SendMessage("You Get " + rewardbp + " bountypoints!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.BountyPoints += rewardbp;
            player.Out.SendMessage("You Get " + rewardrp + " realm points!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.RealmPoints += rewardrp;

            return loot;
        }
    }
}

#endregion loot generator