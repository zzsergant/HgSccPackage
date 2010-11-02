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
		}

		//-----------------------------------------------------------------------------
		public void Add(string file)
		{
			files.Add(file);
		}

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
				proc.Exited -= OnDeleteEventHandler;
			}

			// FIXME: wait few seconds in case of redirection ?
			Delete();
		}
	}
}
