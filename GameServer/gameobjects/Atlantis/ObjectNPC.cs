using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.Events;
using log4net;
using System.Reflection;
using DOL.Database;

namespace DOL.GS
{
    public class ObjectNPC : GameMovingObject
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        public override void SaveIntoDatabase()
        {
        }
        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            this.Realm = eRealm.None;
            Flags = eFlags.PEACE;
            Flags |= eFlags.CANTTARGET;
            this.Level = 0;
            return true;
        }
    }
}
