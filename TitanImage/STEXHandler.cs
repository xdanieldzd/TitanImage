using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;

namespace TitanImage
{
	public static class STEXHandler
	{
		public static (Bitmap Bitmap, ImageMetadata Metadata) ImportBinary(string fileName, string relativePath)
		{
			var metadata = new ImageMetadata(Path.Combine(relativePath, Path.GetFileName(fileName)));

			using (BinaryReader reader = new BinaryReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
			{
				/* Read and check magic number */
				reader.BaseStream.Seek(0x00, SeekOrigin.Begin);
				if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "STEX") throw new Exception("Magic number mismatch; not an STEX file?");

				/* Read metadata */
				reader.BaseStream.Seek(0x0C, SeekOrigin.Begin);
				metadata.Width = reader.ReadInt32();
				metadata.Height = reader.ReadInt32();
				metadata.DataType = (ImageHandler.PicaDataType)reader.ReadUInt32();
				metadata.PixelFormat = (ImageHandler.PicaPixelFormat)reader.ReadUInt32();

				/* Read pixel data */
				var pixelDataOffset = (reader.BaseStream.Length % 0x100);
				var pixelDataSize = (reader.BaseStream.Length - pixelDataOffset);
				reader.BaseStream.Seek(pixelDataOffset, SeekOrigin.Begin);
				var pixelData = reader.ReadBytes((int)pixelDataSize);

				return (ImageHandler.GetBitmap(pixelData, metadata.Width, metadata.Height, metadata.DataType, metadata.PixelFormat), metadata);
			}
		}

		public static void ExportBinary(Bitmap bitmap, ImageMetadata metadata, string fileName)
		{
			using (BinaryWriter writer = new BinaryWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
			{
				var imageData = ImageHandler.GetData(bitmap, metadata.DataType, metadata.PixelFormat);

				writer.Write(Encoding.ASCII.GetBytes("STEX"));
				writer.Write((uint)0);  // TODO: sometimes one, not zero?
				writer.Write((uint)3553);
				writer.Write(metadata.Width);
				writer.Write(metadata.Height);
				writer.Write((uint)metadata.DataType);
				writer.Write((uint)metadata.PixelFormat);
				writer.Write(imageData.Length);
				writer.Write((uint)0x80);
				writer.Write(new byte[0x5C]);
				writer.Write(imageData);
			}
		}
	}
}
