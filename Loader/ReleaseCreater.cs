using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Rynchodon.Loader
{
	static class ReleaseCreater
	{

		public static void Publish(ModVersion modVersion, string directory, string oAuthToken)
		{
			GitHubClient client = new GitHubClient(modVersion, oAuthToken);
			if (!client.HasOAuthToken)
				throw new ArgumentException("Need oAuthToken");

			CreateRelease create = new CreateRelease();
			create.version = modVersion.version;
			create.draft = true;
			create.body = "body";
			create.name = "name";
			create.target_commitish = "master";

			string zipTempFile = PathExtensions.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
			Task compress = new Task(() => CompressFiles(zipTempFile, directory, modVersion.filePaths));
			compress.Start();
			string zipFileName = null;

			string releaseFile = PathExtensions.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
			try
			{
				DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CreateRelease));
				using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(new FileStream(releaseFile, FileMode.CreateNew), Encoding.UTF8, true, true))
					serializer.WriteObject(writer, create);

				while (true)
				{
					Process edit = Process.Start(releaseFile);
					edit.WaitForExit();

					using (FileStream file = new FileStream(releaseFile, FileMode.Open))
					using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(file, new XmlDictionaryReaderQuotas()))
						try
						{
							create = (CreateRelease)serializer.ReadObject(reader);
							break;
						}
						catch (SerializationException ex)
						{
							Console.WriteLine(ex.Message);
							if (MessageBox.Show(ex.Message, "Error", MessageBoxButtons.RetryCancel) == DialogResult.Cancel)
								return;
						}
				}

				Console.WriteLine("Release created");
				compress.Wait();

				zipFileName = PathExtensions.Combine(Path.GetTempPath(), create.tag_name + ".zip");
				if (File.Exists(zipFileName))
					File.Delete(zipFileName);
				File.Move(zipTempFile, zipFileName);

				client.PublishRelease(create, zipFileName);

				Logger.WriteLine("client finished");
			}
			finally
			{
				compress.Wait();
				if (File.Exists(releaseFile))
					File.Delete(releaseFile);
				if (File.Exists(zipTempFile))
					File.Delete(zipTempFile);
				//if (File.Exists(zipFileName))
				//	File.Delete(zipFileName);
			}
		}

		private static void CompressFiles(string zipFileName, string directory, IEnumerable<string> filePaths)
		{
			using (FileStream zipFile = new FileStream(zipFileName, FileMode.Create))
			using (ZipArchive archive = new ZipArchive(zipFile, ZipArchiveMode.Create))
				foreach (string file in filePaths)
					archive.CreateEntryFromFile(file, file.Substring(directory.Length + 1));
		}

	}
}
