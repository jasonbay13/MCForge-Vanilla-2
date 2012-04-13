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
using MCForge.Core;
using MCForge.Entity;
using MCForge.Interface.Command;
using System.Threading;

namespace CommandDll
{
    public class CmdRestart : ICommand
    {
        public string Name { get { return "Restart"; } }
        public CommandTypes Type { get { return CommandTypes.mod; } }
        public string Author { get { return "jasonbay13"; } }
        public int Version { get { return 1; } }
        public string CUD { get { return ""; } }
        public byte Permission { get { return 100; } }
        bool alive;

        public void Use(Player p, string[] args)
        {
            if (args.Length == 1)
            {
                int b;
                if (args[0].ToLower() == "/a")
                {
                    alive = false;
                    Player.UniversalChat("Server restart has been canceled");
                    return;
                }
                if (int.TryParse(args[0], out b))
                {
                    alive = true;
                    Player.UniversalChat("Server restart in " + b + " seconds.");
                    for (int i = b; i > 0; i--)
                    {
                        Thread.Sleep(1000);
                        if (!alive) return;
                        Player.UniversalChat(i != 1 ? "Server restart in " + (i - 1) + " seconds." : "Server restarting!");
                    }
                }
                else { Help(p); return; }
            }
            Server.Restart();
        }

        public void Help(Player p)
        {
            p.SendMessage("/restart (seconds) - Restarts the server with optional countdown.");
            p.SendMessage("/restart /a - Aborts countdown.");
        }

        public void Initialize()
        {
            Command.AddReference(this, "restart");
        }
    }
}
