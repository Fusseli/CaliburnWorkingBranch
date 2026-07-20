using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Events;
using DOL.Database;
using log4net;
using System.Reflection;

namespace DOL.GS
{
    public class SoundAreaNPC : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        public bool Enable = false;
        public int AreaRadius = 0;
        public bool DirectSound = false;
        public int ChancePlayPercent = 0;
        public int ChancePlayDelaySecond = 1;
        public int Sound1ID = 0;
        public int Sound2ID = 0;
        public int Sound3ID = 0;
        public int Sound4ID = 0;
        public int Sound5ID = 0;
        public bool OnlyNight = false;

        public override bool AddToWorld()
        {
            this.Model = 665;
            this.Flags |= eFlags.CANTTARGET;
            this.Flags |= eFlags.PEACE;

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(SoundTimer), 1000);

            return base.AddToWorld();
        }
        public override void SaveIntoDatabase()
        {
        }

        public int SoundTimer(ECSGameTimer timer)
        {
            if (Enable == false) return 0;
            if (ChancePlayDelaySecond == 0) ChancePlayDelaySecond = 1;

            int NumberOfSounds = 0;
            if (Sound1ID != 0) NumberOfSounds = NumberOfSounds + 1;
            if (Sound2ID != 0) NumberOfSounds = NumberOfSounds + 1;
            if (Sound3ID != 0) NumberOfSounds = NumberOfSounds + 1;
            if (Sound4ID != 0) NumberOfSounds = NumberOfSounds + 1;
            if (Sound5ID != 0) NumberOfSounds = NumberOfSounds + 1;
            if (NumberOfSounds == 0)
            {
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(SoundTimer), ChancePlayDelaySecond * 1000);
                return 0;
            }

            int Chance = Util.Random(1, 100);
            if (Chance <= ChancePlayPercent)
            {
                int SoundToPlayID = 0;
                int ChanceSound = Util.Random(1, NumberOfSounds);
                if (ChanceSound == 1) SoundToPlayID = Sound1ID;
                if (ChanceSound == 2) SoundToPlayID = Sound2ID;
                if (ChanceSound == 3) SoundToPlayID = Sound3ID;
                if (ChanceSound == 4) SoundToPlayID = Sound4ID;
                if (ChanceSound == 5) SoundToPlayID = Sound5ID;

                if (DirectSound == true)
                {
                    foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        p.Out.SendPlaySound(eSoundType.Divers, (ushort)SoundToPlayID);
                    }
                }

                if (debug == true)
                {
                    string Method = "";
                    if (DirectSound == true) Method = "DirectSound";
                    if (DirectSound == false) Method = "EffectSound";
                    foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (p.Client.Account.PrivLevel > 1)
                        {
                            p.Out.SendMessage("Sound id : " + SoundToPlayID + " played by " + this.Name + " with " + Method + " !", eChatType.CT_Staff, eChatLoc.CL_ChatWindow);
                        }
                    }
                }
            }

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(SoundTimer), ChancePlayDelaySecond * 1000);

            return 0;
        }
    }
}
