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
using System.Windows.Forms.Integration;

namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	public partial class SynchronizeForm : Form
	{
		//-----------------------------------------------------------------------------
		public SynchronizeForm()
		{
			InitializeComponent();

			elementHost1.Child = new SynchronizeControl();
		}

		//------------------------------------------------------------------
		void CloseButton_Click(object sender, EventArgs e)
		{
			Close();
		}

		//-----------------------------------------------------------------------------
		private SynchronizeControl SynchronizeControl
		{
			get
			{
				return elementHost1.Child as SynchronizeControl;
			}
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir
		{
			get
			{
				return SynchronizeControl.WorkingDir;
			}
			set
			{
				SynchronizeControl.WorkingDir = value;
			}
		}

		//-----------------------------------------------------------------------------
		private void SynchronizeWindow_Load(object sender, EventArgs e)
		{
			Text = string.Format("Synchronize: '{0}'", WorkingDir);
			SynchronizeControl.CloseEvent += SynchronizeControl_CloseEvent;
		}

		//------------------------------------------------------------------
		void SynchronizeControl_CloseEvent(object sender, EventArgs e)
		{
			Close();
		}

		//-----------------------------------------------------------------------------
		private void SynchronizeWindow_FormClosing(object sender, FormClosingEventArgs e)
		{
			SynchronizeControl.CloseEvent -= SynchronizeControl_CloseEvent;
		}

		//------------------------------------------------------------------
		private void SynchronizeForm_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
				Close();
		}
	}
}
