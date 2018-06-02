/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Collections.Generic;

namespace MapleLib.WzLib.WzStructure.Data
{

    public static class Tables
    {
        public static Dictionary<string, string> PortalTypeNames = new Dictionary<string, string>() { 
            { "sp", "Start Point"},
            { "pi", "Invisible" },
            { "pv", "Visible" },
            { "pc", "Collision" },
            { "pg", "Changable" },
            { "pgi", "Changable Invisible" },
            { "tp", "Town Portal" },
            { "ps", "Script" },
            { "psi", "Script Invisible" },
            { "pcs", "Script Collision" },
            { "ph", "Hidden" },
            { "psh", "Script Hidden" },
            { "pcj", "Vertical Spring" },
            { "pci", "Custom Impact Spring" },
            { "pcig", "Unknown (PCIG)" }};

        public static string[] BackgroundTypeNames = new string[] {
            "Regular",
            "Horizontal Copies",
            "Vertical Copies",
            "H+V Copies",
            "Horizontal Moving+Copies",
            "Vertical Moving+Copies",
            "H+V Copies, Horizontal Moving",
            "H+V Copies, Vertical Moving"
        };
    }

    public static class WzConstants
    {
        public const int MinMap = 0;
        public const int MaxMap = 999999999;
    }

    public enum QuestState
    {
        Available = 0,
        InProgress = 1,
        Completed = 2
    }
    
}