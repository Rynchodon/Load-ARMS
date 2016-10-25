using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using RGiesecke.DllExport;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.World;
using VRage.ObjectBuilders;

namespace Extender
{
	public class ArmsLoader
	{

		public const string LOG_NAME = "ExtendWhitelist.log";

		static TextWriter _log;
		static Assembly _armsAssembly;

		[DllExport]
		public static void RunInSEProcess()
		{
			try
			{
				string myDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string dllPath = myDirectory + "\\ARMS.dll";

				if (!File.Exists(dllPath))
				{
					WriteLine("ARMS.dll not found");
					return;
				}

				WriteLine("Trying to load:\n" + dllPath);
				_armsAssembly = Assembly.LoadFile(dllPath);

				if (_armsAssembly == null)
				{
					WriteLine("Failed to load ARMS assembly");
					return;
				}
				
				MySession.OnLoading += CheckForArmsAndLoad;

				WriteLine("ARMS ready");
			}
			catch (Exception ex)
			{
				MySession.OnLoading -= CheckForArmsAndLoad;
				WriteLine(ex.ToString());
			}
		}

		private static void CheckForArmsAndLoad()
		{
			if (MyDefinitionManager.Static.GetCubeBlockDefinition(new SerializableDefinitionId(typeof(MyObjectBuilder_Cockpit), "Autopilot-Block_Large")) == null)
				return;

			WriteLine("ARMS models loaded, registering ARMS assembly");
			MySession.Static.RegisterComponentsFromAssembly(_armsAssembly, true);
		}
		
		private static void WriteLine(string line, [CallerMemberName] string memberName = null)
		{
			line = DateTime.Now + ": " + line;
			//Console.WriteLine(line);

			if (_log == null)
				_log = new StreamWriter(LOG_NAME);
			_log.WriteLine(line);
			_log.Flush();
		}

	}
}
