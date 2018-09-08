using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

using Newtonsoft.Json.Linq;

using TitanTools;

namespace TitanImage
{
	class Program
	{
		readonly static List<ConsoleHelper.ArgumentHandler> argumentHandlers = new List<ConsoleHelper.ArgumentHandler>()
		{
			{ new ConsoleHelper.ArgumentHandler("png", "p", "Convert STEX files to PNG+JSON files", "[source path] [target path]", ArgumentHandlerStexToPngJson) },
			{ new ConsoleHelper.ArgumentHandler("stex", "s", "Convert PNG+JSON files to STEX files", null, ArgumentHandlerPngJsonToStex) },
			{ new ConsoleHelper.ArgumentHandler("overwrite", "o", "Allow overwriting of existing files", null, (arg) => { overwriteExistingFiles = true; }) },
			{ new ConsoleHelper.ArgumentHandler("ignore", "i", "Ignore untranslated files on binary creation", null, (arg) => { ignoreUntranslatedFiles = true; }) },
			{ new ConsoleHelper.ArgumentHandler("unattended", "u", "Run unattended, i.e. don't wait for key on exit", null, (arg) => { verbose = false; }) },
		};

		static bool overwriteExistingFiles = false;
		static bool ignoreUntranslatedFiles = false;
		static bool verbose = true;

		static void Main()
		{
			ConsoleHelper.PrintApplicationInformation();

			var args = ConsoleHelper.GetAndVerifyArguments(1, () =>
			{
				Console.WriteLine("No arguments specified!");
				Console.WriteLine();
				ConsoleHelper.PrintUsageAndExit(argumentHandlers, -1);
			});

			ConsoleHelper.ExecuteArguments(args, argumentHandlers, ref verbose);
		}

		static string GetRelativePath(FileInfo sourceFile, DirectoryInfo sourceRoot)
		{
			return sourceFile.DirectoryName.Replace(sourceRoot.FullName, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}

		static bool CheckOkayToContinue(FileInfo outputFileInfo)
		{
			if (outputFileInfo.Exists && !overwriteExistingFiles)
			{
				Console.WriteLine($"[-] File {outputFileInfo.Name} already exists, skipping...");
				return false;
			}
			else
				return true;
		}

		static void ArgumentHandlerStexToPngJson(string[] args)
		{
			if (args.Length != 3) throw new Exception($"Invalid number of arguments for {args[0]}");

			var sourceRoot = new DirectoryInfo(args[1]);
			var targetRoot = new DirectoryInfo(args[2]);

			// Special cases: exclude known leftovers, etc. (mainly "effect" stuff)
			var stexFiles = sourceRoot.EnumerateFiles("*.stex", SearchOption.AllDirectories)
				.Where(x => !x.DirectoryName.Contains("effect\\tex") && !x.DirectoryName.Contains("effect_editor"));

			foreach (var stexFile in stexFiles)
			{
				var relativePath = GetRelativePath(stexFile, sourceRoot);

				var outputFileJsonInfo = new FileInfo(Path.Combine(targetRoot.FullName, relativePath, $"{Path.GetFileNameWithoutExtension(stexFile.FullName)}.json"));
				var outputFilePngOriginalInfo = new FileInfo(Path.Combine(targetRoot.FullName, relativePath, $"{Path.GetFileNameWithoutExtension(stexFile.FullName)} (Original).png"));
				var outputFilePngTranslationInfo = new FileInfo(Path.Combine(targetRoot.FullName, relativePath, $"{Path.GetFileNameWithoutExtension(stexFile.FullName)} (Translation).png"));

				if (CheckOkayToContinue(outputFileJsonInfo) && CheckOkayToContinue(outputFilePngOriginalInfo) && CheckOkayToContinue(outputFilePngTranslationInfo))
				{
					Console.WriteLine($"[*] Converting STEX {stexFile.Name} to PNG+JSON...");

					outputFileJsonInfo.Directory.Create();

					(Bitmap bitmap, ImageMetadata metadata) = STEXHandler.ImportBinary(stexFile.FullName, relativePath);
					bitmap.Save(outputFilePngOriginalInfo.FullName, ImageFormat.Png);
					bitmap.Save(outputFilePngTranslationInfo.FullName, ImageFormat.Png);
					metadata.SerializeToFile(outputFileJsonInfo.FullName);
				}
			}
		}

		static void ArgumentHandlerPngJsonToStex(string[] args)
		{
			if (args.Length != 3) throw new Exception($"Invalid number of arguments for {args[0]}");

			var sourceRoot = new DirectoryInfo(args[1]);
			var targetRoot = new DirectoryInfo(args[2]);

			var jsonFiles = sourceRoot.EnumerateFiles("*.json", SearchOption.AllDirectories);

			foreach (var jsonFile in jsonFiles)
			{
				var metadataFile = jsonFile.FullName.DeserializeFromFile<ImageMetadata>();

				var inputFilePngOriginalInfo = new FileInfo(Path.Combine(sourceRoot.FullName, Path.GetDirectoryName(metadataFile.RelativePath), $"{Path.GetFileNameWithoutExtension(metadataFile.RelativePath)} (Original).png"));
				var inputFilePngTranslationInfo = new FileInfo(Path.Combine(sourceRoot.FullName, Path.GetDirectoryName(metadataFile.RelativePath), $"{Path.GetFileNameWithoutExtension(metadataFile.RelativePath)} (Translation).png"));

				if (ignoreUntranslatedFiles && ((inputFilePngOriginalInfo.Length == inputFilePngTranslationInfo.Length) && (File.ReadAllBytes(inputFilePngOriginalInfo.FullName).SequenceEqual(File.ReadAllBytes(inputFilePngTranslationInfo.FullName)))))
				{
					Console.WriteLine($"[-] File {jsonFile.Name} has no translation, skipping...");
					continue;
				}

				var relativePath = GetRelativePath(jsonFile, sourceRoot);
				var outputFileInfo = new FileInfo(Path.Combine(targetRoot.FullName, metadataFile.RelativePath));

				if (CheckOkayToContinue(outputFileInfo))
				{
					Console.WriteLine($"[*] Converting PNG+JSON {jsonFile.Name} to STEX...");

					if (metadataFile.DataType == ImageHandler.PicaDataType.UnsignedByte && (metadataFile.PixelFormat == ImageHandler.PicaPixelFormat.ETC1RGB8NativeDMP || metadataFile.PixelFormat == ImageHandler.PicaPixelFormat.ETC1AlphaRGB8A4NativeDMP))
					{
						Console.WriteLine($"[+] Original STEX was ETC1, encoding to RGBA8 instead...");
						metadataFile.DataType = ImageHandler.PicaDataType.UnsignedByte;
						metadataFile.PixelFormat = ImageHandler.PicaPixelFormat.RGBANativeDMP;
					}

					outputFileInfo.Directory.Create();

					using (var bitmap = Image.FromFile(inputFilePngTranslationInfo.FullName))
					{
						STEXHandler.ExportBinary((Bitmap)bitmap, metadataFile, outputFileInfo.FullName);
					}
				}
			}
		}
	}
}
