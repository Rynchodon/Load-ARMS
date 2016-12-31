using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LoadArms
{
	static class Logger
	{

		private const string logFile = "LoadARMS.log";

		private static StreamWriter _writer;

		public static void WriteLine(string line, [CallerFilePath] string callerPath = null, [CallerMemberName] string memberName = null, [CallerLineNumber] int lineNumber = 0)
		{
			if (_writer == null)
				_writer = new StreamWriter(File.Open(logFile, FileMode.Create));

			callerPath = Path.GetFileName(callerPath);

			_writer.WriteLine(DateTime.UtcNow.ToString() + ":" + callerPath + ":" + memberName + ":" + lineNumber + ":" + line);
			_writer.Flush();
		}

	}
}
