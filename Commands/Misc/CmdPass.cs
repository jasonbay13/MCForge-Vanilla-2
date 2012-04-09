﻿/*
Copyright 2012 MCForge
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
using MCForge.Interface.Command;
using MCForge.Entity;
using MCForge.Core;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Text;

namespace CommandDll.Misc
{
    class CmdPass : ICommand
    {
        public string Name { get { return "Pass"; } }
        public CommandTypes Type { get { return CommandTypes.Misc; } }
        public string Author { get { return "Sinjai"; } }
        public Version Version { get { return new Version(1,0); } }
        public string CUD { get { return ""; } }
        public byte Permission { get { return 0; } }
        public static bool gotpass = false;
        public static string password = "";
        public void Use(Player p, string[] args)
        {
            if (p.verified) { p.SendMessage("You already verified!"); return; }
            if (!Server.Verifying) { p.SendMessage("You don't need to verify!"); return; }
            if (p.group.permission < Server.VerifyGroup.permission) { p.SendMessage("Only " + Server.VerifyGroup.color + Server.VerifyGroup.name + "s " + Server.DefaultColor + "and above need to verify."); return; }
            #region *
            if (p.passtries == 3) { p.Kick("Did you really think you could keep on guessing?"); return; }
            int foundone = 0;
            if (args[0] == "") { Help(p); return; }
            int number = args.Length;
            if (number > 1)
            {
                p.SendMessage("Your password must be &cone " + Server.DefaultColor + "word!");
                return;
            }
            if (!Directory.Exists("extra/passwords"))
            {
                p.SendMessage("You have not &cset a password" + Server.DefaultColor + ", use &a/setpass [Password] " + Server.DefaultColor + "to set one!");
                return;
            }
            DirectoryInfo di = new DirectoryInfo("extra/passwords/");
            FileInfo[] fi = di.GetFiles("*.xml");
            Thread.Sleep(10);
            try
            {
                foreach (FileInfo file in fi)
                {
                    if (file.Name.Replace(".xml", "") == p.Username)
                    {
                        foundone++;
                    }
                }
            }
            catch
            {
                p.SendMessage("An Error Occurred! Try again soon!");
                return;
            }
            if (foundone < 0)
            {
                p.SendMessage("You have not &cset a password, " + Server.DefaultColor + "use &a/setpass [Password] &cto set one!");
                return;
            }
            if (foundone > 1)
            {
                p.SendMessage("&cAn error has occurred!");
                return;
            }
            if (!File.Exists("extra/passwords/" + p.Username + ".xml"))
            {
                p.SendMessage("You have not &cset a password, " + Server.DefaultColor + "use &a/setpass [Password] &cto set one!");
                return;
            }
            Crypto.DecryptStringAES(File.ReadAllText("extra/passwords/" + p.Username + ".xml"), "MCForgeEncryption", p, args[0]);
            if (args[0] == password)
            {
                p.SendMessage("Thank you, " + p.color + p.Username + Server.DefaultColor + "! You have now &averified " + Server.DefaultColor + "and have &aaccess to admin commands and features!");
                if (p.verified == false) { p.verified = true; }
                password = "";
                p.passtries = 0;
                return;
            }
            p.passtries++;
            p.SendMessage("&cIncorrect Password. " + Server.DefaultColor + "Remember your password is &ccase sensitive!");
            p.SendMessage("If you have &cforgotten your password, " + Server.DefaultColor + "contact the server host and they can reset it! &cIncorrect " + Server.DefaultColor + "Tries: &b" + p.passtries);
            return;
        }
        public class Crypto
        {
           
            // This is the base encryption salt! DO NOT CHANGE IT!!!
            private static byte[] _salt = Encoding.ASCII.GetBytes("o6806642kbM7c5");
            /// <summary>
            /// Encrypt the given string using AES.  The string can be decrypted using 
            /// DecryptStringAES().  The sharedSecret parameters must match.
            /// </summary>
            /// <param name="plainText">The text to encrypt.</param>
            /// <param name="sharedSecret">A password used to generate a key for encryption.</param>
            /// <summary>
            /// Decrypt the given string.  Assumes the string was encrypted using 
            /// EncryptStringAES(), using an identical sharedSecret.
            /// </summary>
            /// <param name="cipherText">The text to decrypt.</param>
            /// <param name="sharedSecret">A password used to generate a key for decryption.</param>
            public static string DecryptStringAES(string cipherText, string sharedSecret, Player who, string triedpass)
            {
                if (string.IsNullOrEmpty(cipherText))
                    throw new ArgumentNullException("cipherText");
                if (string.IsNullOrEmpty(sharedSecret))
                    throw new ArgumentNullException("sharedSecret");

                // Declare the RijndaelManaged object
                // used to decrypt the data.
                RijndaelManaged aesAlg = null;

                // Declare the string used to hold
                // the decrypted text.
                string plaintext = null;

                try
                {
                    // generate the key from the shared secret and the salt
                    Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                    // Create a RijndaelManaged object
                    // with the specified key and IV.
                    aesAlg = new RijndaelManaged();
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                    aesAlg.IV = key.GetBytes(aesAlg.BlockSize / 8);

                    // Create a decrytor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    // Create the streams used for decryption.                
                    byte[] bytes = Convert.FromBase64String(cipherText);
                    using (MemoryStream msDecrypt = new MemoryStream(bytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))

                                // Read the decrypted bytes from the decrypting stream
                                // and place them in a string.
                                plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
                finally
                {
                    // Clear the RijndaelManaged object.
                    if (aesAlg != null)
                        aesAlg.Clear();
                }
                password = plaintext;
                gotpass = true;
                return plaintext;
            }
            #endregion
        }
        public void Help(Player p)
        {
            p.SendMessage("/pass <password> - complete password verification");
        }
        public void Initialize()
        {
            Command.AddReference(this, new string[1] { "pass" });
        }
    }
}
