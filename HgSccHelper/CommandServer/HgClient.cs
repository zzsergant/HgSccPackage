//=========================================================================
// Copyright 2012 Sergey Antonov <sergant_@mail.ru>
// 
// This software may be used and distributed according to the terms of the
// GNU General Public License version 2 as published by the Free Software
// Foundation.
// 
// See the file COPYING.TXT for the full text of the license, or see
// http://www.gnu.org/licenses/gpl-2.0.txt
// 
//=========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HgSccHelper.CommandServer
{
	class HgClient : IDisposable
	{
		private HgCmdServer server;
		private volatile bool is_running_command;

		//-----------------------------------------------------------------------------
		public Encoding Encoding { get; private set; }

		//-----------------------------------------------------------------------------
		public string WorkDir { get; private set; }

		//-----------------------------------------------------------------------------
		private List<string> capabilities;

		//-----------------------------------------------------------------------------
		public IEnumerable<string> Capabilities
		{
			get { return capabilities; }
		}

		//-----------------------------------------------------------------------------
		public HgClient()
		{
		}

		//-----------------------------------------------------------------------------
		public bool Open(string work_dir)
		{
			if (IsStarted)
				return false;

			WorkDir = work_dir;
			server = new HgCmdServer();

			var p = new HgCmdServerParams();
			p.Args = "serve --cmdserver pipe";
			p.ForceSystemEncoding = true;
			p.WorkingDir = work_dir;

			if (!server.Run(p))
				return false;

			// TODO: utf8 ?
			Encoding = Encoding.Default;

			return ReadHello();
		}

		//-----------------------------------------------------------------------------
		public void Close()
		{
			server.Stop();
		}

		//-----------------------------------------------------------------------------
		public bool IsStarted
		{
			get { return server != null && server.IsBusy; }
		}

		//-----------------------------------------------------------------------------
		public bool IsRunningCommand
		{
			get
			{
				return is_running_command;
			}
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (server != null)
			{
				server.Dispose();
				server = null;
			}
		}

		//-----------------------------------------------------------------------------
		private bool ReadHello()
		{
			var msg = new Message();
			server.ReadChannel(ref msg);

			if (msg.Channel != 'o')
				return false;

			var hello_msg = Encoding.ASCII.GetString(msg.Data, 0, (int)msg.Length);
			var hello_strings = hello_msg.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

			if (hello_strings.Length == 0)
				return false;

			const string caps_prefix = "capabilities: ";
			var	caps_list = hello_strings[0].Substring(caps_prefix.Length).Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
			
			capabilities = new List<string>(caps_list);
			if (!capabilities.Contains("runcommand"))
				return false;
			
			return true;
		}

		//-----------------------------------------------------------------------------
		public int RunCommand(HgArgsBuilder args,
			Dictionary<char, Func<int, byte[]>> in_channels,
			Dictionary<char, Action<byte[], int, int>> out_channels)
		{
			if (!IsStarted)
				return -1;

			try
			{
				is_running_command = true;

				var data = Encoding.ASCII.GetBytes("runcommand\n");
				server.Stdin.Write(data, 0, data.Length);

				var str = args.ToString().Replace(' ', '\0');
				server.WriteBlock(Encoding.GetBytes(str));

				var msg = new Message();
				while (true)
				{
					if (!server.ReadChannel(ref msg))
						return -2;

					if (in_channels.ContainsKey(msg.Channel))
					{
						server.WriteBlock(in_channels[msg.Channel]((int)msg.Length));
					}
					else if (out_channels.ContainsKey(msg.Channel))
					{
						out_channels[msg.Channel](msg.Data, 0, (int)msg.Length);
					}
					else if (msg.Channel == 'r')
					{
						int b0 = msg.Data[0];
						int b1 = msg.Data[1];
						int b2 = msg.Data[2];
						int b3 = msg.Data[3];

						int exit_code = (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
						return exit_code;
					}
					else if (msg.Channel == Char.ToUpperInvariant(msg.Channel))
					{
						return -3;
					}
				}
			}
			catch(Exception ex)
			{
				Logger.WriteLine(ex.Message);
				return -5;
			}
			finally
			{
				is_running_command = false;
			}
		}

		//-----------------------------------------------------------------------------
		public int RawCommandStream(HgArgsBuilder args)
		{
			return RawCommandStream(args, null, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandStream(HgArgsBuilder args, Stream output)
		{
			return RawCommandStream(args, output, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandStream(HgArgsBuilder args, Stream output, Stream error)
		{
			var in_channels = new Dictionary<char, Func<int, byte[]>>();
			var out_channels = new Dictionary<char, Action<byte[], int, int>>();

			if (output != null)
				out_channels['o'] = output.Write;

			if (error != null)
				out_channels['e'] = error.Write;

			int ret = RunCommand(args, in_channels, out_channels);
			return ret;
		}

		//-----------------------------------------------------------------------------
		public int RawCommandString(HgArgsBuilder args)
		{
			string error;
			string output;
			return RawCommandString(args, out output, out error);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandString(HgArgsBuilder args, out string output)
		{
			string error;
			return RawCommandString(args, out output, out error);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandString(HgArgsBuilder args, out string output, out string error)
		{
			using (var mem_out = new MemoryStream())
			using (var mem_err = new MemoryStream())
			{
				var in_channels = new Dictionary<char, Func<int, byte[]>>();
				var out_channels = new Dictionary<char, Action<byte[], int, int>>();

				out_channels['o'] = mem_out.Write;
				out_channels['e'] = mem_err.Write;

				int ret = RunCommand(args, in_channels, out_channels);

				mem_out.Seek(0, SeekOrigin.Begin);
				mem_err.Seek(0, SeekOrigin.Begin);

				using (var reader_out = new StreamReader(mem_out, Encoding))
				using (var reader_error = new StreamReader(mem_err, Encoding))
				{
					output = reader_out.ReadToEnd();
					error = reader_error.ReadToEnd();
				}

				return ret;
			}
		}

		//-----------------------------------------------------------------------------
		public int RawCommandCallBack(HgArgsBuilder args)
		{
			return RawCommandCallBack(args, null, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandCallBack(HgArgsBuilder args, Action<string> ouput_line)
		{
			return RawCommandCallBack(args, ouput_line, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandCallBack(HgArgsBuilder args, Action<string> ouput_line, Action<string> error_line)
		{
			using (var mem_out = new MemoryStream())
			using (var mem_err = new MemoryStream())
			using (var reader_out = new StreamReader(mem_out, Encoding))
			using (var reader_err = new StreamReader(mem_err, Encoding))
			{
				var in_channels = new Dictionary<char, Func<int, byte[]>>();
				var out_channels = new Dictionary<char, Action<byte[], int, int>>();

				if (ouput_line != null)
				{
					out_channels['o'] = (buffer, offset, count) =>
					{
						mem_out.SetLength(0);
						mem_out.Write(buffer, offset, count);
						mem_out.Seek(0, SeekOrigin.Begin);

						reader_err.DiscardBufferedData();

						while (true)
						{
							string str = reader_out.ReadLine();
							if (str == null)
								break;

							ouput_line(str);
						}
					};
				}

				if (error_line != null)
				{
					out_channels['e'] = (buffer, offset, count) =>
					{
						mem_err.SetLength(0);
						mem_err.Write(buffer, offset, count);
						mem_err.Seek(0, SeekOrigin.Begin);

						reader_err.DiscardBufferedData();

						while (true)
						{
							string str = reader_err.ReadLine();
							if (str == null)
								break;

							error_line(str);
						}
					};
				}

				return RunCommand(args, in_channels, out_channels);
			}
		}
	}
}
