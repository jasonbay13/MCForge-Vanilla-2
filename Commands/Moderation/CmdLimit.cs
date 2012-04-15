/*
Copyright 2011 MCForge
Dual-licensed under the Educational Community License, Version 2.0 and
the GNU General Public License, Version 3 (the "Licenses"); you may
not use this file except in compliance with the Licenses. You may
obtain a copy of the Licenses at
http://www.opensource.org/licenses/ecl2.php
http://www.gnu.org/licenses/gpl-3.0.html
Unless required by applicable law or agreed to in writing,
software distributed under the Licenses are distributed on an "AS IS"
BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
or implied. See the Licenses for the specific language governing
permissions and limitations under the Licenses.
*/
using System;
using System.Collections.Generic;
using MCForge.Entity;
using MCForge.Interface.Command;
using MCForge.Groups;
using MCForge.Utilities.Settings;
using MCForge.Core;

namespace CommandDll
{
    public class CmdLimit : ICommand
    {
        public string Name { get { return "Limit"; } }
        public CommandTypes Type { get { return CommandTypes.mod; } }
        public string Author { get { return "jasonbay13"; } }
        public int Version { get { return 1; } }
        public string CUD { get { return ""; } }
        public byte Permission { get { return 80; } }

        public void Use(Player p, string[] args)
        {
            int num;
            switch (args.Length)
            {
                case 0:
                    for (int i = 0; i < PlayerGroup.groups.Count; i++)
                    {
                        p.SendMessage(String.Concat(PlayerGroup.groups[i].color, PlayerGroup.groups[i].name, " | ", PlayerGroup.groups[i].maxBlockChange));
                    }
                    break;
                case 1:
                    if (int.TryParse(args[0], out num))
                    {
                        if (p.group == PlayerGroup.groups[PlayerGroup.groups.Count - 1])
                        {
                            p.group.maxBlockChange = num;
                            p.SendMessage("Changed your own limit to " + num + ".");
                        }
                        else { p.SendMessage("Can't change your own limits."); return; }
                    }
                    else p.SendMessage("Error parsing number.");
                    break;
                case 2:
                    PlayerGroup grp = PlayerGroup.Find(args[0]);
                    if (grp == null) return;
                    if (p.group.permission < grp.permission) { p.SendMessage("Cannot change limit to group higher than your own."); return; }
                    if (int.TryParse(args[1], out num))
                    {
                        grp.maxBlockChange = num;
                        p.SendMessage(String.Concat("Changed ", grp.color, grp.name, Colors.yellow, " limit to ", num));
                    }
                    else p.SendMessage("Error parsing number.");
                    break;
                default:
                    Help(p); return;
            }
        }

        public void Help(Player p)
        {
            p.SendMessage("/limit [rank] [number] - Sets max block change for rank.");
            p.SendMessage("/limit - Shows ranks and their block change limit.");
        }

        public void Initialize()
        {
            Command.AddReference(this, "limit");
        }
    }
}
