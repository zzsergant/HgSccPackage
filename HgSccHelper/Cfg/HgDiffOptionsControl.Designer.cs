namespace HgSccHelper
{
	partial class HgDiffOptionsControl
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.btnBrowseCustom = new System.Windows.Forms.Button();
			this.textDiffTool = new System.Windows.Forms.TextBox();
			this.comboDiffTools = new System.Windows.Forms.ComboBox();
			this.radioAutoDetect = new System.Windows.Forms.RadioButton();
			this.radioCustom = new System.Windows.Forms.RadioButton();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.btnBrowseCustom);
			this.groupBox1.Controls.Add(this.textDiffTool);
			this.groupBox1.Controls.Add(this.comboDiffTools);
			this.groupBox1.Controls.Add(this.radioAutoDetect);
			this.groupBox1.Controls.Add(this.radioCustom);
			this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.groupBox1.Location = new System.Drawing.Point(0, 0);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(404, 185);
			this.groupBox1.TabIndex = 8;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Select diff tool";
			// 
			// btnBrowseCustom
			// 
			this.btnBrowseCustom.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
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
			this.textDiffTool.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.textDiffTool.Location = new System.Drawing.Point(24, 93);
			this.textDiffTool.Name = "textDiffTool";
			this.textDiffTool.ReadOnly = true;
			this.textDiffTool.Size = new System.Drawing.Size(284, 20);
			this.textDiffTool.TabIndex = 10;
			// 
			// comboDiffTools
			// 
			this.comboDiffTools.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
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
			// HgDiffOptionsControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.groupBox1);
			this.Name = "HgDiffOptionsControl";
			this.Size = new System.Drawing.Size(404, 185);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Button btnBrowseCustom;
		private System.Windows.Forms.TextBox textDiffTool;
		private System.Windows.Forms.ComboBox comboDiffTools;
		private System.Windows.Forms.RadioButton radioAutoDetect;
		private System.Windows.Forms.RadioButton radioCustom;
	}
}
