using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using VRage.Game;

namespace Rynchodon.Loader
{
	[DataContract]
	public struct Version : IComparable<Version>
	{
		private const string stableTag = "-stable", unstableTag = "-unstable";

		[DataMember]
		public int Major, Minor, Build, Revision;
		[DataMember]
		public bool StableBuild, UnstableBuild;

		public Version(FileVersionInfo info, bool allBuilds)
		{
			Major = Math.Max(info.FileMajorPart, info.ProductMajorPart);
			Minor = Math.Max(info.FileMinorPart, info.ProductMinorPart);
			Build = Math.Max(info.FileBuildPart, info.ProductBuildPart);
			Revision = Math.Max(info.FilePrivatePart, info.ProductPrivatePart);

			if (allBuilds)
			{
				StableBuild = UnstableBuild = true;
			}
			else if ((bool)typeof(MyFinalBuildConstants).GetField("IS_STABLE").GetValue(null))
			{
				StableBuild = true;
				UnstableBuild = false;
			}
			else
			{
				StableBuild = false;
				UnstableBuild = true;
			}
		}

		/// <summary>
		/// Construct a version from a string.
		/// </summary>
		/// <param name="versionString">The string to create the version from.</param>
		public Version(string versionString)
		{
			Regex versionParts = new Regex(@"(\d+)\.(\d+)\.?(\d*)\.?(\d*)");
			Match match = versionParts.Match(versionString);

			if (!match.Success)
				throw new ArgumentException("Could not parse: " + versionString);

			string group = match.Groups[1].Value;
			this.Major = string.IsNullOrWhiteSpace(group) ? 0 : int.Parse(group);
			group = match.Groups[2].Value;
			this.Minor = string.IsNullOrWhiteSpace(group) ? 0 : int.Parse(group);
			group = match.Groups[3].Value;
			this.Build = string.IsNullOrWhiteSpace(group) ? 0 : int.Parse(group);
			group = match.Groups[4].Value;
			this.Revision = string.IsNullOrWhiteSpace(group) ? 0 : int.Parse(group);

			StableBuild = versionString.Contains(stableTag);
			UnstableBuild = versionString.Contains(unstableTag);

			if (!StableBuild && !UnstableBuild)
				StableBuild = UnstableBuild = true;
		}

		public int CompareTo(Version other)
		{
			int diff = this.Major - other.Major;
			if (diff != 0)
				return diff;
			diff = this.Minor - other.Minor;
			if (diff != 0)
				return diff;
			diff = this.Build - other.Build;
			if (diff != 0)
				return diff;
			diff = this.Revision - other.Revision;
			if (diff != 0)
				return diff;
			diff = this.StableBuild.CompareTo(other.StableBuild);
			if (diff != 0)
				return diff;
			diff = this.UnstableBuild.CompareTo(other.UnstableBuild);
			if (diff != 0)
				return diff;

			return 0;
		}

		public override string ToString()
		{
			string result = "v" + Major + "." + Minor + "." + Build + "." + Revision;
			if (StableBuild && UnstableBuild)
				return result;
			if (StableBuild)
				result += stableTag;
			if (UnstableBuild)
				result += unstableTag;
			return result;
		}
	}
}
