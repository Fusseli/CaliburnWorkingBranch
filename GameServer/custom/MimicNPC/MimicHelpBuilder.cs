using System.Collections.Generic;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Help text for the mimic command family (PR21), shown by /mimic in a
    /// standard text window. Player commands always listed; GM commands only
    /// for game masters (the world-shaping commands are GM-privated).
    /// Pure text builder so the split is unit-testable.
    /// </summary>
    public static class MimicHelpBuilder
    {
        public static List<string> BuildText(bool isGameMaster)
        {
            var lines = new List<string>
            {
                "Mimic bots - your companions:",
                "/mheal - stay back and heal, or engage in combat.",
                "/mguard [name/class] - guard a group member.",
                "/mprotect [name/class] - protect a group member.",
                "/mintercept [name/class] - intercept for a group member.",
                "/mrole (leader/tank/assist/cc/puller) - set a group role.",
                "/mcamp (here/set/remove/aggrorange/filter) - camp point and aggro.",
                "/mfollow - clear camp/pull, all grouped mimics follow you.",
                "/mattack - all grouped mimics attack your target.",
                "/mpull - camp here and pull your target.",
                "/mpullfrom (here/set/remove) - where the puller pulls from.",
                "/msummon - call all grouped mimics to your location.",
                "/mlfg - list mimics looking for a group.",
                "/mpc (true/false) [group] - toggle PreventCombat.",
                "/mpvp (true/false) - toggle PvP mode.",
                "/mrez - toggle resurrecting fallen group members.",
                "/mrez outside - also rez strangers when all is calm.",
                "/mra - show a mimic's realm rank and realm abilities.",
                "/msave [newname] - keep a targeted bot in your stable.",
                "/mbots - list your saved bots.",
                "/mcall <slot|name> - summon a saved bot to your side.",
                "/mdismiss - send a saved bot back into storage.",
                "/mdelete <slot|name> - destroy a saved bot and its gear.",
                "/mbstats [Battleground] - battleground stats.",
                "/mimic - this help.",
            };
            if (isGameMaster)
            {
                lines.Add("");
                lines.Add("Game master - shaping the world:");
                lines.Add("/mcreate class [level] [spec] [inv] - spawn a mimic at you.");
                lines.Add("/mspawner - spawn mimics on a timer (realm, levels, max).");
                lines.Add("/mgroup - summon a whole group of realm mimics.");
                lines.Add("/mbattle [Region] (Start/Stop/Clear) - battleground control.");
            }
            return lines;
        }
    }
}
