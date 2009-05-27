namespace HgSccHelper
{
	partial class OptionsForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.tabOptions = new System.Windows.Forms.TabControl();
			this.tabDiff = new System.Windows.Forms.TabPage();
			this.tabPageAbout = new System.Windows.Forms.TabPage();
			this.btnOK = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.hgAboutControl1 = new HgSccPackage.HgSccHelper.HgAboutControl();
			this.hgDiffOptionsControl1 = new HgSccPackage.HgSccHelper.HgDiffOptionsControl();
			this.tabOptions.SuspendLayout();
			this.tabDiff.SuspendLayout();
			this.tabPageAbout.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabOptions
			// 
			this.tabOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.tabOptions.Controls.Add(this.tabDiff);
			this.tabOptions.Controls.Add(this.tabPageAbout);
			this.tabOptions.Location = new System.Drawing.Point(2, 5);
			this.tabOptions.Name = "tabOptions";
			this.tabOptions.SelectedIndex = 0;
			this.tabOptions.Size = new System.Drawing.Size(526, 222);
			this.tabOptions.TabIndex = 2;
			// 
			// tabDiff
			// 
			this.tabDiff.Controls.Add(this.hgDiffOptionsControl1);
			this.tabDiff.Location = new System.Drawing.Point(4, 22);
			this.tabDiff.Name = "tabDiff";
			this.tabDiff.Padding = new System.Windows.Forms.Padding(3);
			this.tabDiff.Size = new System.Drawing.Size(518, 196);
			this.tabDiff.TabIndex = 0;
			this.tabDiff.Text = "Diff tool";
			this.tabDiff.UseVisualStyleBackColor = true;
			// 
			// tabPageAbout
			// 
			this.tabPageAbout.Controls.Add(this.hgAboutControl1);
			this.tabPageAbout.Location = new System.Drawing.Point(4, 22);
			this.tabPageAbout.Name = "tabPageAbout";
			this.tabPageAbout.Size = new System.Drawing.Size(518, 196);
			this.tabPageAbout.TabIndex = 1;
			this.tabPageAbout.Text = "About";
			this.tabPageAbout.UseVisualStyleBackColor = true;
			// 
			// btnOK
			// 
			this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnOK.Location = new System.Drawing.Point(368, 233);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(75, 23);
			this.btnOK.TabIndex = 3;
			this.btnOK.Text = "OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(449, 233);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 23);
			this.btnCancel.TabIndex = 4;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// hgAboutControl1
			// 
			this.hgAboutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hgAboutControl1.Location = new System.Drawing.Point(0, 0);
			this.hgAboutControl1.Name = "hgAboutControl1";
			this.hgAboutControl1.Size = new System.Drawing.Size(518, 196);
			this.hgAboutControl1.TabIndex = 0;
			// 
			// hgDiffOptionsControl1
			// 
			this.hgDiffOptionsControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hgDiffOptionsControl1.Location = new System.Drawing.Point(3, 3);
			this.hgDiffOptionsControl1.Name = "hgDiffOptionsControl1";
			this.hgDiffOptionsControl1.Size = new System.Drawing.Size(512, 190);
			this.hgDiffOptionsControl1.TabIndex = 0;
			// 
			// OptionsForm
			// 
			this.AcceptButton = this.btnOK;
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(532, 263);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnOK);
			this.Controls.Add(this.tabOptions);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "OptionsForm";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "HgScc Options";
			this.tabOptions.ResumeLayout(false);
			this.tabDiff.ResumeLayout(false);
			this.tabPageAbout.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TabControl tabOptions;
		private System.Windows.Forms.TabPage tabDiff;
		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.TabPage tabPageAbout;
		private HgSccPackage.HgSccHelper.HgDiffOptionsControl hgDiffOptionsControl1;
		private HgSccPackage.HgSccHelper.HgAboutControl hgAboutControl1;
	}
}