using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Rynchodon
{
	[DataContract]
	public class Release : IComparable<Release>
	{
#pragma warning disable CS0649
		[DataContract]
		public class Asset
		{
			[DataMember]
			public string name, browser_download_url;
		}

		[DataContract]
		private class Create
		{
			[DataMember]
			public string tag_name;
			[DataMember]
			public bool draft, prerelease;
		}

		[DataMember]
		public string tag_name, name;
		[DataMember]
		public long id;
		[DataMember]
		public bool draft, prerelease;
		[DataMember]
		public Asset[] assets;
#pragma warning restore CS0649

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

		public void WriteCreateJson(Stream writeTo)
		{
			Create c = new Create() { tag_name = tag_name, draft = draft, prerelease = prerelease };
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Create));
			serializer.WriteObject(writeTo, c);
		}
	}
}
