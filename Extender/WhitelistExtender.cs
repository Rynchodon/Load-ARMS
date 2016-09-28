using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using RGiesecke.DllExport;
using Sandbox.Engine.Utils;
using Sandbox.ModAPI;
using VRage.Scripting;

namespace Extender
{
	public class WhitelistExtender
	{

		public const string LOG_NAME = "ExtendWhitelist.log";

		static TextWriter _log;

		static bool run = false;

		[DllExport]
		public static void RunInSEProcess()
		{
			try
			{
				if (run)
				{
					WriteLine("Whitelist already updated");
					return;
				}

				if (!WaitForIlInit())
					return;

				try { DoExtend(); }
				catch (MyWhitelistException ex)
				{
					WriteLine("Failed to update whitelist: ");
					WriteLine(ex.ToString());
				}
			}

			finally
			{
				_log.Close();
				_log = null;
				run = true;
			}
		}

		private static bool WaitForIlInit()
		{
			int assemblies = 0, prevAssemblies, failedAt = 0;

			for (int loop = 0; loop < 6000; loop++)
			{
				prevAssemblies = assemblies;
				assemblies = MyScriptCompiler.Static.AssemblyLocations.Count;

				if (assemblies != 0 && assemblies == prevAssemblies && assemblies != failedAt)
				{
					try
					{
						foreach (string assembly in MyScriptCompiler.Static.AssemblyLocations)
						{
							if (Path.GetFileName(assembly) == "SpaceEngineers.Game.dll")
							{
								WriteLine("IL Compiler has requisite assemblies");
								return true;
							}
							else
								failedAt = assemblies;
						}
					}
					catch (InvalidOperationException) { }
				}
				Thread.Sleep(100);
			}

			WriteLine("Requisite assemblies not loaded into IL Compiler");
			return false;
		}

		private static void DoExtend()
		{
			if (MyFakes.ENABLE_ROSLYN_SCRIPTS)
			{
				using (var handle = MyScriptCompiler.Static.Whitelist.OpenBatch())
				{
					handle.AllowNamespaceOfTypes(MyWhitelistTarget.ModApi,
						typeof(Sandbox.Game.Entities.Blocks.IMyTriggerableBlock),
						typeof(Sandbox.Game.Entities.Cube.ConnectivityResult),
						typeof(Sandbox.Game.Gui.HudBlockInfoExtensions),
						typeof(Sandbox.Game.Weapons.MyAmmoBase),
						typeof(Sandbox.Game.World.MyAudioComponent),
						typeof(SpaceEngineers.Game.Entities.Blocks.MyAirVent),
						typeof(VRage.Game.Gui.MyHudEntityParams));

					handle.AllowTypes(MyWhitelistTarget.ModApi, typeof(Entities.Blocks.MySpaceProjector));

					handle.AllowTypes(MyWhitelistTarget.ModApi,
						typeof(System.ArgumentOutOfRangeException),
						typeof(System.IConvertible),
						typeof(System.IndexOutOfRangeException),
						typeof(System.NotImplementedException),
						typeof(System.ObjectDisposedException),
						typeof(System.ObsoleteAttribute),
						typeof(System.OverflowException),
						typeof(System.ThreadStaticAttribute),
						typeof(System.TypeCode),
						typeof(System.Diagnostics.ConditionalAttribute),
						typeof(System.Runtime.CompilerServices.CallerFilePathAttribute),
						typeof(System.Runtime.CompilerServices.CallerLineNumberAttribute),
						typeof(System.Runtime.CompilerServices.CallerMemberNameAttribute),
						typeof(System.Runtime.InteropServices.FieldOffsetAttribute));
				}
			}
			else
			{
				WriteLine("Extended Whitelist requires rosyln scripts.");
				return;
			}

			WriteLine("Whitelist updated");
		}

		private static void WriteLine(string line, [CallerMemberName] string memberName = null)
		{
			line = memberName + ": " + line;
			Console.WriteLine(line);

			if (_log == null)
				_log = new StreamWriter(LOG_NAME);
			_log.WriteLine(line);
			_log.Flush();
		}

	}
}
