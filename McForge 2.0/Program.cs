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
using System.Linq;
using System.Windows.Forms;
using System.Threading;

namespace McForge
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
        [STAThread]
        static void Main()
        {
            new Thread(new ThreadStart(Server.Init)).Start();

            if (ServerSettings.EnableGUI)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Main());
            }
            else
            {
                while (true)
                {
                    string input = Console.ReadLine();
                    if (String.IsNullOrWhiteSpace(input)) continue;
                    if (input.Length > 1 && input[0] == '/' && input[1] != '/')
                        switch (input.ToLower())
                        {
                            case "/stop":
                                return; ;
                            case "/save":
                                Console.WriteLine(Server.Mainlevel.SaveLevel() ? "Sucessfully save level." : "Failed to save level.");
                                continue;
                            case "/load":
                                Console.WriteLine((Server.Mainlevel = Level.LoadLevel("main")) != null ? "Sucessfully loaded level." : "Failed to load level.");
                                continue;
                            default:
                                Console.WriteLine("Invalid command.");
                                continue;
                        }
                    Player.UniversalChat("%1[%eConsole%1]%0:%f " + input); //cant get the first color code to work
                }
            }
        }
	}
}
