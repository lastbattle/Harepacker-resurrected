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
using System.Net;
using System.Net.Sockets;
using MapleLib.MapleCryptoLib;

namespace MapleLib.PacketLib
{
	public class Monitor
	{
		/// <summary>
		/// The Monitor socket
		/// </summary>
		private readonly Socket _socket;

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
		public delegate void PacketReceivedHandler(PacketReader packet);

		/// <summary>
		/// Packet received event
		/// </summary>
		public event PacketReceivedHandler OnPacketReceived;

		/// <summary>
		/// Method to handle client disconnected
		/// </summary>
		public delegate void ClientDisconnectedHandler(Monitor monitor);

		/// <summary>
		/// Client disconnected event
		/// </summary>
		public event ClientDisconnectedHandler OnClientDisconnected;

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
		/// The Monitor's socket
		/// </summary>
		public Socket Socket
		{
			get { return _socket; }
		}

		/// <summary>
		/// Creates a new instance of Monitor
		/// </summary>
		public Monitor()
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
		}

		/// <summary>
		/// Starts listening and accepting connections
		/// </summary>
		/// <param name="port">Port to listen to</param>
		public void StartMonitoring(IPAddress IP)
		{

			_socket.Bind(new IPEndPoint(IP, 0));

			_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);//??

			byte[] byIn = new byte[4] { 1, 0, 0, 0 };
			byte[] byOut = null;

			_socket.IOControl(IOControlCode.ReceiveAll, byIn, byOut);

			WaitForData();
		}

		/// <summary>
		/// Waits for more data to arrive
		/// </summary>
		public void WaitForData()
		{
			WaitForData(new SocketInfo(_socket, short.MaxValue));
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


				byte[] dataa = new byte[received];
				Buffer.BlockCopy(socketInfo.DataBuffer, 0, dataa, 0, received);
                if (OnPacketReceived != null)
                    OnPacketReceived.Invoke(new PacketReader(dataa));
				//Console.WriteLine(BitConverter.ToString(dataa));
				//Console.WriteLine(HexEncoding.ToStringFromAscii(dataa));
				WaitForData();
				/*if (socketInfo.Index == socketInfo.DataBuffer.Length) {
					switch (socketInfo.State) {
						case SocketInfo.StateEnum.Header:
							PacketReader headerReader = new PacketReader(socketInfo.DataBuffer);
							byte[] packetHeaderB = headerReader.ToArray();
							int packetHeader = headerReader.ReadInt();
							short packetLength = (short)MapleCrypto.getPacketLength(packetHeader);
							if (!_RIV.checkPacket(packetHeader)) {
								Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Packet check failed. Disconnecting client.");
								this.Socket.Close();
								}
							socketInfo.State = SocketInfo.StateEnum.Content;
							socketInfo.DataBuffer = new byte[packetLength];
							socketInfo.Index = 0;
							WaitForData(socketInfo);
							break;
						case SocketInfo.StateEnum.Content:
							byte[] data = socketInfo.DataBuffer;

							_RIV.crypt(data);
							MapleCustomEncryption.Decrypt(data);

							if (data.Length != 0 && OnPacketReceived != null) {
								OnPacketReceived(new PacketReader(data));
								}
							WaitForData();
							break;
						}
					} else {
					Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Warning] Not enough data");
					WaitForData(socketInfo);
					}*/
			}
			catch (ObjectDisposedException)
			{
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: Socket has been closed");
                //Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: Socket has been closed");
			}
			catch (SocketException se)
			{
				if (se.ErrorCode != 10054)
				{
                    Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + se);
					//Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + se);
				}
			}
			catch (Exception e)
			{
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + e);
				//Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + e);
			}
		}

	}
}