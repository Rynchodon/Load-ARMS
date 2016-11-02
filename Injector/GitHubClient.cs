using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;

namespace Rynchodon
{
	public class GitHubClient
	{

		public static void CreateRelease(string repo, string userAgent, string oAuthToken, Release release, params string[] assetsPaths)
		{
			HttpWebRequest request = WebRequest.CreateHttp(@"https://api.github.com/repos/Rynchodon/" + repo + "/releases");
			request.UserAgent = userAgent;
			request.Method = "POST";
			request.Headers.Add("Authorization", "token " + oAuthToken);

			using (Stream requestStream = request.GetRequestStream())
				release.WriteCreateJson(requestStream);
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release));
			using (WebResponse response = request.GetResponse())
			using (Stream responseStream = response.GetResponseStream())
				release = (Release)serializer.ReadObject(responseStream);

			Console.WriteLine("Release id: " + release.id);

			foreach (string asset in assetsPaths)
			{
				string fileName = Path.GetFileName(asset);
				request = WebRequest.CreateHttp(@"https://uploads.github.com/repos/Rynchodon/" + repo + "/releases/" + release.id + "/assets?name=" + fileName);
				request.UserAgent = userAgent;
				request.Method = "POST";
				request.ContentType = "application/" + Path.GetExtension(fileName);
				request.Headers.Add("Authorization", "token " + oAuthToken);

				Stream upStream = request.GetRequestStream();
				FileStream fileRead = new FileStream(asset, FileMode.Open);

				fileRead.CopyTo(upStream);
				Console.WriteLine("Posting: " + fileName);
				request.GetResponse().Dispose();

				fileRead.Dispose();
				upStream.Dispose();
			}

			Console.WriteLine();
			Console.WriteLine("Release successful");
		}

		public static Release[] GetReleases(string repo, string userAgent)
		{
			HttpWebRequest request = WebRequest.CreateHttp(@"https://api.github.com/repos/Rynchodon/" + repo + "/releases");
			request.UserAgent = userAgent;
			using (WebResponse response = request.GetResponse())
			{
				DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release[]));
				using (Stream responseStream = response.GetResponseStream())
					return (Release[])serializer.ReadObject(responseStream);
			}
		}

	}
}
