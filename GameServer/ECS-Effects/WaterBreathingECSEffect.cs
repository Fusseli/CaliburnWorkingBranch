using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;

namespace DOL.GS
{
    public class WaterBreathingECSEffect : ECSGameSpellEffect
    {
        public WaterBreathingECSEffect(ECSGameEffectInitParams initParams) : base(initParams) { }

        public override void OnStartEffect()
        {
            if (Owner is GamePlayer player)
            {
                player.CanBreathUnderWater = true;
                player.BaseBuffBonusCategory[eProperty.WaterSpeed] += (int)SpellHandler.Spell.Value;
                player.OnMaxSpeedChange();
            }
            OnEffectStartsMsg(Owner, true, true, true);
        }

        public override void OnStopEffect()
        {
            if (Owner is GamePlayer player)
            {
                DbInventoryItem item = player.Inventory.GetItem((eInventorySlot)37);
                if (item == null || !item.Name.ToLower().Contains("ektaktos"))
                    player.CanBreathUnderWater = false;

                player.BaseBuffBonusCategory[eProperty.WaterSpeed] -= (int)SpellHandler.Spell.Value;
                player.OnMaxSpeedChange();

                if (player.IsDiving && !player.CanBreathUnderWater)
                    ((SpellHandler)SpellHandler).MessageToLiving(player,
                        "With a gulp and a gasp you realize that you are unable to breathe underwater any longer!",
                        eChatType.CT_SpellExpires);
            }
            OnEffectExpiresMsg(Owner, true, false, true);
        }
    }
}
