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
	public partial class FileHistoryForm : Form
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir
		{
			get { return FileHistoryControl.WorkingDir; }
			set { FileHistoryControl.WorkingDir = value; }
		}

		//------------------------------------------------------------------
		public string FileName
		{
			get { return FileHistoryControl.FileName; }
			set { FileHistoryControl.FileName = value; }
		}

		//------------------------------------------------------------------
		public string Rev
		{
			get { return FileHistoryControl.Rev; }
			set { FileHistoryControl.Rev = value; }
		}

		//------------------------------------------------------------------
		public bool IsUpdated { get; private set; }

		//-----------------------------------------------------------------------------
		public FileHistoryForm()
		{
			InitializeComponent();

			elementHost1.Child = new FileHistoryControl();
		}

		//-----------------------------------------------------------------------------
		private FileHistoryControl FileHistoryControl
		{
			get
			{
				return elementHost1.Child as FileHistoryControl;
			}
		}

		//-----------------------------------------------------------------------------
		private void FileHistoryWindow_Load(object sender, EventArgs e)
		{
			Text = string.Format("FileHistory: '{0}'", WorkingDir);
			FileHistoryControl.CloseEvent += FileHistoryControl_CloseEvent;
		}

		//------------------------------------------------------------------
		void FileHistoryControl_CloseEvent(object sender, EventArgs e)
		{
			Close();
		}

		//-----------------------------------------------------------------------------
		private void FileHistoryWindow_FormClosing(object sender, FormClosingEventArgs e)
		{
			FileHistoryControl.CloseEvent -= FileHistoryControl_CloseEvent;
		}
	}
}
