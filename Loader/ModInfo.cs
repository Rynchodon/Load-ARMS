using System;
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

		public bool Equals(ModName other)
		{
			return this.author == other.author && this.repository == other.repository;
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

		public override int GetHashCode()
		{
			return this.author.GetHashCode() + this.repository.GetHashCode();
		}
	}

	/// <summary>
	/// Information about the current version of a mod.
	/// </summary>
	[DataContract]
	public class ModVersion : ModName
	{
		[DataMember]
		public Version version;
		[DataMember]
		public bool locallyCompiled;
		[DataMember]
		public string[] filePaths;
		/// <summary>Should not contain subclasses of ModName</summary>
		[DataMember]
		public ModName[] requirements;

		public ModVersion() { }

		public ModVersion(ModName name)
		{
			this.author = name.author;
			this.repository = name.repository;
		}
	}
}
