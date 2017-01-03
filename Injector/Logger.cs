using System;
using System.Runtime.CompilerServices;

namespace Rynchodon.Injector
{
	static class Logger
	{

		public static void WriteLine(string line, bool skipMemeberName = false, [CallerMemberName] string memberName = null)
		{
			if (!skipMemeberName)
				line = DateTime.Now + ": " + memberName + ": " + line;
			Console.WriteLine(line);
		}

	}
}
