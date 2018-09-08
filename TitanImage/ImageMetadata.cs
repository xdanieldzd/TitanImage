using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace TitanImage
{
	public class ImageMetadata
	{
		public string RelativePath { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }

		[JsonIgnore]
		public ImageHandler.PicaDataType DataType { get; set; }

		[JsonProperty("DataType")]
		public string DataTypeString
		{
			get => DataType.ToString();
			set => DataType = (ImageHandler.PicaDataType)Enum.Parse(typeof(ImageHandler.PicaDataType), value);
		}

		[JsonIgnore]
		public ImageHandler.PicaPixelFormat PixelFormat { get; set; }

		[JsonProperty("PixelFormat")]
		public string PixelFormatString
		{
			get => PixelFormat.ToString();
			set => PixelFormat = (ImageHandler.PicaPixelFormat)Enum.Parse(typeof(ImageHandler.PicaPixelFormat), value);
		}

		public ImageMetadata(string relativePath)
		{
			RelativePath = relativePath;
			Width = 0;
			Height = 0;
			DataType = 0;
			PixelFormat = 0;
		}
	}
}
