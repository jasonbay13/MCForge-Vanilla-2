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
using System.Collections.Generic;
using System.Linq;
using MCForge.Core;
using MCForge.Entity;

namespace MCForge.API.PlayerEvent
{
    public class OnPlayerMove : PlayerEvent
    {
		/// <summary>
		/// Creates a new event.  This is NOT meant to be used by user-code, only internally by events.
		/// </summary>
		/// <param name="callback">the method used for the delegate to callback upon event fire</param>
		/// <param name="target">The target Player we want the event for.</param>
		private OnPlayerMove(OnCall callback, Player target) : base(target) {
			_queue += callback;
		}

		public Vector3 oldPos { get; set; }

		/// <summary>
		/// This is meant to be called from the code where you mean for the event to happen.
		/// 
		/// In this case, it is called from the command processing code.
		/// </summary>
		/// <param name="p">The player that caused the event.</param>
		/// <param name="oldPos">The old position of the person.</param>
		internal static bool Call(Player p, Vector3 oldPos) {
			//Event was called from the code.
			List<OnPlayerMove> opcList = new List<OnPlayerMove>();
			//Do we keep or discard the event?
			_eventQueue.ForEach(opc => {
				if (opc.Player == null || opc.Player.Username == p.Username) {// We keep it
					//Set up variables, then fire all callbacks.
					opc.oldPos = oldPos;
					Player oldPlayer = opc.Player; 
					opc._target = p; // Set player that triggered event.
					opc._queue(opc); // fire callback
					opcList.Add(opc); // add to used list
					opc._target = oldPlayer;
				}
			});
			return opcList.Any(pe => pe.cancel); //Return if any canceled the event.
		}

		/// <summary>
		/// The delegate used for callbacks.  The caller will have this method run when the event fires.
		/// </summary>
		/// <param name="e">The Event that fired</param>
		public delegate void OnCall(OnPlayerMove e);

		/// <summary>
		/// The queue of delegates to call for the particular tag (One for each event)
		/// </summary>
		private OnCall _queue;

		/// <summary>
		/// The list of all events currently active of a PlayerEvent type.
		/// </summary>
		protected static List<OnPlayerMove> _eventQueue = new List<OnPlayerMove>(); // Same across all events of this kind

		/// <summary>
		/// Used to register a method to be executed when the event is fired.
		/// </summary>
		/// <param name="callback">The method to call</param>
		/// <param name="target">The player to watch for. (null for any players)</param>
		/// <returns>A reference to the event</returns>
		public static OnPlayerMove Register(OnCall callback, Player target) {
			//We add it to the list here
			OnPlayerMove pe = _eventQueue.Find(match => match.Player == null || match.Player.Username == target.Username);
			if (pe != null)
				//It already exists, so we just add it to the queue.
				pe._queue += callback;
			else {
				//Doesn't exist yet.  Make a new one.
				pe = new OnPlayerMove(callback, target);
				_eventQueue.Add(pe);
			}
			return pe;
		}

		/// <summary>
		/// Unregisters the sxpecified event
		/// </summary>
		/// <param name="pe">The event to unregister</param>
		public static void Unregister(OnPlayerMove pe) {
			pe.Unregister();
		}
		/// <summary>
		/// Unregisters the sxpecified event
		/// </summary>
		/// <param name="pe">The event to unregister</param>
		public override void Unregister() {
			_eventQueue.Remove(this);
		}
	}
}
