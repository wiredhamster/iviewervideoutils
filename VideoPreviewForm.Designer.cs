namespace iviewer
{
    partial class VideoPreviewForm
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

        private void InitializeComponent()
        {
            btnExport = new Button();
            btnImport = new Button();
            tabPreview = new TabControl();
            tabPageFull = new TabPage();
            tabPagePerPrompt = new TabPage();
            tabPreview.SuspendLayout();
            SuspendLayout();
            // 
            // btnExport
            // 
            btnExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExport.Location = new Point(696, 1);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(100, 23);
            btnExport.TabIndex = 6;
            btnExport.Text = "Export Stitched";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += BtnExport_Click;
            // 
            // btnImport
            // 
            btnImport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnImport.Location = new Point(593, 1);
            btnImport.Name = "btnImport";
            btnImport.Size = new Size(100, 23);
            btnImport.TabIndex = 6;
            btnImport.Text = "Import Videos";
            btnImport.UseVisualStyleBackColor = true;
            btnImport.Click += BtnImport_Click;
            // 
            // tabPreview
            // 
            tabPreview.Controls.Add(tabPageFull);
            tabPreview.Controls.Add(tabPagePerPrompt);
            tabPreview.Dock = DockStyle.Fill;
            tabPreview.Location = new Point(0, 0);
            tabPreview.Name = "tabPreview";
            tabPreview.SelectedIndex = 0;
            tabPreview.Size = new Size(800, 600);
            tabPreview.TabIndex = 0;
            // 
            // tabPageFull
            // 
            tabPageFull.Location = new Point(4, 24);
            tabPageFull.Name = "tabPageFull";
            tabPageFull.Padding = new Padding(3);
            tabPageFull.Size = new Size(792, 572);
            tabPageFull.TabIndex = 0;
            tabPageFull.Text = "Full Video";
            tabPageFull.UseVisualStyleBackColor = true;
            // 
            // tabPagePerPrompt
            // 
            tabPagePerPrompt.Location = new Point(4, 24);
            tabPagePerPrompt.Name = "tabPagePerPrompt";
            tabPagePerPrompt.Padding = new Padding(3);
            tabPagePerPrompt.Size = new Size(792, 572);
            tabPagePerPrompt.TabIndex = 1;
            tabPagePerPrompt.Text = "Per-Prompt Videos";
            tabPagePerPrompt.UseVisualStyleBackColor = true;
            // 
            // VideoPreviewForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(800, 600);
            Controls.Add(btnExport);
            Controls.Add(btnImport);
            Controls.Add(tabPreview);
            Name = "VideoPreviewForm";
            Text = "Video Preview";
            tabPreview.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabPreview;
        private VideoPlayerControl videoPlayerFull;
        private VideoPlayerControl videoPlayerPerPrompt;
        private TabPage tabPageFull;
        private TabPage tabPagePerPrompt;
        private Button btnExport;
        private Button btnImport;
    }
}