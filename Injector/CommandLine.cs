using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Rynchodon.Injector
{
	class CommandLine
	{

		private enum OptionName : byte { help, author, repo, basedir, publish, version }

		private class Option
		{
			private readonly Type _type;
			public readonly string[] Alias;
			public readonly bool Optional;
			public readonly string HelpMessage;

			public object Value { get; private set; }
			public string TypeName { get { return _type == null ? string.Empty : _type.Name; } }

			public Option(string[] alias, string helpMessage, Type contentType = null, bool optional = true)
			{
				this.Alias = alias;
				this._type = contentType;
				this.Optional = optional;
				this.HelpMessage = helpMessage;
			}

			public bool Match(string arg)
			{
				foreach (string alias in Alias)
					if (arg.StartsWith(alias))
						try
						{
							string value = arg.Substring(alias.Length);
							if (_type == null)
							{
								if (value.Length == 0)
								{
									Value = true;
									return true;
								}
							}
							else
							{
								Value = Convert.ChangeType(value, _type);
								return true;
							}
						}
						catch (Exception) { }

				return false;
			}
		}

		static void Main(string[] args)
		{
			try
			{
				if (args == null || args.Length == 0)
				{
					DllInjector.Run();
					return;
				}
				Parse(args);
				Thread.Sleep(3000);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				Thread.Sleep(3000);
				throw;
			}
		}

		private static Dictionary<OptionName, Option> GetOptions()
		{
			Dictionary<OptionName, Option> opts = new Dictionary<OptionName, Option>();

			opts.Add(OptionName.help, new Option(new string[] { "-h", "--help" }, "print this help message and then exit"));
			opts.Add(OptionName.author, new Option(new string[] { "-a=", "--author=" }, "the author of the mod, required", typeof(string), false));
			opts.Add(OptionName.repo, new Option(new string[] { "-r=", "--repo=", "--repository=" }, "the repository of the mod, required", typeof(string), false));
			opts.Add(OptionName.basedir, new Option(new string[] { "--basedir=" }, "file paths relative to this directory determine where they will be copied to, defaults to current working directory", typeof(string)));
			opts.Add(OptionName.publish, new Option(new string[] { "-p", "--publish" }, "publish the mod to GitHub"));
			opts.Add(OptionName.version, new Option(new string[] { "-v=", "--version=" }, "the version of the mod, by default the version is determined from the files", typeof(string)));

			return opts;
		}

		private static void Parse(string[] args)
		{
			Dictionary<OptionName, Option> opts = GetOptions();

			List<string> filePaths = new List<string>();
			for (int index = 0; index < args.Length; ++index)
			{
				string a = args[index];
				if (a.StartsWith("\"") && a.EndsWith("\""))
					a = a.Substring(1, a.Length - 2);

				foreach (KeyValuePair<OptionName, Option> pair in opts)
					if (pair.Value.Match(a))
					{
						if (pair.Key == OptionName.help)
						{
							PrintHelp(opts);
							return;
						}
						goto NextArg;
					}

				if (File.Exists(a))
					filePaths.Add(a);
				else
				{
					PrintHelp(opts);
					throw new ArgumentException("Invalid argument: " + a);
				}

				NextArg:;
			}

			foreach (KeyValuePair<OptionName, Option> pair in opts)
				if (!pair.Value.Optional && pair.Value.Value == null)
				{
					PrintHelp(opts);
					throw new ArgumentException(pair.Key + " not provided");
				}

			if (filePaths.Count == 0)
			{
				PrintHelp(opts);
				throw new ArgumentException("No files specified");
			}

			LocallyCompiled(opts, filePaths);
		}

		private static void PrintHelp(Dictionary<OptionName, Option> options)
		{
			int longestAlias = 0;
			int longestType = 0;
			foreach (Option opt in options.Values)
			{
				int length = 0;
				foreach (string ali in opt.Alias)
					length += 1 + ali.Length;
				if (length > longestAlias)
					longestAlias = length;

				length = opt.TypeName.Length;
				if (length > longestType)
					longestType = length;
			}

			StringBuilder builder = new StringBuilder();
			builder.AppendLine();

			foreach (Option opt in options.Values)
			{
				string line = string.Join(",", opt.Alias);
				builder.Append(line);
				int spaces = longestAlias + 4 - line.Length;
				for (int s = 0; s < spaces; ++s)
					builder.Append(' ');

				string type = opt.TypeName;
				builder.Append(type);
				spaces = longestType + 4 - type.Length;
				for (int s = 0; s < spaces; ++s)
					builder.Append(' ');

				builder.AppendLine(opt.HelpMessage);
			}

			builder.AppendLine("All other arguments should be paths to files to include");
			builder.AppendLine();
			Console.Write(builder.ToString());
		}

		private static void LocallyCompiled(Dictionary<OptionName, Option> opts, List<string> filePaths)
		{
			Logger.WriteLine("Adding locally compiled mod");

			string myDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			string dllPath = myDirectory + "\\LoadARMS.dll";

			Assembly.LoadFile(dllPath).GetType("Rynchodon.Loader.LoadArms").GetMethod("AddLocallyCompiled", BindingFlags.Static | BindingFlags.Public).Invoke(null,
				new object[] { opts[OptionName.author].Value, opts[OptionName.repo].Value, opts[OptionName.version].Value, filePaths, opts[OptionName.basedir].Value, opts[OptionName.publish].Value });

			// exception for some reason
			//Loader.LoadArms.AddLocallyCompiled((string)opts[OptionName.author].Value, (string)opts[OptionName.repo].Value, (string)opts[OptionName.version].Value, filePaths, (string)opts[OptionName.basedir].Value);
		}

	}
}
