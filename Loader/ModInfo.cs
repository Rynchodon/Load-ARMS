using System;
using System.IO;
using System.Runtime.Serialization;

namespace Rynchodon.Loader
{
	[DataContract]
	public class ModName : IEquatable<ModName>
	{
		[DataMember]
		public string author, repository;

		[IgnoreDataMember]
		public string fullName { get { return author + '.' + repository; } }

		[IgnoreDataMember]
		public string releases_site { get { return @"https://api.github.com/repos/" + author + "/" + repository + "/releases"; } }

		public ModName() { }

		public ModName(string author, string repository)
		{
			this.author = author;
			this.repository = repository;
		}

		public bool Equals(ModName other)
		{
			return this.author == other.author && this.repository == other.repository;
		}

		public override int GetHashCode()
		{
			return fullName.GetHashCode();
		}
	}

	/// <summary>
	/// Info needed to download a mod from github.
	/// </summary>
	[DataContract]
	public class ModInfo : ModName
	{
		[DataMember]
		public bool downloadPreRelease;
	}

	/// <summary>
	/// Information about the current version of a mod.
	/// </summary>
	[DataContract]
	public class ModVersion : ModName
	{
		[DataMember]
		public Version version;
		/// <summary>Allows version to be greater than latest release.</summary>
		[DataMember]
		public bool locallyCompiled;
		[DataMember]
		public string[] filePaths;
		/// <summary>Mods that need to be loaded before this one. Should not contain subclasses of ModName.</summary>
		[DataMember]
		public ModName[] requirements;

		public ModVersion() { }

		public ModVersion(ModName name)
		{
			this.author = name.author;
			this.repository = name.repository;
		}

		public void EraseAllFiles()
		{
			if (filePaths == null)
				return;

			foreach (string filePath in filePaths)
				if (File.Exists(filePath))
				{
					Logger.WriteLine("Delete file: " + filePath);
					File.Delete(filePath);
				}

			filePaths = null;
		}
	}
}
