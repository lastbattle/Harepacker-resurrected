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
using System.Net.Sockets;
using MapleLib.MapleCryptoLib;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Class to a network session socket
	/// </summary>
	public class Session
	{

		/// <summary>
		/// The Session's socket
		/// </summary>
		private readonly Socket _socket;

		private SessionType _type;

		/// <summary>
		/// The Recieved packet crypto manager
		/// </summary>
		private MapleCrypto _RIV;

		/// <summary>
		/// The Sent packet crypto manager
		/// </summary>
		private MapleCrypto _SIV;

        /// <summary>
        /// Method to handle packets received
        /// </summary>
        public delegate void PacketReceivedHandler(PacketReader packet, bool mIsInit);

        /// <summary>
		/// Packet received event
		/// </summary>
		public event PacketReceivedHandler OnPacketReceived;

		/// <summary>
		/// Method to handle client disconnected
		/// </summary>
		public delegate void ClientDisconnectedHandler(Session session);

		/// <summary>
		/// Client disconnected event
		/// </summary>
		public event ClientDisconnectedHandler OnClientDisconnected;

		public delegate void InitPacketReceived(short version, byte serverIdentifier);
		public event InitPacketReceived OnInitPacketReceived;

		/// <summary>
		/// The Recieved packet crypto manager
		/// </summary>
		public MapleCrypto RIV
		{
			get { return _RIV; }
			set { _RIV = value; }
		}

		/// <summary>
		/// The Sent packet crypto manager
		/// </summary>
		public MapleCrypto SIV
		{
			get { return _SIV; }
			set { _SIV = value; }
		}

		/// <summary>
		/// The Session's socket
		/// </summary>
		public Socket Socket
		{
			get { return _socket; }
		}

		public SessionType Type
		{
			get { return _type; }
		}
		/// <summary>
		/// Creates a new instance of a Session
		/// </summary>
		/// <param name="socket">Socket connection of the session</param>

		public Session(Socket socket, SessionType type)
		{
			_socket = socket;
			_type = type;
		}

		/// <summary>
		/// Waits for more data to arrive
		/// </summary>
		public void WaitForData()
		{
			WaitForData(new SocketInfo(_socket, 4));
		}

		public void WaitForDataNoEncryption()
		{
			WaitForData(new SocketInfo(_socket, 2, true));
		}

		/// <summary>
		/// Waits for more data to arrive
		/// </summary>
		/// <param name="socketInfo">Info about data to be received</param>
		private void WaitForData(SocketInfo socketInfo)
		{
			try
			{
				_socket.BeginReceive(socketInfo.DataBuffer,
					socketInfo.Index,
					socketInfo.DataBuffer.Length - socketInfo.Index,
					SocketFlags.None,
					new AsyncCallback(OnDataReceived),
					socketInfo);
			}
			catch (Exception se)
			{
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.WaitForData: " + se);
				//Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.WaitForData: " + se);
			}
		}

		/// <summary>
		/// Data received event handler
		/// </summary>
		/// <param name="iar">IAsyncResult of the data received event</param>
		private void OnDataReceived(IAsyncResult iar)
		{
			SocketInfo socketInfo = (SocketInfo)iar.AsyncState;
			try
			{
				int received = socketInfo.Socket.EndReceive(iar);
				if (received == 0)
				{
					if (OnClientDisconnected != null)
					{
						OnClientDisconnected(this);
					}
					return;
				}

				socketInfo.Index += received;

				if (socketInfo.Index == socketInfo.DataBuffer.Length)
				{
					switch (socketInfo.State)
					{
						case SocketInfo.StateEnum.Header:
							if (socketInfo.NoEncryption)
							{
								PacketReader headerReader = new PacketReader(socketInfo.DataBuffer);
								short packetHeader = headerReader.ReadShort();
								socketInfo.State = SocketInfo.StateEnum.Content;
								socketInfo.DataBuffer = new byte[packetHeader];
								socketInfo.Index = 0;
								WaitForData(socketInfo);
							}
							else
							{
								PacketReader headerReader = new PacketReader(socketInfo.DataBuffer);
								byte[] packetHeaderB = headerReader.ToArray();
								int packetHeader = headerReader.ReadInt();
								short packetLength = (short)MapleCrypto.getPacketLength(packetHeader);
								if (_type == SessionType.SERVER_TO_CLIENT && !_RIV.checkPacketToServer(BitConverter.GetBytes(packetHeader)))
								{
									Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Packet check failed. Disconnecting client.");
									//this.Socket.Close();
								}
								socketInfo.State = SocketInfo.StateEnum.Content;
								socketInfo.DataBuffer = new byte[packetLength];
								socketInfo.Index = 0;
								WaitForData(socketInfo);
							}
							break;
						case SocketInfo.StateEnum.Content:
							byte[] data = socketInfo.DataBuffer;
							if (socketInfo.NoEncryption)
							{
								socketInfo.NoEncryption = false;
								PacketReader reader = new PacketReader(data);
								short version = reader.ReadShort();
								string unknown = reader.ReadMapleString();
								_SIV = new MapleCrypto(reader.ReadBytes(4), version);
								_RIV = new MapleCrypto(reader.ReadBytes(4), version);
								byte serverType = reader.ReadByte();
								if (_type == SessionType.CLIENT_TO_SERVER)
								{
									OnInitPacketReceived(version, serverType);
								}
								OnPacketReceived(new PacketReader(data), true);
								WaitForData();
							}
							else
							{
								_RIV.crypt(data);
								MapleCustomEncryption.Decrypt(data);
								if (data.Length != 0 && OnPacketReceived != null)
								{
									OnPacketReceived(new PacketReader(data), false);
								}
								WaitForData();
							}
							break;
					}
				}
				else
				{
					Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Warning] Not enough data");
					WaitForData(socketInfo);
				}
			}
			catch (ObjectDisposedException)
			{
				Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: Socket has been closed");
			}
			catch (SocketException se)
			{
				if (se.ErrorCode != 10054)
				{
					Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + se);
				}
			}
			catch (Exception e)
			{
				Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + e);
			}
		}

        public void SendInitialPacket(int pVersion, string pPatchLoc, byte[] pRIV, byte[] pSIV, byte pServerType)
        {
            PacketWriter writer = new PacketWriter();
            writer.WriteShort(pPatchLoc == "" ? 0x0D : 0x0E);
            writer.WriteShort(pVersion);
            writer.WriteMapleString(pPatchLoc);
            writer.WriteBytes(pRIV);
            writer.WriteBytes(pSIV);
            writer.WriteByte(pServerType);
            SendRawPacket(writer);
        }

		/// <summary>
		/// Encrypts the packet then send it to the client.
		/// </summary>
		/// <param name="packet">The PacketWrtier object to be sent.</param>
		public void SendPacket(PacketWriter packet)
		{
			SendPacket(packet.ToArray());
		}

		/// <summary>
		/// Encrypts the packet then send it to the client.
		/// </summary>
		/// <param name="input">The byte array to be sent.</param>
		public void SendPacket(byte[] input)
		{
			byte[] cryptData = input;
			byte[] sendData = new byte[cryptData.Length + 4];
			byte[] header = _type == SessionType.SERVER_TO_CLIENT ? _SIV.getHeaderToClient(cryptData.Length) : _SIV.getHeaderToServer(cryptData.Length);

			MapleCustomEncryption.Encrypt(cryptData);
			_SIV.crypt(cryptData);

			System.Buffer.BlockCopy(header, 0, sendData, 0, 4);
			System.Buffer.BlockCopy(cryptData, 0, sendData, 4, cryptData.Length);
			SendRawPacket(sendData);
		}

        /// <summary>
        /// Sends a raw packet to the client
        /// </summary>
        /// <param name="pPacket">The PacketWriter</param>
        public void SendRawPacket(PacketWriter pPacket)
        {
            SendRawPacket(pPacket.ToArray());
        }

		/// <summary>
		/// Sends a raw buffer to the client.
		/// </summary>
		/// <param name="buffer">The buffer to be sent.</param>
		public void SendRawPacket(byte[] buffer)
		{
			//_socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, ar => _socket.EndSend(ar), null);//async
			_socket.Send(buffer);//sync
		}

	}
}