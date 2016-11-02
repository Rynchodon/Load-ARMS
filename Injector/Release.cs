using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Rynchodon
{
	[DataContract]
	public class CreateRelease
	{
		[DataMember]
		public string tag_name, body;
		[DataMember]
		public bool draft, prerelease;

		public CreateRelease() { }

		public CreateRelease(CreateRelease copy)
		{
			this.tag_name = copy.tag_name;
			this.body = copy.body;
			this.draft = copy.draft;
			this.prerelease = copy.prerelease;
		}

		public void WriteCreateJson(Stream writeTo)
		{
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CreateRelease));
			serializer.WriteObject(writeTo, new CreateRelease(this));
		}
	}

	[DataContract]
	public class Release : CreateRelease, IComparable<Release>
	{
		[DataContract]
		public class Asset
		{
			[DataMember]
			public string name, browser_download_url;
		}

		[DataMember]
		public string name;
		[DataMember]
		public long id;
		[DataMember]
		public Asset[] assets;

		private Version value_version;

		public Version Version
		{
			get
			{
				if (Comparer<Version>.Default.Compare(value_version, default(Version)) == 0)
					value_version = new Version(BestName);
				return value_version;
			}
		}

		public string BestName { get { return string.IsNullOrWhiteSpace(tag_name) ? name : tag_name; } }

		public int CompareTo(Release other)
		{
			if (this.prerelease != other.prerelease)
				return this.prerelease ? int.MinValue : int.MaxValue;
			return this.Version.CompareTo(other.Version);
		}
	}
}
