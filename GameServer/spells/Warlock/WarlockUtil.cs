/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or modify it under the terms of
 * the GNU General Public License as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 */
using DOL.GS.Effects;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Helpers for the Warlock class.
    /// 'GameSpellEffect.RemoveEffect' is a no-op in this codebase, so 'Cancel' alone leaves
    /// the effect in the owner's legacy list. These helpers remove it explicitly.
    /// </summary>
    public static class WarlockUtil
    {
        public static void CancelAndRemove(GameSpellEffect effect)
        {
            if (effect == null)
                return;

            GameLiving owner = effect.Owner;
            effect.Cancel(false);
            owner?.EffectList?.Remove(effect);
        }
    }
}
