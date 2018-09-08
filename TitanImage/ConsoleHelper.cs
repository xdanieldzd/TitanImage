using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace TitanTools
{
	public static class ConsoleHelper
	{
		public static void PrintApplicationInformation()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var productAttribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
			var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
			var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();

			Console.WriteLine($"{productAttribute.Product} - {descriptionAttribute.Description}");
			Console.WriteLine($"{copyrightAttribute.Copyright}");
			Console.WriteLine();
		}

		public static void PrintUsageAndExit(List<ArgumentHandler> argumentHandlers, int code)
		{
			Console.WriteLine($"Usage: {Path.GetFileName(Assembly.GetExecutingAssembly().Location)} [options]...");
			Console.WriteLine();
			Console.WriteLine("Options:");

			var maxSpecifierLength = argumentHandlers.Select(x => $" {x.Short}{(x.Long != string.Empty ? "," : " ")} {x.Long}").Max(x => x.Length);
			foreach (var argumentHandler in argumentHandlers)
			{
				var specifierString = $"{argumentHandler.Short}{(argumentHandler.Long != string.Empty ? "," : " ")} {argumentHandler.Long}";
				var padding = string.Empty.PadRight((maxSpecifierLength + 3) - specifierString.Length, ' ');
				Console.WriteLine($" {specifierString}{padding}{argumentHandler.Description}");
				if (!string.IsNullOrWhiteSpace(argumentHandler.Syntax))
					Console.WriteLine($"{new string(' ', specifierString.Length)}{padding}  [{specifierString.Replace(", ", "|")}] {argumentHandler.Syntax}");
			}

			Console.WriteLine();
			Exit(code);
		}

		public static void Exit(int code)
		{
			Console.WriteLine("Press any key to exit.");
			Console.ReadKey();

			Environment.Exit(code);
		}

		public static string[] GetAndVerifyArguments(int minArgs, Action onInvalidArgs)
		{
			string[] args = CommandLineTools.CreateArgs(Environment.CommandLine);
			if (args.Length <= minArgs)
				onInvalidArgs?.Invoke();
			return args;
		}

		public static List<string[]> GetArgumentGroups(string[] args)
		{
			var argGroups = new List<string[]>();
			for (int argIdx = 1; argIdx < args.Length; argIdx++)
			{
				if (args[argIdx].StartsWith("-"))
				{
					var argGroup = new List<string> { args[argIdx].TrimStart('-') };
					argGroup.AddRange(args.Skip(argIdx + 1).TakeWhile(x => !x.StartsWith("-")));
					argGroups.Add(argGroup.ToArray());
					argIdx += (argGroup.Count - 1);
				}
			}
			return argGroups;
		}

		public static void ExecuteArguments(string[] args, List<ArgumentHandler> argumentHandlers, ref bool verbose)
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			try
			{
				foreach (var argGroup in GetArgumentGroups(args))
				{
					argumentHandlers.FirstOrDefault(x => x.Long == argGroup[0] || x.Short == argGroup[0]).Method?.Invoke(argGroup);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Exception occured: {ex.Message}");
				Console.WriteLine();
				Exit(-1);
			}
			finally
			{
				if (verbose)
				{
					Console.WriteLine();
					Console.WriteLine("Operation completed in {0}.", GetReadableTimespan(stopwatch.Elapsed));
					Console.WriteLine();
					Exit(0);
				}
			}
		}

		/* Slightly modified from https://stackoverflow.com/a/4423615 */
		public static string GetReadableTimespan(TimeSpan span)
		{
			string formatted = string.Format("{0}{1}{2}{3}{4}",
			span.Duration().Days > 0 ? string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? string.Empty : "s") : string.Empty,
			span.Duration().Hours > 0 ? string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? string.Empty : "s") : string.Empty,
			span.Duration().Minutes > 0 ? string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? string.Empty : "s") : string.Empty,
			span.Duration().Seconds > 0 ? string.Format("{0:0} second{1}, ", span.Seconds, span.Seconds == 1 ? string.Empty : "s") : string.Empty,
			span.Duration().Milliseconds > 0 ? string.Format("{0:0} millisecond{1}", span.Milliseconds, span.Milliseconds == 1 ? string.Empty : "s") : string.Empty);
			if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);
			if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";
			return formatted;
		}

		public class ArgumentHandler
		{
			public string Long { get; set; }
			public string Short { get; set; }
			public string Description { get; set; }
			public string Syntax { get; set; }
			public Action<string[]> Method { get; set; }

			public ArgumentHandler(string @long, string @short, string description, string syntax, Action<string[]> method)
			{
				Long = @long;
				Short = @short;
				Description = description;
				Syntax = syntax;
				Method = method;
			}
		}
	}
}
