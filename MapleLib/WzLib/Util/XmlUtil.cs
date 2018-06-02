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
using System.Collections;
using System.IO;
using System.Text;
using MapleLib.MapleCryptoLib;

namespace MapleLib.WzLib.Util
{
	public class XmlUtil
	{

		private static readonly char[] specialCharacters = {'"', '\'', '&', '<', '>'};
		private static readonly string[] replacementStrings = {"&quot;", "&apos;", "&amp;", "&lt;", "&gt;"};

		public static string SanitizeText(string text)
		{
			string fixedText = "";
			bool charFixed;
			for (int i = 0; i < text.Length; i++)
			{
				charFixed = false;
				for (int k = 0; k < specialCharacters.Length; k++)
				{

					if (text[i] == specialCharacters[k])
					{
						fixedText += replacementStrings[k];
						charFixed = true;
						break;
					}
				}
				if (!charFixed)
				{
					fixedText += text[i];
				}
			}
			return fixedText;
		}

		public static string OpenNamedTag(string tag, string name, bool finish)
		{
			return OpenNamedTag(tag, name, finish, false);
		}

		public static string EmptyNamedTag(string tag, string name)
		{
			return OpenNamedTag(tag, name, true, true);
		}

		public static string EmptyNamedValuePair(string tag, string name, string value)
		{
			return OpenNamedTag(tag, name, false, false) + Attrib("value", value, true, true);
		}

		public static string OpenNamedTag(string tag, string name, bool finish, bool empty)
		{
			return "<" + tag + " name=\"" + name + "\"" + (finish ? (empty ? "/>" : ">") : " ");
		}

		public static string Attrib(string name, string value)
		{
			return Attrib(name, value, false, false);
		}

		public static string Attrib(string name, string value, bool closeTag, bool empty)
		{
			return name + "=\"" + SanitizeText(value) + "\"" + (closeTag ? (empty ? "/>" : ">") : " ");
		}

		public static string CloseTag(string tag)
		{
			return "</" + tag + ">";
		}

		public static string Indentation(int level)
		{
			char[] indent = new char[level];
			for (int i = 0; i < indent.Length; i++)
			{
				indent[i] = '\t';
			}
			return new String(indent);
		}
	}
}