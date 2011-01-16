//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
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
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;

//=============================================================================
namespace ProcessWrapper
{
	public delegate void UserCallBack(string data);

	//=============================================================================
	// This class is used by Process to read streams asynchronously.
	// This is reimplementation of internal AsyncStreamReader class from System.Diagnostics.
	public class AsyncStreamReader : IDisposable
	{
		private const int DefaultBufferSize = 1024;
		private const int MinBufferSize = 128;

		private Stream stream;
		private Encoding encoding;
		private Decoder decoder;
		private byte[] byte_buffer;
		private char[] char_buffer;

		private int max_chars_per_buffer;
		private UserCallBack user_call_back;

		private bool cancel_operation;
		private ManualResetEvent eof_event;
		private readonly Queue message_queue;
		private StringBuilder sb;
		private bool last_carriage_return;

		//-----------------------------------------------------------------------------
		public AsyncStreamReader(Stream stream, UserCallBack callback, Encoding encoding)
			: this(stream, callback, encoding, DefaultBufferSize)
		{
		}

		//-----------------------------------------------------------------------------
		internal AsyncStreamReader(Stream stream, UserCallBack callback, Encoding encoding, int buffer_size)
		{
			Init(stream, callback, encoding, buffer_size);
			message_queue = new Queue();
		}

		//-----------------------------------------------------------------------------
		private void Init(Stream stream, UserCallBack callback, Encoding encoding, int buffer_size)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.user_call_back = callback;
			decoder = encoding.GetDecoder();

			if (buffer_size < MinBufferSize)
				buffer_size = MinBufferSize;

			byte_buffer = new byte[buffer_size];
			max_chars_per_buffer = encoding.GetMaxCharCount(buffer_size);
			char_buffer = new char[max_chars_per_buffer];
			cancel_operation = false;
			eof_event = new ManualResetEvent(false);
			sb = null;

			this.last_carriage_return = false;
		}

		//-----------------------------------------------------------------------------
		public virtual void Close()
		{
			Dispose(true);
		}

		//-----------------------------------------------------------------------------
		void IDisposable.Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		//-----------------------------------------------------------------------------
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (stream != null)
					stream.Close();
			}

			if (stream != null)
			{
				stream = null;
				encoding = null;
				decoder = null;
				byte_buffer = null;
				char_buffer = null;
			}

			if (eof_event != null)
			{
				eof_event.Close();
				eof_event = null;
			}
		}

		//-----------------------------------------------------------------------------
		public virtual Encoding CurrentEncoding
		{
			get { return encoding; }
		}

		//-----------------------------------------------------------------------------
		public virtual Stream BaseStream
		{
			get { return stream; }
		}

		//-----------------------------------------------------------------------------
		public void BeginReadLine()
		{
			if (cancel_operation)
			{
				cancel_operation = false;
			}

			if (sb == null)
			{
				sb = new StringBuilder(DefaultBufferSize);
				stream.BeginRead(byte_buffer, 0, byte_buffer.Length, ReadBuffer, null);
			}
			else
			{
				FlushMessageQueue();
			}
		}

		//-----------------------------------------------------------------------------
		public void CancelOperation()
		{
			cancel_operation = true;
		}

		//-----------------------------------------------------------------------------
		private void ReadBuffer(IAsyncResult ar)
		{
			int num_bytes;

			try
			{
				num_bytes = stream.EndRead(ar);
			}
			catch (IOException)
			{
				num_bytes = 0;
			}
			catch (OperationCanceledException)
			{
				num_bytes = 0;
			}

			if (num_bytes == 0)
			{
				lock (message_queue)
				{
					if (sb.Length != 0)
					{
						message_queue.Enqueue(sb.ToString());
						sb.Length = 0;
					}

					message_queue.Enqueue(null);
				}

				try
				{
					FlushMessageQueue();
				}
				finally
				{
					eof_event.Set();
				}
			}
			else
			{
				int chars_num = decoder.GetChars(byte_buffer, 0, num_bytes, char_buffer, 0);
				sb.Append(char_buffer, 0, chars_num);
				GetLinesFromStringBuilder();
				stream.BeginRead(byte_buffer, 0, byte_buffer.Length, ReadBuffer, null);
			}
		}

		//-----------------------------------------------------------------------------
		private void GetLinesFromStringBuilder()
		{
			int i = 0;
			int lineStart = 0;
			int len = sb.Length;

			if (last_carriage_return && (len > 0) && sb[0] == '\n')
			{
				i = 1;
				lineStart = 1;
				last_carriage_return = false;
			}

			while (i < len)
			{
				char ch = sb[i];

				if (ch == '\r' || ch == '\n')
				{
					string s = sb.ToString(lineStart, i - lineStart);
					lineStart = i + 1;

					if ((ch == '\r') && (lineStart < len) && (sb[lineStart] == '\n'))
					{
						lineStart++;
						i++;
					}

					lock (message_queue)
						message_queue.Enqueue(s);
				}
				i++;
			}

			if (sb[len - 1] == '\r')
				last_carriage_return = true;

			if (lineStart < len)
				sb.Remove(0, lineStart);
			else
				sb.Length = 0;

			FlushMessageQueue();
		}

		//-----------------------------------------------------------------------------
		private void FlushMessageQueue()
		{
			while (true)
			{
				if (message_queue.Count > 0)
				{
					lock (message_queue)
					{
						if (message_queue.Count > 0)
						{
							string s = (string)message_queue.Dequeue();

							if (!cancel_operation)
								user_call_back(s);
						}
					}
				}
				else
				{
					break;
				}
			}
		}

		//-----------------------------------------------------------------------------
		public void WaitUtilEof()
		{
			if (eof_event != null)
			{
				eof_event.WaitOne();
				eof_event.Close();
				eof_event = null;
			}
		}
	}
} 
