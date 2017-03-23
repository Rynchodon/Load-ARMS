using System;
using System.Reflection;
using VRage.Game;

namespace Rynchodon.Loader
{
	public static class BuildTest
	{

		/// <summary>
		/// Check that the current Space Engineers build is stable.
		/// </summary>
		/// <returns>True iff the current Space Engineers build is stable.</returns>
		public static bool IsStable()
		{
			FieldInfo field = typeof(MyFinalBuildConstants).GetField("IS_STABLE");
			if (field == null)
				throw new NullReferenceException("MyFinalBuildConstants does not have a field named IS_STABLE or it has unexpected binding");
			return (bool)field.GetValue(null);
		}

		/// <summary>
		/// Check that a version's build matches the current Space Engineers build.
		/// </summary>
		/// <param name="version">The version that may be compatible.</param>
		/// <returns>True iff the version is compatible with the current Space Engineers build.</returns>
		public static bool MatchesCurrent(Version version)
		{
			return IsStable() ? version.StableBuild : version.UnstableBuild;
		}

	}
}
