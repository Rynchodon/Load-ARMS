using System;
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
using Sandbox.Graphics.GUI;
using VRage.Plugins;

namespace Rynchodon.Loader
{
	/// <summary>
	/// Main entry point.
	/// </summary>
	public class LoadArms : IPlugin
	{

		private const string
			launcherArgs = "-plugin LoadArms.exe",
			configFilePath = "Config.json",
			dataFilePath = "Data.json";

		private const string authRyn = "Rynchodon", repoLoad = "Load-ARMS", repoArms = "ARMS";

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
					LoadArms instance = new LoadArms();
					MySandboxGame.Static.Invoke(() => instance.Init(MySandboxGame.Static));
					return;
				}
				Thread.Sleep(1);
			}

			throw new TimeoutException("Timed out waiting for instance of MySandboxGame");
		}

		#endregion

		/// <summary>
		/// Starting point when run from command line.
		/// </summary>
		public static void FromCommandLine(string author, string repo, string basedir, string oAuthToken, bool publish, string versionString, IEnumerable<string> files)
		{
			if (_instance == null)
				new LoadArms(false);

			ModName name = new ModName(author, repo);

			Version version;
			if (versionString != null)
				version = new Version(versionString);
			else
			{
				version = new Version();
				foreach (string file in files)
				{
					Version fileVersion = new Version(FileVersionInfo.GetVersionInfo(file));
					if (version.CompareTo(fileVersion) < 0)
						version = fileVersion;
				}
			}

			ModVersion modVersion = _instance.AddLocallyCompiled(name, version, files, basedir);

			if (publish)
				ReleaseCreater.Publish(modVersion, PathExtensions.Combine(_instance._directory, "mods", name.fullName), oAuthToken);
		}

		[DataContract]
		public struct Config
		{
			[DataMember]
			public ModInfo[] GitHubMods;
		}

		[DataContract]
		public struct Data
		{
			[DataMember]
			public Dictionary<int, ModVersion> ModsCurrentVersions;
		}

		private string _directory;
		private Config _config;
		private Data _data;
		private ParallelTasks.Task _task;
		private DownloadProgress.Stats _downProgress = new DownloadProgress.Stats();
		private bool _startedRobocopy, _loadedPlugins;

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
			Logger.logFile = _directory + "Load-ARMS.log";

			if (start)
				_task = ParallelTasks.Parallel.StartBackground(Run);
		}

		private void Run()
		{
			Cleanup();
			Load();
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

			if (!_task.IsComplete)
				MyGuiSandbox.AddScreen(new DownloadProgress(_task, _downProgress));
		}

		/// <summary>
		/// Load plugins, if updating has finished.
		/// </summary>
		void IPlugin.Update()
		{
			if (_instance != this)
				return;

			if (!_loadedPlugins && _task.IsComplete)
			{
				Logger.WriteLine("Finished task, loading plugins");
				_loadedPlugins = true;
				foreach (IPlugin plugin in LoadPlugins())
					plugin.Init(MySandboxGame.Static);

				// TODO: more cleanup
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
			string configFilePath = _directory + LoadArms.configFilePath;
			if (File.Exists(configFilePath))
			{
				try
				{
					DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Config));
					using (FileStream file = new FileStream(configFilePath, FileMode.Open))
						_config = (Config)serializer.ReadObject(file);
					if (_config.GitHubMods != null)
						return;
					Logger.WriteLine("ERORR: Saved config is incomplete");
				}
				catch (Exception ex)
				{
					Logger.WriteLine("ERROR: Failed to read saved config:\n" + ex);
				}
			}

			_config.GitHubMods = DefaultModInfo();
			SaveConfig();
		}

		private void LoadData()
		{
			string dataFilePath = _directory + LoadArms.dataFilePath;
			if (File.Exists(dataFilePath))
			{
				try
				{
					DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Data));
					using (FileStream file = new FileStream(dataFilePath, FileMode.Open))
						_data = (Data)serializer.ReadObject(file);
					if (_data.ModsCurrentVersions != null)
						return;
					Logger.WriteLine("ERROR: Saved data is incomplete");
				}
				catch (Exception ex)
				{
					Logger.WriteLine("ERROR: Failed to read saved data:\n" + ex);
				}
			}

			_data.ModsCurrentVersions = new Dictionary<int, ModVersion>();
			SaveData();
		}

		private void SaveConfig()
		{
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Config));
			using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(new FileStream(_directory + configFilePath, FileMode.Create), Encoding.UTF8, true, true))
				serializer.WriteObject(writer, _config);
		}

		private void SaveData()
		{
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Data));
			using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(new FileStream(_directory + dataFilePath, FileMode.Create), Encoding.UTF8, true, true))
				serializer.WriteObject(writer, _data);
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
				LoadPlugins(chainedPlugins, mod, loadedMods);

			Logger.WriteLine("Adding plugins to MyPlugins");

			FieldInfo MyPlugins__m_plugins = typeof(MyPlugins).GetField("m_plugins", BindingFlags.Static | BindingFlags.NonPublic);
			List<IPlugin> allPlugins = (List<IPlugin>)MyPlugins__m_plugins.GetValue(null);
			allPlugins = new List<IPlugin>(allPlugins);
			allPlugins.AddList(chainedPlugins);
			MyPlugins__m_plugins.SetValue(null, allPlugins);

			return chainedPlugins;
		}

		private bool LoadPlugins(List<IPlugin> chainedPlugins, ModVersion mod, HashSet<ModVersion> loadedMods, int depth = 0)
		{
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

		private ModVersion AddLocallyCompiled(ModName name, Version version, IEnumerable<string> files, string baseDir)
		{
			LoadData();

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
				File.Copy(fullPathSource, fileDestination);
				copied.Add(fileDestination);
			}

			current.filePaths = copied.ToArray();
			current.locallyCompiled = true;

			SaveData();

			if (name.author == authRyn && name.repository == repoLoad)
				Robocopy();

			return current;
		}

		private void Cleanup()
		{
			string myDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";

			foreach (string fileName in new string[] { "ARMS.dll", "ARMS - Release Notes.txt", "ExtendWhitelist.exe", "ExtendWhitelist.dll", "ExtendWhitelist.log", "LoadARMS.log" })
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
				new ModInfo() { author = authRyn, repository = repoArms },
				new ModInfo() { author = authRyn, repository = repoLoad }
			};
		}

	}
}
