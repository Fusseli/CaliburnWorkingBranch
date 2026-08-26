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

using DOL.GS.Effects;

namespace DOL.GS.PacketHandler.Client.v168
{
	/// <summary>
	/// Handles effect cancel requests
	/// </summary>
	[PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerCancelsEffect, "Handle Player Effect Cancel Request.", eClientStatus.PlayerInGame)]
	public class PlayerCancelsEffectHandler : IPacketHandler
	{
		public void HandlePacket(GameClient client, GSPacketIn packet)
		{
			int effectID = packet.ReadShort();
			if (client.Version <= GameClient.eClientVersion.Version1109)
				new CancelEffectHandler(client.Player, effectID).Start(1);
			else
				new CancelEffectHandler1110(client.Player, effectID).Start(1);
		}

		/// <summary>
		/// Handles players cancel effect actions
		/// </summary>
		protected class CancelEffectHandler : ECSGameTimerWrapperBase
		{
			/// <summary>
			/// The effect Id
			/// </summary>
			protected readonly int m_effectId;

			/// <summary>
			/// Constructs a new CancelEffectHandler
			/// </summary>
			/// <param name="actionSource">The action source</param>
			/// <param name="effectId">The effect Id</param>
			public CancelEffectHandler(GamePlayer actionSource, int effectId) : base(actionSource)
			{
				m_effectId = effectId;
			}

			/// <summary>
			/// Called on every timer tick
			/// </summary>
			protected override int OnTick(ECSGameTimer timer)
			{
				GamePlayer player = (GamePlayer) timer.Owner;

				IGameEffect found = null;
				lock (player.EffectList)
				{
					foreach (IGameEffect effect in player.EffectList)
					{
						if (effect.InternalID == m_effectId)
						{
							found = effect;
							break;
						}
					}
				}
				if (found != null)
					found.Cancel(true);
				return 0;
			}
		}

		/// <summary>
		/// Handles players cancel effect actions
		/// </summary>
		protected class CancelEffectHandler1110 : ECSGameTimerWrapperBase
		{
			/// <summary>
			/// The effect Id
			/// </summary>
			protected readonly int m_effectId;

			/// <summary>
			/// Constructs a new CancelEffectHandler
			/// </summary>
			/// <param name="actionSource">The action source</param>
			/// <param name="effectId">The effect Id</param>
			public CancelEffectHandler1110(GamePlayer actionSource, int effectId) : base(actionSource)
			{
				m_effectId = effectId;
			}

			/// <summary>
			/// Called on every timer tick
			/// </summary>
			protected override int OnTick(ECSGameTimer timer)
			{
				GamePlayer player = (GamePlayer) timer.Owner;
				EffectListComponent effectListComponent = player.effectListComponent;
				ECSGameEffect effect = effectListComponent.TryGetEffectFromEffectId(m_effectId);

				if (effect != null)
				{
					EffectService.RequestImmediateCancelEffect(effect, true);
					return 0;
				}

				// Fallback: legacy effects (Warlock chambers/primers) live in the old list
				// and never appear in the ECS component. On 1.110+ clients the merged
				// UpdateIcons (PacketLib1110) writes Spell.InternalID as delveId, so the
				// cancel packet may carry InternalID OR SpellID. Check both.
				IGameEffect legacy = null;
				lock (player.EffectList)
				{
					foreach (IGameEffect fx in player.EffectList)
					{
						if (fx.InternalID == m_effectId)
						{
							legacy = fx;
							break;
						}
						if (fx is GameSpellEffect lgse && lgse.Spell != null)
						{
							// PacketLib1110 merged rendering writes Spell.InternalID as the 6th short
							if (lgse.Spell.InternalID == m_effectId || lgse.Spell.Icon == m_effectId || lgse.Spell.ID == m_effectId)
							{
								legacy = fx;
								break;
							}
						}
					}
				}

				if (legacy is GameSpellEffect gse)
				{
					// Allow player to remove any positive chamber/primer immediately, live-like.
					// Use Cancel(false) to bypass HasPositiveEffect block.
					DOL.GS.Spells.WarlockUtil.CancelAndRemove(gse);
					// Refresh the floating balls for this warlock
					if (gse.SpellHandler?.Spell.SpellType == eSpellType.Chamber
					    || gse.SpellHandler is DOL.GS.Spells.ChamberSpellHandler)
					{
						player.Out.SendWarlockChamberEffect(player);
					}
				}
				else if (legacy != null)
				{
					legacy.Cancel(false);
					player.EffectList.Remove(legacy);
				}

				return 0;
			}
		}
	}
}
