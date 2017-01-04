using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using VRage.Game;

namespace Rynchodon.Loader
{
	/// <summary>
	/// Connects to GitHub and sends and receives release information.
	/// </summary>
	public class GitHubClient
	{

		private readonly ModName _mod;
		private readonly string _oAuthToken, _userAgent;

		private Task<Release[]> _releaseDownload;
		private bool _releaseDownloadFailed;
		private Release[] _releases;

		/// <summary>True if oAuthToken was provided or retrieved from environment variable, false otherwise.</summary>
		public bool HasOAuthToken { get { return _oAuthToken != null; } }

		/// <summary>
		/// Create a GitHubClient and start downloading release information.
		/// </summary>
		/// <param name="mod">Name of the mod</param>
		/// <param name="oAuthToken">Authentication token for GitHub, not required for updating.</param>
		/// <param name="userAgent">Name of the application</param>
		public GitHubClient(ModName mod, string oAuthToken = null, string userAgent = "Rynchodon:Load-ARMS")
		{
			this._mod = mod;
			this._oAuthToken = oAuthToken ?? Environment.GetEnvironmentVariable("oAuthToken");
			this._userAgent = userAgent;

			_releaseDownload = new Task<Release[]>(DownloadReleases);
			_releaseDownload.Start();
		}

		/// <summary>
		/// Publish a new release.
		/// </summary>
		/// <param name="create">Release information.</param>
		/// <param name="assetsPaths">Assets to be included, do not include folders. It is recommended that you compress the files and pass the zip file path to this method.</param>
		public bool PublishRelease(CreateRelease create, params string[] assetsPaths)
		{
			if (_oAuthToken == null)
				throw new NullReferenceException("Cannot publish if authentication token is null");

			if (assetsPaths == null || assetsPaths.Length == 0)
				throw new ArgumentException("No Assets, cannot publish");
			foreach (string path in assetsPaths)
				if (!File.Exists(path))
					throw new ArgumentException("File does not exist: " + path);

			// check for extant release
			Release[] releases = GetReleases();
			if (releases == null)
			{
				Logger.WriteLine("Failed to download releases");
				return false;
			}
			foreach (Release rel in releases)
				if (rel.version.CompareTo(create.version) == 0)
				{
					Logger.WriteLine("Release exists: " + create.version);
					return false;
				}

			// release needs to be draft while it is being created, in case of failure
			bool draft = create.draft;
			create.draft = true;

			HttpWebRequest request = WebRequest.CreateHttp(_mod.releases_site);
			request.UserAgent = _userAgent;
			request.Method = "POST";
			request.Headers.Add("Authorization", "token " + _oAuthToken);

			using (Stream requestStream = request.GetRequestStream())
				create.WriteCreateJson(requestStream);
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release));
			Release release;
			using (WebResponse response = request.GetResponse())
			using (Stream responseStream = response.GetResponseStream())
				release = (Release)serializer.ReadObject(responseStream);

			Logger.WriteLine("Release id: " + release.id);

			try
			{
				foreach (string asset in assetsPaths)
				{
					string fileName = Path.GetFileName(asset);
					request = WebRequest.CreateHttp(_mod.uploads_site + '/' + release.id + "/assets?name=" + fileName);
					request.UserAgent = _userAgent;
					request.Method = "POST";
					request.ContentType = "application/" + Path.GetExtension(fileName).Substring(1);
					request.Headers.Add("Authorization", "token " + _oAuthToken);

					using (Stream upStream = request.GetRequestStream())
					using (FileStream fileRead = new FileStream(asset, FileMode.Open))
						fileRead.CopyTo(upStream);

					Logger.WriteLine("Posting: " + fileName);
					request.GetResponse().Dispose();
				}
			}
			catch (WebException ex)
			{
				Logger.WriteLine("Failed to post asset(s)\n" + ex);
				DeleteRelease(release);
				throw ex;
			}

			if (!draft)
			{
				release.draft = draft;
				EditRelease(ref release);
			}

			_releases = null; // needs to be updated

			Logger.WriteLine("Release published");
			return true;
		}

		/// <summary>
		/// Edit information about a release.
		/// </summary>
		/// <param name="edit">The new information for the release.</param>
		public void EditRelease(ref Release edit)
		{
			if (_oAuthToken == null)
				throw new NullReferenceException("Cannot edit if authentication token is null");

			HttpWebRequest request = WebRequest.CreateHttp(_mod.releases_site + '/' + edit.id);
			request.UserAgent = _userAgent;
			request.Method = "PATCH";
			request.Headers.Add("Authorization", "token " + _oAuthToken);

			using (Stream requestStream = request.GetRequestStream())
				edit.WriteCreateJson(requestStream);
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release));
			using (WebResponse response = request.GetResponse())
			using (Stream responseStream = response.GetResponseStream())
				edit = (Release)serializer.ReadObject(responseStream);

			Logger.WriteLine("Release edited: " + edit.tag_name);

			_releases = null; // needs to be updated
		}

		/// <summary>
		/// Delete a release from GitHub
		/// </summary>
		/// <param name="delete">The release to delete</param>
		public void DeleteRelease(Release delete)
		{
			if (_oAuthToken == null)
				throw new NullReferenceException("Cannot delete if authentication token is null");

			HttpWebRequest request = WebRequest.CreateHttp(_mod.releases_site + '/' + delete.id);
			request.UserAgent = _userAgent;
			request.Method = "DELETE";
			request.Headers.Add("Authorization", "token " + _oAuthToken);

			using (Stream requestStream = request.GetRequestStream())
				request.GetResponse().Dispose();

			Logger.WriteLine("Release deleted: " + delete.tag_name);

			_releases = null; // needs to be updated
		}

		/// <summary>
		/// Get all the releases, the value is cached and only updated after PublishRelease, EditRelease, or DeleteRelease.
		/// The array should not be modified.
		/// </summary>
		/// <returns>An array representing all the GitHub releases for this mod.</returns>
		public Release[] GetReleases()
		{
			if (_releases == null)
			{
				if (_releaseDownload == null)
				{
					if (_releaseDownloadFailed)
						return null;
					_releaseDownload = new Task<Release[]>(DownloadReleases);
					_releaseDownload.Start();
				}
				try
				{
					_releaseDownload.Wait();
					_releases = _releaseDownload.Result;
				}
				catch (AggregateException aex)
				{
					_releaseDownloadFailed = true;
					Logger.WriteLine("Failed to download releases:\n" + aex);
				}
				_releaseDownload.Dispose();
				_releaseDownload = null;
			}
			return _releases;
		}

		/// <summary>
		/// Download all releases for this mod from GitHub.
		/// </summary>
		/// <returns>All the releases for this mod.</returns>
		private Release[] DownloadReleases()
		{
			HttpWebRequest request = WebRequest.CreateHttp(_mod.releases_site);
			request.UserAgent = _userAgent;
			if (_oAuthToken != null)
				request.Headers.Add("Authorization", "token " + _oAuthToken);

			using (WebResponse response = request.GetResponse())
			{
				DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release[]));
				using (Stream responseStream = response.GetResponseStream())
					return (Release[])serializer.ReadObject(responseStream);
			}
		}

		/// <summary>
		/// Download an update for a mod.
		/// </summary>
		/// <param name="info">Information about the mod.</param>
		/// <param name="current">The current version, to determine if the mod needs to be updated</param>
		/// <param name="destinationDirectory">Path to Load-ARMS folder, \mods\info.fullName will be appended.</param>
		/// <returns>True iff the mod was updated.</returns>
		internal bool Update(ModInfo info, ModVersion current, string destinationDirectory)
		{
			Release[] releases = GetReleases();
			if (releases == null)
				// already complained about it in depth
				return false;

			Release mostRecent = null;
			foreach (Release rel in releases)
			{
				if (rel.draft)
					continue;
				if (rel.prerelease && !info.downloadPreRelease)
					continue;

				if (MyFinalBuildConstants.IS_STABLE ? rel.version.StableBuild : rel.version.UnstableBuild)
					if (mostRecent == null || mostRecent.version.CompareTo(rel.version) < 0)
						mostRecent = rel;
			}

			if (mostRecent == null)
			{
				Logger.WriteLine("ERROR: No available releases");
				return false;
			}

			int relative = mostRecent.version.CompareTo(current.version);
			if (relative == 0)
			{
				Logger.WriteLine("Up-to-date: " + current.version);
				return false;
			}
			if (relative < 0)
			{
				if (current.locallyCompiled)
				{
					Logger.WriteLine("Locally compiled version: " + current.version);
					return false;
				}
				Logger.WriteLine("Rolling back version: " + current.version);
			}

			if (mostRecent.assets == null || mostRecent.assets.Length == 0)
			{
				Logger.WriteLine("ERROR: Release has no assets");
				return false;
			}

			current.EraseAllFiles();

			Logger.WriteLine("Downloading version: " + mostRecent.version);

			List<string> filePaths = new List<string>();
			destinationDirectory = destinationDirectory + "mods\\" + info.fullName + "\\";
			Directory.CreateDirectory(destinationDirectory);

			foreach (Release.Asset asset in mostRecent.assets)
			{
				Logger.WriteLine("Downloading asset: " + asset.name);
				HttpWebRequest request = WebRequest.CreateHttp(asset.browser_download_url);
				request.Accept = "application/octet-stream";
				request.UserAgent = _userAgent;

				WebResponse response = request.GetResponse();
				Stream responseStream = response.GetResponseStream();
				string assetDestination = destinationDirectory + asset.name;

				if (asset.name.EndsWith(".zip"))
				{
					using (FileStream zipFile = new FileStream(assetDestination, FileMode.Create))
						responseStream.CopyTo(zipFile);

					Logger.WriteLine("Unpacking: " + asset.name);
					using (ZipArchive archive = ZipFile.OpenRead(assetDestination))
						foreach (ZipArchiveEntry entry in archive.Entries)
						{
							string entryDestination = destinationDirectory + entry.FullName;
							Directory.CreateDirectory(Path.GetDirectoryName(entryDestination));

							if (File.Exists(entryDestination))
							{
								Logger.WriteLine("ERROR: File exists: " + entryDestination);
								return false;
							}

							filePaths.Add(entryDestination);
							entry.ExtractToFile(entryDestination);
						}

					File.Delete(assetDestination);
				}
				else
				{
					if (File.Exists(assetDestination))
					{
						Logger.WriteLine("ERROR: File exists: " + assetDestination);
						return false;
					}

					filePaths.Add(assetDestination);
					using (FileStream file = new FileStream(assetDestination, FileMode.CreateNew))
						responseStream.CopyTo(file);
				}

				responseStream.Dispose();
				response.Dispose();
			}

			current.version = mostRecent.version;
			current.locallyCompiled = false;
			current.filePaths = filePaths.ToArray();

			return true;
		}

	}
}
