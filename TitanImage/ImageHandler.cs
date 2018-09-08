using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TitanImage
{
	// https://github.com/xdanieldzd/Tharsis/tree/0d731925e13dae7a1632280bdfdfa28a7b6bb28a/Tharsis/Images

	public static class ImageHandler
	{
		public delegate void TileDecoderDelegate(BinaryReader reader, byte[] targetData, int x, int y, int width, int height);
		public delegate void TileEncoderDelegate(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height);

		class Codec
		{
			public PicaDataType DataType { get; private set; }
			public PicaPixelFormat PixelFormat { get; private set; }
			public TileDecoderDelegate Decoder { get; private set; }
			public TileEncoderDelegate Encoder { get; private set; }

			public Codec(PicaDataType dataType, PicaPixelFormat pixelFormat, TileDecoderDelegate decoder, TileEncoderDelegate encoder)
			{
				DataType = dataType;
				PixelFormat = pixelFormat;
				Decoder = decoder;
				Encoder = encoder;
			}
		}

		readonly static List<Codec> codecs = new List<Codec>()
		{
            /* RGBA4444 */ new Codec(PicaDataType.UnsignedShort4444,    PicaPixelFormat.RGBANativeDMP,            DecodeRGBA4444, EncodeRGBA4444),
            /* RGBA5551 */ new Codec(PicaDataType.UnsignedShort5551,    PicaPixelFormat.RGBANativeDMP,            DecodeRGBA5551, EncodeRGBA5551),
            /* RGBA8888 */ new Codec(PicaDataType.UnsignedByte,         PicaPixelFormat.RGBANativeDMP,            DecodeRGBA8888, EncodeRGBA8888),
            /* RGB565   */ new Codec(PicaDataType.UnsignedShort565,     PicaPixelFormat.RGBNativeDMP,             DecodeRGB565,   EncodeRGB565),
            /* RGB888   */ new Codec(PicaDataType.UnsignedByte,         PicaPixelFormat.RGBNativeDMP,             DecodeRGB888,   EncodeRGB888),
            /* ETC1     */ new Codec(PicaDataType.UnsignedByte,         PicaPixelFormat.ETC1RGB8NativeDMP,        DecodeETC1,     null),
            /* ETC1_A4  */ new Codec(PicaDataType.UnsignedByte,         PicaPixelFormat.ETC1AlphaRGB8A4NativeDMP, DecodeETC1_A4,  null),
            /* A8       */ new Codec(PicaDataType.UnsignedByte,         PicaPixelFormat.AlphaNativeDMP,           DecodeA8,       EncodeA8),
            /* A4       */ new Codec(PicaDataType.Unsigned4BitsDMP,     PicaPixelFormat.AlphaNativeDMP,           DecodeA4,       null),
            /* L8       */ new Codec(PicaDataType.UnsignedByte,         PicaPixelFormat.LuminanceNativeDMP,       DecodeL8,       EncodeL8),
            /* L4       */ new Codec(PicaDataType.Unsigned4BitsDMP,     PicaPixelFormat.LuminanceNativeDMP,       DecodeL4,       null),
            /* LA88     */ new Codec(PicaDataType.UnsignedByte,         PicaPixelFormat.LuminanceAlphaNativeDMP,  DecodeLA88,     EncodeLA88),
            /* LA44     */ new Codec(PicaDataType.UnsignedByte44DMP,    PicaPixelFormat.LuminanceAlphaNativeDMP,  DecodeLA44,     EncodeLA44)
		};

		public enum PicaDataType : uint
		{
			Byte = 0x1400,
			UnsignedByte = 0x1401,
			Short = 0x1402,
			UnsignedShort = 0x1403,
			Int = 0x1404,
			UnsignedInt = 0x1405,
			Float = 0x1406,
			UnsignedByte44DMP = 0x6760,
			Unsigned4BitsDMP = 0x6761,
			UnsignedShort4444 = 0x8033,
			UnsignedShort5551 = 0x8034,
			UnsignedShort565 = 0x8363
		};

		public enum PicaPixelFormat : uint
		{
			RGBANativeDMP = 0x6752,
			RGBNativeDMP = 0x6754,
			AlphaNativeDMP = 0x6756,
			LuminanceNativeDMP = 0x6757,
			LuminanceAlphaNativeDMP = 0x6758,
			ETC1RGB8NativeDMP = 0x675A,
			ETC1AlphaRGB8A4NativeDMP = 0x675B
		};

		public static Bitmap GetBitmap(byte[] data, int width, int height, PicaDataType dataType, PicaPixelFormat pixelFormat)
		{
			var bitmap = new Bitmap(width, height);
			var decoder = GetDecoder(dataType, pixelFormat);

			var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
			var targetData = new byte[bmpData.Height * bmpData.Stride];
			Marshal.Copy(bmpData.Scan0, targetData, 0, targetData.Length);

			using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
			{
				for (int y = 0; y < height; y += 8)
					for (int x = 0; x < width; x += 8)
						decoder(reader, targetData, x, y, width, height);
			}

			Marshal.Copy(targetData, 0, bmpData.Scan0, targetData.Length);
			bitmap.UnlockBits(bmpData);

			return bitmap;
		}

		public static byte[] GetData(Bitmap bitmap, PicaDataType dataType, PicaPixelFormat pixelFormat)
		{
			var output = new MemoryStream(32);
			var writer = new BinaryWriter(output);
			var encoder = GetEncoder(dataType, pixelFormat);

			var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
			var sourceData = new byte[bmpData.Height * bmpData.Stride];
			Marshal.Copy(bmpData.Scan0, sourceData, 0, sourceData.Length);

			for (int y = 0; y < bitmap.Height; y += 8)
				for (int x = 0; x < bitmap.Width; x += 8)
					encoder(writer, sourceData, x, y, bitmap.Width, bitmap.Height);

			Marshal.Copy(sourceData, 0, bmpData.Scan0, sourceData.Length);
			bitmap.UnlockBits(bmpData);

			return output.ToArray();
		}

		static readonly int[] tileOrder =
		{
			0, 1, 8, 9,
			2, 3, 10, 11,
			16, 17, 24, 25,
			18, 19, 26, 27,

			4, 5, 12, 13,
			6, 7, 14, 15,
			20, 21, 28, 29,
			22, 23, 30, 31,

			32, 33, 40, 41,
			34, 35, 42, 43,
			48, 49, 56, 57,
			50, 51, 58, 59,

			36, 37, 44, 45,
			38, 39, 46, 47,
			52, 53, 60, 61,
			54, 55, 62, 63
		};

		static readonly int[,] etc1ModifierTables =
		{
			{ 2, 8, -2, -8 },
			{ 5, 17, -5, -17 },
			{ 9, 29, -9, -29 },
			{ 13, 42, -13, -42 },
			{ 18, 60, -18, -60 },
			{ 24, 80, -24, -80 },
			{ 33, 106, -33, -106 },
			{ 47, 183, -47, -183 }
		};

		private static Codec GetCodec(PicaDataType dataType, PicaPixelFormat picaPixelFormat)
		{
			Codec codec = codecs.FirstOrDefault(x => x.DataType == dataType && x.PixelFormat == picaPixelFormat);
			if (codec == null) throw new Exception(string.Format("Codec not found for {0} {1}", dataType, picaPixelFormat));
			return codec;
		}

		public static TileDecoderDelegate GetDecoder(PicaDataType dataType, PicaPixelFormat picaPixelFormat)
		{
			Codec codec = GetCodec(dataType, picaPixelFormat);
			if (codec.Decoder == null) throw new Exception(string.Format("Decoder not found for {0} {1}", dataType, picaPixelFormat));
			return codec.Decoder;
		}

		public static TileEncoderDelegate GetEncoder(PicaDataType dataType, PicaPixelFormat picaPixelFormat)
		{
			Codec codec = GetCodec(dataType, picaPixelFormat);
			if (codec.Encoder == null) throw new Exception(string.Format("Encoder not found for {0} {1}", dataType, picaPixelFormat));
			return codec.Encoder;
		}

		private static int GetTilePixelIndex(int t, int x, int y, int width)
		{
			return (int)((((tileOrder[t] / 8) + y) * width) + ((tileOrder[t] % 8) + x));
		}

		private static int GetTilePixelOffset(int t, int x, int y, int width, PixelFormat pixelFormat)
		{
			return (GetTilePixelIndex(t, x, y, width) * (Image.GetPixelFormatSize(pixelFormat) / 8));
		}

		private static byte ResampleChannel(int value, int sourceBits, int targetBits)
		{
			byte sourceMask = (byte)((1 << sourceBits) - 1);
			byte targetMask = (byte)((1 << targetBits) - 1);
			return (byte)((((value & sourceMask) * targetMask) + (sourceMask >> 1)) / sourceMask);
		}

		private static void DecodeRGBA4444(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				ushort rgba = reader.ReadUInt16();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = ResampleChannel((rgba >> 0), 4, 8);
				targetData[pixelOffset + 0] = ResampleChannel((rgba >> 4), 4, 8);
				targetData[pixelOffset + 1] = ResampleChannel((rgba >> 8), 4, 8);
				targetData[pixelOffset + 2] = ResampleChannel((rgba >> 12), 4, 8);
			}
		}

		private static void DecodeRGBA5551(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				ushort rgba = reader.ReadUInt16();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = ResampleChannel((rgba >> 0), 1, 8);
				targetData[pixelOffset + 0] = ResampleChannel((rgba >> 1), 5, 8);
				targetData[pixelOffset + 1] = ResampleChannel((rgba >> 6), 5, 8);
				targetData[pixelOffset + 2] = ResampleChannel((rgba >> 11), 5, 8);
			}
		}

		private static void DecodeRGBA8888(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				byte a = reader.ReadByte(), b = reader.ReadByte(), g = reader.ReadByte(), r = reader.ReadByte();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = a;
				targetData[pixelOffset + 0] = b;
				targetData[pixelOffset + 1] = g;
				targetData[pixelOffset + 2] = r;
			}
		}

		private static void DecodeRGB565(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				ushort rgba = reader.ReadUInt16();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = 0xFF;
				targetData[pixelOffset + 0] = ResampleChannel((rgba >> 0), 5, 8);
				targetData[pixelOffset + 1] = ResampleChannel((rgba >> 5), 6, 8);
				targetData[pixelOffset + 2] = ResampleChannel((rgba >> 11), 5, 8);
			}
		}

		private static void DecodeRGB888(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				byte b = reader.ReadByte(), g = reader.ReadByte(), r = reader.ReadByte();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = 0xFF;
				targetData[pixelOffset + 0] = b;
				targetData[pixelOffset + 1] = g;
				targetData[pixelOffset + 2] = r;
			}
		}

		private static void DecodeETC1(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			DecodeETC1Tile(reader, targetData, x, y, width, height, false);
		}

		private static void DecodeETC1_A4(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			DecodeETC1Tile(reader, targetData, x, y, width, height, true);
		}

		private static void DecodeA8(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				byte a = reader.ReadByte();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = a;
				targetData[pixelOffset + 0] = targetData[pixelOffset + 1] = targetData[pixelOffset + 2] = 0xFF;
			}
		}

		private static void DecodeA4(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t += 2)
			{
				byte a4 = reader.ReadByte();
				for (int i = 0; i < 2; i++)
				{
					int pixelOffset = GetTilePixelOffset(t + i, x, y, width, PixelFormat.Format32bppArgb);
					if (pixelOffset >= targetData.Length) continue;

					targetData[pixelOffset + 3] = (byte)(((a4 >> (i * 4)) & 0x0F) << 4 | ((a4 >> (i * 4)) & 0x0F));
					targetData[pixelOffset + 0] = targetData[pixelOffset + 1] = targetData[pixelOffset + 2] = 0xFF;
				}
			}
		}

		private static void DecodeL8(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				byte l = reader.ReadByte();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = 0xFF;
				targetData[pixelOffset + 0] = targetData[pixelOffset + 1] = targetData[pixelOffset + 2] = l;
			}
		}

		private static void DecodeL4(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t += 2)
			{
				byte l4 = reader.ReadByte();
				for (int i = 0; i < 2; i++)
				{
					int pixelOffset = GetTilePixelOffset(t + i, x, y, width, PixelFormat.Format32bppArgb);
					if (pixelOffset >= targetData.Length) continue;

					targetData[pixelOffset + 3] = 0xFF;
					targetData[pixelOffset + 0] = targetData[pixelOffset + 1] = targetData[pixelOffset + 2] = (byte)(((l4 >> (i * 4)) & 0x0F) << 4 | ((l4 >> (i * 4)) & 0x0F));
				}
			}
		}

		private static void DecodeLA88(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				byte a = reader.ReadByte(), l = reader.ReadByte();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = a;
				targetData[pixelOffset + 0] = targetData[pixelOffset + 1] = targetData[pixelOffset + 2] = l;
			}
		}

		private static void DecodeLA44(BinaryReader reader, byte[] targetData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				byte la = reader.ReadByte();
				int pixelOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				if (pixelOffset >= targetData.Length) continue;

				targetData[pixelOffset + 3] = (byte)(((la >> 0) & 0x0F) << 4 | ((la >> 0) & 0x0F));
				targetData[pixelOffset + 0] = targetData[pixelOffset + 1] = targetData[pixelOffset + 2] = (byte)(((la >> 4) & 0x0F) << 4 | ((la >> 4) & 0x0F));
			}
		}

		private static void DecodeETC1Tile(BinaryReader reader, byte[] targetData, int x, int y, int width, int height, bool hasAlpha)
		{
			/* Specs: https://www.khronos.org/registry/gles/extensions/OES/OES_compressed_ETC1_RGB8_texture.txt */

			/* Other implementations:
             * https://github.com/richgel999/rg-etc1/blob/master/rg_etc1.cpp
             * https://github.com/Gericom/EveryFileExplorer/blob/master/3DS/GPU/Textures.cs
             * https://github.com/gdkchan/Ohana3DS-Rebirth/blob/master/Ohana3DS%20Rebirth/Ohana/TextureCodec.cs */

			for (int by = 0; by < 8; by += 4)
			{
				for (int bx = 0; bx < 8; bx += 4)
				{
					ulong alpha = (hasAlpha ? reader.ReadUInt64() : 0xFFFFFFFFFFFFFFFF);
					ulong block = reader.ReadUInt64();

					using (BinaryReader decodedReader = new BinaryReader(new MemoryStream(DecodeETC1Block(block))))
					{
						for (int py = 0; py < 4; py++)
						{
							for (int px = 0; px < 4; px++)
							{
								if (x + bx + px >= width) continue;
								if (y + by + py >= height) continue;

								int pixelOffset = (int)((((y + by + py) * width) + (x + bx + px)) * 4);
								Buffer.BlockCopy(decodedReader.ReadBytes(3), 0, targetData, pixelOffset, 3);
								byte pixelAlpha = (byte)((alpha >> (((px * 4) + py) * 4)) & 0xF);
								targetData[pixelOffset + 3] = (byte)((pixelAlpha << 4) | pixelAlpha);
							}
						}
					}
				}
			}
		}

		private static byte[] DecodeETC1Block(ulong block)
		{
			byte r1, g1, b1, r2, g2, b2;

			byte tableIndex1 = (byte)((block >> 37) & 0x07);
			byte tableIndex2 = (byte)((block >> 34) & 0x07);
			byte diffBit = (byte)((block >> 33) & 0x01);
			byte flipBit = (byte)((block >> 32) & 0x01);

			if (diffBit == 0x00)
			{
				/* Individual mode */
				r1 = (byte)(((block >> 60) & 0x0F) << 4 | (block >> 60) & 0x0F);
				g1 = (byte)(((block >> 52) & 0x0F) << 4 | (block >> 52) & 0x0F);
				b1 = (byte)(((block >> 44) & 0x0F) << 4 | (block >> 44) & 0x0F);

				r2 = (byte)(((block >> 56) & 0x0F) << 4 | (block >> 56) & 0x0F);
				g2 = (byte)(((block >> 48) & 0x0F) << 4 | (block >> 48) & 0x0F);
				b2 = (byte)(((block >> 40) & 0x0F) << 4 | (block >> 40) & 0x0F);
			}
			else
			{
				/* Differential mode */

				/* 5bit base values */
				byte r1a = (byte)(((block >> 59) & 0x1F));
				byte g1a = (byte)(((block >> 51) & 0x1F));
				byte b1a = (byte)(((block >> 43) & 0x1F));

				/* Subblock 1, 8bit extended */
				r1 = (byte)((r1a << 3) | (r1a >> 2));
				g1 = (byte)((g1a << 3) | (g1a >> 2));
				b1 = (byte)((b1a << 3) | (b1a >> 2));

				/* 3bit modifiers */
				sbyte dr2 = (sbyte)((block >> 56) & 0x07);
				sbyte dg2 = (sbyte)((block >> 48) & 0x07);
				sbyte db2 = (sbyte)((block >> 40) & 0x07);
				if (dr2 >= 4) dr2 -= 8;
				if (dg2 >= 4) dg2 -= 8;
				if (db2 >= 4) db2 -= 8;

				/* Subblock 2, 8bit extended */
				r2 = (byte)((r1a + dr2) << 3 | (r1a + dr2) >> 2);
				g2 = (byte)((g1a + dg2) << 3 | (g1a + dg2) >> 2);
				b2 = (byte)((b1a + db2) << 3 | (b1a + db2) >> 2);
			}

			byte[] decodedData = new byte[(4 * 4) * 3];

			using (BinaryWriter writer = new BinaryWriter(new MemoryStream(decodedData)))
			{
				for (int py = 0; py < 4; py++)
				{
					for (int px = 0; px < 4; px++)
					{
						int index = (int)(((block >> ((px * 4) + py)) & 0x1) | ((block >> (((px * 4) + py) + 16)) & 0x1) << 1);

						if ((flipBit == 0x01 && py < 2) || (flipBit == 0x00 && px < 2))
						{
							int modifier = etc1ModifierTables[tableIndex1, index];
							writer.Write((byte)((b1 + modifier)).Clamp(byte.MinValue, byte.MaxValue));
							writer.Write((byte)((g1 + modifier)).Clamp(byte.MinValue, byte.MaxValue));
							writer.Write((byte)((r1 + modifier)).Clamp(byte.MinValue, byte.MaxValue));
						}
						else
						{
							int modifier = etc1ModifierTables[tableIndex2, index];
							writer.Write((byte)((b2 + modifier)).Clamp(byte.MinValue, byte.MaxValue));
							writer.Write((byte)((g2 + modifier)).Clamp(byte.MinValue, byte.MaxValue));
							writer.Write((byte)((r2 + modifier)).Clamp(byte.MinValue, byte.MaxValue));
						}
					}
				}
			}

			return decodedData;
		}

		private static void EncodeRGBA4444(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				ushort rgba4444 = (ushort)(ResampleChannel(sourceData[sourceOffset + 2], 8, 4) << 12);
				rgba4444 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 1], 8, 4) << 8);
				rgba4444 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 0], 8, 4) << 4);
				rgba4444 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 3], 8, 4) << 0);
				writer.Write(rgba4444);
			}
		}

		private static void EncodeRGBA5551(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				ushort rgba5551 = (ushort)(ResampleChannel(sourceData[sourceOffset + 2], 8, 5) << 11);
				rgba5551 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 1], 8, 5) << 6);
				rgba5551 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 0], 8, 5) << 1);
				rgba5551 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 3], 8, 1) << 0);
				writer.Write(rgba5551);
			}
		}

		private static void EncodeRGBA8888(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				writer.Write(sourceData[sourceOffset + 3]);
				writer.Write(sourceData[sourceOffset + 0]);
				writer.Write(sourceData[sourceOffset + 1]);
				writer.Write(sourceData[sourceOffset + 2]);
			}
		}

		private static void EncodeRGB565(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				ushort rgb565 = (ushort)(ResampleChannel(sourceData[sourceOffset + 2], 8, 5) << 11);
				rgb565 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 1], 8, 6) << 5);
				rgb565 |= (ushort)(ResampleChannel(sourceData[sourceOffset + 0], 8, 5) << 0);
				writer.Write(rgb565);
			}
		}

		private static void EncodeRGB888(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				writer.Write(sourceData[sourceOffset + 0]);
				writer.Write(sourceData[sourceOffset + 1]);
				writer.Write(sourceData[sourceOffset + 2]);
			}
		}

		private static void EncodeA8(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				writer.Write(sourceData[sourceOffset + 3]);
			}
		}

		private static void EncodeL8(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				byte r = sourceData[sourceOffset + 2], g = sourceData[sourceOffset + 1], b = sourceData[sourceOffset + 0];
				writer.Write((byte)((r + g + b) / 3));
			}
		}

		private static void EncodeLA88(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				writer.Write(sourceData[sourceOffset + 3]);
				byte r = sourceData[sourceOffset + 2], g = sourceData[sourceOffset + 1], b = sourceData[sourceOffset + 0];
				writer.Write((byte)((r + g + b) / 3));
			}
		}

		private static void EncodeLA44(BinaryWriter writer, byte[] sourceData, int x, int y, int width, int height)
		{
			for (int t = 0; t < tileOrder.Length; t++)
			{
				int sourceOffset = GetTilePixelOffset(t, x, y, width, PixelFormat.Format32bppArgb);
				byte r = sourceData[sourceOffset + 2], g = sourceData[sourceOffset + 1], b = sourceData[sourceOffset + 0];
				byte la = (ResampleChannel(sourceData[sourceOffset + 3], 8, 4));
				la |= (byte)(ResampleChannel((byte)((r + g + b) / 3), 8, 4) << 4);
				writer.Write(la);
			}
		}
	}
}
