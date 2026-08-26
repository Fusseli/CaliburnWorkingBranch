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
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.SkillHandler;
using log4net;

namespace DOL.GS.Spells
{
	/// <summary>
	/// 
	/// </summary>
    public class PrimerSpellHandler : SpellHandler
	{
		/// <summary>
		/// Cast Powerless
		/// </summary>
		/// <param name="target"></param>
		public override void FinishSpellCast(GameLiving target)
		{
			m_caster.Mana -= PowerCost(target);
			
			base.FinishSpellCast(target);
		}

		protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
		{
			// DB Duration for primers is in seconds (10 = 10 sec), GameSpellEffect expects ms
			int durationMs = Spell.Duration * 1000;
			if (durationMs <= 0)
				durationMs = 10 * 1000;
			return new GameSpellEffect(this, durationMs, 0, effectiveness);
		}

		/// <summary>
		/// Warlock primers bypass the ECS effect pipeline and start a legacy GameSpellEffect instead,
		/// so the existing lookups keep working: free-cast check in 'PowerCost', primer cancellation
		/// in 'FinishSpellCast' and the mutual exclusion checks in each primer's 'CheckBeginCast'.
		/// </summary>
		public override void ApplyEffectOnTarget(GameLiving target)
		{
			if (!target.IsAlive || target.EffectList == null)
				return;

			// Replace any leftover primer of the same type instead of stacking.
			GameSpellEffect existing = SpellHandler.FindEffectOnTarget(target, Spell.SpellType.ToString());
			if (existing != null)
				WarlockUtil.CancelAndRemove(existing);

			GameSpellEffect effect = CreateSpellEffect(target, CasterEffectiveness);
			effect.Start(target);
		}

		public override void OnEffectStart(GameSpellEffect effect)
		{			
			GameEventMgr.AddHandler(effect.Owner, GamePlayerEvent.Moving, new DOLEventHandler(OnMove));
			SendEffectAnimation(effect.Owner, 0, false, 1);			
		}

		public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
		{
			if(effect.Owner is GamePlayer && !noMessages)
				((GamePlayer)effect.Owner).Out.SendMessage("You modification spell effect has expired.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);

			GameEventMgr.RemoveHandler(effect.Owner, GamePlayerEvent.Moving, new DOLEventHandler(OnMove));

			return base.OnEffectExpires (effect, false);
		}

	
		/// <summary>
		/// Handles attacks on player/by player
		/// </summary>
		/// <param name="e"></param>
		/// <param name="sender"></param>
		/// <param name="arguments"></param>
		private void OnMove(DOLEvent e, object sender, EventArgs arguments)
		{
			GameLiving living = sender as GameLiving;
			if (living == null) return;
			if(living.IsMoving)
			{
				// remove speed buff if in combat
				GameSpellEffect effect = SpellHandler.FindEffectOnTarget(living, this);
				if (effect != null)
				{
					WarlockUtil.CancelAndRemove(effect);
					((GamePlayer)living).Out.SendMessage("You move and break your modification spell.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				}
			}
		}


		// constructor
		public PrimerSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) {}
	}
}
