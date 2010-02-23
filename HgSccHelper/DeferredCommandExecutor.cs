using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace HgSccHelper
{
	/// <summary>
	/// This class helps to workaround context menu popup visibility
	/// while running context menu command.
	/// This needs only in VS2008. VS2010 works fine, without this hack.
	/// </summary>
	class DeferredCommandExecutor : IDisposable
	{
		public delegate void DeferredCommandExecuteDelegate();

		DispatcherTimer timer;
		DeferredCommandExecuteDelegate deferred_execute;

		//-----------------------------------------------------------------------------
		/// <summary>
		/// This field control execution. If true - then execution will be deferred.
		/// </summary>
		public static bool IsExecutionDeferred = true;

		//-----------------------------------------------------------------------------
		public DeferredCommandExecutor()
		{
			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(150);
			timer.Tick += OnTimerTick;
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();
			if (deferred_execute != null)
			{
				var local_delegate = deferred_execute;
				deferred_execute = null;

				local_delegate();
			}
		}

		//-----------------------------------------------------------------------------
		public void QueueDefferedExecute(DeferredCommandExecuteDelegate cmd)
		{
			if (cmd != null)
			{
				if (IsExecutionDeferred)
				{
					deferred_execute = cmd;
					timer.Start();
				}
				else
				{
					cmd();
				}
			}
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			deferred_execute = null;
			timer.Stop();
		}
	}
}
