using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

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
				Console.Write("Posting asset " + fileName + ": ");

				using (WebClient client = new WebClient())
				{
					client.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
					client.Headers.Add(HttpRequestHeader.ContentType, "application/dll");
					client.Headers.Add(HttpRequestHeader.Authorization, "token " + oAuthToken);

					int cursorLeft = Console.CursorLeft, cursorTop = Console.CursorTop;
					long lastPercent = -1L;
					object locker = new object();
					UploadProgressChangedEventHandler handler = (sender, e) => {
						long percent = e.BytesSent * 100L / e.TotalBytesToSend;
						lock (locker)
						{
							if (percent == lastPercent)
								return;
							lastPercent = percent;
							Console.SetCursorPosition(cursorLeft, cursorTop);
							if (percent < 10)
								Console.Write(' ');
							if (percent < 100)
								Console.Write(' ');
							Console.Write(percent);
							Console.Write('%');
						}
					};

					client.UploadProgressChanged += handler;
					Task uploadTask = client.UploadFileTaskAsync(@"https://uploads.github.com/repos/Rynchodon/ARMS/releases/" + release.id + "/assets?name=" + fileName, asset);
					uploadTask.Wait();
					uploadTask.Dispose();
				}

				Console.WriteLine();
			}
		}

		public static Release[] GetReleases(string userAgent)
		{
			HttpWebRequest request = WebRequest.CreateHttp(@"https://api.github.com/repos/Rynchodon/ARMS/releases");
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
