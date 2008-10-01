namespace HgSccHelper
{
	partial class HistoryForm
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
			this.listViewChanges = new System.Windows.Forms.ListView();
			this.columnRev = new System.Windows.Forms.ColumnHeader();
			this.columnDate = new System.Windows.Forms.ColumnHeader();
			this.columnAuthor = new System.Windows.Forms.ColumnHeader();
			this.columnComment = new System.Windows.Forms.ColumnHeader();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.listViewChangedFiles = new System.Windows.Forms.ListView();
			this.columnStatus = new System.Windows.Forms.ColumnHeader();
			this.columnPath = new System.Windows.Forms.ColumnHeader();
			this.btnOk = new System.Windows.Forms.Button();
			this.btnDiff = new System.Windows.Forms.Button();
			this.btnDiffPrev = new System.Windows.Forms.Button();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.SuspendLayout();
			// 
			// listViewChanges
			// 
			this.listViewChanges.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnRev,
            this.columnDate,
            this.columnAuthor,
            this.columnComment});
			this.listViewChanges.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listViewChanges.FullRowSelect = true;
			this.listViewChanges.GridLines = true;
			this.listViewChanges.HideSelection = false;
			this.listViewChanges.Location = new System.Drawing.Point(0, 0);
			this.listViewChanges.Name = "listViewChanges";
			this.listViewChanges.Size = new System.Drawing.Size(1010, 317);
			this.listViewChanges.TabIndex = 0;
			this.listViewChanges.UseCompatibleStateImageBehavior = false;
			this.listViewChanges.View = System.Windows.Forms.View.Details;
			this.listViewChanges.SelectedIndexChanged += new System.EventHandler(this.listViewChanges_SelectedIndexChanged);
			// 
			// columnRev
			// 
			this.columnRev.Tag = "";
			this.columnRev.Text = "Revision";
			// 
			// columnDate
			// 
			this.columnDate.Text = "Date";
			this.columnDate.Width = 139;
			// 
			// columnAuthor
			// 
			this.columnAuthor.Text = "Author";
			this.columnAuthor.Width = 219;
			// 
			// columnComment
			// 
			this.columnComment.Text = "Comment";
			this.columnComment.Width = 700;
			// 
			// splitContainer1
			// 
			this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.splitContainer1.Location = new System.Drawing.Point(12, 12);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.listViewChanges);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.listViewChangedFiles);
			this.splitContainer1.Size = new System.Drawing.Size(1010, 635);
			this.splitContainer1.SplitterDistance = 317;
			this.splitContainer1.TabIndex = 1;
			// 
			// listViewChangedFiles
			// 
			this.listViewChangedFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnStatus,
            this.columnPath});
			this.listViewChangedFiles.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listViewChangedFiles.FullRowSelect = true;
			this.listViewChangedFiles.GridLines = true;
			this.listViewChangedFiles.HideSelection = false;
			this.listViewChangedFiles.Location = new System.Drawing.Point(0, 0);
			this.listViewChangedFiles.MultiSelect = false;
			this.listViewChangedFiles.Name = "listViewChangedFiles";
			this.listViewChangedFiles.Size = new System.Drawing.Size(1010, 314);
			this.listViewChangedFiles.TabIndex = 0;
			this.listViewChangedFiles.UseCompatibleStateImageBehavior = false;
			this.listViewChangedFiles.View = System.Windows.Forms.View.Details;
			this.listViewChangedFiles.SelectedIndexChanged += new System.EventHandler(this.listViewChangedFiles_SelectedIndexChanged);
			// 
			// columnStatus
			// 
			this.columnStatus.Text = "Status";
			this.columnStatus.Width = 95;
			// 
			// columnPath
			// 
			this.columnPath.Text = "Path";
			this.columnPath.Width = 910;
			// 
			// btnOk
			// 
			this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnOk.Location = new System.Drawing.Point(947, 653);
			this.btnOk.Name = "btnOk";
			this.btnOk.Size = new System.Drawing.Size(75, 23);
			this.btnOk.TabIndex = 3;
			this.btnOk.Text = "Exit";
			this.btnOk.UseVisualStyleBackColor = true;
			this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
			// 
			// btnDiff
			// 
			this.btnDiff.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnDiff.Location = new System.Drawing.Point(754, 653);
			this.btnDiff.Name = "btnDiff";
			this.btnDiff.Size = new System.Drawing.Size(75, 23);
			this.btnDiff.TabIndex = 4;
			this.btnDiff.Text = "Diff two Rev";
			this.btnDiff.UseVisualStyleBackColor = true;
			this.btnDiff.Click += new System.EventHandler(this.btnDiff_Click);
			// 
			// btnDiffPrev
			// 
			this.btnDiffPrev.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnDiffPrev.Location = new System.Drawing.Point(835, 653);
			this.btnDiffPrev.Name = "btnDiffPrev";
			this.btnDiffPrev.Size = new System.Drawing.Size(75, 23);
			this.btnDiffPrev.TabIndex = 5;
			this.btnDiffPrev.Text = "Diff Previous";
			this.btnDiffPrev.UseVisualStyleBackColor = true;
			this.btnDiffPrev.Click += new System.EventHandler(this.btnDiffPrev_Click);
			// 
			// HistoryForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnOk;
			this.ClientSize = new System.Drawing.Size(1034, 683);
			this.Controls.Add(this.btnDiffPrev);
			this.Controls.Add(this.btnDiff);
			this.Controls.Add(this.btnOk);
			this.Controls.Add(this.splitContainer1);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "HistoryForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "HistoryForm";
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			this.splitContainer1.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView listViewChanges;
		private System.Windows.Forms.ColumnHeader columnRev;
		private System.Windows.Forms.ColumnHeader columnDate;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.ListView listViewChangedFiles;
		private System.Windows.Forms.ColumnHeader columnAuthor;
		private System.Windows.Forms.ColumnHeader columnComment;
		private System.Windows.Forms.ColumnHeader columnStatus;
		private System.Windows.Forms.ColumnHeader columnPath;
		private System.Windows.Forms.Button btnOk;
		private System.Windows.Forms.Button btnDiff;
		private System.Windows.Forms.Button btnDiffPrev;
	}
}