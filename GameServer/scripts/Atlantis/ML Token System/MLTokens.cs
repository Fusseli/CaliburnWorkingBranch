using DOL.Database;
using DOL.Events;
using log4net;
using System;
using System.Reflection;

namespace DOL.GS
{
    public class MLTokens
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            CreateToken("ml1token", "ML1 Credit", 10000);
            CreateToken("ml2token", "ML2 Credit", 10000);
            CreateToken("ml3token", "ML3 Credit", 10000);
            CreateToken("ml4token", "ML4 Credit", 10000);
            CreateToken("ml5token", "ML5 Credit", 10000);
            CreateToken("ml6token", "ML6 Credit", 10000);
            CreateToken("ml7token", "ML7 Credit", 10000);
            CreateToken("ml8token", "ML8 Credit", 10000);
            CreateToken("ml9token", "ML9 Credit", 10000);
            CreateToken("ml10token", "ML10 Credit", 10000);

            if (DOLDB<DbMerchantItem>.SelectObjects(DB.Column("ItemListID").IsEqualTo("mltokens")).Count == 0)
            {
                for (int i = 1; i <= 10; i++)
                {
                    GameServer.Database.AddObject(new DbMerchantItem
                    {
                        ItemListID = "mltokens",
                        ItemTemplateID = "ml" + i + "token",
                        PageNumber = 0,
                        SlotPosition = i - 1
                    });
                }
                log.Info("ML Token merchant items added.");
            }
        }

        private static void CreateToken(string id, string name, long price)
        {
            if (GameServer.Database.FindObjectByKey<DbItemTemplate>(id) == null)
            {
                GameServer.Database.AddObject(new DbItemTemplate
                {
                    Id_nb = id,
                    Name = name,
                    Level = 50,
                    Item_Type = 40,
                    Model = 485,
                    Object_Type = 0,
                    Quality = 100,
                    Weight = 1,
                    Price = price,
                    MaxCondition = 100,
                    MaxDurability = 100,
                    Condition = 100,
                    Durability = 100
                });
                if (log.IsDebugEnabled)
                    log.Debug("Added " + id);
            }
        }
    }
}