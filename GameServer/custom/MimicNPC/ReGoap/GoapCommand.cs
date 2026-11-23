using System;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic
{
    [CmdAttribute("&mgoap", ePrivLevel.GM, "Inspect or toggle tactical GOAP on the targeted mimic.",
        "/mgoap [status|on|off]")]
    public class MimicGoapCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.TargetObject is not MimicNPC mimic)
            {
                client.Player.Out.SendMessage("Target a mimic first.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            string mode = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
            var brain = mimic.MimicBrain;
            switch (mode)
            {
                case "on": brain.GoapEnabled = true; break;
                case "off": brain.GoapEnabled = false; break;
                case "status": break;
                default:
                    client.Player.Out.SendMessage("Usage: /mgoap [status|on|off]", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
            }
            string report = $"{mimic.Name}: GOAP requested {(brain.GoapEnabled ? "on" : "off")} (applied next AI tick)\n" +
                (brain.GoapAgent?.GetDebugInfo() ?? "Agent initializes at the first tactical decision.");
            foreach (string line in report.Split('\n'))
                client.Player.Out.SendMessage(line, eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}
