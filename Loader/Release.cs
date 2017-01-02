using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Rynchodon.Loader
{
	/// <summary>
	/// Information about a GitHub account.
	/// </summary>
	[DataContract]
	public class Account
	{
		[DataMember]
		public string login, avatar_url, gravatar_id, url, html_url, followers_url, following_url, gists_url, starred_url, subscriptions_url, organizations_url, repos_url, events_url, received_events_url, type;
		[DataMember]
		public long id;
		[DataMember]
		public bool site_admin;
	}

	/// <summary>
	/// Data that is needed to create a GitHub release.
	/// </summary>
	[DataContract]
	public class CreateRelease
	{

		[DataMember]
		public string target_commitish, name, body;
		[DataMember]
		public bool draft, prerelease;

		[IgnoreDataMember]
		private string _tag_name;
		private Version _version;

		[DataMember]
		public string tag_name
		{
			get { return _tag_name; }
			set
			{
				_tag_name = value;
				_version = new Version(value);
			}
		}

		[IgnoreDataMember]
		public Version version
		{
			get { return _version; }
			set
			{
				_version = value;
				_tag_name = _version.ToString();
			}
		}

		public CreateRelease() { }

		private CreateRelease(CreateRelease copy)
		{
			this.tag_name = copy.tag_name;
			this.target_commitish = copy.target_commitish;
			this.name = copy.name;
			this.body = copy.body;
			this.draft = copy.draft;
			this.prerelease = copy.prerelease;
		}
		
		/// <summary>
		/// Write this object to a stream as CreateRelease & json.
		/// </summary>
		/// <param name="writeTo">The stream to write to.</param>
		public void WriteCreateJson(Stream writeTo)
		{
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CreateRelease));
			serializer.WriteObject(writeTo, new CreateRelease(this));
		}
	}

	/// <summary>
	/// All the information about a GitHub release.
	/// </summary>
	[DataContract]
	public class Release : CreateRelease
	{
		[DataContract]
		public class Asset
		{
			[DataMember]
			public string url, browser_download_url, name, label, state, content_type, created_at, updated_at;
			[DataMember]
			public long id, size, download_count;
			public Account uploader;
		}

		[DataMember]
		public string url, html_url, assets_url, upload_url, tarball_url, zipball_url, created_at, published_at;
		[DataMember]
		public long id;
		[DataMember]
		public Account author;
		[DataMember]
		public Asset[] assets;
	}
}
