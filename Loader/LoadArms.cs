﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Xml;
using RGiesecke.DllExport;
using Sandbox;
using Sandbox.Engine.Platform;
using Sandbox.Graphics.GUI;
using SpaceEngineers.Game;
using VRage.Plugins;

namespace Rynchodon.Loader
{
	/// <summary>
	/// Main entry point.
	/// </summary>
	public class LoadArms : IPlugin
	{
		private const string
			LauncherArgs = "-plugin LoadArms.exe",
			ConfigFileName = "Config.json",
			DataFileName = "Data.json";

		internal const string Rynchodon = "Rynchodon", LoadArmsRepo = "Load-ARMS", ArmsRepo = "ARMS";

		/// <summary>If both inject and -plugin are used, there will be two LoadArms. This is a reference to the first one created, the second will be suppressed.</summary>
		private static LoadArms _instance;

		// Steam generates a popup with this method.
		#region Launch SE with Args

		//public static void Main(string[] args)
		//{
		//	try { LaunchSpaceEngineers(); }
		//	catch (Exception ex)
		//	{
		//		Logger.WriteLine(ex.ToString());
		//		Console.WriteLine(ex.ToString());
		//		Thread.Sleep(60000);
		//	}
		//}

		//private static void LaunchSpaceEngineers()
		//{
		//	string myDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		//	string launcher = myDirectory + "\\SpaceEngineers.exe";
		//	if (File.Exists(launcher))
		//	{
		//		Process.Start(launcher, launcherArgs).Dispose();
		//		return;
		//	}

		//	launcher = myDirectory + "\\SpaceEngineersDedicated.exe";
		//	if (File.Exists(launcher))
		//	{
		//		Process.Start(launcher, launcherArgs).Dispose();
		//		return;
		//	}

		//	throw new Exception("Not in Space Engineers folder");
		//}

		#endregion

		#region Injected Init

		/// <summary>
		/// Starting point when injected into SE.
		/// </summary>
		[DllExport]
		public static void RunInSEProcess()
		{
			for (int i = 0; i < 1000000; ++i)
			{
				if (MySandboxGame.Static != null)
				{
					if (_instance != null)
						return;
					MySandboxGame.Static.Invoke(() => typeof(MyPlugins).GetMethod("LoadPlugins", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Assembly.GetExecutingAssembly() }));
					return;
				}
				Thread.Sleep(1);
			}

			throw new TimeoutException("Timed out waiting for instance of MySandboxGame");
		}

		#endregion

		/// <summary>
		/// Adds a locally compiled mod and optionally publishes it.
		/// </summary>
		/// <param name="files">Paths to files to include in the mod.</param>
		/// <param name="author">The author of GitHub repository</param>
		/// <param name="repo">The name of the GitHub repository</param>
		/// <param name="allBuilds">If true and seVersion is zero, the mod will be considered compatible with all Space Engineers versions.</param>
		/// <param name="basedir">Files will be orgainzed relative to this directory.</param>
		/// <param name="oAuthToken">Personal access token for GitHub, it may also be set as an environment variable.</param>
		/// <param name="publish">If true, publish a release to GitHub</param>
		/// <param name="versionString">The version of the release. If it is null, the highest version number from any file is used.</param>
		/// <param name="seVersion">Version of Space Engineers the mod was compiled against. 0 - get version from SpaceEngineersGame, unless allBuilds is true.</param>
		public static void AddLocallyCompiledMod(IEnumerable<string> files, string author, string repo, bool allBuilds = false, string basedir = null, string oAuthToken = null, bool publish = false, string versionString = null, int seVersion = 0)
		{
			if (_instance == null)
				new LoadArms(false);

			ModName name = new ModName(author, repo);

			if (seVersion < 1 && !allBuilds)
				seVersion = GetCurrentSEVersion();

			Version version;
			version.SeVersion = seVersion;
			if (versionString != null)
				version = new Version(versionString);
			else
			{
				version = new Version();
				foreach (string file in files)
				{
					Version fileVersion = new Version(FileVersionInfo.GetVersionInfo(file), seVersion);
					if (version.CompareTo(fileVersion) < 0)
						version = fileVersion;
				}
			}

			_instance.Load();
			ModVersion modVersion = _instance.AddLocallyCompiled(name, version, seVersion, files, basedir);

			if (publish && GitChecks.Check(basedir, _instance._config.PathToGit))
				ReleaseCreater.Publish(modVersion, PathExtensions.Combine(_instance._directory, "mods", name.fullName), oAuthToken);
		}

		/// <summary>
		/// Get the current Space Engineers version from SpaceEngineersGame.
		/// </summary>
		/// <returns>The current version of Space Engineers.</returns>
		public static int GetCurrentSEVersion()
		{
			FieldInfo field = typeof(SpaceEngineersGame).GetField("SE_VERSION", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
				throw new NullReferenceException("SpaceEngineersGame does not have field SE_VERSION, or it has unexpected binding");
			return (int)field.GetValue(null);
		}

		[DataContract]
		private struct Config
		{
			[DataMember]
			public ModInfo[] GitHubMods;
#pragma warning disable CS0649
			[DataMember]
			public string PathToGit;
#pragma warning restore CS0649
		}

		[DataContract]
		private struct Data
		{
			[DataMember]
			public Dictionary<int, ModVersion> ModsCurrentVersions;
		}

		private string _directory;
		private Config _config;
		private Data _data;
		private ParallelTasks.Task _task;
		private DownloadProgress.Stats _downProgress = new DownloadProgress.Stats();
		private List<IPlugin> _plugins;
		private bool _initialized, _startedRobocopy;

		/// <summary>
		/// Creates an instance of LoadArms and starts the updating process.
		/// </summary>
		public LoadArms() : this(true) { }

		/// <summary>
		/// Creates an instance of LoadArms and, optionally, starts the updating process.
		/// </summary>
		/// <param name="start">Iff true, start the updating process.</param>
		private LoadArms(bool start)
		{
			if (_instance != null)
				return;

			_instance = this;
			_directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			if (!File.Exists(_directory + "\\SpaceEngineers.exe") && !File.Exists(_directory + "\\SpaceEngineersDedicated.exe"))
				throw new Exception("Not in Space Engineers folder");

			_directory = Path.GetDirectoryName(_directory) + "\\Load-ARMS\\";
			Directory.CreateDirectory(_directory);

			Logger.logFile = _directory + (Game.IsDedicated ? "Load-ARMS Dedicated.log" : "Load-ARMS.log");
			Logger.WriteLine("Load-ARMS version: " + new Version(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location), 0));

			if (start)
				_task = ParallelTasks.Parallel.StartBackground(Run);
		}

		private void Run()
		{
			Cleanup();
			Load();
			VerifyDataIntegrity();
			UpdateMods();
			SaveData();
		}

		/// <summary>
		/// Update LoadARMS.dll and LoadARMS.exe from download folder.
		/// </summary>
		void IDisposable.Dispose()
		{
			Robocopy();
		}

		/// <summary>
		/// Update LoadARMS.dll and LoadARMS.exe from download folder using robocopy.
		/// </summary>
		private void Robocopy()
		{
			if (_instance != this || _startedRobocopy)
				return;
			_startedRobocopy = true;

			string license = PathExtensions.Combine(_directory, "mods\\Rynchodon.Load-ARMS\\Load-ARMS License.txt");
			if (File.Exists(license))
				File.Copy(license, PathExtensions.Combine(_directory, "License.txt"), true);

			Logger.WriteLine("starting robocopy");

			string first = '"' + _directory + "mods\\Rynchodon.Load-ARMS\" \"" + _directory + "..\\";
			string second = "\" LoadARMS.dll LoadARMS.exe /copyall /W:1 /xx";

			string toBin64 = first + "Bin64" + second;
			string toDed64 = first + "DedicatedServer64" + second;

			Process robocopy = new Process();
			robocopy.StartInfo.FileName = "cmd.exe";
			robocopy.StartInfo.Arguments = "/C robocopy " + toBin64 + " & robocopy " + toDed64;
			robocopy.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			robocopy.Start();
		}

		/// <summary>
		/// Initialize this object.
		/// </summary>
		/// <param name="gameInstance">MySandboxGame.Static, so I don't know why it's a param</param>
		public void Init(object gameInstance)
		{
			if (_instance != this)
				return;
			_initialized = true;

			if (!_task.IsComplete && !Game.IsDedicated)
				MyGuiSandbox.AddScreen(new DownloadProgress(_task, _downProgress));
		}

		/// <summary>
		/// Load plugins, if updating has finished.
		/// </summary>
		void IPlugin.Update()
		{
			if (_instance != this)
				return;
			if (!_initialized)
				Init(MySandboxGame.Static);

			if (_plugins != null)
			{
				foreach (IPlugin plugin in _plugins)
					plugin.Update();
			}
			else if (_task.IsComplete)
			{
				Logger.WriteLine("Finished task, loading plugins");
				_plugins = LoadPlugins();
				foreach (IPlugin plugin in _plugins)
					plugin.Init(MySandboxGame.Static);

				_config = default(Config);
				_data = default(Data);
				_task = default(ParallelTasks.Task);
				_downProgress = null;
			}
		}

		private void Load()
		{
			LoadConfig();
			LoadData();
		}

		private void LoadConfig()
		{
			string configFilePath = _directory + LoadArms.ConfigFileName;
			if (File.Exists(configFilePath))
			{
				try
				{
					DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Config));
					using (FileStream file = new FileStream(configFilePath, FileMode.Open))
						_config = (Config)serializer.ReadObject(file);
					if (_config.GitHubMods != null)
					{
						SaveConfig();
						return;
					}
					Logger.WriteLine("ERORR: Saved config is incomplete");
				}
				catch (Exception ex)
				{
					Logger.WriteLine("ERROR: Failed to read saved config");
					throw ex;
				}
			}

			_config.GitHubMods = DefaultModInfo();
			SaveConfig();
		}

		private void LoadData()
		{
			string dataFilePath = PathExtensions.Combine(_directory, DataFileName);
			if (File.Exists(dataFilePath))
			{
				try
				{
					FileInfo dataFileInfo = new FileInfo(dataFilePath);
					dataFileInfo.IsReadOnly = false;

					DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Data));
					using (FileStream file = new FileStream(dataFilePath, FileMode.Open))
						_data = (Data)serializer.ReadObject(file);

					dataFileInfo.IsReadOnly = true;

					if (_data.ModsCurrentVersions != null)
						return;
					Logger.WriteLine("ERROR: Saved data is incomplete");
				}
				catch (Exception ex)
				{
					Logger.WriteLine("ERROR: Failed to read saved data");
					throw ex;
				}
			}

			_data.ModsCurrentVersions = new Dictionary<int, ModVersion>();
			SaveData();
		}

		/// <summary>
		/// Check for mod files that got deleted.
		/// </summary>
		private void VerifyDataIntegrity()
		{
			List<int> removals = new List<int>();
			foreach (var mod in _data.ModsCurrentVersions)
				if (mod.Value.MissingFiles())
				{
					Logger.WriteLine(mod.Value.fullName + " is missing files, removing");
					mod.Value.EraseAllFiles();
					removals.Add(mod.Key);
				}

			if (removals.Count != 0)
			{
				foreach (int key in removals)
					_data.ModsCurrentVersions.Remove(key);
				SaveData();
			}
		}

		private void SaveConfig()
		{
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Config));
			using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(new FileStream(_directory + ConfigFileName, FileMode.Create), Encoding.UTF8, true, true))
				serializer.WriteObject(writer, _config);
		}

		private void SaveData()
		{
			string dataFilePath = PathExtensions.Combine(_directory, DataFileName);
			FileInfo dataFileInfo = new FileInfo(dataFilePath);
			if (File.Exists(dataFilePath))
				dataFileInfo.IsReadOnly = false;

			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Data));
			using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(new FileStream(_directory + DataFileName, FileMode.Create), Encoding.UTF8, true, true))
				serializer.WriteObject(writer, _data);

			dataFileInfo.IsReadOnly = true;
		}

		private void UpdateMods()
		{
			_downProgress.TotalMods = _config.GitHubMods.Length;
			_downProgress.CurrentMod = 0;

			foreach (ModInfo mod in _config.GitHubMods)
			{
				++_downProgress.CurrentMod;

				Logger.WriteLine("mod: " + mod.fullName);

				GitHubClient client = new GitHubClient(mod);

				int hashCode = mod.GetHashCode();
				ModVersion current;
				if (_data.ModsCurrentVersions.TryGetValue(hashCode, out current))
				{
					if (client.Update(mod, current, _directory))
						Logger.WriteLine("Updated");
				}
				else
				{
					current = new ModVersion(mod);
					if (client.Update(mod, current, _directory))
					{
						Logger.WriteLine("New download");
						_data.ModsCurrentVersions.Add(hashCode, current);
					}
				}
			}
		}

		private List<IPlugin> LoadPlugins()
		{
			HashSet<ModVersion> loadedMods = new HashSet<ModVersion>();
			List<IPlugin> chainedPlugins = new List<IPlugin>();

			foreach (ModVersion mod in _data.ModsCurrentVersions.Values)
				if (InConfig(mod))
					LoadPlugins(chainedPlugins, mod, loadedMods);
				else
					Logger.WriteLine("Not loading " + mod.fullName + ", not in config");

			return chainedPlugins;
		}

		private bool InConfig(ModName name)
		{
			for (int index = _config.GitHubMods.Length - 1; index >= 0; --index)
				if (_config.GitHubMods[index].fullName == name.fullName)
					return true;
			return false;
		}

		private void EnsureInConfig(ModName name)
		{
			if (InConfig(name))
				return;

			Logger.WriteLine("Adding " + name.fullName + " to config file");
			ModInfo[] newArray = new ModInfo[_config.GitHubMods.Length + 1];
			_config.GitHubMods.CopyTo(newArray, 0);
			newArray[newArray.Length - 1] = new ModInfo(name, true);
			_config.GitHubMods = newArray;

			SaveConfig();
		}

		private bool LoadPlugins(List<IPlugin> chainedPlugins, ModVersion mod, HashSet<ModVersion> loadedMods, int depth = 0)
		{
			if (mod.author == Rynchodon && mod.repository == LoadArmsRepo)
				return true;

			if (loadedMods.Contains(mod))
				return true;
			if (depth > 100)
			{
				Logger.WriteLine("ERROR Failed to load " + mod.fullName + ", recursive requirements");
				return false;
			}

			if (mod.requirements != null)
				foreach (ModName name in mod.requirements)
				{
					ModVersion required;
					if (!_data.ModsCurrentVersions.TryGetValue(name.GetHashCode(), out required))
					{
						Logger.WriteLine("ERROR: Failed to load " + mod.fullName + ", missing required mod: " + name.fullName);
						return false;
					}
					if (!LoadPlugins(chainedPlugins, required, loadedMods, depth + 1))
					{
						Logger.WriteLine("ERROR: Failed to load " + mod.fullName + ", failed to load required mod: " + name.fullName);
						return false;
					}
				}

			if (!loadedMods.Add(mod))
				throw new Exception(mod.fullName + " already loaded");

			Type pluginType = typeof(IPlugin);

			if (mod.filePaths != null)
				foreach (string filePath in mod.filePaths)
				{
					if (!File.Exists(filePath))
					{
						Logger.WriteLine("ERROR: File is missing: " + filePath);
						mod.version.Major = -1;
						continue;
					}
					string ext = Path.GetExtension(filePath);
					if (ext == ".dll")
					{
						Logger.WriteLine("Loading plugins from " + filePath);

						Assembly assembly = Assembly.LoadFrom(filePath);
						if (assembly == null)
							Logger.WriteLine("ERROR: Could not load assembly: " + filePath);
						else
							foreach (Type t in assembly.ExportedTypes)
								if (pluginType.IsAssignableFrom(t))
								{
									Logger.WriteLine("Plugin: " + t.FullName);
									try { chainedPlugins.Add((IPlugin)Activator.CreateInstance(t)); }
									catch (Exception ex) { Logger.WriteLine("ERROR: Could not create instance of type \"" + t.FullName + "\": " + ex); }
								}
					}
				}

			return true;
		}

		private ModVersion AddLocallyCompiled(ModName name, Version version, int seVersion, IEnumerable<string> files, string baseDir)
		{
			int hashCode = name.GetHashCode();
			ModVersion current;
			if (!_data.ModsCurrentVersions.TryGetValue(hashCode, out current))
			{
				current = new ModVersion(name);
				_data.ModsCurrentVersions.Add(hashCode, current);
			}
			current.version = version;
			Logger.WriteLine("mod: " + name.fullName + ", compiled version: " + current.version);
			current.EraseAllFiles();

			string downloadDirectory = PathExtensions.Combine(_directory, "mods", name.fullName);
			Directory.CreateDirectory(downloadDirectory);
			string root = Path.GetPathRoot(baseDir);

			List <string> copied = new List<string>();
			foreach (string fileSource in files)
			{
				// do not allow files to be deployed outside of download folder and avoid an overly complicated structure

				string fullPathSource = Path.GetFullPath(fileSource);
				string fileDestination = null;

				string path = baseDir;
				while (path != root)
				{
					if (fullPathSource.StartsWith(path))
					{
						fileDestination = PathExtensions.Combine(downloadDirectory, fullPathSource.Substring(path.Length));
						break;
					}
					path = Path.GetDirectoryName(path);
				}

				if (fileDestination == null)
					throw new ArgumentException("Cannot construct relative path from " + fullPathSource + " using " + baseDir);

				Logger.WriteLine("Copy: " + fileSource + " to " + fileDestination);
				File.Copy(fullPathSource, fileDestination, true);
				copied.Add(fileDestination);
			}

			current.filePaths = copied.ToArray();
			current.locallyCompiled = true;

			EnsureInConfig(current);
			SaveData();

			if (name.author == Rynchodon && name.repository == LoadArmsRepo)
				Robocopy();

			return current;
		}

		private void Cleanup()
		{
			string myDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";

			foreach (string fileName in new string[] { "ARMS.dll", "ARMS - Release Notes.txt", "ExtendWhitelist.exe", "ExtendWhitelist.dll", "ExtendWhitelist.log", "LoadARMS.log", "Load-ARMS Readme.txt", "Load-ARMS License.txt" })
			{
				string fullPath = myDirectory + fileName;
				if (File.Exists(fullPath))
				{
					Logger.WriteLine("Deleting: " + fullPath);
					File.Delete(fullPath);
				}
			}
		}

		private ModInfo[] DefaultModInfo()
		{
			return new ModInfo[] {
				new ModInfo() { author = Rynchodon, repository = ArmsRepo },
				new ModInfo() { author = Rynchodon, repository = LoadArmsRepo }
			};
		}

	}
}
