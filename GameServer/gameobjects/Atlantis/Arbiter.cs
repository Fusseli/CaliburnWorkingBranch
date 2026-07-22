using System;
using System.Collections.Generic;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    public class Arbiter : Researcher
    {
        private static readonly string[] MLPathNames = { "Banelord", "Battlemaster", "Convoker", "Perfecter", "Sojourner", "Spymaster", "Stormlord", "Warlord" };

        public Arbiter() : base() { }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (!player.MLGranted)
            {
                if (player.Level >= 40)
                {
                    player.MLGranted = true;
                    player.SaveIntoDatabase();
                    SayTo(player, eChatLoc.CL_PopupWindow, "You have been granted permission to undertake the Master Levels! You may now view your progress in the Master Level window.");
                    player.Out.SendMasterLevelWindow(0);
                }
                else
                {
                    SayTo(player, eChatLoc.CL_PopupWindow, "You must be at least level 40 to undertake the Master Levels.");
                }
                return true;
            }

            if (player.MLLevel < 10)
            {
                byte currentML = (byte)(player.MLLevel + 1);
                byte completed = player.GetCountMLStepsCompleted(currentML);
                byte total = player.GetStepCountForML(currentML);

                if (completed >= total)
                {
                    if (player.MLLevel == 0)
                    {
                        string pathA, pathB;
                        GetMLPathChoices(player, out pathA, out pathB);
                        SayTo(player, eChatLoc.CL_PopupWindow, $"You have completed all trials for Master Level 1! You must now choose a path. Say [{pathA}] or [{pathB}] to select your specialization.");
                    }
                    else
                    {
                        SayTo(player, eChatLoc.CL_PopupWindow, $"You have completed all trials for Master Level {player.MLLevel + 1}! Say [advance] to be promoted to the next level.");
                    }
                }
                else
                {
                    SayTo(player, eChatLoc.CL_PopupWindow, $"You have completed {completed} of {total} trials for Master Level {currentML}. Complete all trials and return to me for promotion.");
                }
            }
            else
            {
                SayTo(player, eChatLoc.CL_PopupWindow, "You have achieved the highest Master Level. There is nothing more I can teach you.");
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text)) return false;
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;

            string lower = text.ToLower();

            if (lower == "advance" && player.MLGranted && player.MLLevel < 10)
            {
                byte currentML = (byte)(player.MLLevel + 1);
                if (player.GetCountMLStepsCompleted(currentML) >= player.GetStepCountForML(currentML))
                {
                    if (player.MLLevel < 10)
                        AdvanceML(player, currentML);
                }
                return false;
            }

            string chosenPath = GuessMLPath(lower);
            if (chosenPath != null && player.MLGranted && player.MLLevel == 0)
            {
                byte completed = player.GetCountMLStepsCompleted(1);
                if (completed >= player.GetStepCountForML(1))
                {
                    string pathA, pathB;
                    GetMLPathChoices(player, out pathA, out pathB);
                    if (lower == pathA.ToLower() || lower == pathB.ToLower())
                    {
                        GrantMLPath(player, chosenPath);
                    }
                }
                return false;
            }

            return true;
        }

        private void GetMLPathChoices(GamePlayer player, out string pathA, out string pathB)
        {
            pathA = "Banelord";
            pathB = "Battlemaster";
            switch (player.CharacterClass.ID)
            {
                case 1:  pathA = "Warlord"; pathB = "Battlemaster"; break;
                case 2:  pathA = "Warlord"; pathB = "Battlemaster"; break;
                case 3:  pathA = "Battlemaster"; pathB = "Sojourner"; break;
                case 4:  pathA = "Warlord"; pathB = "Sojourner"; break;
                case 5:  pathA = "Convoker"; pathB = "Stormlord"; break;
                case 6:  pathA = "Warlord"; pathB = "Perfecter"; break;
                case 7:  pathA = "Convoker"; pathB = "Stormlord"; break;
                case 8:  pathA = "Convoker"; pathB = "Stormlord"; break;
                case 9:  pathA = "Spymaster"; pathB = "Battlemaster"; break;
                case 10: pathA = "Battlemaster"; pathB = "Perfecter"; break;
                case 11: pathA = "Battlemaster"; pathB = "Banelord"; break;
                case 12: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 13: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 14: pathA = "Battlemaster"; pathB = "Banelord"; break;
                case 15: pathA = "Battlemaster"; pathB = "Stormlord"; break;
                case 16: pathA = "Warlord"; pathB = "Battlemaster"; break;
                case 17: pathA = "Spymaster"; pathB = "Battlemaster"; break;
                case 18: pathA = "Warlord"; pathB = "Sojourner"; break;
                case 19: pathA = "Sojourner"; pathB = "Battlemaster"; break;
                case 20: pathA = "Sojourner"; pathB = "Perfecter"; break;
                case 21: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 22: pathA = "Convoker"; pathB = "Perfecter"; break;
                case 23: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 24: pathA = "Convoker"; pathB = "Banelord"; break;
                case 25: pathA = "Battlemaster"; pathB = "Banelord"; break;
                case 26: pathA = "Warlord"; pathB = "Battlemaster"; break;
                case 27: pathA = "Banelord"; pathB = "Perfecter"; break;
                case 28: pathA = "Stormlord"; pathB = "Warlord"; break;
                case 29: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 30: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 31: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 32: pathA = "Stormlord"; pathB = "Warlord"; break;
                case 33: pathA = "Battlemaster"; pathB = "Banelord"; break;
                case 34: pathA = "Battlemaster"; pathB = "Warlord"; break;
                case 35: pathA = "Battlemaster"; pathB = "Banelord"; break;
                case 36: pathA = "Battlemaster"; pathB = "Perfecter"; break;
                case 37: pathA = "Convoker"; pathB = "Perfecter"; break;
                case 38: pathA = "Sojourner"; pathB = "Perfecter"; break;
                case 39: pathA = "Spymaster"; pathB = "Stormlord"; break;
                case 40: pathA = "Battlemaster"; pathB = "Sojourner"; break;
                case 41: pathA = "Convoker"; pathB = "Stormlord"; break;
                case 42: pathA = "Battlemaster"; pathB = "Stormlord"; break;
                case 43: pathA = "Banelord"; pathB = "Warlord"; break;
                case 44: pathA = "Banelord"; pathB = "Convoker"; break;
                case 45: pathA = "Battlemaster"; pathB = "Warlord"; break;
                case 46: pathA = "Battlemaster"; pathB = "Warlord"; break;
                case 47: pathA = "Battlemaster"; pathB = "Warlord"; break;
            }
        }

        private string GuessMLPath(string lower)
        {
            foreach (string name in MLPathNames)
            {
                if (lower == name.ToLower())
                    return name;
            }
            return null;
        }

        private void GrantMLPath(GamePlayer player, string pathName)
        {
            player.MLLevel = 1;
            player.MLExperience = 0;
            SpellLine line = SkillBase.GetSpellLine("ML1 " + pathName);
            if (line != null)
                player.AddSpellLine(line);
            player.Out.SendMessage($"You have chosen the path of the {pathName}!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
            player.Out.SendMasterLevelWindow(1);
            player.Out.SendUpdatePlayerSkills();
            player.Out.SendUpdatePlayer();
            player.SaveIntoDatabase();
        }

        private void AdvanceML(GamePlayer player, byte nextML)
        {
            string currentPath = GetCurrentMLPath(player);
            if (currentPath == null) return;

            player.MLLevel = nextML;
            player.MLExperience = 0;
            player.MLGranted = false;

            SpellLine line = SkillBase.GetSpellLine($"ML{nextML} {currentPath}");
            if (line != null)
                player.AddSpellLine(line);

            player.Out.SendMessage($"You have been promoted to Master Level {nextML}!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
            player.Out.SendMasterLevelWindow(nextML);
            player.Out.SendUpdatePlayerSkills();
            player.Out.SendUpdatePlayer();
            player.SaveIntoDatabase();
        }

        private string GetCurrentMLPath(GamePlayer player)
        {
            foreach (string name in MLPathNames)
            {
                for (int ml = 1; ml <= 10; ml++)
                {
                    if (player.GetSpellLine($"ML{ml} {name}") != null)
                        return name;
                }
            }
            return null;
        }
    }
}
