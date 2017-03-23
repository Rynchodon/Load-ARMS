using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace Rynchodon.Loader
{
	/// <summary>
	/// Basic data about a mod.
	/// </summary>
	[DataContract]
	public class ModName : IEquatable<ModName>
	{
		[DataMember]
		public string author, repository;

		[IgnoreDataMember]
		public string fullName { get { return author + '.' + repository; } }

		[IgnoreDataMember]
		public string releases_site { get { return @"https://api.github.com/repos/" + author + "/" + repository + "/releases"; } }

		[IgnoreDataMember]
		public string uploads_site { get { return @"https://uploads.github.com/repos/" + author + "/" + repository + "/releases"; } }

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

		public override string ToString()
		{
			return fullName;
		}
	}

	/// <summary>
	/// Info needed to download a mod from github.
	/// </summary>
	[DataContract]
	internal class ModInfo : ModName
	{
#pragma warning disable CS0649
		[DataMember]
		public bool downloadPrerelease;
#pragma warning restore CS0649
	}

	/// <summary>
	/// Information about the current version of a mod.
	/// </summary>
	[DataContract]
	internal class ModVersion : ModName
	{
		/// <summary>The version of the mod.</summary>
		[DataMember]
		public Version version;
		/// <summary>The version of Space Engineers the mod was compiled against.</summary>
		[DataMember]
		public int seVersion;
		/// <summary>Allows version to be greater than latest release.</summary>
		[DataMember]
		public bool locallyCompiled;
		[DataMember]
		public string[] filePaths;
#pragma warning disable CS0649
		/// <summary>Mods that need to be loaded before this one. Should not contain subclasses of ModName.</summary>
		[DataMember]
		public ModName[] requirements;
#pragma warning restore CS0649

		public ModVersion() { }

		public ModVersion(ModName name)
		{
			this.author = name.author;
			this.repository = name.repository;
		}

		/// <summary>
		/// Erase all files associated with this version.
		/// </summary>
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
