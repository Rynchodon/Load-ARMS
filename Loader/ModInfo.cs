using System;
using System.Runtime.Serialization;

namespace LoadArms
{
	/// <summary>
	/// Info needed to download a mod from github.
	/// </summary>
	[DataContract]
	public struct ModInfo : IEquatable<ModInfo>
	{
		[DataMember]
		public string author, repository, compileScripts;
		[DataMember]
		public bool downloadPreRelease;
		[IgnoreDataMember]
		public string releases_site { get { return @"https://api.github.com/repos/" + author + "/" + repository + "/releases"; } }

		public bool Equals(ModInfo other)
		{
			return this.author == other.author && this.repository == other.repository;
		}

		public override int GetHashCode()
		{
			return this.author.GetHashCode() + this.repository.GetHashCode();
		}
	}

	/// <summary>
	/// Information about the current version of a mod.
	/// </summary>
	[DataContract]
	public struct ModVersion
	{
		[DataMember]
		public ModInfo mod;
		[DataMember]
		public Version version;
		[DataMember]
		public bool draft, prerelease, locallyCompiled;
		[DataMember]
		public string[] filePaths;
	}
}
