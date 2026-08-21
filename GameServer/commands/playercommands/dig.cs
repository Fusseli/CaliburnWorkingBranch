using System;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Database;
using DOL.GS.Atlantis;
using log4net;
using System.Reflection;

namespace DOL.GS.Commands
{
	[CmdAttribute(
		"&dig",
		ePrivLevel.Player,
		"Dig for buried items",
		"/dig")]
	public class DigCommandHandler : AbstractCommandHandler, ICommandHandler
	{
		//Log - Debug
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		//Oceanus Notos (Hibernia)
		private const int RegionID = 130;

		//Chance to find a token on a completed dig ( in percent )
		private const int DigChance = 50;

		//Time it takes to complete a dig ( in milliseconds )
		private const int DigTime = 3000;

		//Desmona Revelation Tokens available on the island at any time
		private const int MaxTokens = 10;

		//TempProperties key - is this player currently digging ?
		private const string DiggingProperty = "Desmona_DiggingInProgress";

		//Dig spots ( X , Y , Z )
		private static readonly int[,] DigSpots = {
			{ 360347, 636709, 8054 },
			{ 355674, 641482, 8093 },
		};

		//Max distance from a dig spot to find a token ( in units )
		private const int DigRange = 400;

		public void OnCommand(GameClient client, string[] args)
		{
			GamePlayer player = client.Player;
			if (player == null)
				return;

			//Only in Oceanus Notos
			if (player.CurrentRegionID != RegionID)
			{
				DisplayMessage(client, "You dig but find nothing of interest here.");
				return;
			}

			//Don't allow digging while another dig is in progress
			if (player.TempProperties.GetProperty<bool>(DiggingProperty, false))
			{
				DisplayMessage(client, "You are already digging.");
				return;
			}

			//Check if the player stands on a dig spot
			if (!IsOnDigSpot(player))
			{
				DisplayMessage(client, "You dig but find nothing of interest here.");
				return;
			}

			//Start the dig
			player.TempProperties.SetProperty(DiggingProperty, true);
			DisplayMessage(client, "You begin to dig...");
			new ECSGameTimer(player, new ECSGameTimer.ECSTimerCallback(DigFinishedCallback), DigTime);
		}

		//Called when the dig completes
		public int DigFinishedCallback(ECSGameTimer timer)
		{
			GamePlayer player = timer.Owner as GamePlayer;
			if (player != null)
				player.TempProperties.SetProperty(DiggingProperty, false);

			if (player == null || player.ObjectState != GameObject.eObjectState.Active || !player.IsAlive || player.CurrentRegionID != RegionID)
				return 0;

			//The player moved away from the dig spot
			if (!IsOnDigSpot(player))
			{
				DisplayMessage(player.Client, "You stop digging.");
				return 0;
			}

			//Check the amount of tokens available at any time
			if (CountTokens() >= MaxTokens)
			{
				DisplayMessage(player.Client, "The ground seems picked clean of Desmona Revelation Tokens right now.");
				return 0;
			}

			//Roll the dig chance
			if (!Util.Chance(DigChance))
			{
				DisplayMessage(player.Client, "Your digging turns up nothing useful.");
				return 0;
			}

			DbItemTemplate token = GameServer.Database.FindObjectByKey<DbItemTemplate>("ToaManager_Desmona_Token");
			if (token == null)
			{
				log.Error("Dig: Could not find item template ToaManager_Desmona_Token");
				return 0;
			}

			if (!player.Inventory.AddTemplate(GameInventoryItem.Create(token), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
			{
				DisplayMessage(player.Client, "Your backpack is full !");
				return 0;
			}

			player.Out.SendMessage("You dig up a Desmona Revelation Token !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
			return 0;
		}

		//Is the player standing on a dig spot ?
		private static bool IsOnDigSpot(GamePlayer player)
		{
			for (int i = 0; i < DigSpots.GetLength(0); i++)
			{
				if (player.IsWithinRadius(new Point3D(DigSpots[i, 0], DigSpots[i, 1], DigSpots[i, 2]), DigRange))
					return true;
			}
			return false;
		}

		//Count DesmonaCoins currently held by all players
		private static int CountTokens()
		{
			int count = 0;
			foreach (GameClient otherClient in WorldMgr.GetAllClients())
			{
				if (otherClient.ClientState != GameClient.eClientState.Playing)
					continue;
				GamePlayer otherPlayer = otherClient.Player;
				if (otherPlayer == null)
					continue;
				foreach (DbInventoryItem item in otherPlayer.Inventory.AllItems)
				{
					if (item is DesmonaCoin)
						count++;
				}
			}
			return count;
		}
	}
}