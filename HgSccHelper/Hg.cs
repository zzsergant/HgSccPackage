using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

//=============================================================================
namespace HgSccHelper
{
	//=============================================================================
	class HgDiffException : Exception
	{
		//-----------------------------------------------------------------------------
		public HgDiffException()			
		{

		}

		//-----------------------------------------------------------------------------
		public HgDiffException(string message)
			: base(message)
		{

		}
	}

	//=============================================================================
	class Hg : IDisposable
	{
		string full_change_style_file;
		bool disposed;

		//-----------------------------------------------------------------------------
		public Hg()
		{
			full_change_style_file = Path.GetTempFileName();
			Logger.WriteLine("Creating temp file: " + full_change_style_file);

			using (var stream = new StreamWriter(File.OpenWrite(full_change_style_file)))
			{
				stream.WriteLine(@"changeset = '==:\ndate: {date|isodate}\nauthor: {author}\ndesc: {desc|strip|tabindent}\nrev: {rev}\n{files}\n'");
				stream.WriteLine(@"changeset_verbose = '==:\ndate: {date|isodate}\nauthor: {author}\ndesc: {desc|strip|tabindent}\nrev: {rev}\nA:{file_adds}\nR:{file_dels}\nM:{files}\n'");
				stream.WriteLine(@"file = '{file}:'");
				stream.WriteLine(@"last_file = '{file}'");
				stream.WriteLine(@"file_add = '{file_add}:'");
				stream.WriteLine(@"last_file_add = '{file_add}'");
				stream.WriteLine(@"file_del = '{file_del}:'");
				stream.WriteLine(@"last_file_del = '{file_del}'");
			}
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (!disposed)
			{
				Logger.WriteLine("Deleting " + full_change_style_file);
				File.Delete(full_change_style_file);

				disposed = true;
			}
		}

		//-----------------------------------------------------------------------------
		private ProcessStartInfo PrepareProcess(string work_dir, string arguments)
		{
			var info = new ProcessStartInfo("hg.exe");
			info.Arguments = arguments;

			info.CreateNoWindow = true;
			info.WorkingDirectory = work_dir;

			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			info.UseShellExecute = false;

			return info;
		}

		//-----------------------------------------------------------------------------
		public List<LightChangeDesc> Changes(string work_dir, string path)
		{
			return Changes(work_dir, path, "");
		}

		//-----------------------------------------------------------------------------
		public List<LightChangeDesc> Changes(string work_dir, string path, string rev)
		{
			var template = @"==:\ndate: {date|isodate}\nauthor: {author}\ndesc: {desc}\nrev: {rev}\n".Quote();
			var args = new StringBuilder();
			args.Append("log " + rev);
			args.Append(" -f");
			args.Append(" --template " + template);

			if (path.Length > 0)
			{
				args.Append(" " + path.Quote());
			}

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var lst = LightChangeDesc.ParseChanges(proc.StandardOutput);
				proc.WaitForExit();

				return lst;
			}
		}

		//-----------------------------------------------------------------------------
		public List<ChangeDesc> ChangesFull(string work_dir, string path)
		{
			return ChangesFull(work_dir, path, "");
		}

		//-----------------------------------------------------------------------------
		public List<ChangeDesc> ChangesFull(string work_dir, string path, string rev)
		{
			var args = new StringBuilder();
			args.Append("log");
			args.Append(" --debug");
			args.Append(" -v");
			args.Append(" -f");
			if (rev.Length > 0)
				args.Append(" -r " + rev);

			args.Append(" --style " + full_change_style_file.Quote());

			if (path.Length > 0)
			{
				args.Append(" " + path.Quote());
			}

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var lst = ChangeDesc.ParseChanges(proc.StandardOutput);
				proc.WaitForExit();

				return lst;
			}
		}

		//-----------------------------------------------------------------------------
/*
		public List<FileInfo> FilesInfo(string work_dir, int revision)
		{
			string style_path = @"c:\Work\Mercurial\files.style";

			var args = new StringBuilder();
			args.Append("log");
			args.Append(" -r " + revision);
			args.Append(" -v --debug");
			args.Append(" --style " + style_path);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var lst = FileInfo.ParseFileInfo(proc.StandardOutput);
				proc.WaitForExit();

				return lst;
			}
		}
*/

		//-----------------------------------------------------------------------------
		public bool HaveRoot(string work_dir)
		{
			return Root(work_dir).Length > 0;
		}

		//-----------------------------------------------------------------------------
		public string Root(string work_dir)
		{
			string args = "root";
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				string root = proc.StandardOutput.ReadLine();
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return string.Empty;

				return root;
			}
		}

		//-----------------------------------------------------------------------------
		public bool CreateRepository(string work_dir)
		{
			string args = "init";
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public List<string> Manifest(string work_dir)
		{
			string args = "manifest";
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				var reader = proc.StandardOutput;
				var files = new List<string>();

				while (true)
				{
					string str = reader.ReadLine();

					if (str == null)
						break;

					files.Add(str);
				}

				proc.WaitForExit();

				return files;
			}
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string work_dir)
		{
			return Status(work_dir, "");
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string work_dir, string path)
		{
			var args = new StringBuilder();
			args.Append("status");
			args.Append(" -amrdC");
			if (path.Length > 0)
				args.Append(" " + path.Quote());

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var reader = proc.StandardOutput;
				var files_info = HgFileInfo.ParseFileInfo(reader);

				proc.WaitForExit();
				if (proc.ExitCode != 0)
				{
					Logger.WriteLine(args.ToString());
					Logger.WriteLine(reader.ReadToEnd());
				}

				return files_info;
			}
		}

		//-----------------------------------------------------------------------------
		public bool Add(string work_dir, string[] files)
		{
			return Add(work_dir, files, 0);
		}

		//-----------------------------------------------------------------------------
		public bool Add(string work_dir, string[] files, int approx_cmd_line_length)
		{
			// FIXME: cmd line length
			StringBuilder args = new StringBuilder(approx_cmd_line_length);
			args.Append("add");
			foreach (string f in files)
			{
				args.Append(" " + f.Quote());
			}

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public bool Commit(string work_dir, string[] files, string comment)
		{
			return Commit(work_dir, files, comment, 0);
		}

		//-----------------------------------------------------------------------------
		public bool Commit(string work_dir, string[] files, string comment, int approx_cmd_line_length)
		{
			// FIXME: cmd line length
			StringBuilder args = new StringBuilder(approx_cmd_line_length);
			args.Append("commit");
			args.Append(" -m " + comment.EscapeQuotes().Quote());

			foreach (string f in files)
			{
				args.Append(" " + f.Quote());
			}

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

/*
				foreach (var f in files)
				{
					string path = Path.Combine(work_dir, f);
					if (File.Exists(path))
					{
						FileAttributes attr = File.GetAttributes(path);
						if ((attr | FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
						{
							attr |= FileAttributes.ReadOnly;
							File.SetAttributes(path, attr);
						}
					}
				}
*/

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public bool Checkout(string work_dir, string[] files)
		{
/*
			DateTime now = DateTime.Now;
			foreach (var f in files)
			{
				string path = Path.Combine(work_dir, f);
				FileAttributes attr = File.GetAttributes(path);
				if ((attr | FileAttributes.ReadOnly) != 0)
				{
					attr &= ~FileAttributes.ReadOnly;
					File.SetAttributes(path, attr);
				}

				File.SetLastWriteTime(path, now);
			}
*/

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool UnCheckOut(string work_dir, string[] files)
		{
			bool no_backups = true;
			StringBuilder args = new StringBuilder();
			args.Append("revert");
			if (no_backups)
				args.Append(" --no-backup");

			foreach (string f in files)
			{
				args.Append(" " + f.Quote());
			}

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

//				return true;
			}

/*
			foreach (var f in files)
			{
				string path = Path.Combine(work_dir, f);
				FileAttributes attr = File.GetAttributes(path);
				if ((attr | FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
				{
					attr |= FileAttributes.ReadOnly;
					File.SetAttributes(path, attr);
				}
			}
*/

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool DiffSilent(string work_dir, string file, out bool is_different)
		{
			is_different = true;

			string args = "diff " + file.Quote();
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				var line = proc.StandardOutput.ReadToEnd();

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

				if (line == null || line.Length == 0)
					is_different = false;
				else
					is_different = true;

				return true;
			}
		}

		//-----------------------------------------------------------------------------
/*
		public bool Diff(string work_dir, string file, out bool is_different, out string diff_text)
		{
			is_different = true;

			string args = "diff " + file.Quote();
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				diff_text = proc.StandardOutput.ReadToEnd();

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

				if (diff_text == null || diff_text.Length == 0)
				{
					diff_text = string.Empty;
					is_different = false;
				}
				else
					is_different = true;

				return true;
			}
		}
*/

		//-----------------------------------------------------------------------------
		public bool Diff(string work_dir, string file, out bool is_different)
		{
			is_different = true;

			var files = Status(work_dir, file);
/*
			Logger.WriteLine("KDiff, files count = " + files.Count);
			foreach (var f in files)
			{
				Logger.WriteLine(String.Format("[File = {0}, Status = {1}, CopiedFrom = {2}]", f.File, f.Status, f.CopiedFrom));
			}
*/

			if (	files.Count == 1
				&&	files[0].Status == HgFileStatus.Added
				&&	files[0].CopiedFrom != null)
			{
				var file_info = files[0];

				string temp1 = Path.GetTempFileName();
				try
				{
					if (!CheckoutFile(work_dir, file_info.CopiedFrom, "", temp1))
					{
//						Logger.WriteLine("Checkout failed: " + file_info.CopiedFrom);
						return false;
					}

//					Logger.WriteLine("KDiffing: " + temp1 + ", " + file);
					return RunDiffTool(temp1, Path.Combine(work_dir, file));
				}
				finally
				{
					File.Delete(temp1);
				}
			}

			if (	files.Count == 1
				&&	files[0].Status == HgFileStatus.Modified)
			{
				var file_info = files[0];

				string temp1 = Path.GetTempFileName();
				try
				{
					if (!CheckoutFile(work_dir, file_info.File, "", temp1))
					{
						//						Logger.WriteLine("Checkout failed: " + file_info.CopiedFrom);
						return false;
					}

					//					Logger.WriteLine("KDiffing: " + temp1 + ", " + file);
					return RunDiffTool(temp1, Path.Combine(work_dir, file));
				}
				finally
				{
					File.Delete(temp1);
				}
			}

			//-----------------------------------------------------------------------------
			var hg_files = Manifest(work_dir);
			bool is_tracked = false;
			var lower_f = file.ToLower();

			foreach (var str in hg_files)
			{
				string f = str.ToLower().Replace('/', '\\');
				if (f == lower_f.ToLower())
				{
					Logger.WriteLine(file + " is tracked");
					is_tracked = true;
					break;
				}
			}

			if (is_tracked)
			{
				is_different = false;
				return true;
			}

			Logger.WriteLine(file + " is not tracked");
			return false;
		}

		//-----------------------------------------------------------------------------
/*
		public bool KDiff(string work_dir, string file, int rev1, int rev2)
		{
			var args = new StringBuilder();
			args.Append("kdiff");
			args.Append(" -r " + rev1.ToString() + " -r " + rev2.ToString());
			args.Append(" " + file.Quote());

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}
*/

		//-----------------------------------------------------------------------------
/*
		public bool KDiff(string work_dir, string file1, int rev1, string file2, int rev2)
		{
			var args = new StringBuilder();
			args.Append("kdiff");
			args.Append(" -r " + rev1.ToString() + " -r " + rev2.ToString());
			args.Append(" " + file1.Quote());
			args.Append(" " + file2.Quote());

//			Logger.WriteLine(args.ToString());

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}
*/
		//-----------------------------------------------------------------------------
		private bool CheckoutFile(string work_dir, string file, int rev, string temp_file)
		{
			return CheckoutFile(work_dir, file, rev.ToString(), temp_file);
		}

		//-----------------------------------------------------------------------------
		private bool CheckoutFile(string work_dir, string file, string rev, string temp_file)
		{
			var args = new StringBuilder();
			args.Append("cat");
			args.Append(" -o " + temp_file.Quote());
			if (rev.Length > 0)
				args.Append(" -r " + rev.ToString());

			args.Append(" " + file.Quote());

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public bool RunDiffTool(string file1, string file2)
		{
			if (!File.Exists(HgSccOptions.Options.DiffTool))
				throw new HgDiffException("DiffTool is not exist");

			var info = new ProcessStartInfo(HgSccOptions.Options.DiffTool);
			info.Arguments = file1.Quote() + " " + file2.Quote();
			info.UseShellExecute = false;

			using (var proc = Process.Start(info))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public bool Diff(string work_dir, string file1, int rev1, string file2, int rev2)
		{
			string temp1 = Path.GetTempFileName();
			string temp2 = Path.GetTempFileName();

			try
			{
				if (	!CheckoutFile(work_dir, file1, rev1, temp1)
					||	!CheckoutFile(work_dir, file2, rev2, temp2))
				{
					return false;
				}

				return RunDiffTool(temp1, temp2);
			}
			finally
			{
				File.Delete(temp1);
				File.Delete(temp2);
			}
		}

		//-----------------------------------------------------------------------------
		public bool TrackRename(string work_dir, string file, string rev, out string old_file)
		{
			var args = new StringBuilder();
			args.Append("debugrename");
			if (rev.Length > 0)
				args.Append(" -r " + rev);

			args.Append(" " + file.Quote());

			old_file = string.Empty;

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				string line = proc.StandardOutput.ReadLine();

				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				string[] arr = line.Split(new string[] { " renamed from ", ":" }, StringSplitOptions.RemoveEmptyEntries);
				if (arr.Length != 3)
				{
					return false;
				}

				old_file = arr[1];
				return true;
			}
		}


		//-----------------------------------------------------------------------------
		public bool Remove(string work_dir, string[] files, string comment)
		{
			if (comment.Length == 0)
				comment = "Removed";

			StringBuilder args = new StringBuilder();
			args.Append("remove");

			foreach (string f in files)
			{
				args.Append(" " + f.Quote());

				string real_path = Path.Combine(work_dir, f);
				if (!File.Exists(real_path))
					return false;

/*
				FileAttributes attr = File.GetAttributes(real_path);
				if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				{
					attr &= ~FileAttributes.ReadOnly;
					File.SetAttributes(real_path, attr);
				}
*/
			}

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool Rename(string work_dir, string file, string new_file)
		{
			StringBuilder args = new StringBuilder();
			args.Append("rename");
			args.Append(" -A");
			args.Append(" " + file.Quote());
			args.Append(" " + new_file.Quote());

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}
	}
}
