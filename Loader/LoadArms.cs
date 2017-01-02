using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using RGiesecke.DllExport;
using Sandbox;
using VRage.Plugins;

namespace Rynchodon.Loader
{
	public class LoadArms : IPlugin
	{

		private const string
			launcherArgs = "-plugin LoadArms.exe",
			configFilePath = "Config.json",
			dataFilePath = "Data.json";

		private const string authRyn = "Rynchodon", repoLoad = "Load-ARMS", repoArms = "ARMS";

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

		[DllExport]
		public static void RunInSEProcess()
		{
			for (int i = 0; i < 1000000; ++i)
			{
				if (MySandboxGame.Static != null)
				{
					LoadArms instance = new LoadArms();
					MySandboxGame.Static.Invoke(() => instance.Init(MySandboxGame.Static));
					return;
				}
				Thread.Sleep(1);
			}

			throw new TimeoutException("Timed out waiting for instance of MySandboxGame");
		}

		#endregion

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
			public List<ModVersion> ModsCurrentVersions;
		}

		private string _directory;
		private Config _config;
		private Data _data;
		private Task _task;

		public LoadArms()
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

			_task = new Task(Run);
			_task.Start();
		}

		private void Run()
		{
			Cleanup();
			Load();
			//UpdateMods();
			SaveData();
		}

		public void Dispose()
		{
			if (_instance != this)
				return;

			string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\LoadArms.exe";
			string updateExe = _directory + "\\mods\\Rynchodon.Load-ARMS\\LoadArms.exe";
			if (!File.Exists(updateExe))
				return;

			for (int i = 0; i < 60; ++i)
				try
				{
					File.Delete(exePath);
					File.Move(updateExe, exePath);
					Logger.WriteLine("Updated " + Path.GetFileName(exePath));
					return;
				}
				catch (UnauthorizedAccessException) { Thread.Sleep(1000); }

			Logger.WriteLine("ERROR: Failed to update " + Path.GetFileName(exePath));
		}

		public void Init(object gameInstance)
		{
			if (_instance != this)
				return;

			_task.Wait();
			_task.Dispose();
			_task = null;

			foreach (IPlugin plugin in LoadPlugins())
				plugin.Init(gameInstance);
		}

		public void Update() { }

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

			_data.ModsCurrentVersions = new List<ModVersion>();
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
			foreach (ModInfo mod in _config.GitHubMods)
			{
				Logger.WriteLine("mod: " + mod.fullName);

				GitHubClient client = new GitHubClient(mod);

				foreach (ModVersion current in _data.ModsCurrentVersions)
				{
					if (mod.Equals(current))
					{
						if (client.Update(mod, current, _directory))
							Logger.WriteLine("Updated");
						client = null;
					}
				}

				if (client != null)
				{
					ModVersion current = new ModVersion(mod);
					if (client.Update(mod, current, _directory))
					{
						Logger.WriteLine("New download");
						_data.ModsCurrentVersions.Add(current);
					}
				}
			}
		}

		private List<IPlugin> LoadPlugins()
		{
			List<IPlugin> chainedPlugins = new List<IPlugin>();
			Type pluginType = typeof(IPlugin);

			foreach (ModVersion mod in _data.ModsCurrentVersions)
			{
				if (mod.author == authRyn && mod.repository == repoLoad)
					continue;

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
			}

			Logger.WriteLine("Adding plugins to MyPlugins");

			FieldInfo MyPlugins__m_plugins = typeof(MyPlugins).GetField("m_plugins", BindingFlags.Static | BindingFlags.NonPublic);
			List<IPlugin> allPlugins = (List<IPlugin>)MyPlugins__m_plugins.GetValue(null);
			allPlugins = new List<IPlugin>(allPlugins);
			allPlugins.AddList(chainedPlugins);
			MyPlugins__m_plugins.SetValue(null, allPlugins);

			return chainedPlugins;
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
