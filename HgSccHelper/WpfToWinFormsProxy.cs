using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace HgSccHelper
{
	//==================================================================
	/// <summary>
	/// This class is a proxy object that can show WPF window
	/// using windows forms message loop.
	/// This class should be used to create top level WPF windows of HgSccPackage plugin.
	/// </summary>
	/// <typeparam name="WpfWindowT">WPF window to show</typeparam>
	public class WpfToWinFormsProxy<WpfWindowT> : System.Windows.Forms.Form where WpfWindowT : Window, new()
	{
		//------------------------------------------------------------------
		/// <summary>
		/// WPF window accessor
		/// </summary>
		public WpfWindowT Wnd { get; private set; }

		//------------------------------------------------------------------
		public WpfToWinFormsProxy()
		{
			Wnd = new WpfWindowT();
			System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(Wnd);
			
			this.Visible = false;
			this.Size = new System.Drawing.Size(0, 0);
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
		}

		//------------------------------------------------------------------
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
		}

		//-----------------------------------------------------------------------------
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			Wnd.ShowDialog();

			Close();
		}

		//------------------------------------------------------------------
		protected override CreateParams CreateParams
		{
			get
			{
				// Moving this window to off screen.
				// Without this it will flash for a second on close

				CreateParams createParams = base.CreateParams;
				createParams.X = -Width;
				createParams.Y = -Height;
				return createParams;
			}
		}
	}
}
