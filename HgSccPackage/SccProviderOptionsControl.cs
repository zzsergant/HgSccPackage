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
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.VisualStudio.Shell.Interop;

namespace HgSccPackage
{
	/// <summary>
    /// Summary description for SccProviderOptionsControl.
	/// </summary>
	public class SccProviderOptionsControl : System.Windows.Forms.UserControl
	{

		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private TabControl tabOptions;
		private TabPage tabDiff;
		private HgSccHelper.HgDiffOptionsControl hgDiffOptionsControl1;
		private TabPage tabPageAbout;
		private HgSccHelper.HgAboutControl hgAboutControl1;
		private TabPage tabSettings;
		private HgSccHelper.HgSettingsControl hgSettingsControl1;
		// The parent page, use to persist data
        private SccProviderOptions _customPage;

        public SccProviderOptionsControl()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call

		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
				GC.SuppressFinalize(this);
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.tabOptions = new System.Windows.Forms.TabControl();
			this.tabDiff = new System.Windows.Forms.TabPage();
			this.hgDiffOptionsControl1 = new HgSccHelper.HgDiffOptionsControl();
			this.tabPageAbout = new System.Windows.Forms.TabPage();
			this.hgAboutControl1 = new HgSccHelper.HgAboutControl();
			this.tabSettings = new System.Windows.Forms.TabPage();
			this.hgSettingsControl1 = new HgSccHelper.HgSettingsControl();
			this.tabOptions.SuspendLayout();
			this.tabDiff.SuspendLayout();
			this.tabSettings.SuspendLayout();
			this.tabPageAbout.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabOptions
			// 
			this.tabOptions.Controls.Add(this.tabDiff);
			this.tabOptions.Controls.Add(this.tabSettings);
			this.tabOptions.Controls.Add(this.tabPageAbout);
			this.tabOptions.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabOptions.Location = new System.Drawing.Point(0, 0);
			this.tabOptions.Name = "tabOptions";
			this.tabOptions.SelectedIndex = 0;
			this.tabOptions.Size = new System.Drawing.Size(432, 234);
			this.tabOptions.TabIndex = 3;
			// 
			// tabDiff
			// 
			this.tabDiff.Controls.Add(this.hgDiffOptionsControl1);
			this.tabDiff.Location = new System.Drawing.Point(4, 22);
			this.tabDiff.Name = "tabDiff";
			this.tabDiff.Padding = new System.Windows.Forms.Padding(3);
			this.tabDiff.Size = new System.Drawing.Size(424, 208);
			this.tabDiff.TabIndex = 0;
			this.tabDiff.Text = "Diff tool";
			this.tabDiff.UseVisualStyleBackColor = true;
			// 
			// hgDiffOptionsControl1
			// 
			this.hgDiffOptionsControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hgDiffOptionsControl1.Location = new System.Drawing.Point(3, 3);
			this.hgDiffOptionsControl1.Name = "hgDiffOptionsControl1";
			this.hgDiffOptionsControl1.Size = new System.Drawing.Size(418, 202);
			this.hgDiffOptionsControl1.TabIndex = 0;
			// 
			// tabPageAbout
			// 
			this.tabPageAbout.Controls.Add(this.hgAboutControl1);
			this.tabPageAbout.Location = new System.Drawing.Point(4, 22);
			this.tabPageAbout.Name = "tabPageAbout";
			this.tabPageAbout.Size = new System.Drawing.Size(424, 208);
			this.tabPageAbout.TabIndex = 1;
			this.tabPageAbout.Text = "About";
			this.tabPageAbout.UseVisualStyleBackColor = true;
			// 
			// hgAboutControl1
			// 
			this.hgAboutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hgAboutControl1.Location = new System.Drawing.Point(0, 0);
			this.hgAboutControl1.Name = "hgAboutControl1";
			this.hgAboutControl1.Size = new System.Drawing.Size(424, 208);
			this.hgAboutControl1.TabIndex = 0;
			// 
			// tabSettings
			// 
			this.tabSettings.Controls.Add(this.hgSettingsControl1);
			this.tabSettings.Location = new System.Drawing.Point(4, 22);
			this.tabSettings.Name = "tabSettings";
			this.tabSettings.Size = new System.Drawing.Size(424, 208);
			this.tabSettings.TabIndex = 2;
			this.tabSettings.Text = "Settings";
			this.tabSettings.UseVisualStyleBackColor = true;
			// 
			// hgSettingsControl1
			// 
			this.hgSettingsControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hgSettingsControl1.Location = new System.Drawing.Point(3, 3);
			this.hgSettingsControl1.Name = "hgSettingsControl1";
			this.hgSettingsControl1.Size = new System.Drawing.Size(418, 202);
			this.hgSettingsControl1.TabIndex = 0;
			// 
			// SccProviderOptionsControl
			// 
			this.AllowDrop = true;
			this.Controls.Add(this.tabOptions);
			this.Name = "SccProviderOptionsControl";
			this.Size = new System.Drawing.Size(432, 234);
			this.tabOptions.ResumeLayout(false);
			this.tabDiff.ResumeLayout(false);
			this.tabPageAbout.ResumeLayout(false);
			this.tabSettings.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion
    
		//------------------------------------------------------------------
		public SccProviderOptions OptionsPage
        {
            set
            {
                _customPage = value;
            }
        }

		//------------------------------------------------------------------
		public string DiffToolPath
		{
			get
			{
				return hgDiffOptionsControl1.DiffToolPath;
			}
		}

		//------------------------------------------------------------------
		public bool UseSccBindings
		{
			get
			{
				return hgSettingsControl1.UseSccBindings;
			}
		}

		//------------------------------------------------------------------
		public void Activate()
		{
			hgDiffOptionsControl1.Activate();
		}
    }

}
