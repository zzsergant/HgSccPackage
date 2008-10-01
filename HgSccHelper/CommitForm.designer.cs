namespace HgSccHelper
{
	partial class CommitForm
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
			this.label2 = new System.Windows.Forms.Label();
			this.checkedListFiles = new System.Windows.Forms.CheckedListBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBoxComment = new System.Windows.Forms.TextBox();
			this.btnCancel = new System.Windows.Forms.Button();
			this.btnOk = new System.Windows.Forms.Button();
			this.btnDiffPrevious = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(15, 132);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(31, 13);
			this.label2.TabIndex = 7;
			this.label2.Text = "Files:";
			// 
			// checkedListFiles
			// 
			this.checkedListFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.checkedListFiles.FormattingEnabled = true;
			this.checkedListFiles.Location = new System.Drawing.Point(12, 148);
			this.checkedListFiles.Name = "checkedListFiles";
			this.checkedListFiles.Size = new System.Drawing.Size(616, 214);
			this.checkedListFiles.TabIndex = 6;
			this.checkedListFiles.SelectedIndexChanged += new System.EventHandler(this.checkedListFiles_SelectedIndexChanged);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(15, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(54, 13);
			this.label1.TabIndex = 5;
			this.label1.Text = "Comment:";
			// 
			// textBoxComment
			// 
			this.textBoxComment.AcceptsReturn = true;
			this.textBoxComment.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxComment.Location = new System.Drawing.Point(12, 21);
			this.textBoxComment.Multiline = true;
			this.textBoxComment.Name = "textBoxComment";
			this.textBoxComment.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBoxComment.Size = new System.Drawing.Size(616, 108);
			this.textBoxComment.TabIndex = 4;
			// 
			// btnCancel
			// 
			this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(553, 368);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 23);
			this.btnCancel.TabIndex = 9;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			// 
			// btnOk
			// 
			this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnOk.Location = new System.Drawing.Point(472, 368);
			this.btnOk.Name = "btnOk";
			this.btnOk.Size = new System.Drawing.Size(75, 23);
			this.btnOk.TabIndex = 8;
			this.btnOk.Text = "OK";
			this.btnOk.UseVisualStyleBackColor = true;
			this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
			// 
			// btnDiffPrevious
			// 
			this.btnDiffPrevious.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnDiffPrevious.Location = new System.Drawing.Point(13, 368);
			this.btnDiffPrevious.Name = "btnDiffPrevious";
			this.btnDiffPrevious.Size = new System.Drawing.Size(75, 23);
			this.btnDiffPrevious.TabIndex = 10;
			this.btnDiffPrevious.Text = "Diff Previous";
			this.btnDiffPrevious.UseVisualStyleBackColor = true;
			this.btnDiffPrevious.Click += new System.EventHandler(this.btnDiffPrevious_Click);
			// 
			// CommitForm
			// 
			this.AcceptButton = this.btnOk;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(640, 401);
			this.Controls.Add(this.btnDiffPrevious);
			this.Controls.Add(this.btnOk);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.checkedListFiles);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.textBoxComment);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "CommitForm";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "CommitForm";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.CheckedListBox checkedListFiles;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textBoxComment;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Button btnOk;
		private System.Windows.Forms.Button btnDiffPrevious;
	}
}