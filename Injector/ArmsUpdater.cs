using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using VRage.Game;

namespace Injector
{
	public static class ArmsUpdater
	{

		[DataContract]
		private class Release
		{
#pragma warning disable CS0649
			[DataContract]
			public class Asset
			{
				[DataMember]
				public string name, browser_download_url;
			}
			[DataMember]
			public string tag_name, name, created_at;
			[DataMember]
			public Asset[] assets;

			private DateTime value_createdAt = DateTime.MinValue;
			public DateTime CreatedAt
			{
				get
				{
					if (value_createdAt == DateTime.MinValue)
						value_createdAt = DateTime.Parse(created_at);
					return value_createdAt;
				}
			}

			public string BestName { get { return string.IsNullOrWhiteSpace(tag_name) ? name : tag_name; } }
#pragma warning restore CS0649
		}

		private const string ArmsDll = "ARMS.dll";

		public static void UpdateArms()
		{
			const string stable = "-stable", unstable = "-unstable";
			const string userAgent = "ARMS-Updater";

			HttpWebRequest request = WebRequest.CreateHttp(@"https://api.github.com/repos/Rynchodon/Autopilot/releases");
			request.UserAgent = userAgent;
			WebResponse response = request.GetResponse();

			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release[]));
			Release[] allReleases = (Release[])serializer.ReadObject(response.GetResponseStream());

			Release bestRelease = null;

			foreach (Release rel in allReleases)
				if (MyFinalBuildConstants.IS_STABLE ? rel.BestName.Contains(stable) : rel.BestName.Contains(unstable))
					if (bestRelease == null || bestRelease.CreatedAt < rel.CreatedAt)
						bestRelease = rel;

			if (bestRelease == null)
			{
				WriteLine("No releases found");
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
				WriteLine("Could not get asset");
				return;
			}

			if (CurrentVersionIsNewer(bestRelease))
				return;

			WriteLine("Downloading update: " + bestRelease.BestName);
			request = WebRequest.CreateHttp(asset.browser_download_url);
			request.UserAgent = userAgent;
			response = request.GetResponse();

			FileStream file = File.Create("ARMS.dll");
			response.GetResponseStream().CopyTo(file);
			file.Close();

			WriteLine("ARMS has been updated");
		}

		private static bool CurrentVersionIsNewer(Release rel)
		{
			string armsDllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + '\\' + ArmsDll;
			if (!File.Exists(armsDllPath))
				return false;
			FileVersionInfo currentVersion = FileVersionInfo.GetVersionInfo(armsDllPath);

			Regex versionParts = new Regex(@"(\d+)\.(\d+)\.(\d+)\.?(\d*)");
			Match match = versionParts.Match(rel.BestName);
			if (!match.Success)
			{
				WriteLine("Failed to get version from " + rel.BestName);
				return true;
			}

			if (Math.Max(currentVersion.FileMajorPart, currentVersion.ProductMajorPart) < int.Parse(match.Groups[1].Value))
			{
				WriteLine("New major version: " + match.Groups[0].Value);
				return false;
			}
			if (Math.Max(currentVersion.FileMinorPart, currentVersion.ProductMinorPart) < int.Parse(match.Groups[2].Value))
			{
				WriteLine("New minor version: " + match.Groups[0].Value);
				return false;
			}
			if (Math.Max(currentVersion.FileBuildPart, currentVersion.ProductBuildPart) < int.Parse(match.Groups[3].Value))
			{
				WriteLine("New build version: " + match.Groups[0].Value);
				return false;
			}
			string group4 = match.Groups[4].Value;
			if (!string.IsNullOrWhiteSpace(group4) && Math.Max(currentVersion.FilePrivatePart, currentVersion.ProductPrivatePart) < int.Parse(match.Groups[4].Value))
			{
				WriteLine("New revision version: " + match.Groups[0].Value);
				return false;
			}

			WriteLine("ARMS is up to date");
			return true;
		}

		private static void WriteLine(string line, bool skipMemeberName = false, [CallerMemberName] string memberName = null)
		{
			if (!skipMemeberName)
				line = DateTime.Now + ": " + line;
			Console.WriteLine(line);
		}

	}
}
