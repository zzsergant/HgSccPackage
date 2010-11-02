using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace HgSccHelper.Misc
{
	//=============================================================================
	public enum DeleteFlag
	{
		Keep,
		Delete
	}

	//=============================================================================
	public class AsyncDeleter
	{
		private List<string> files;
		private object critical;

		//-----------------------------------------------------------------------------
		public AsyncDeleter()
		{
			files = new List<string>();
			critical = new object();

			DeleteEventDelay = TimeSpan.Zero;
		}

		//-----------------------------------------------------------------------------
		public void Add(string file)
		{
			files.Add(file);
		}

		//-----------------------------------------------------------------------------
		public TimeSpan DeleteEventDelay { get; set; }

		//-----------------------------------------------------------------------------
		public void Delete()
		{
			lock (critical)
			{
				foreach (var file in files)
				{
					Logger.WriteLine("Deleting file: {0}", file);
					File.Delete(file);
				}

				files.Clear();
			}
		}

		//-----------------------------------------------------------------------------
		public void OnDeleteEventHandler(object o, EventArgs e)
		{
			var proc = o as Process;
			if (proc != null)
			{
				Logger.WriteLine("Exit code: {0}", proc.ExitCode);
				proc.Exited -= OnDeleteEventHandler;
			}

			if (DeleteEventDelay == TimeSpan.Zero)
			{
				Delete();
			}
			else
			{
				Util.QueueThreadPoolFn(() =>
				{
					Logger.WriteLine("Waiting delete delay: {0}", DeleteEventDelay);
					System.Threading.Thread.Sleep((int)DeleteEventDelay.TotalMilliseconds);
					Delete();
				});
			}
		}
	}
}
