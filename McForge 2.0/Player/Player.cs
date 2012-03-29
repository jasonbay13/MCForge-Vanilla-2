﻿/*
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
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Text;

namespace McForge
{
	public class Player
	{
		#region Variables
		internal static System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
		internal static MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
		protected static packet pingPacket = new packet(new byte[1] { 1 });
		protected static packet mapSendStartPacket = new packet(new byte[1] { 2 });
		private static byte ForceTpCounter = 0;

		protected static packet MOTD_NonAdmin = new packet();
		protected static packet MOTD_Admin = new packet();
		protected void CheckMotdPackets()
		{
			if (MOTD_NonAdmin.bytes == null)
			{
				MOTD_NonAdmin.Add((byte)0);
				MOTD_NonAdmin.Add(ServerSettings.version);
				MOTD_NonAdmin.Add(ServerSettings.NAME, 64);
				MOTD_NonAdmin.Add(ServerSettings.MOTD, 64);
				MOTD_NonAdmin.Add((byte)0);
				MOTD_Admin = MOTD_NonAdmin;
				MOTD_Admin.bytes[130] = 100;
			}
		}

		protected Socket socket;

		protected byte lastPacket = 0;

		/// <summary>
		/// The players real username
		/// </summary>
		public string USERNAME;
		protected string _username; //Lowercase Username
		/// <summary>
		/// This is the players LOWERCASE username, use this for comparison instead of calling USERNAME.ToLower()
		/// </summary>
		public string username //Lowercase Username feild
		{
			get
			{
				if (_username == null) _username = USERNAME.ToLower();
				return _username;
			}
		}
		/// <summary>
		/// This is the players IP Address
		/// </summary>
		public string ip;

		protected System.Timers.Timer loginTimer = new System.Timers.Timer(30000);
		protected System.Timers.Timer pingTimer = new System.Timers.Timer(1000);
		protected byte[] buffer = new byte[0];
		protected byte[] tempBuffer = new byte[0xFF];
		protected string tempString = null;
		protected byte tempByte = 0xFF;

		/// <summary>
		/// True if the player is currently loading a map
		/// </summary>
		public bool isLoading = true;
		/// <summary>
		/// True if the player is Online (false if the player has disconnected
		/// </summary>
		public bool isOnline = true;
		/// <summary>
		/// True if the player has completed the login process
		/// </summary>
		public bool isLoggedIn = false;

		/// <summary>
		/// This players current level
		/// </summary>
		public Level level = Server.Mainlevel;
		/// <summary>
		/// The players MC Id, this changes each time the player logs in
		/// </summary>
		public byte id;
		/// <summary>
		/// The players current position
		/// </summary>
		public Point3 Pos;
		/// <summary>
		/// The players last known position
		/// </summary>
		public Point3 oldPos;
		/// <summary>
		/// The players current rotation
		/// </summary>
		public byte[] Rot;
		/// <summary>
		/// The players last known rotation
		/// </summary>
		public byte[] oldRot;
		/// <summary>
		/// The players COLOR
		/// </summary>
		public string color = Colors.navy;
		/// <summary>
		/// True if this player is hidden
		/// </summary>
		public bool isHidden = false;

		/// <summary>
		/// True if this player is an admin
		/// </summary>
		public bool isAdmin = true;

		object PassBackData;
		/// <summary>
		/// This delegate is used for when a command wants to be activated the first time a player places a block
		/// </summary>
		/// <param name="p">This is a player object</param>
		/// <param name="x">The position of the block that was changed (x)</param>
		/// <param name="z">The position of the block that was changed (z)</param>
		/// <param name="y">The position of the block that was changed (y)</param>
		/// <param name="newType">The type of block the user places (air if user is deleting)</param>
		/// <param name="placing">True if the player is placing a block</param>
		/// <param name="PassBack">A passback object that can be used for a command to send data back to itself for use</param>
		public delegate void BlockChangeDelegate(Player p, ushort x, ushort z, ushort y, byte newType, bool placing, object PassBack);
		/// <summary>
		/// This delegate is used for when a command wants to be activated the next time the player sends a message.
		/// </summary>
		/// <param name="p">The player object</param>
		/// <param name="message">The string the player sent</param>
		/// <param name="PassBack">A passback object that can be used for a command to send data back to itself for use</param>
		public delegate void NextChatDelegate(Player p, string message, object PassBack);
		protected BlockChangeDelegate blockChange;
		protected NextChatDelegate nextChat;

		#endregion

		internal Player(TcpClient TcpClient)
		{
			CheckMotdPackets();
			try
			{

				socket = TcpClient.Client;

				ip = socket.RemoteEndPoint.ToString().Split(':')[0];
				Server.Log("[System]: " + ip + " connected", ConsoleColor.Gray, ConsoleColor.Black);

				CheckMultipleConnections();
				if (CheckIfBanned()) return;

				socket.BeginReceive(tempBuffer, 0, tempBuffer.Length, SocketFlags.None, new AsyncCallback(Incoming), this);

				loginTimer.Elapsed += delegate { HandleConnect(); };
				loginTimer.Start();
				
				pingTimer.Elapsed += delegate { SendPing(); };
				pingTimer.Start();

				Server.Connections.Add(this);
			}
			catch (Exception e)
			{
				SKick("There has been an Error.");
				Server.Log(e);
			}
		}

		#region Incoming Data
		protected void HandleConnect()
		{
			if (!isLoading)
			{
				loginTimer.Stop();
				foreach (string w in ServerSettings.WelcomeText) SendMessage(w);
			}
		}
		protected static void Incoming(IAsyncResult result)
		{
			while (!Server.Started)
				Thread.Sleep(100);

			Player p = (Player)result.AsyncState;

			if (!p.isOnline)
				return;

			try
			{
				int length = p.socket.EndReceive(result);
				if (length == 0) { p.CloseConnection(); return; }
				byte[] b = new byte[p.buffer.Length + length];
				Buffer.BlockCopy(p.buffer, 0, b, 0, p.buffer.Length);
				Buffer.BlockCopy(p.tempBuffer, 0, b, p.buffer.Length, length);
				p.buffer = p.HandlePacket(b);
				p.socket.BeginReceive(p.tempBuffer, 0, p.tempBuffer.Length, SocketFlags.None, new AsyncCallback(Incoming), p);
			}
			catch (SocketException e)
			{
				SocketException rawr = e;
				p.CloseConnection();
				return;
			}
			catch (Exception e)
			{
				Exception rawr = e;
				p.Kick("Error!");
				Server.Log(e);
				return;
			}
		}
		protected byte[] HandlePacket(byte[] buffer)
		{
			try
			{
				int length = 0; byte msg = buffer[0];
				// Get the length of the message by checking the first byte
				switch (msg)
				{
					case 0: length = 130; break; // login
					case 2: SMPKick("This is not an SMP Server!"); break; // login??
					case 5: length = 8; break; // blockchange
					case 8: length = 9; break; // input
					case 13: length = 65; break; // chat
					default: Kick("Unhandled message id \"" + msg + "\"!"); return new byte[0];
				}
				if (buffer.Length > length)
				{
					byte[] message = new byte[length];
					Buffer.BlockCopy(buffer, 1, message, 0, length);

					byte[] tempbuffer = new byte[buffer.Length - length - 1];
					Buffer.BlockCopy(buffer, length + 1, tempbuffer, 0, buffer.Length - length - 1);

					buffer = tempbuffer;

					ThreadPool.QueueUserWorkItem(delegate
					{
						switch (msg)
						{
							case 0: HandleLogin(message); break;
							case 5: HandleBlockchange(message); break;
							case 8: HandleIncomingPos(message); break;
							case 13: HandleChat(message); break;
						}
					});

					if (buffer.Length > 0)
						buffer = HandlePacket(buffer);
					else
						return new byte[0];
					
					
				}
			}
			catch (Exception e)
			{
				Kick("CONNECTION ERROR: (0x03)");
				Server.Log("[ERROR]: PLAYER MESSAGE RECIEVE ERROR (0x03)", ConsoleColor.Red, ConsoleColor.Black);
				Server.Log(e);
			}
			return buffer;
		}

		protected void HandleLogin(byte[] message)
		{
			try
			{
				if (isLoggedIn) return;
				byte version = message[0];
				USERNAME = enc.GetString(message, 1, 64).Trim();
				string verify = enc.GetString(message, 65, 32).Trim();
				byte type = message[129];
				if (!VerifyAccount(USERNAME, verify)) return;
				if (version != ServerSettings.version) { SKick("Wrong Version!."); return; }

				//TODO Database Stuff

				Server.Log("[System]: " + ip + " logging in as " + USERNAME + ".", ConsoleColor.Green, ConsoleColor.Black);

				CheckDuplicatePlayers(USERNAME);

				SendMotd();

				isLoading = true;
				SendMap();
				if (!isOnline) return;
				isLoggedIn = true;

				id = FreeId();

				UpgradeConnectionToPlayer();

				//Do we want the same-ip-new-account code?

				//ushort x = (ushort)((0.5 + level.SpawnPos.x) * 32);
				//ushort y = (ushort)((1 + level.SpawnPos.y) * 32);
				//ushort z = (ushort)((0.5 + level.SpawnPos.z) * 32);

				short x = (short)((0.5 + level.SpawnPos.x) * 32);
				short y = (short)((1 + level.SpawnPos.y) * 32);
				short z = (short)((0.5 + level.SpawnPos.z) * 32);

				//x = (ushort)Math.Abs(x);
				//y = (ushort)Math.Abs(y);
				//z = (ushort)Math.Abs(z);
				
				Pos = new Point3(x, z, y);
				Rot = level.SpawnRot;
				oldPos = Pos;
				oldRot = Rot;

				SpawnThisPlayerToOtherPlayers();
				SpawnOtherPlayersForThisPlayer();
				SendSpawn(this);

				isLoading = false;

			}
			catch (Exception e)
			{
				Server.Log(e);
			}
		}
		protected void HandleBlockchange(byte[] message)
		{
			if (!isLoggedIn) return;

			ushort x = packet.NTHO(message, 0);
			ushort y = packet.NTHO(message, 2);
			ushort z = packet.NTHO(message, 4);
			byte action = message[6];
			byte newType = message[7];

			if (newType > 49 || (newType == 7 && !isAdmin))
			{
				Kick("HACKED CLIENT!");
				//TODO Send message to op's for adminium hack
				return;
			}
			
			byte currentType = level.GetBlock(x, z, y);
			if (currentType == (byte)Blocks.Types.zero)
			{
				Kick("HACKED CLIENT!");
				return;
			}

			//TODO Check for permissions to build and distance > max

			if (blockChange != null)
			{
				SendBlockChange(x, z, y, currentType);
				bool placing = false;
				if (action == 1) placing = true;
				ThreadPool.QueueUserWorkItem(delegate {
					blockChange.Invoke(this, x, z, y, newType, placing, PassBackData);
					blockChange = null;
					PassBackData = null;
				});
				

				return;
			}

			if (action == 0) //Deleting
			{
				level.BlockChange(x, z, y, (byte)Blocks.Types.air);
			}
			else //Placing
			{
				level.BlockChange(x, z, y, newType);
			}
		}
		protected void HandleIncomingPos(byte[] message)
		{
			if (!isLoggedIn)
				return;

			byte thisid = message[0];

			if (thisid != 0xFF && thisid != id && thisid != 0)
			{
				//TODO Player.GlobalMessageOps("Player sent a malformed packet!");
				Kick("Hacked Client!");
				return;
			}

			ushort x = packet.NTHO(message, 1);
			ushort y = packet.NTHO(message, 3);
			ushort z = packet.NTHO(message, 5);
			byte rotx = message[7];
			byte roty = message[8];
			Pos.x = (short)x;
			Pos.y = (short)y;
			Pos.z = (short)z;
			Rot = new byte[2] { rotx, roty };
		}
		protected void HandleChat(byte[] message)
		{
			if (!isLoggedIn) return;

			string incomingText = enc.GetString(message, 1, 64).Trim();

			byte incomingID = message[0];
			if (incomingID != 0xFF && incomingID != id && incomingID != 0)
			{
				//TODO Player.GlobalMessageOps("Player sent a malformed packet!");
				Kick("Hacked Client!");
				return;
			}

			incomingText = Regex.Replace(incomingText, @"\s\s+", " ");
			foreach (char ch in incomingText)
			{
				if (ch < 32 || ch >= 127 || ch == '&')
				{
					Kick("Illegal character in chat message!");
					return;
				}
			}
			if (incomingText.Length == 0)
				return;

			//Get rid of whitespace
			while (incomingText.Contains("  "))
				incomingText.Replace("  ", " ");

			if (incomingText[0] == '/')
			{
				incomingText = incomingText.Remove(0, 1);

				string[] args = incomingText.Split(' ');

				HandleCommand(args);

				return;
			}

			if (nextChat != null)
			{
				ThreadPool.QueueUserWorkItem(delegate { nextChat.Invoke(this, incomingText, PassBackData); });
				nextChat = null;
				PassBackData = null;

				return;
			}

			Server.Log("<" + USERNAME + "> " + incomingText);

			UniversalChat(USERNAME + ": &f" + incomingText);
		}
		#endregion
		#region Outgoing Packets
		protected void SendPacket(packet pa)
		{
			try
			{
				lastPacket = pa.bytes[0];
			}
			catch (Exception e) { Server.Log(e); }
			for (int i = 0; i < 3; i++)
			{
				try
				{
					socket.BeginSend(pa.bytes, 0, pa.bytes.Length, SocketFlags.None, delegate(IAsyncResult result) { }, null);
					return;
				}
				catch
				{
					continue;
				}
			}
			CloseConnection();
		}

		protected void SendMessage(byte PlayerID, string message)
		{
			packet pa = new packet();

			for (int i = 0; i < 10; i++)
			{
				message = message.Replace("%" + i, "&" + i);
				message = message.Replace("&" + i + " &", "&");
			}
			for (char ch = 'a'; ch <= 'f'; ch++)
			{
				message = message.Replace("%" + ch, "&" + ch);
				message = message.Replace("&" + ch + " &", "&");
			}
			for (int i = 0; i < 10; i++)
			{
				message = message.Replace("%" + i, "&" + i);
				message = message.Replace("&" + i + " &", "&");
			}
			for (char ch = 'a'; ch <= 'f'; ch++)
			{
				message = message.Replace("%" + ch, "&" + ch);
				message = message.Replace("&" + ch + " &", "&");
			}

			pa.Add((byte)13);
			pa.Add(PlayerID);

			try
			{
				foreach (string line in LineWrapping(message))
				{
					if (pa.bytes.Length < 64)
						pa.Add(line, 64);
					else
						pa.Set(2, line, 64);

					SendPacket(pa);
				}
			}
			catch (Exception e)
			{
				Server.Log(e);
			}

		}
		protected void SendMotd()
		{
			if (isAdmin) SendPacket(MOTD_Admin);
			else SendPacket(MOTD_NonAdmin);
		}
		protected void SendMap()
		{
			try
			{
				SendPacket(mapSendStartPacket); //Send the pre-fab map start packet

				packet pa = new packet(); //Create a packet to handle the data for the map
				pa.Add(level.TotalBlocks); //Add the total amount of blocks to the packet
				byte[] blocks = new byte[level.TotalBlocks]; //Temporary byte array so we dont have to keep modding the packet array

				byte block; //A block byte outside the loop, we save cycles by not making this for every loop iteration
				level.ForEachBlock(delegate(int pos)
				{
					//Here we loop through the whole map and check/convert the blocks as necesary
					//We then add them to our blocks array so we can send them to the player
					block = level.data[pos];
                    if (block < 50) blocks[pos] = block;
                    else if (Blocks.CustomBlocks.ContainsKey(block))
                        blocks[pos] = Blocks.CustomBlocks[block].VisibleType;
				});

				pa.Add(blocks); //add the blocks to the packet
				pa.GZip(); //GZip the packet

				int number = (int)Math.Ceiling(((double)(pa.bytes.Length)) / 1024); //The magic number for this packet

				for (int i = 1; pa.bytes.Length > 0; ++i)
				{
					short length = (short)Math.Min(pa.bytes.Length, 1024);
					byte[] send = new byte[1027];
					packet.HTNO(length).CopyTo(send, 0);
					Buffer.BlockCopy(pa.bytes, 0, send, 2, length);
					byte[] tempbuffer = new byte[pa.bytes.Length - length];
					Buffer.BlockCopy(pa.bytes, length, tempbuffer, 0, pa.bytes.Length - length);
					pa.bytes = tempbuffer;
					send[1026] = (byte)(i * 100 / number);

					packet Send = new packet(send);
					Send.AddStart(new byte[1] { 3 });

					SendPacket(Send);
				}

				pa = new packet();
				pa.Add((byte)4);
				pa.Add((short)level.Size.x);
				pa.Add((short)level.Size.y);
				pa.Add((short)level.Size.z);
				SendPacket(pa);

				isLoading = false;
			}
			catch (Exception e)
			{
				Server.Log(e);
			}
		}
		protected void SendSpawn(Player p)
		{
			byte ID = 0xFF;
			if (p != this)
				ID = p.id;

			packet pa = new packet();
			pa.Add((byte)7);
			pa.Add((byte)ID);
			pa.Add(p.USERNAME, 64);
			pa.Add(p.Pos.x);
			pa.Add(p.Pos.y);
			pa.Add(p.Pos.z);
			pa.Add(p.Rot);
			SendPacket(pa);
		}
		//protected void SendDie(byte id)
		//{
		//    packet pa = new packet(new byte[2] { 12, id });
		//}
		protected void SendBlockChange(ushort x, ushort z, ushort y, byte type)
		{
			if (x < 0 || y < 0 || z < 0 || x >= level.Size.x || y >= level.Size.y || z >= level.Size.z) return;

			packet pa = new packet();
			pa.Add((byte)6);
			pa.Add(x);
			pa.Add(y);
			pa.Add(z);

			if (type > 49) type = Blocks.CustomBlocks[type].VisibleType;
			pa.Add(type);

			SendPacket(pa);
		}
		protected void SendKick(string message)
		{
			packet pa = new packet();
			pa.Add((byte)14);
			pa.Add(message, 64);
			SendPacket(pa);
		}
		protected void SMPKick(string a)
		{
			//TODO SMPKICK
		}
		protected void SendPing()
		{
			SendPacket(pingPacket);
		}

		/// <summary>
		/// Send this player a message
		/// </summary>
		/// <param name="message">The message to send</param>
		public void SendMessage(string message)
		{
			SendMessage(0xFF, message);
		}
		/// <summary>
		/// Exactly what the function name is, it might be useful to change this players pos first ;)
		/// </summary>
		public void SendThisPlayerTheirOwnPos()
		{
			packet pa = new packet();
			pa.Add((byte)8);
			pa.Add(Pos.x);
			pa.Add(Pos.y);
			pa.Add(Pos.z);
			pa.Add(Rot);
			SendPacket(pa);
		}
		/// <summary>
		/// Kick this player with the specified message, the message broadcasts across the server
		/// </summary>
		/// <param name="message">The message to send</param>
		public void Kick(string message)
		{
			//GlobalMessage(message);
			SKick(message);
		}
		/// <summary>
		/// Kick this player with a specified message, this message will only get sent to op's
		/// </summary>
		/// <param name="message">The message to send</param>
		public void SKick(string message)
		{
			Server.Log("[Info]: Kicked: *" + USERNAME + "* " + message, ConsoleColor.Yellow, ConsoleColor.Black);
			SendKick(message);
			//CloseConnection();
		}

		protected void UpdatePosition(bool ForceTp)
		{
			byte changed = 0;   //Denotes what has changed (x,y,z, rotation-x, rotation-y)

			int diffX = Pos.x - oldPos.x;
			int diffZ = Pos.z - oldPos.z;
			int diffY = Pos.y - oldPos.y;
			int diffR0 = Rot[0] - oldRot[0];
			int diffR1 = Rot[1] - oldRot[1];

			if (ForceTp) changed = 4;
			else
			{
				//TODO rewrite local pos change code
				if (diffX == 0 && diffY == 0 && diffZ == 0 && diffR0 == 0 && diffR1 == 0)
				{
					return; //No changes
				}
				if (Math.Abs(diffX) > 100 || Math.Abs(diffY) > 100 || Math.Abs(diffZ) > 100)
				{
					changed = 4; //Teleport Required
				}
				else if (diffR0 == 0 && diffR1 == 0)
				{
					changed = 1; //Pos Update Required
				}
				else
				{
					changed += 2; //Rot Update Required

					if (diffX != 0 || diffY != 0 || diffZ != 0)
					{
						changed += 1;
					}
				}
			}

			oldPos = Pos; oldRot = Rot;
			packet pa = new packet();

			switch (changed)
			{
				case 1: //Pos Change
					pa.Add((byte)10);
					pa.Add(id);
					pa.Add((sbyte)(diffX));
					pa.Add((sbyte)(diffY));
					pa.Add((sbyte)(diffZ));
					break;
				case 2: //Rot Change
					pa.Add((byte)11);
					pa.Add(id);
					//pa.Add(new byte[2] { 128, 128 });
					pa.Add(Rot);
					break;
				case 3: //Pos AND Rot Change
					pa.Add((byte)9);
					pa.Add(id);
					pa.Add((sbyte)(Pos.x - oldPos.x));
					pa.Add((sbyte)(Pos.y - oldPos.y));
					pa.Add((sbyte)(Pos.z - oldPos.z));
					//pa.Add(new byte[2] { 128, 128 });
					pa.Add(Rot);
					break;
				case 4: //Teleport Required
					pa.Add((byte)8);
					pa.Add(id);
					pa.Add(Pos.x);
					pa.Add(Pos.y);
					pa.Add(Pos.z);
					//pa.Add(new byte[2] { 128, 128 });
					pa.Add(Rot);
					break;
			}

			
			foreach (Player p in Server.Players.ToArray())
			{
				if (p != this && p.level == level && p.isLoggedIn && !p.isLoading)
				{
					p.SendPacket(pa);
				}
			}
		}
		#endregion

		#region Special Chat Handlers
		protected void HandleCommand(string[] args)
		{
			string[] sendArgs = new string[0];
			if (args.Length > 1)
			{
				sendArgs = new string[args.Length - 1];
				args.CopyTo(sendArgs, 1);
			}

			string name = args[0].ToLower().Trim();
			if (Command.Commands.ContainsKey(name))
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					ICommand cmd = Command.Commands[name];
					cmd.Use(this, sendArgs);
				});
			}

			foreach (string s in Command.Commands.Keys)
			{
				Console.WriteLine(args[0]);
				Console.WriteLine("'" + s + "'");
			}
		}
		#endregion

		#region Global and Universal shit
		internal static void GlobalUpdate()
		{
			//Player update code
			ForceTpCounter++;
			foreach (Player p in Server.Players.ToArray())
			{
				if (ForceTpCounter == 100) { if (!p.isHidden) p.UpdatePosition(true); }
				else { if (!p.isHidden) p.UpdatePosition(false); }
					
			}
		}
		internal static void GlobalBlockchange(Level l, ushort x, ushort z, ushort y, byte block)
		{
			foreach (Player p in Server.Players.ToArray())
			{
				if (p.level == l)
					p.SendBlockChange(x, z, y, block);
			}
		}
		/// <summary>
		/// Kill this player for everyone.
		/// </summary>
		public void GlobalDie()
		{
			packet pa = new packet(new byte[2] { 12, id });
			foreach (Player p in Server.Players.ToArray())
			{
				if (p != this)
				{
					p.SendPacket(pa);
				}
			}
		}
		/// <summary>
		/// Send a message to everyone, on every world
		/// </summary>
		/// <param name="text">The message to send.</param>
		public static void UniversalChat(string text)
		{
			foreach (Player p in Server.Players.ToArray())
			{
				p.SendMessage(text);
			}
		}
		#endregion

		#region PluginStuff
		public void CatchNextBlockchange(BlockChangeDelegate change, object data)
		{
			PassBackData = data;
			nextChat = null;
			blockChange = change;
		}
		public void CatchNextChat(NextChatDelegate chat, object data)
		{
			PassBackData = data;
			blockChange = null;
			nextChat = chat;
		}
		#endregion

		protected static List<string> LineWrapping(string message)
		{
			List<string> lines = new List<string>();
			message = Regex.Replace(message, @"(&[0-9a-f])+(&[0-9a-f])", "$2");
			message = Regex.Replace(message, @"(&[0-9a-f])+$", "");
			int limit = 64; string color = "";
			while (message.Length > 0)
			{
				if (lines.Count > 0) { message = "> " + color + message.Trim(); }
				if (message.Length <= limit) { lines.Add(message); break; }
				for (int i = limit - 1; i > limit - 9; --i)
				{
					if (message[i] == ' ')
					{
						lines.Add(message.Substring(0, i)); goto Next;
					}
				}
				lines.Add(message.Substring(0, limit));
				Next: message = message.Substring(lines[lines.Count - 1].Length);
				if (lines.Count == 1)
				{
					limit = 60;
				}
				int index = lines[lines.Count - 1].LastIndexOf('&');
				if (index != -1)
				{
					if (index < lines[lines.Count - 1].Length - 1)
					{
						char next = lines[lines.Count - 1][index + 1];
						if ("0123456789abcdef".IndexOf(next) != -1) { color = "&" + next; }
						if (index == lines[lines.Count - 1].Length - 1)
						{
							lines[lines.Count - 1] = lines[lines.Count - 1].
								Substring(0, lines[lines.Count - 1].Length - 2);
						}
					}
					else if (message.Length != 0)
					{
						char next = message[0];
						if ("0123456789abcdef".IndexOf(next) != -1)
						{
							color = "&" + next;
						}
						lines[lines.Count - 1] = lines[lines.Count - 1].
							Substring(0, lines[lines.Count - 1].Length - 1);
						message = message.Substring(1);
					}
				}
			} return lines;
		}

		protected void SpawnThisPlayerToOtherPlayers()
		{
			foreach (Player p in Server.Players.ToArray())
			{
				if (p == this) continue;
				p.SendSpawn(this);
			}
		}
		protected void SpawnOtherPlayersForThisPlayer()
		{
			foreach (Player p in Server.Players)
			{
				if (p == this) continue;
				SendSpawn(p);
			}
		}

		protected void CloseConnection()
		{
			isLoggedIn = false;
			isOnline = false;

			GlobalDie();

			Server.Log("[System]: " + USERNAME + " Has DC'ed (" + lastPacket + ")", ConsoleColor.Gray, ConsoleColor.Black);

			pingTimer.Stop();

			Server.Players.Remove(this);
			Server.Connections.Remove(this);
			
			socket.Close();
		}

		protected byte FreeId()
		{
			if (Server.Players.Count == 0) return 0;
			for (byte i = 0; i < ServerSettings.MaxPlayers; ++i)
				foreach (Player p in Server.Players)
					if (p.id == i) continue;
					else return i;
			
			unchecked { return (byte)-1; }
		}
		protected void UpgradeConnectionToPlayer()
		{
			try
			{
				Server.Connections.Remove(this);
				Server.Players.Add(this);
			}
			catch (Exception e)
			{
				Server.Log(e);
			}
			//TODO Update form list
		}

		#region Verification Stuffs
		protected void CheckMultipleConnections()
		{
			foreach (Player p in Server.Connections.ToArray())
			{
				if (p.ip == ip && p != this)
				{
					p.Kick("Only one half open connection is allowed per IP address.");
				}
			}
		}
		protected static void CheckDuplicatePlayers(string username)
		{
			foreach (Player p in Server.Players.ToArray())
			{
				if (p.username == username)
				{
					p.Kick("You have logged in elsewhere!");
				}
			}
		}
		protected bool CheckIfBanned()
		{
			if (Server.BannedIP.Contains(ip)) { Kick("You're Banned!"); return true; }
			return false;
		}
		protected bool VerifyAccount(string name, string verify)
		{
			if (ServerSettings.VerifyAccounts && ip != "127.0.0.1")
			{
				if (Server.Players.Count >= ServerSettings.MaxPlayers) { SKick("Server is full, please try again later!"); return false; }

				if (verify == null || verify == "" || verify == "--" || (verify != BitConverter.ToString(md5.ComputeHash(enc.GetBytes(ServerSettings.salt + name))).Replace("-", "").ToLower().TrimStart('0') && verify != BitConverter.ToString(md5.ComputeHash(enc.GetBytes(ServerSettings.password + name))).Replace("-", "").ToLower().TrimStart('0')))
				{
					SKick("Account could not be verified, try again.");
					//Server.Log("'" + verify + "' != '" + BitConverter.ToString(md5.ComputeHash(enc.GetBytes(ServerSettings.salt + name))).Replace("-", "").ToLower().TrimStart('0') + "'");
					return false;
				}
			}
			if (name.Length > 16 || !ValidName(name)) { SKick("Illegal name!"); return false; } //Illegal Name Kick
			return true;
		}
		/// <summary>
		/// Check to see is a given name is valid
		/// </summary>
		/// <param name="name">the name to check</param>
		/// <returns>returns true if name is valid</returns>
		public static bool ValidName(string name)
		{
			string allowedchars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz01234567890._";
			foreach (char ch in name) { if (allowedchars.IndexOf(ch) == -1) { return false; } } return true;
		}
		#endregion
		
	}

	public struct packet
	{
		public byte[] bytes;

		#region Constructors
		public packet(byte[] data)
		{
			bytes = data;
		}
		public packet(packet p)
		{
			bytes = p.bytes;
		}
		#endregion
		#region Adds
		public void AddStart(byte[] data)
		{
			byte[] temp = bytes;

			bytes = new byte[temp.Length + data.Length];

			data.CopyTo(bytes, 0);
			temp.CopyTo(bytes, data.Length);
		}

		public void Add(byte[] data)
		{
			if (bytes == null)
			{
				bytes = data;
			}
			else
			{
				byte[] temp = bytes;

				bytes = new byte[temp.Length + data.Length];

				temp.CopyTo(bytes, 0);
				data.CopyTo(bytes, temp.Length);
			}
		}
		public void Add(sbyte a)
		{
			Add(new byte[1] { (byte)a });
		}
		public void Add(byte a)
		{
			Add(new byte[1] { a });
		}
		public void Add(short a)
		{
			Add(HTNO(a));
		}
		public void Add(ushort a)
		{
			Add(HTNO(a));
		}
		public void Add(int a)
		{
			Add(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(a)));
		}
		public void Add(string a)
		{
			Add(a, a.Length);
		}
		public void Add(string a, int size)
		{
			Add(Player.enc.GetBytes(a.PadRight(size).Substring(0, size)));
		}
		#endregion
		#region Sets
		public void Set(int offset, short a)
		{
			HTNO(a).CopyTo(bytes, offset);
		}
		public void Set(int offset, ushort a)
		{
			HTNO(a).CopyTo(bytes, offset);
		}
		public void Set(int offset, string a, int length)
		{
			Player.enc.GetBytes(a.PadRight(length).Substring(0, length)).CopyTo(bytes, offset);
		}
		#endregion

		public void GZip()
		{
			System.IO.MemoryStream ms = new System.IO.MemoryStream();
			
			GZipStream gs = new GZipStream(ms, CompressionMode.Compress, true);
			gs.Write(bytes, 0, bytes.Length);
			gs.Close(); 
			gs.Dispose();
			
			ms.Position = 0; 
			bytes = new byte[ms.Length];
			ms.Read(bytes, 0, (int)ms.Length);
			ms.Close();
			ms.Dispose();
		}

		#region == Host <> Network ==
		public static byte[] HTNO(ushort x)
		{
			byte[] y = BitConverter.GetBytes(x); Array.Reverse(y); return y;
		}
		public static ushort NTHO(byte[] x, int offset)
		{
			byte[] y = new byte[2];
			Buffer.BlockCopy(x, offset, y, 0, 2); Array.Reverse(y);
			return BitConverter.ToUInt16(y, 0);
		}
		public static byte[] HTNO(short x)
		{
			byte[] y = BitConverter.GetBytes(x); Array.Reverse(y); return y;
		}
		#endregion

		public enum types
		{
			Message = 13,
			MOTD = 0,
			MapStart = 2,
			MapData = 3,
			MapEnd = 4,
			SendSpawn = 7,
			SendDie = 12,
			SendBlockchange = 6,
			SendKick = 14,
			SendPing = 1,

			SendPosChange = 10,
			SendRotChange = 11,
			SendPosANDRotChange = 9,
			SendTeleport = 8,

		}
	}
}
