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

		[DataContract]
		private class Input
		{
			[DataMember]
			public string ZipFileName = null;
			[DataMember]
			public CreateRelease Release = new CreateRelease();
		}

		public static void Publish(ModVersion modVersion, string directory, string oAuthToken)
		{
			GitHubClient client = new GitHubClient(modVersion, oAuthToken);
			if (!client.HasOAuthToken)
				throw new ArgumentException("Need oAuthToken");

			Input input = new Input();
			input.Release.version = modVersion.version;
			input.Release.draft = true;

			string zipTempFile = PathExtensions.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
			Task compress = new Task(() => CompressFiles(zipTempFile, directory, modVersion.filePaths));
			compress.Start();

			string releaseFile = PathExtensions.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
			try
			{
				DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Input));
				using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(new FileStream(releaseFile, FileMode.CreateNew), Encoding.UTF8, true, true))
					serializer.WriteObject(writer, input);

				while (true)
				{
					Process edit = Process.Start(releaseFile);
					edit.WaitForExit();

					using (FileStream file = new FileStream(releaseFile, FileMode.Open))
					using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(file, new XmlDictionaryReaderQuotas()))
						try
						{
							input = (Input)serializer.ReadObject(reader);
							break;
						}
						catch (SerializationException ex)
						{
							Console.WriteLine(ex.Message);
							if (MessageBox.Show(ex.Message, "Error", MessageBoxButtons.RetryCancel) == DialogResult.Cancel)
								return;
						}
				}

				// force release version to match mod version
				input.Release.version = modVersion.version;

				Console.WriteLine("Release created");
				compress.Wait();

				if (input.ZipFileName == null)
					input.ZipFileName = modVersion.repository + ".zip";
				else if (!input.ZipFileName.EndsWith(".zip"))
					input.ZipFileName = input.ZipFileName + ".zip";

				input.ZipFileName = PathExtensions.Combine(Path.GetTempPath(), input.ZipFileName);
				if (File.Exists(input.ZipFileName))
					File.Delete(input.ZipFileName);
				File.Move(zipTempFile, input.ZipFileName);

				if (client.PublishRelease(input.Release, input.ZipFileName))
					Console.WriteLine("Release published");
				else
					Console.WriteLine("Publish failed, see log for details");
			}
			finally
			{
				compress.Wait();
				if (File.Exists(releaseFile))
					File.Delete(releaseFile);
				if (File.Exists(zipTempFile))
					File.Delete(zipTempFile);
				if (File.Exists(input.ZipFileName))
					File.Delete(input.ZipFileName);
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
