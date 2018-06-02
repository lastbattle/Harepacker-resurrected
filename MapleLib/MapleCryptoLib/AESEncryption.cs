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
using System.Security.Cryptography;

namespace MapleLib.MapleCryptoLib
{

	/// <summary>
	/// Class to handle the AES Encryption routines
	/// </summary>
	public class AESEncryption
	{

		/// <summary>
		/// Encrypt data using MapleStory's AES algorithm
		/// </summary>
		/// <param name="IV">IV to use for encryption</param>
		/// <param name="data">Data to encrypt</param>
		/// <param name="length">Length of data</param>
		/// <returns>Crypted data</returns>
		public static byte[] aesCrypt(byte[] IV, byte[] data, int length)
		{
			return aesCrypt(IV, data, length, CryptoConstants.getTrimmedUserKey());
		}

		/// <summary>
		/// Encrypt data using MapleStory's AES method
		/// </summary>
		/// <param name="IV">IV to use for encryption</param>
		/// <param name="data">data to encrypt</param>
		/// <param name="length">length of data</param>
		/// <param name="key">the AES key to use</param>
		/// <returns>Crypted data</returns>
		public static byte[] aesCrypt(byte[] IV, byte[] data, int length, byte[] key)
		{
			AesManaged crypto = new AesManaged();
			crypto.KeySize = 256; //in bits
			crypto.Key = key;
			crypto.Mode = CipherMode.ECB; // Should be OFB, but this works too

			MemoryStream memStream = new MemoryStream();
			CryptoStream cryptoStream = new CryptoStream(memStream, crypto.CreateEncryptor(), CryptoStreamMode.Write);

			int remaining = length;
			int llength = 0x5B0;
			int start = 0;
			while (remaining > 0)
			{
				byte[] myIV = MapleCrypto.multiplyBytes(IV, 4, 4);
				if (remaining < llength)
				{
					llength = remaining;
				}
				for (int x = start; x < (start + llength); x++)
				{
					if ((x - start) % myIV.Length == 0)
					{
						cryptoStream.Write(myIV, 0, myIV.Length);
						byte[] newIV = memStream.ToArray();
						Array.Copy(newIV, myIV, myIV.Length);
						memStream.Position = 0;
					}
					data[x] ^= myIV[(x - start) % myIV.Length];
				}
				start += llength;
				remaining -= llength;
				llength = 0x5B4;
			}

			try
			{
				cryptoStream.Dispose();
				memStream.Dispose();
			}
			catch (Exception e)
			{
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "Error disposing AES streams" + e);
				//Console.WriteLine("Error disposing AES streams" + e);
			}

			return data;
		}
	}
}