using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DOL.GS;

namespace DOL.GS.Scripts
{
    class MimicNPCInventory : GameLivingInventory
    {
        /// <summary>PR24: the mimic this inventory belongs to, so VisibleItems can ask
        /// whether it is lying dead. GameLivingInventory exposes no owner of its own.</summary>
        private readonly MimicNPC _owner;

        public MimicNPCInventory(MimicNPC owner)
        {
            _owner = owner;
        }

        // Mimic gear is moved into stable storage without deleting the item row.
        public override bool RemoveItemWithoutDbDeletion(global::DOL.Database.DbInventoryItem item)
        {
            return base.RemoveItem(item);
        }

        /// <summary>
        /// PR24: a dead mimic is drawn with a corpse model, so optionally strip its
        /// visible equipment while it lies there.
        ///
        /// Why this is needed: the model change packet (0xDB) carries only OID, model
        /// and size - no equipment at all. Every equipment packet in the codebase is
        /// rebuilt from living.Inventory.VisibleItems at send time, so swapping the
        /// model does not stop the client being told about the armour and weapons: it
        /// keeps its equipment state and re-applies it to the new mesh. Whether that
        /// looks right on a skeleton is client behaviour, so it is a live toggle rather
        /// than an assumption.
        ///
        /// Safe by construction: VisibleItems is recomputed per packet and nothing
        /// caches it (EffectiveOverallAF sums over it on every access), so the gear
        /// returns by itself the moment m_isDead goes false on revive. Off by default
        /// so the honest result is visible first.
        /// </summary>
        public override ICollection<global::DOL.Database.DbInventoryItem> VisibleItems
        {
            get
            {
                if (ServerProperties.Properties.MIMIC_CORPSE_HIDE_EQUIPMENT
                    && _owner != null && !_owner.IsAlive)
                {
                    return Array.Empty<global::DOL.Database.DbInventoryItem>();
                }
                return base.VisibleItems;
            }
        }

        protected override eInventorySlot GetValidInventorySlot(eInventorySlot slot)
        {
            switch (slot)
            {
                case eInventorySlot.LastEmptyQuiver:
                slot = FindLastEmptySlot(eInventorySlot.FirstQuiver, eInventorySlot.FourthQuiver);
                break;
                case eInventorySlot.FirstEmptyQuiver:
                slot = FindFirstEmptySlot(eInventorySlot.FirstQuiver, eInventorySlot.FourthQuiver);
                break;
                case eInventorySlot.LastEmptyVault:
                slot = FindLastEmptySlot(eInventorySlot.FirstVault, eInventorySlot.LastVault);
                break;
                case eInventorySlot.FirstEmptyVault:
                slot = FindFirstEmptySlot(eInventorySlot.FirstVault, eInventorySlot.LastVault);
                break;
                case eInventorySlot.LastEmptyBackpack:
                slot = FindLastEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                break;
                case eInventorySlot.FirstEmptyBackpack:
                slot = FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                break;
                case eInventorySlot.LastEmptyBagHorse:
                slot = FindLastEmptySlot(eInventorySlot.FirstBagHorse, eInventorySlot.LastBagHorse);
                break;
                case eInventorySlot.FirstEmptyBagHorse:
                slot = FindFirstEmptySlot(eInventorySlot.FirstBagHorse, eInventorySlot.LastBagHorse);
                break;
            }

            if ((slot >= eInventorySlot.FirstBackpack && slot <= eInventorySlot.LastBackpack)
                // || ( slot >= eInventorySlot.Mithril && slot <= eInventorySlot.Copper ) // can't place items in money slots, is it?
                || (slot >= eInventorySlot.HorseArmor && slot <= eInventorySlot.Horse)
                || (slot >= eInventorySlot.FirstVault && slot <= eInventorySlot.LastVault)
                || (slot >= eInventorySlot.HouseVault_First && slot <= eInventorySlot.HouseVault_Last)
                || (slot >= eInventorySlot.Consignment_First && slot <= eInventorySlot.Consignment_Last)
                || (slot == eInventorySlot.PlayerPaperDoll)
                || (slot == eInventorySlot.Mythical)

                || (slot >= eInventorySlot.FirstBagHorse && slot <= eInventorySlot.LastBagHorse))
                return slot;


            return base.GetValidInventorySlot(slot);
        }
    }
}
