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
using System.IO;
using System.Text;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Class to handle reading data from a packet
	/// </summary>
	public class PacketReader : AbstractPacket
	{
		/// <summary>
		/// The main reader tool
		/// </summary>
		private readonly BinaryReader _binReader;

		/// <summary>
		/// Amount of data left in the reader
		/// </summary>
		public short Length
		{
			get { return (short)_buffer.Length; }
		}

		/// <summary>
		/// Creates a new instance of PacketReader
		/// </summary>
		/// <param name="arrayOfBytes">Starting byte array</param>
		public PacketReader(byte[] arrayOfBytes)
		{
			_buffer = new MemoryStream(arrayOfBytes, false);
			_binReader = new BinaryReader(_buffer, Encoding.ASCII);
		}

		/// <summary>
		/// Restart reading from the point specified.
		/// </summary>
		/// <param name="length">The point of the packet to start reading from.</param>
		public void Reset(int length)
		{
			_buffer.Seek(length, SeekOrigin.Begin);
		}

		public void Skip(int length)
		{
			_buffer.Position += length;
		}

		/// <summary>
		/// Reads an unsigned byte from the stream
		/// </summary>
		/// <returns> an unsigned byte from the stream</returns>
		public byte ReadByte()
		{
			return _binReader.ReadByte();
		}

		/// <summary>
		/// Reads a byte array from the stream
		/// </summary>
		/// <param name="length">Amount of bytes</param>
		/// <returns>A byte array</returns>
		public byte[] ReadBytes(int count)
		{
			return _binReader.ReadBytes(count);
		}

		/// <summary>
		/// Reads a bool from the stream
		/// </summary>
		/// <returns>A bool</returns>
		public bool ReadBool()
		{
			return _binReader.ReadBoolean();
		}

		/// <summary>
		/// Reads a signed short from the stream
		/// </summary>
		/// <returns>A signed short</returns>
		public short ReadShort()
		{
			return _binReader.ReadInt16();
		}

		/// <summary>
		/// Reads a signed int from the stream
		/// </summary>
		/// <returns>A signed int</returns>
		public int ReadInt()
		{
			return _binReader.ReadInt32();
		}

		/// <summary>
		/// Reads a signed long from the stream
		/// </summary>
		/// <returns>A signed long</returns>
		public long ReadLong()
		{
			return _binReader.ReadInt64();
		}

		/// <summary>
		/// Reads an ASCII string from the stream
		/// </summary>
		/// <param name="length">Amount of bytes</param>
		/// <returns>An ASCII string</returns>
		public string ReadString(int length)
		{
			return Encoding.ASCII.GetString(ReadBytes(length));
		}

		/// <summary>
		/// Reads a maple string from the stream
		/// </summary>
		/// <returns>A maple string</returns>
		public string ReadMapleString()
		{
			return ReadString(ReadShort());
		}
	}
}