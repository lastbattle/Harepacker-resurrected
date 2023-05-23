using LZ4;
using MapleLib.WzLib.Serialization;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// The code was modified from https://github.com/angelsl/wz2nx
/// </summary>
namespace MapleLib.WzLib.Nx
{
	internal static class Extension
	{
		public static void EnsureMultiple(this Stream s, int multiple)
		{
			int skip = (int)(multiple - (s.Position % multiple));
			if (skip == multiple)
				return;
			s.Write(new byte[skip], 0, skip);
		}


		public static T[] SubArray<T>(this T[] array, int offset, int length)
		{
			T[] result = new T[length];
			Array.Copy(array, offset, result, 0, length);
			return result;
		}
	}

	public class WzToNxSerializer : ProgressingWzSerializer, IWzFileSerializer
	{
		private static readonly byte[] PKG4 = { 0x50, 0x4B, 0x47, 0x34 }; // PKG4
		private static readonly bool _is64bit = IntPtr.Size == 8;


		public void SerializeFile(WzFile file, string path)
		{
			String filename = file.Name.Replace(".wz", ".nx");

			using (FileStream fs = new FileStream(Path.Combine(Path.GetDirectoryName(path), filename), FileMode.Create, FileAccess.ReadWrite,
					FileShare.None))
			using (BinaryWriter bw = new BinaryWriter(fs))
			{
				DumpState state = new DumpState();
				bw.Write(PKG4);
				bw.Write(new byte[(4 + 8) * 4]);
				fs.EnsureMultiple(4);
				ulong nodeOffset = (ulong)bw.BaseStream.Position;
				List<WzObject> nodeLevel = new List<WzObject> { file.WzDirectory };
				while (nodeLevel.Count > 0)
					WriteNodeLevel(ref nodeLevel, state, bw);

				ulong stringOffset;
				uint stringCount = (uint)state.Strings.Count;
				{
					Dictionary<uint, string> strings = state.Strings.ToDictionary(kvp => kvp.Value,
						kvp => kvp.Key);
					ulong[] offsets = new ulong[stringCount];
					for (uint idx = 0; idx < stringCount; ++idx)
					{
						fs.EnsureMultiple(2);
						offsets[idx] = (ulong)bw.BaseStream.Position;
						WriteString(strings[idx], bw);
					}

					fs.EnsureMultiple(8);
					stringOffset = (ulong)bw.BaseStream.Position;
					for (uint idx = 0; idx < stringCount; ++idx)
						bw.Write(offsets[idx]);
				}

				ulong bitmapOffset = 0UL;
				uint bitmapCount = 0U;
				bool flag = true;
				if (flag)
				{
					bitmapCount = (uint)state.Canvases.Count;
					ulong[] offsets = new ulong[bitmapCount];
					long cId = 0;
					foreach (WzCanvasProperty cNode in state.Canvases)
					{
						fs.EnsureMultiple(8);
						offsets[cId++] = (ulong)bw.BaseStream.Position;
						WriteBitmap(cNode, bw);
					}
					fs.EnsureMultiple(8);
					bitmapOffset = (ulong)bw.BaseStream.Position;
					for (uint idx3 = 0U; idx3 < bitmapCount; idx3 += 1U)
					{
						bw.Write(offsets[(int)idx3]);
					}
				}
				ulong soundOffset = 0UL;
				uint soundCount = 0U;
				bool flag2 = true;
				if (flag2)
				{
					soundCount = (uint)state.MP3s.Count;
					ulong[] offsets = new ulong[soundCount];
					long cId = 0L;
					foreach (WzBinaryProperty mNode in state.MP3s)
					{
						fs.EnsureMultiple(8);
						offsets[cId++] = (ulong)bw.BaseStream.Position;
						WriteMP3(mNode, bw);
					}
					fs.EnsureMultiple(8);
					soundOffset = (ulong)bw.BaseStream.Position;
					for (uint idx4 = 0U; idx4 < soundCount; idx4 += 1U)
					{
						bw.Write(offsets[(int)idx4]);
					}
				}
				byte[] uolReplace = new byte[16];
				foreach (KeyValuePair<WzUOLProperty, Action<BinaryWriter, byte[]>> pair in state.UOLs)
				{
					WzObject result = pair.Key.LinkValue;
					bool flag3 = result == null;
					if (!flag3)
					{
						bw.BaseStream.Position = (long)(nodeOffset + (ulong)(state.GetNodeID(result) * 20U) + 4UL);
						bw.BaseStream.Read(uolReplace, 0, 16);
						pair.Value(bw, uolReplace);
					}
				}
				bw.Seek(4, SeekOrigin.Begin);
				bw.Write((uint)state.Nodes.Count);
				bw.Write(nodeOffset);
				bw.Write(stringCount);
				bw.Write(stringOffset);
				bw.Write(bitmapCount);
				bw.Write(bitmapOffset);
				bw.Write(soundCount);
				bw.Write(soundOffset);


			}

		}


		private byte[] GetCompressedBitmap(Bitmap b)
		{
			BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			int inLen = Math.Abs(bd.Stride) * bd.Height;
			byte[] rgbValues = new byte[inLen];
			Marshal.Copy(bd.Scan0, rgbValues, 0, inLen);

			var compressed = LZ4Codec.WrapHC(rgbValues);

			return compressed.SubArray(8, compressed.Length - 8);
		}

		private void WriteBitmap(WzCanvasProperty node, BinaryWriter bw)
		{
			Bitmap b = node.PngProperty.GetBitmap();
			byte[] compressed = GetCompressedBitmap(b);
			bw.Write((uint)compressed.Length);
			bw.Write(compressed);
		}

		private void WriteMP3(WzBinaryProperty node, BinaryWriter bw)
		{
			byte[] i = node.GetBytes();
			bw.Write(i);
		}

		private void WriteString(string s, BinaryWriter bw)
		{
			bool flag = s.Any(new Func<char, bool>(char.IsControl));
			if (flag)
			{
				Console.WriteLine("Warning; control character in string. Perhaps toggle /wzn?");
			}
			byte[] toWrite = Encoding.UTF8.GetBytes(s);
			bw.Write((ushort)toWrite.Length);
			bw.Write(toWrite);
		}

		private void WriteNodeLevel(ref List<WzObject> nodeLevel, DumpState ds, BinaryWriter bw)
		{
			uint nextChildId = (uint)((ulong)ds.GetNextNodeID() + (ulong)((long)nodeLevel.Count));
			foreach (WzObject levelNode in nodeLevel)
			{
				bool flag = levelNode is WzUOLProperty;
				if (flag)
				{
					WriteUOL((WzUOLProperty)levelNode, ds, bw);
				}
				else
				{
					WriteNode(levelNode, ds, bw, nextChildId);
				}
				nextChildId += (uint)GetChildCount(levelNode);
			}
			List<WzObject> @out = new List<WzObject>();
			foreach (WzObject levelNode2 in nodeLevel)
			{
				List<WzObject> childs = GetChildObjects(levelNode2);
				@out.AddRange(childs);
			}
			nodeLevel.Clear();
			nodeLevel = @out;
		}

		private void WriteUOL(WzUOLProperty node, DumpState ds, BinaryWriter bw)
		{
			ds.AddNode(node);
			bw.Write(ds.AddString(node.Name));
			ds.AddUOL(node, bw.BaseStream.Position);
			bw.Write(0L);
			bw.Write(0L);
		}

		public List<WzObject> GetChildObjects(WzObject node)
		{
			List<WzObject> childs = new List<WzObject>();
			bool flag = node is WzDirectory;
			if (flag)
			{
				childs.AddRange(((WzDirectory)node).WzImages);
				childs.AddRange(((WzDirectory)node).WzDirectories);
			}
			else
			{
				bool flag2 = node is WzImage;
				if (flag2)
				{
					childs.AddRange(((WzImage)node).WzProperties);
				}
				else
				{
					bool flag3 = node is WzImageProperty && !(node is WzUOLProperty);
					if (flag3)
					{
						bool flag4 = ((WzImageProperty)node).WzProperties != null;
						if (flag4)
						{
							childs.AddRange(((WzImageProperty)node).WzProperties);
						}
					}
				}
			}
			return childs;
		}
		private int GetChildCount(WzObject node)
		{
			return GetChildObjects(node).Count<WzObject>();
		}
		private void WriteNode(WzObject node, DumpState ds, BinaryWriter bw, uint nextChildID)
		{
			ds.AddNode(node);
			bw.Write(ds.AddString(node.Name));
			bw.Write(nextChildID);
			bw.Write((ushort)GetChildCount(node));

			ushort type;

			if (node is WzDirectory || node is WzImage || node is WzSubProperty || node is WzConvexProperty || node is WzNullProperty)
				type = 0; // no data; children only (8)
			else if (node is WzIntProperty || node is WzShortProperty || node is WzLongProperty)
				type = 1; // int32 (4)
			else if (node is WzDoubleProperty || node is WzFloatProperty)
				type = 2; // Double (0)
			else if (node is WzStringProperty || node is WzLuaProperty)
				type = 3; // String (4)
			else if (node is WzVectorProperty)
				type = 4; // (0)
			else if (node is WzCanvasProperty)
				type = 5; // (4)
			else if (node is WzBinaryProperty)
				type = 6; // (4)
			else
				throw new InvalidOperationException("Unhandled WZ node type [1]");

			bw.Write(type);
			if (node is WzIntProperty)
			{
				bw.Write((long)((WzIntProperty)node).Value);
			}
			else if (node is WzShortProperty)
			{
				bw.Write((long)((WzShortProperty)node).Value);
			}
			else if (node is WzLongProperty)
			{
				bw.Write(((WzLongProperty)node).Value);
			}
			else if (node is WzFloatProperty)
			{
				bw.Write((double)((WzFloatProperty)node).Value);
			}
			else if (node is WzDoubleProperty)
			{
				bw.Write(((WzDoubleProperty)node).Value);
			}
			else if (node is WzStringProperty)
			{
				bw.Write(ds.AddString(((WzStringProperty)node).Value));
			}
			else if (node is WzVectorProperty)
			{
				Point pNode = ((WzVectorProperty)node).Pos;
				bw.Write(pNode.X);
				bw.Write(pNode.Y);
			}
			else if (node is WzCanvasProperty)
			{
				WzCanvasProperty wzcp = (WzCanvasProperty)node;
				bw.Write(ds.AddCanvas(wzcp));
				bool flag16 = true; // export canvas
				if (flag16)
				{
					bw.Write((ushort)wzcp.PngProperty.GetBitmap().Width);
					bw.Write((ushort)wzcp.PngProperty.GetBitmap().Height);
				}
				else
				{
					bw.Write(0);
				}
			}
			else if (node is WzBinaryProperty)
			{
				WzBinaryProperty wzmp = (WzBinaryProperty)node;
				bw.Write(ds.AddMP3(wzmp));
				bool flag18 = true;
				if (flag18)
				{
					bw.Write((uint)wzmp.GetBytes().Length);
				}
				else
				{
					bw.Write(0);
				}
			}
			switch (type)
			{
				case 0:
					bw.Write(0L);
					break;
				case 3:
					bw.Write(0);
					break;
			}
		}

		private sealed class DumpState
		{
			public DumpState()
			{
				Canvases = new List<WzCanvasProperty>();
				Strings = new Dictionary<string, uint>(StringComparer.Ordinal) { { "", 0 } };
				MP3s = new List<WzBinaryProperty>();
				UOLs = new Dictionary<WzUOLProperty, Action<BinaryWriter, byte[]>>();
				Nodes = new Dictionary<WzObject, uint>();
			}

			public List<WzCanvasProperty> Canvases { get; }

			public Dictionary<string, uint> Strings { get; }

			public List<WzBinaryProperty> MP3s { get; }

			public Dictionary<WzUOLProperty, Action<BinaryWriter, byte[]>> UOLs { get; }

			public Dictionary<WzObject, uint> Nodes { get; }

			public uint AddCanvas(WzCanvasProperty node)
			{
				uint ret = (uint)Canvases.Count;
				Canvases.Add(node);
				return ret;
			}

			public uint AddMP3(WzBinaryProperty node)
			{
				uint ret = (uint)MP3s.Count;
				MP3s.Add(node);
				return ret;
			}

			public uint AddString(string str)
			{
				if (Strings.ContainsKey(str))
					return Strings[str];
				uint ret = (uint)Strings.Count;
				Strings.Add(str, ret);
				return ret;
			}

			public void AddNode(WzObject node)
			{
				uint ret = (uint)Nodes.Count;
				Nodes.Add(node, ret);
			}

			public uint GetNodeID(WzObject node)
			{
				return Nodes[node];
			}

			public uint GetNextNodeID()
			{
				return (uint)Nodes.Count;
			}

			public void AddUOL(WzUOLProperty node, long currentPosition)
			{
				UOLs.Add(node, (bw, data) => {
					bw.BaseStream.Position = currentPosition;
					bw.Write(data);
				});
			}
		}
	}
}
