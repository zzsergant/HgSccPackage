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
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.btnBrowseCustom = new System.Windows.Forms.Button();
			this.textDiffTool = new System.Windows.Forms.TextBox();
			this.comboDiffTools = new System.Windows.Forms.ComboBox();
			this.radioAutoDetect = new System.Windows.Forms.RadioButton();
			this.radioCustom = new System.Windows.Forms.RadioButton();
			this.tabPageAbout = new System.Windows.Forms.TabPage();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.btnOK = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.tabOptions.SuspendLayout();
			this.tabDiff.SuspendLayout();
			this.groupBox1.SuspendLayout();
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
			this.tabDiff.Controls.Add(this.groupBox1);
			this.tabDiff.Location = new System.Drawing.Point(4, 22);
			this.tabDiff.Name = "tabDiff";
			this.tabDiff.Padding = new System.Windows.Forms.Padding(3);
			this.tabDiff.Size = new System.Drawing.Size(518, 196);
			this.tabDiff.TabIndex = 0;
			this.tabDiff.Text = "Diff tool";
			this.tabDiff.UseVisualStyleBackColor = true;
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.btnBrowseCustom);
			this.groupBox1.Controls.Add(this.textDiffTool);
			this.groupBox1.Controls.Add(this.comboDiffTools);
			this.groupBox1.Controls.Add(this.radioAutoDetect);
			this.groupBox1.Controls.Add(this.radioCustom);
			this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.groupBox1.Location = new System.Drawing.Point(3, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(512, 190);
			this.groupBox1.TabIndex = 7;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Select diff tool";
			// 
			// btnBrowseCustom
			// 
			this.btnBrowseCustom.Location = new System.Drawing.Point(314, 93);
			this.btnBrowseCustom.Name = "btnBrowseCustom";
			this.btnBrowseCustom.Size = new System.Drawing.Size(75, 23);
			this.btnBrowseCustom.TabIndex = 11;
			this.btnBrowseCustom.Text = "Browse...";
			this.btnBrowseCustom.UseVisualStyleBackColor = true;
			this.btnBrowseCustom.Click += new System.EventHandler(this.btnBrowseCustom_Click);
			// 
			// textDiffTool
			// 
			this.textDiffTool.Location = new System.Drawing.Point(24, 93);
			this.textDiffTool.Name = "textDiffTool";
			this.textDiffTool.ReadOnly = true;
			this.textDiffTool.Size = new System.Drawing.Size(284, 20);
			this.textDiffTool.TabIndex = 10;
			// 
			// comboDiffTools
			// 
			this.comboDiffTools.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboDiffTools.FormattingEnabled = true;
			this.comboDiffTools.Location = new System.Drawing.Point(24, 43);
			this.comboDiffTools.Name = "comboDiffTools";
			this.comboDiffTools.Size = new System.Drawing.Size(284, 21);
			this.comboDiffTools.TabIndex = 9;
			// 
			// radioAutoDetect
			// 
			this.radioAutoDetect.AutoSize = true;
			this.radioAutoDetect.Checked = true;
			this.radioAutoDetect.Location = new System.Drawing.Point(7, 20);
			this.radioAutoDetect.Name = "radioAutoDetect";
			this.radioAutoDetect.Size = new System.Drawing.Size(79, 17);
			this.radioAutoDetect.TabIndex = 7;
			this.radioAutoDetect.TabStop = true;
			this.radioAutoDetect.Text = "AutoDetect";
			this.radioAutoDetect.UseVisualStyleBackColor = true;
			this.radioAutoDetect.CheckedChanged += new System.EventHandler(this.radioAutoDetect_CheckedChanged);
			// 
			// radioCustom
			// 
			this.radioCustom.AutoSize = true;
			this.radioCustom.Location = new System.Drawing.Point(7, 70);
			this.radioCustom.Name = "radioCustom";
			this.radioCustom.Size = new System.Drawing.Size(60, 17);
			this.radioCustom.TabIndex = 8;
			this.radioCustom.Text = "Custom";
			this.radioCustom.UseVisualStyleBackColor = true;
			this.radioCustom.CheckedChanged += new System.EventHandler(this.radioCustom_CheckedChanged);
			// 
			// tabPageAbout
			// 
			this.tabPageAbout.Controls.Add(this.textBox2);
			this.tabPageAbout.Controls.Add(this.label3);
			this.tabPageAbout.Controls.Add(this.textBox1);
			this.tabPageAbout.Controls.Add(this.linkLabel1);
			this.tabPageAbout.Controls.Add(this.label2);
			this.tabPageAbout.Controls.Add(this.label1);
			this.tabPageAbout.Location = new System.Drawing.Point(4, 22);
			this.tabPageAbout.Name = "tabPageAbout";
			this.tabPageAbout.Size = new System.Drawing.Size(518, 196);
			this.tabPageAbout.TabIndex = 1;
			this.tabPageAbout.Text = "About";
			this.tabPageAbout.UseVisualStyleBackColor = true;
			// 
			// textBox2
			// 
			this.textBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.textBox2.Location = new System.Drawing.Point(83, 33);
			this.textBox2.Name = "textBox2";
			this.textBox2.ReadOnly = true;
			this.textBox2.Size = new System.Drawing.Size(431, 20);
			this.textBox2.TabIndex = 5;
			this.textBox2.Text = "sergant_@mail.ru";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(36, 36);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(39, 13);
			this.label3.TabIndex = 4;
			this.label3.Text = "E-Mail:";
			// 
			// textBox1
			// 
			this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.textBox1.Location = new System.Drawing.Point(83, 7);
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(431, 20);
			this.textBox1.TabIndex = 3;
			this.textBox1.Text = "Sergey Antonov (zz|sergant)";
			// 
			// linkLabel1
			// 
			this.linkLabel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(80, 56);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(154, 13);
			this.linkLabel1.TabIndex = 2;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Tag = "";
			this.linkLabel1.Text = "http://www.newsupaplex.pp.ru";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(9, 56);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(65, 13);
			this.label2.TabIndex = 1;
			this.label2.Text = "Home page:";
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(36, 10);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(41, 13);
			this.label1.TabIndex = 0;
			this.label1.Text = "Author:";
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
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.tabPageAbout.ResumeLayout(false);
			this.tabPageAbout.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TabControl tabOptions;
		private System.Windows.Forms.TabPage tabDiff;
		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Button btnBrowseCustom;
		private System.Windows.Forms.TextBox textDiffTool;
		private System.Windows.Forms.ComboBox comboDiffTools;
		private System.Windows.Forms.RadioButton radioAutoDetect;
		private System.Windows.Forms.RadioButton radioCustom;
		private System.Windows.Forms.TabPage tabPageAbout;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.LinkLabel linkLabel1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textBox2;
		private System.Windows.Forms.Label label3;
	}
}