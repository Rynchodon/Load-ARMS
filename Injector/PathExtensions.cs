using System;
using System.IO;

namespace Rynchodon.Injector
{
	static class PathExtensions
	{

		/// <summary>
		/// Combine paths without a special case for rooted paths.
		/// </summary>
		/// <param name="pathParts">Parts of the path to combine.</param>
		/// <returns>The combined paths.</returns>
		public static string Combine(params string[] pathParts)
		{
			int index = 0;
			string path = pathParts[index];
			if (path == null)
				throw new ArgumentNullException("pathParts[" + index + "]");

			for (index = 1; index < pathParts.Length; ++index)
			{
				string pp = pathParts[index];

				if (pp == null)
					throw new ArgumentNullException("pathParts[" + index + "]");

				char lastChar, firstChar;
				if ((lastChar = path[path.Length - 1]) == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar || (firstChar = pp[0]) == Path.DirectorySeparatorChar || firstChar == Path.AltDirectorySeparatorChar)
					path += pp;
				else
					path += Path.DirectorySeparatorChar + pp;
			}
			return path;
		}

	}
}
