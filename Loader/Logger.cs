using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Rynchodon.Loader
{
	static class Logger
	{

		public static string logFile;

		private static StreamWriter _writer;

		public static void WriteLine(string line, [CallerFilePath] string callerPath = null, [CallerMemberName] string memberName = null, [CallerLineNumber] int lineNumber = 0)
		{
			if (logFile == null)
				throw new NullReferenceException("logFile");

			if (_writer == null)
				_writer = new StreamWriter(File.Open(logFile, FileMode.Create));

			callerPath = Path.GetFileName(callerPath);

			_writer.WriteLine(DateTime.UtcNow.ToString() + ":" + callerPath + ":" + memberName + ":" + lineNumber + ":" + line);
			_writer.Flush();
		}

	}
}
