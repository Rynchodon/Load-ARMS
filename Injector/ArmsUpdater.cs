using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using VRage.Game;

namespace Rynchodon
{
	public static class ArmsUpdater
	{
		public const string ArmsZip = "ARMS.zip", ArmsDll = "ARMS.dll", ArmsReleaseNotes = "ARMS - Release Notes.txt", User_Agent = "ARMS-Updater";

		public static void UpdateArms()
		{
			const string stable = "-stable", unstable = "-unstable";

			Release[] allReleases;
			try { allReleases = GitHubClient.GetReleases("ARMS", User_Agent); }
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
			{
				if (a.name == ArmsZip)
				{
					asset = a;
					break;
				}
				if (a.name == ArmsDll)
					asset = a;
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

			DownloadAsset(bestRelease, asset);

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

		private static void DownloadAsset(Release bestRelease, Release.Asset asset)
		{
			WriteLine("Downloading update: " + bestRelease.BestName);
			HttpWebRequest request = WebRequest.CreateHttp(asset.browser_download_url);
			request.UserAgent = User_Agent;

			WebResponse response = request.GetResponse();
			Stream responseStream = response.GetResponseStream();

			if (asset.name == ArmsZip)
			{
				FileStream zipFile = File.Create(ArmsZip);
				responseStream.CopyTo(zipFile);
				zipFile.Dispose();
				ZipFile.ExtractToDirectory(ArmsZip, ".");
				File.Delete(ArmsZip);
			}
			else if (asset.name == ArmsDll)
			{
				FileStream dllFile = File.Create(ArmsDll);
				responseStream.CopyTo(dllFile);
				dllFile.Dispose();
			}
			else
				throw new Exception("Unknown asset: " + asset.name);

			responseStream.Dispose();
			response.Dispose();
		}

		private static void WriteLine(string line, bool skipMemeberName = false, [CallerMemberName] string memberName = null)
		{
			if (!skipMemeberName)
				line = DateTime.Now + ": " + line;
			Console.WriteLine(line);
		}

	}
}
