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
	public partial class TagsForm : Form
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir
		{
			get { return TagsControl.WorkingDir; }
			set { TagsControl.WorkingDir = value; }
		}

		//------------------------------------------------------------------
		public string TargetRevision
		{
			get { return TagsControl.TargetRevision; }
			set { TagsControl.TargetRevision = value; }
		}

		//-----------------------------------------------------------------------------
		public TagsForm()
		{
			InitializeComponent();

			elementHost1.Child = new TagsControl();
		}

		//-----------------------------------------------------------------------------
		private TagsControl TagsControl
		{
			get
			{
				return elementHost1.Child as TagsControl;
			}
		}

		//-----------------------------------------------------------------------------
		private void TagsWindow_Load(object sender, EventArgs e)
		{
			TagsControl.CloseEvent += TagsControl_CloseEvent;
		}

		//-----------------------------------------------------------------------------
		private void TagsWindow_FormClosing(object sender, FormClosingEventArgs e)
		{
			TagsControl.CloseEvent -= TagsControl_CloseEvent;
		}

		//------------------------------------------------------------------
		void TagsControl_CloseEvent(object sender, System.EventArgs e)
		{
			Close();
		}
	}
}
