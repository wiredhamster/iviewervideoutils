using System.Windows.Forms;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace iviewer
{
    partial class VideoGenerator
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
			DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
			DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
			tabControl = new TabControl();
			tabPageGeneration = new TabPage();
			btnMoveDown = new Button();
			btnMoveUp = new Button();
			btnDeleteImage = new Button();
			lblProgress = new Label();
			pbProgress = new ProgressBar();
			dgvPrompts = new DataGridView();
			colImage = new DataGridViewImageColumn();
			colPrompt = new DataGridViewTextBoxColumn();
			colLora = new DataGridViewButtonColumn();
			colQueue = new DataGridViewButtonColumn();
			btnExtractLast = new Button();
			btnQueueAll = new Button();
			btnDeleteRow = new Button();
			btnAddRow = new Button();
			lblResolution = new Label();
			tabPagePerPrompt = new TabPage();
			btnPlay2x = new Button();
			btnPreview = new Button();
			btnImport = new Button();
			btnPlayAll = new Button();
			tabPageFullVideo = new TabPage();
			btnExport = new Button();
			tabControl.SuspendLayout();
			tabPageGeneration.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)dgvPrompts).BeginInit();
			tabPagePerPrompt.SuspendLayout();
			tabPageFullVideo.SuspendLayout();
			SuspendLayout();
			// 
			// tabControl
			// 
			tabControl.Controls.Add(tabPageGeneration);
			tabControl.Controls.Add(tabPagePerPrompt);
			tabControl.Controls.Add(tabPageFullVideo);
			tabControl.Dock = DockStyle.Fill;
			tabControl.Location = new Point(0, 0);
			tabControl.Name = "tabControl";
			tabControl.SelectedIndex = 0;
			tabControl.Size = new Size(1000, 700);
			tabControl.TabIndex = 0;
			// 
			// tabPageGeneration
			// 
			tabPageGeneration.Controls.Add(btnMoveDown);
			tabPageGeneration.Controls.Add(btnMoveUp);
			tabPageGeneration.Controls.Add(btnDeleteImage);
			tabPageGeneration.Controls.Add(lblProgress);
			tabPageGeneration.Controls.Add(pbProgress);
			tabPageGeneration.Controls.Add(dgvPrompts);
			tabPageGeneration.Controls.Add(btnExtractLast);
			tabPageGeneration.Controls.Add(btnQueueAll);
			tabPageGeneration.Controls.Add(btnDeleteRow);
			tabPageGeneration.Controls.Add(btnAddRow);
			tabPageGeneration.Controls.Add(lblResolution);
			tabPageGeneration.Location = new Point(4, 24);
			tabPageGeneration.Name = "tabPageGeneration";
			tabPageGeneration.Padding = new Padding(3);
			tabPageGeneration.Size = new Size(992, 672);
			tabPageGeneration.TabIndex = 0;
			tabPageGeneration.Text = "Generation";
			tabPageGeneration.UseVisualStyleBackColor = true;
			// 
			// btnMoveDown
			// 
			btnMoveDown.Location = new Point(490, 6);
			btnMoveDown.Name = "btnMoveDown";
			btnMoveDown.Size = new Size(84, 23);
			btnMoveDown.TabIndex = 11;
			btnMoveDown.Text = "Move Down";
			btnMoveDown.UseVisualStyleBackColor = true;
			btnMoveDown.Click += btnMoveDown_Click;
			// 
			// btnMoveUp
			// 
			btnMoveUp.Location = new Point(400, 6);
			btnMoveUp.Name = "btnMoveUp";
			btnMoveUp.Size = new Size(84, 23);
			btnMoveUp.TabIndex = 10;
			btnMoveUp.Text = "Move Up";
			btnMoveUp.UseVisualStyleBackColor = true;
			btnMoveUp.Click += btnMoveUp_Click;
			// 
			// btnDeleteImage
			// 
			btnDeleteImage.Location = new Point(289, 6);
			btnDeleteImage.Name = "btnDeleteImage";
			btnDeleteImage.Size = new Size(105, 23);
			btnDeleteImage.TabIndex = 9;
			btnDeleteImage.Text = "- Delete Image";
			btnDeleteImage.UseVisualStyleBackColor = true;
			btnDeleteImage.Click += btnDeleteImage_Click;
			// 
			// lblProgress
			// 
			lblProgress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lblProgress.BackColor = Color.Transparent;
			lblProgress.FlatStyle = FlatStyle.Flat;
			lblProgress.Location = new Point(12, 645);
			lblProgress.Name = "lblProgress";
			lblProgress.Size = new Size(968, 20);
			lblProgress.TabIndex = 8;
			lblProgress.Text = "Ready";
			lblProgress.TextAlign = ContentAlignment.MiddleCenter;
			// 
			// pbProgress
			// 
			pbProgress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			pbProgress.Location = new Point(6, 619);
			pbProgress.Name = "pbProgress";
			pbProgress.Size = new Size(980, 23);
			pbProgress.TabIndex = 7;
			// 
			// dgvPrompts
			// 
			dgvPrompts.AllowUserToAddRows = false;
			dgvPrompts.AllowUserToDeleteRows = false;
			dgvPrompts.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			dgvPrompts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
			dgvPrompts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			dgvPrompts.Columns.AddRange(new DataGridViewColumn[] { colImage, colPrompt, colLora, colQueue });
			dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.TopLeft;
			dataGridViewCellStyle2.BackColor = SystemColors.Window;
			dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 9F);
			dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
			dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
			dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
			dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
			dgvPrompts.DefaultCellStyle = dataGridViewCellStyle2;
			dgvPrompts.Location = new Point(6, 35);
			dgvPrompts.Name = "dgvPrompts";
			dgvPrompts.RowHeadersWidth = 50;
			dgvPrompts.RowTemplate.Height = 160;
			dgvPrompts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			dgvPrompts.Size = new Size(980, 578);
			dgvPrompts.TabIndex = 6;
			dgvPrompts.RowPostPaint += dgvPrompts_RowPostPaint;
			// 
			// colImage
			// 
			colImage.FillWeight = 20F;
			colImage.HeaderText = "Image";
			colImage.ImageLayout = DataGridViewImageCellLayout.Zoom;
			colImage.Name = "colImage";
			// 
			// colPrompt
			// 
			dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.TopLeft;
			dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
			colPrompt.DefaultCellStyle = dataGridViewCellStyle1;
			colPrompt.FillWeight = 50F;
			colPrompt.HeaderText = "Prompt";
			colPrompt.Name = "colPrompt";
			// 
			// colLora
			// 
			colLora.FillWeight = 15F;
			colLora.HeaderText = "LoRA";
			colLora.Name = "colLora";
			colLora.Text = "Select LoRA";
			colLora.UseColumnTextForButtonValue = true;
			// 
			// colQueue
			// 
			colQueue.FillWeight = 15F;
			colQueue.HeaderText = "Action";
			colQueue.Name = "colQueue";
			colQueue.Text = "Queue";
			// 
			// btnExtractLast
			// 
			btnExtractLast.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnExtractLast.Enabled = false;
			btnExtractLast.Location = new Point(896, 6);
			btnExtractLast.Name = "btnExtractLast";
			btnExtractLast.Size = new Size(90, 23);
			btnExtractLast.TabIndex = 4;
			btnExtractLast.Text = "Extract Last";
			btnExtractLast.UseVisualStyleBackColor = true;
			btnExtractLast.Click += btnExtractLast_Click;
			// 
			// btnQueueAll
			// 
			btnQueueAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnQueueAll.Location = new Point(790, 6);
			btnQueueAll.Name = "btnQueueAll";
			btnQueueAll.Size = new Size(100, 23);
			btnQueueAll.TabIndex = 3;
			btnQueueAll.Text = "Queue All";
			btnQueueAll.UseVisualStyleBackColor = true;
			btnQueueAll.Click += btnQueueAll_Click;
			// 
			// btnDeleteRow
			// 
			btnDeleteRow.Location = new Point(199, 6);
			btnDeleteRow.Name = "btnDeleteRow";
			btnDeleteRow.Size = new Size(84, 23);
			btnDeleteRow.TabIndex = 2;
			btnDeleteRow.Text = "- Delete Row";
			btnDeleteRow.UseVisualStyleBackColor = true;
			btnDeleteRow.Click += btnDeleteRow_Click;
			// 
			// btnAddRow
			// 
			btnAddRow.Location = new Point(118, 6);
			btnAddRow.Name = "btnAddRow";
			btnAddRow.Size = new Size(75, 23);
			btnAddRow.TabIndex = 1;
			btnAddRow.Text = "+ Add Row";
			btnAddRow.UseVisualStyleBackColor = true;
			btnAddRow.Click += btnAddRow_Click;
			// 
			// lblResolution
			// 
			lblResolution.AutoSize = true;
			lblResolution.Location = new Point(12, 10);
			lblResolution.Name = "lblResolution";
			lblResolution.Size = new Size(29, 15);
			lblResolution.TabIndex = 0;
			lblResolution.Text = "N/A";
			// 
			// tabPagePerPrompt
			// 
			tabPagePerPrompt.Controls.Add(btnPlay2x);
			tabPagePerPrompt.Controls.Add(btnPreview);
			tabPagePerPrompt.Controls.Add(btnImport);
			tabPagePerPrompt.Controls.Add(btnPlayAll);
			tabPagePerPrompt.Location = new Point(4, 24);
			tabPagePerPrompt.Name = "tabPagePerPrompt";
			tabPagePerPrompt.Padding = new Padding(3);
			tabPagePerPrompt.Size = new Size(992, 672);
			tabPagePerPrompt.TabIndex = 1;
			tabPagePerPrompt.Text = "Per-Prompt Videos";
			tabPagePerPrompt.UseVisualStyleBackColor = true;
			// 
			// btnPlay2x
			// 
			btnPlay2x.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnPlay2x.Location = new Point(884, 35);
			btnPlay2x.Name = "btnPlay2x";
			btnPlay2x.Size = new Size(100, 23);
			btnPlay2x.TabIndex = 9;
			btnPlay2x.Text = "Play 2x";
			btnPlay2x.UseVisualStyleBackColor = true;
			btnPlay2x.Click += btnPlay2x_Click;
			// 
			// btnPreview
			// 
			btnPreview.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnPreview.Location = new Point(886, 93);
			btnPreview.Name = "btnPreview";
			btnPreview.Size = new Size(100, 23);
			btnPreview.TabIndex = 8;
			btnPreview.Text = "Preview";
			btnPreview.UseVisualStyleBackColor = true;
			btnPreview.Click += btnPreview_Click;
			// 
			// btnImport
			// 
			btnImport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnImport.Location = new Point(886, 64);
			btnImport.Name = "btnImport";
			btnImport.Size = new Size(100, 23);
			btnImport.TabIndex = 7;
			btnImport.Text = "Import Videos";
			btnImport.UseVisualStyleBackColor = true;
			btnImport.Click += btnImport_Click;
			// 
			// btnPlayAll
			// 
			btnPlayAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnPlayAll.Location = new Point(884, 6);
			btnPlayAll.Name = "btnPlayAll";
			btnPlayAll.Size = new Size(100, 23);
			btnPlayAll.TabIndex = 6;
			btnPlayAll.Text = "Play All";
			btnPlayAll.UseVisualStyleBackColor = true;
			btnPlayAll.Click += btnPlayAll_Click;
			// 
			// tabPageFullVideo
			// 
			tabPageFullVideo.Controls.Add(btnExport);
			tabPageFullVideo.Location = new Point(4, 24);
			tabPageFullVideo.Name = "tabPageFullVideo";
			tabPageFullVideo.Padding = new Padding(3);
			tabPageFullVideo.Size = new Size(992, 672);
			tabPageFullVideo.TabIndex = 2;
			tabPageFullVideo.Text = "Full Video";
			tabPageFullVideo.UseVisualStyleBackColor = true;
			// 
			// btnExport
			// 
			btnExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnExport.Location = new Point(909, 6);
			btnExport.Name = "btnExport";
			btnExport.Size = new Size(75, 23);
			btnExport.TabIndex = 0;
			btnExport.Text = "Export";
			btnExport.UseVisualStyleBackColor = true;
			btnExport.Click += btnExport_Click;
			// 
			// VideoGenerator
			// 
			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;
			ClientSize = new Size(1000, 700);
			Controls.Add(tabControl);
			Name = "VideoGenerator";
			Text = "Video Generator";
			tabControl.ResumeLayout(false);
			tabPageGeneration.ResumeLayout(false);
			tabPageGeneration.PerformLayout();
			((System.ComponentModel.ISupportInitialize)dgvPrompts).EndInit();
			tabPagePerPrompt.ResumeLayout(false);
			tabPageFullVideo.ResumeLayout(false);
			ResumeLayout(false);
		}

		#endregion

		private TabControl tabControl;
        private TabPage tabPageGeneration;
        private TabPage tabPageFullVideo;
        private TabPage tabPagePerPrompt;

        // Generation tab controls
        private Label lblResolution;
        private Button btnAddRow;
        private Button btnDeleteRow;
        private Button btnQueueAll;
        private Button btnExtractLast;
        private DataGridView dgvPrompts;
        private DataGridViewImageColumn colImage;
        private DataGridViewTextBoxColumn colPrompt;
        private DataGridViewButtonColumn colLora;
        private DataGridViewButtonColumn colQueue;
        private ProgressBar pbProgress;
        private Label lblProgress;

        // Preview tab controls
        private Button btnPreview;
        private Button btnImport;
        private Button btnPlayAll;

        // Video players (created programmatically)
        private VideoPlayerControl videoPlayerFull;
        private VideoPlayerControl videoPlayerPerPrompt;

        // Event handler method declarations
        private void btnAddRow_Click(object sender, EventArgs e)
        {
            AddNewRow();
        }

        private void btnDeleteRow_Click(object sender, EventArgs e)
        {
            DeleteSelectedRow();
        }

        private void btnQueueAll_Click(object sender, EventArgs e)
        {
            OnQueueAllClick();
        }

        private void btnExtractLast_Click(object sender, EventArgs e)
        {
            ExtractLastFrameFromSelected();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            OnExportClick();
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ImportVideosForTesting();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            OnPreviewClick();
        }

        private void btnDeleteImage_Click(object sender, EventArgs e)
        {
            OnDeleteImage();
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            OnMoveUp();
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            OnMoveDown();
        }

        private void dgvPrompts_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            string rowIdx = (e.RowIndex + 1).ToString(); // 1-based index

            var centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            e.Graphics.DrawString(rowIdx, grid.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }

        private void InitializeVideoPlayers()
        {
            // Create video players programmatically since designer can't handle custom controls well
            videoPlayerFull = new VideoPlayerControl();
            videoPlayerPerPrompt = new VideoPlayerControl();

            // 
            // videoPlayerFull
            // 
            videoPlayerFull.Dock = DockStyle.Fill;
            videoPlayerFull.Location = new Point(3, 3);
            videoPlayerFull.Name = "videoPlayerFull";
            videoPlayerFull.Size = new Size(986, 666);
            videoPlayerFull.TabIndex = 0;

            // 
            // videoPlayerPerPrompt
            // 
            videoPlayerPerPrompt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            videoPlayerPerPrompt.Location = new Point(3, 3);
            videoPlayerPerPrompt.Name = "videoPlayerPerPrompt";
            videoPlayerPerPrompt.Size = new Size(btnPreview.Left - 10, this.Height - 400);
            videoPlayerPerPrompt.TabIndex = 0;
            videoPlayerPerPrompt.Loop = false;

            // Add to respective tab pages
            tabPageFullVideo.Controls.Add(videoPlayerFull);
            tabPagePerPrompt.Controls.Add(videoPlayerPerPrompt);
        }

        private void btnPlayAll_Click(object sender, EventArgs e)
        {
            StartPlayAllSequence();
        }

        private void btnPlay2x_Click(object sender, EventArgs e)
        {
            StartPlayAllSequence(2);
        }

        private Button btnExport;
        private Button btnDeleteImage;
        private Button btnPlay2x;
        private Button btnMoveDown;
        private Button btnMoveUp;
    }
}