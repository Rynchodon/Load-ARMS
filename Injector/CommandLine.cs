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

		private enum OptionName : byte { help, author, repo, basedir, oAuthToken, publish, version }

		private class Option
		{
			private readonly Type _type;
			public readonly string[] Alias;
			public readonly bool Optional;
			public readonly string HelpMessage;

			public object Value { get; private set; }
			public string TypeName { get { return _type == null ? string.Empty : _type.Name; } }

			/// <summary>
			/// Create a new command line option.
			/// </summary>
			/// <param name="alias">Possible names for the command.</param>
			/// <param name="helpMessage">Message displayed when help is requested or required.</param>
			/// <param name="contentType">The type of content that must be provided in the command line, for a switch this is null.</param>
			/// <param name="optional">If false this option must be specified or an exception will be thrown.</param>
			public Option(string[] alias, string helpMessage, Type contentType = null, bool optional = true, object defaultValue = null)
			{
				this.Alias = alias;
				this._type = contentType;
				this.Optional = optional;
				this.HelpMessage = helpMessage;
				this.Value = defaultValue;
			}

			/// <summary>
			/// Try to match this option against a string, getting a value if necessary.
			/// </summary>
			/// <param name="arg">The string that may contain this option.</param>
			/// <returns>True iff this option was matched against arg.</returns>
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
				Thread.Sleep(1000);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				Thread.Sleep(60000);
				throw;
			}
		}

		private static SortedDictionary<OptionName, Option> GetOptions()
		{
			SortedDictionary<OptionName, Option> opts = new SortedDictionary<OptionName, Option>();

			opts.Add(OptionName.help, new Option(new string[] { "-h", "--help" }, "print this help message and then exit"));

			// required
			opts.Add(OptionName.author, new Option(new string[] { "-a=", "--author=" }, "the author of the mod, required", typeof(string), false));
			opts.Add(OptionName.repo, new Option(new string[] { "-r=", "--repo=", "--repository=" }, "the repository of the mod, required", typeof(string), false));

			// optional
			opts.Add(OptionName.basedir, new Option(new string[] { "--basedir=" }, "files will be organized relative to this directory, defaults to current working directory", typeof(string), defaultValue: Environment.CurrentDirectory));
			opts.Add(OptionName.oAuthToken, new Option(new string[] { "--oAuthToken=" }, "token used to log into GitHub, by default the value from the environment variable \"oAuthToken\" will be used", typeof(string)));
			opts.Add(OptionName.publish, new Option(new string[] { "-p", "--publish" }, "publish the mod to GitHub"));
			opts.Add(OptionName.version, new Option(new string[] { "-v=", "--version=" }, "the version of the mod, by default the version is the highest file version", typeof(string)));

			return opts;
		}

		private static void Parse(string[] args)
		{
			SortedDictionary<OptionName, Option> opts = GetOptions();

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
				{
					filePaths.Add(a);
				}
				else
				{
					Console.WriteLine("Not a file: " + Path.GetFullPath(a));
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

		private static void PrintHelp(SortedDictionary<OptionName, Option> options)
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

			builder.AppendLine("All other arguments should be paths to files to include.");
			builder.AppendLine();
			Console.Write(builder.ToString());
		}

		private static void LocallyCompiled(SortedDictionary<OptionName, Option> opts, List<string> filePaths)
		{
			Logger.WriteLine("Adding locally compiled mod");

			List<object> arguments = new List<object>(opts.Count - 1);
			foreach (KeyValuePair<OptionName, Option> pair in opts)
				if (pair.Key != OptionName.help)
					arguments.Add(pair.Value.Value);
			arguments.Add(filePaths);

			Assembly.LoadFile(PathExtensions.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "LoadARMS.dll")).
				GetType("Rynchodon.Loader.LoadArms").
				GetMethod("FromCommandLine", BindingFlags.Static | BindingFlags.Public).
				Invoke(null, arguments.ToArray());
		}

	}
}
