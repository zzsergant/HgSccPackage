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
	//-----------------------------------------------------------------------------
	public partial class UpdateForm : Form
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir
		{
			get { return UpdateControl.WorkingDir; }
			set { UpdateControl.WorkingDir = value; }
		}

		//------------------------------------------------------------------
		public string TargetRevision
		{
			get { return UpdateControl.TargetRevision; }
			set { UpdateControl.TargetRevision = value; }
		}

		//------------------------------------------------------------------
		public bool IsUpdated
		{
			get { return UpdateControl.IsUpdated; }
		}

		//-----------------------------------------------------------------------------
		public UpdateForm()
		{
			InitializeComponent();

			elementHost1.Child = new UpdateControl();
		}

		//-----------------------------------------------------------------------------
		private UpdateControl UpdateControl
		{
			get
			{
				return elementHost1.Child as UpdateControl;
			}
		}

		//-----------------------------------------------------------------------------
		private void UpdateWindow_Load(object sender, EventArgs e)
		{
			UpdateControl.CloseEvent += UpdateControl_CloseEvent;
		}

		//-----------------------------------------------------------------------------
		private void UpdateWindow_FormClosing(object sender, FormClosingEventArgs e)
		{
			UpdateControl.CloseEvent -= UpdateControl_CloseEvent;
		}

		//------------------------------------------------------------------
		void UpdateControl_CloseEvent(object sender, System.EventArgs e)
		{
			Close();
		}
	}
}
