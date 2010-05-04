namespace HgSccHelper
{
	partial class HgSettingsControl
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
			this.checkBoxUseSccBindings = new System.Windows.Forms.CheckBox();
			this.checkProjectsForRepository = new System.Windows.Forms.CheckBox();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.checkProjectsForRepository);
			this.groupBox1.Controls.Add(this.checkBoxUseSccBindings);
			this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.groupBox1.Location = new System.Drawing.Point(0, 0);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(360, 157);
			this.groupBox1.TabIndex = 9;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Settings";
			// 
			// checkBoxUseSccBindings
			// 
			this.checkBoxUseSccBindings.AutoSize = true;
			this.checkBoxUseSccBindings.Location = new System.Drawing.Point(6, 19);
			this.checkBoxUseSccBindings.Name = "checkBoxUseSccBindings";
			this.checkBoxUseSccBindings.Size = new System.Drawing.Size(220, 17);
			this.checkBoxUseSccBindings.TabIndex = 0;
			this.checkBoxUseSccBindings.Text = "Use Scc bindings in projects and solution";
			this.checkBoxUseSccBindings.UseVisualStyleBackColor = true;
			// 
			// checkProjectsForRepository
			// 
			this.checkProjectsForRepository.AutoSize = true;
			this.checkProjectsForRepository.Location = new System.Drawing.Point(6, 42);
			this.checkProjectsForRepository.Name = "checkProjectsForRepository";
			this.checkProjectsForRepository.Size = new System.Drawing.Size(329, 17);
			this.checkProjectsForRepository.TabIndex = 1;
			this.checkProjectsForRepository.Text = "Check projects for mercurial repository if solution is not controlled";
			this.checkProjectsForRepository.UseVisualStyleBackColor = true;
			// 
			// HgSettingsControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.groupBox1);
			this.Name = "HgSettingsControl";
			this.Size = new System.Drawing.Size(360, 157);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.CheckBox checkBoxUseSccBindings;
		private System.Windows.Forms.CheckBox checkProjectsForRepository;
	}
}
