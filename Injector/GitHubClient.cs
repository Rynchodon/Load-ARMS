using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;

namespace Rynchodon
{
	public class GitHubClient
	{

		public static void CreateRelease(string userAgent, string oAuthToken, Release release, params string[] assetsPaths)
		{
			HttpWebRequest request = WebRequest.CreateHttp(@"https://api.github.com/repos/Rynchodon/ARMS/releases");
			request.UserAgent = userAgent;
			request.Method = "POST";
			request.Headers.Add("Authorization", "token " + oAuthToken);

			release.WriteCreateJson(request.GetRequestStream());
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release));
			release = (Release)serializer.ReadObject(request.GetResponse().GetResponseStream());

			Console.WriteLine("Release id: " + release.id);

			foreach (string asset in assetsPaths)
			{
				string fileName = Path.GetFileName(asset);
				Console.WriteLine("Posting asset: " + fileName);
				request = WebRequest.CreateHttp(@"https://uploads.github.com/repos/Rynchodon/ARMS/releases/" + release.id + "/assets?name=" + fileName);
				request.UserAgent = userAgent;
				request.Method = "POST";
				request.ContentType = "application/dll";
				request.Headers.Add("Authorization", "token " + oAuthToken);

				FileStream fileStream = new FileStream(asset, FileMode.Open);
				fileStream.CopyTo(request.GetRequestStream());
				request.GetResponse();

				Console.WriteLine("Posted: " + fileName);
			}
		}

		public static Release[] GetReleases(string userAgent)
		{
			HttpWebRequest request = WebRequest.CreateHttp(@"https://api.github.com/repos/Rynchodon/ARMS/releases");
			request.UserAgent = userAgent;
			WebResponse response;
			response = request.GetResponse();
			
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Release[]));
			return (Release[])serializer.ReadObject(response.GetResponseStream());
		}

	}
}
