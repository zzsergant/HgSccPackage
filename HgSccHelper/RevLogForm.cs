//=========================================================================
// Copyright 2009 Sergey Antonov <sergant_@mail.ru>
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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HgSccHelper
{
	public partial class RevLogForm : Form
	{
		//-----------------------------------------------------------------------------
		public RevLogForm()
		{
			InitializeComponent();
			elementHost1.Child = new RevLogControl();
		}

		//-----------------------------------------------------------------------------
		private RevLogControl RevLogControl
		{
			get
			{
				return elementHost1.Child as RevLogControl;
			}
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir
		{
			get
			{
				return RevLogControl.WorkingDir;
			}
			set
			{
				RevLogControl.WorkingDir = value;
			}
		}

		//------------------------------------------------------------------
		public bool IsUpdated
		{
			get
			{
				return RevLogControl.IsUpdated;
			}
		}

		//-----------------------------------------------------------------------------
		private void RevLogForm_Load(object sender, EventArgs e)
		{
			Text = string.Format("ChangeLog: '{0}'", WorkingDir);
			RevLogControl.CloseEvent += RevLogControl_CloseEvent;
		}

		//------------------------------------------------------------------
		void RevLogControl_CloseEvent(object sender, EventArgs e)
		{
			Close();
		}

		//-----------------------------------------------------------------------------
		private void RevLogForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			RevLogControl.CloseEvent -= RevLogControl_CloseEvent;
		}
	}
}
