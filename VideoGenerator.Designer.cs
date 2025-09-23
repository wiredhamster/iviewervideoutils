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
            tabControl = new TabControl();
            tabPageGeneration = new TabPage();
            lblProgress = new Label();
            pbProgress = new ProgressBar();
            dgvPrompts = new DataGridView();
            colImage = new DataGridViewImageColumn();
            colPrompt = new DataGridViewTextBoxColumn();
            colLora = new DataGridViewButtonColumn();
            colGenerate = new DataGridViewButtonColumn();
            btnPreview = new Button();
            btnExtractLast = new Button();
            btnGenerateAll = new Button();
            btnDeleteRow = new Button();
            btnAddRow = new Button();
            lblResolution = new Label();
            tabPageFullVideo = new TabPage();
            tabPagePerPrompt = new TabPage();
            btnExport = new Button();
            btnImport = new Button();
            tabControl.SuspendLayout();
            tabPageGeneration.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPrompts).BeginInit();
            tabPagePerPrompt.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabPageGeneration);
            tabControl.Controls.Add(tabPageFullVideo);
            tabControl.Controls.Add(tabPagePerPrompt);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1000, 700);
            tabControl.TabIndex = 0;
            // 
            // tabPageGeneration
            // 
            tabPageGeneration.Controls.Add(lblProgress);
            tabPageGeneration.Controls.Add(pbProgress);
            tabPageGeneration.Controls.Add(dgvPrompts);
            tabPageGeneration.Controls.Add(btnPreview);
            tabPageGeneration.Controls.Add(btnExtractLast);
            tabPageGeneration.Controls.Add(btnGenerateAll);
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
            dgvPrompts.Columns.AddRange(new DataGridViewColumn[] { colImage, colPrompt, colLora, colGenerate });
            dgvPrompts.Location = new Point(6, 35);
            dgvPrompts.Name = "dgvPrompts";
            dgvPrompts.RowHeadersVisible = false;
            dgvPrompts.RowTemplate.Height = 160;
            dgvPrompts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPrompts.Size = new Size(980, 578);
            dgvPrompts.TabIndex = 6;
            dgvPrompts.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            // 
            // colImage
            // 
            colImage.HeaderText = "Image";
            colImage.Name = "colImage";
            colImage.FillWeight = 20F;
            colImage.ImageLayout = DataGridViewImageCellLayout.Zoom;
            // 
            // colPrompt
            // 
            colPrompt.HeaderText = "Prompt";
            colPrompt.Name = "colPrompt";
            colPrompt.FillWeight = 50F;
            colPrompt.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            colPrompt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            // 
            // colLora
            // 
            colLora.HeaderText = "LoRA";
            colLora.Name = "colLora";
            colLora.FillWeight = 15F;
            colLora.Text = "Select LoRA";
            colLora.UseColumnTextForButtonValue = true;
            // 
            // colGenerate
            // 
            colGenerate.HeaderText = "Action";
            colGenerate.Name = "colGenerate";
            colGenerate.FillWeight = 15F;
            colGenerate.Text = "Generate";
            colGenerate.UseColumnTextForButtonValue = false;
            // 
            // btnPreview
            // 
            btnPreview.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnPreview.Location = new Point(906, 6);
            btnPreview.Name = "btnPreview";
            btnPreview.Size = new Size(80, 23);
            btnPreview.TabIndex = 5;
            btnPreview.Text = "Preview";
            btnPreview.UseVisualStyleBackColor = true;
            btnPreview.Click += btnPreview_Click;
            // 
            // btnExtractLast
            // 
            btnExtractLast.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExtractLast.Enabled = false;
            btnExtractLast.Location = new Point(810, 6);
            btnExtractLast.Name = "btnExtractLast";
            btnExtractLast.Size = new Size(90, 23);
            btnExtractLast.TabIndex = 4;
            btnExtractLast.Text = "Extract Last";
            btnExtractLast.UseVisualStyleBackColor = true;
            btnExtractLast.Click += btnExtractLast_Click;
            // 
            // btnGenerateAll
            // 
            btnGenerateAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGenerateAll.Location = new Point(704, 6);
            btnGenerateAll.Name = "btnGenerateAll";
            btnGenerateAll.Size = new Size(100, 23);
            btnGenerateAll.TabIndex = 3;
            btnGenerateAll.Text = "Generate All";
            btnGenerateAll.UseVisualStyleBackColor = true;
            btnGenerateAll.Click += btnGenerateAll_Click;
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
            // tabPageFullVideo
            // 
            tabPageFullVideo.Location = new Point(4, 24);
            tabPageFullVideo.Name = "tabPageFullVideo";
            tabPageFullVideo.Padding = new Padding(3);
            tabPageFullVideo.Size = new Size(992, 672);
            tabPageFullVideo.TabIndex = 1;
            tabPageFullVideo.Text = "Full Video";
            tabPageFullVideo.UseVisualStyleBackColor = true;
            // 
            // tabPagePerPrompt
            // 
            tabPagePerPrompt.Controls.Add(btnExport);
            tabPagePerPrompt.Controls.Add(btnImport);
            tabPagePerPrompt.Location = new Point(4, 24);
            tabPagePerPrompt.Name = "tabPagePerPrompt";
            tabPagePerPrompt.Padding = new Padding(3);
            tabPagePerPrompt.Size = new Size(992, 672);
            tabPagePerPrompt.TabIndex = 2;
            tabPagePerPrompt.Text = "Per-Prompt Videos";
            tabPagePerPrompt.UseVisualStyleBackColor = true;
            // 
            // btnExport
            // 
            btnExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExport.Location = new Point(886, 6);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(100, 23);
            btnExport.TabIndex = 8;
            btnExport.Text = "Export Stitched";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += btnExport_Click;
            // 
            // btnImport
            // 
            btnImport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnImport.Location = new Point(780, 6);
            btnImport.Name = "btnImport";
            btnImport.Size = new Size(100, 23);
            btnImport.TabIndex = 7;
            btnImport.Text = "Import Videos";
            btnImport.UseVisualStyleBackColor = true;
            btnImport.Click += btnImport_Click;
            // 
            // VideoGeneratorForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1000, 700);
            Controls.Add(tabControl);
            Name = "VideoGeneratorForm";
            Text = "Video Generator";
            tabControl.ResumeLayout(false);
            tabPageGeneration.ResumeLayout(false);
            tabPageGeneration.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPrompts).EndInit();
            tabPagePerPrompt.ResumeLayout(false);
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
        private Button btnGenerateAll;
        private Button btnExtractLast;
        private Button btnPreview;
        private DataGridView dgvPrompts;
        private DataGridViewImageColumn colImage;
        private DataGridViewTextBoxColumn colPrompt;
        private DataGridViewButtonColumn colLora;
        private DataGridViewButtonColumn colGenerate;
        private ProgressBar pbProgress;
        private Label lblProgress;

        // Preview tab controls
        private Button btnExport;
        private Button btnImport;

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

        private void btnGenerateAll_Click(object sender, EventArgs e)
        {
            OnGenerateAllClick();
        }

        private void btnExtractLast_Click(object sender, EventArgs e)
        {
            ExtractLastFrameFromSelected();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            // Switch to preview tabs
            if (HasGeneratedVideos())
            {
                tabControl.SelectedIndex = 1; // Switch to Full Video tab
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            OnExportClick();
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ImportVideosForTesting();
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
            videoPlayerPerPrompt.Dock = DockStyle.Top;
            videoPlayerPerPrompt.Height = 400;
            videoPlayerPerPrompt.Location = new Point(3, 28);
            videoPlayerPerPrompt.Name = "videoPlayerPerPrompt";
            videoPlayerPerPrompt.Size = new Size(986, 400);
            videoPlayerPerPrompt.TabIndex = 0;
            videoPlayerPerPrompt.Loop = false;

            // Add to respective tab pages
            tabPageFullVideo.Controls.Add(videoPlayerFull);
            tabPagePerPrompt.Controls.Add(videoPlayerPerPrompt);
        }
    }
}