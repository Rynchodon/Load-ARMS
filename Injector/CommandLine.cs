using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Rynchodon.Injector
{
	class CommandLine
	{

		private class Option
		{
			private readonly Type _type;
			private readonly string[] _alias;
			public readonly bool Optional;
			public object Value { get; private set; }

			public Option(string[] alias, Type contentType, bool optional = true)
			{
				this._alias = alias;
				this._type = contentType;
				this.Optional = optional;
			}

			public bool Match(string arg)
			{ 
				foreach (string alias in _alias)
					if (arg.StartsWith(alias))
						try
						{
							Value = Convert.ChangeType(arg.Substring(alias.Length), _type);
							return true;
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
				DllInjector.WriteLine("Success");
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				Thread.Sleep(50000);
			}
			Thread.Sleep(10000);
		}

		private static void Parse(string[] args)
		{
			Dictionary<string, Option> opts = new Dictionary<string, Option>();
			opts.Add("author", new Option(new string[] { "-a=", "--author=" }, typeof(string), false));
			opts.Add("repo", new Option(new string[] { "-r=", "--repo=", "--repository=" }, typeof(string), false));
			opts.Add("startdir", new Option(new string[] { "--startdir=" }, typeof(string)));
			opts.Add("version", new Option(new string[] { "-v=", "--version=" }, typeof(string)));

			List<string> filePaths = new List<string>();
			for (int index = 0; index < args.Length; ++index)
			{
				string a = args[index];
				if (a.StartsWith("\"") && a.EndsWith("\""))
					a = a.Substring(1, a.Length - 2);

				foreach (Option o in opts.Values)
					if (o.Match(a))
						goto NextArg;

				if (File.Exists(a))
					filePaths.Add(a);
				else
					throw new ArgumentException("Invalid argument: " + a);

				NextArg:;
			}

			foreach (KeyValuePair<string, Option> pair in opts)
				if (!pair.Value.Optional && pair.Value.Value == null)
					throw new ArgumentException(pair.Key + " not provided");

			if (filePaths.Count == 0)
				throw new ArgumentException("No files specified");

			DllInjector.WriteLine("Adding locally compiled mod");

			string myDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			string dllPath = myDirectory + "\\LoadARMS.dll";

			Assembly.LoadFile(dllPath).GetType("Rynchodon.Loader.LoadArms").GetMethod("AddLocallyCompiled", BindingFlags.Static | BindingFlags.Public).Invoke(null,
				new object[] { opts["author"].Value, opts["repo"].Value, opts["version"].Value, filePaths, opts["startdir"].Value });

			// exception for some reason
			//Loader.LoadArms.AddLocallyCompiled((string)opts["author"].Value, (string)opts["repo"].Value, (string)opts["version"].Value, filePaths, (string)opts["startdir"].Value);
		}

	}
}
