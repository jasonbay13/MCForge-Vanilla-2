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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace MCForge.Gui.Components {

    /// <summary>
    /// RichTextBox with integreted support for colors
    /// </summary>
    public partial class ColoredTextBox : RichTextBox {

        private List<string> wList;
        private const string END = "\\cf0\\par";

        /// <summary>
        /// RichTextBox with integreted support for colors
        /// </summary>
        /// <param name="container">IContainer to attach to</param>
        public ColoredTextBox(IContainer container) {
            container.Add(this);
            wList = new List<string>();
            InitializeComponent();
        }

        /// <summary>
        /// RichTextBox with integreted support for colors
        /// </summary>
        public ColoredTextBox() {
            InitializeComponent();
            wList = new List<string>();
        }

        private const string TheColorsOfTheRainbow =
          @"{\rtf1\ansi\ansicpg1252\deff0\deflang1033{\colortbl;\red0\green0\blue0;\red255\green255\blue0;\red0\green0\blue139;\red0\green100\blue0;\red0\green139\blue139;\red139\green0\blue139;\red128\green128\blue128;\red255\green215\blue0;\red169\green169\blue169;\red0\green0\blue255;\red0\green128\blue0;\red0\green255\blue255;\red255\green0\blue255;\red255\green255\blue255;\red170\green0\blue170;\red139\green0\blue0;}{\fonttbl{\f0\fnil\fcharset0 Calibri;}}\viewkind4\uc1\pard\f0\fs17";

        /// <summary>
        /// Writes the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Write(string message) {
            if(InvokeRequired) {
                Invoke((MethodInvoker)delegate { Write(message);});
                return;
            }

            message = message.Replace("\\", "\\'5c");

            if (!message.Contains('%') && !message.Contains('&')) {
                wList.Add(message + END);
                WriteAndScroll();
                return;
            }

            string[] messagesSplit = message.Split(new[] { '%', '&' }, StringSplitOptions.RemoveEmptyEntries);
            var coloredMessage = "";
            for(int i = 0; i < messagesSplit.Length; i++) {
                string split = messagesSplit[i];

                if (String.IsNullOrWhiteSpace(split))
                    continue;

                string color = GetColor(split[0]);

                if (color == null) {
                    coloredMessage += '&' + split;
                    continue;
                }

                coloredMessage += color + split.Substring(1);
            }
            wList.Add(coloredMessage + END);
            WriteAndScroll();
        }
        private void WriteAndScroll() {

            string newRtf = TheColorsOfTheRainbow;
            newRtf = wList.Aggregate(newRtf, (msg, s) => msg + s);
            newRtf += '}';
            Rtf = newRtf; //DIS THING

            this.Select(this.Text.Length - 1, 0);
            this.ScrollToCaret();
        }

        private string GetColor(char p) {
            switch (p) {
                case 'e': return "\\cf2";
                case '0': return "\\cf0";
                case '1': return "\\cf3";
                case '2': return "\\cf4";
                case '3': return "\\cf5";
                case '4': return "\\cf6";
                case '5': return "\\cf15";
                case '7': return "\\cf7";
                case '6': return "\\cf8";
                case '8': return "\\cf9";
                case '9': return "\\cf10";
                case 'a': return "\\cf11";
                case 'b': return "\\cf12";
                case 'c': return "\\cf16";
                case 'd': return "\\cf13";
                case 'f': return "\\cf14";
                default: return null;
            }
        }

    }
}