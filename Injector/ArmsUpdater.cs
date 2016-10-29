using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using VRage.Game;

namespace Rynchodon
{
	public static class ArmsUpdater
	{
		public const string ArmsDll = "ARMS.dll", ArmsReleaseNotes = "ARMS - Release Notes.txt";

		public static void UpdateArms()
		{
			const string stable = "-stable", unstable = "-unstable";
			const string userAgent = "ARMS-Updater";

			Release[] allReleases;
			try { allReleases = GitHubClient.GetReleases(userAgent); }
			catch (WebException ex)
			{
				WriteLine("Failed to connect to github:\n" + ex);
				return;
			}

			Release bestRelease = null;
			foreach (Release rel in allReleases)
				if (MyFinalBuildConstants.IS_STABLE ? rel.BestName.Contains(stable) : rel.BestName.Contains(unstable))
					if (bestRelease == null || bestRelease.CompareTo(rel) < 0)
						bestRelease = rel;

			if (bestRelease == null)
			{
				WriteLine("ERROR: No releases found");
				return;
			}

			Release.Asset asset = null;
			foreach (Release.Asset a in bestRelease.assets)
				if (a.name == ArmsDll)
				{
					asset = a;
					break;
				}

			if (asset == null)
			{
				WriteLine("ERROR: Could not get asset");
				return;
			}

			if (!NeedsUpdate(bestRelease))
			{
				WriteLine("ARMS is up to date");
				return;
			}

			WriteLine("Downloading update: " + bestRelease.BestName);
			HttpWebRequest request = WebRequest.CreateHttp(asset.browser_download_url);
			request.UserAgent = userAgent;
			WebResponse response = request.GetResponse();

			FileStream file = File.Create("ARMS.dll");
			response.GetResponseStream().CopyTo(file);
			file.Close();

			File.WriteAllText(ArmsReleaseNotes, bestRelease.body);
			WriteLine("ARMS has been updated");
		}
		
		private static bool NeedsUpdate(Release rel)
		{
			string armsDllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + '\\' + ArmsDll;
			if (!File.Exists(armsDllPath))
				return true;

			Version currentVersion = new Version(FileVersionInfo.GetVersionInfo(armsDllPath));
			return rel.Version.CompareTo(currentVersion) > 0;
		}

		private static void WriteLine(string line, bool skipMemeberName = false, [CallerMemberName] string memberName = null)
		{
			if (!skipMemeberName)
				line = DateTime.Now + ": " + line;
			Console.WriteLine(line);
		}

	}
}
