namespace iviewer.Video
{
    partial class QueueForm
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

        private DataGridView dgvVideos;
        private DataGridView dgvClips;
        private Button btnAdd;
        private Button btnStart;
        private Button btnPause;
        private TabControl tabQueue;
        private TabPage tabVideos;
        private TabPage tabClips;

        private CancellationTokenSource cts;

        private void InitializeComponent()
        {
            tabQueue = new TabControl();
            tabVideos = new TabPage("Videos");
            tabClips = new TabPage("Clips");
            dgvVideos = new DataGridView();
            dgvClips = new DataGridView();
            btnAdd = new Button();
            btnStart = new Button();
            btnPause = new Button();
            tabQueue.SuspendLayout();
            tabVideos.SuspendLayout();
            tabClips.SuspendLayout();
            SuspendLayout();
            // 
            // tabQueue
            // 
            tabQueue.Controls.Add(tabVideos);
            tabQueue.Controls.Add(tabClips);
            tabQueue.Dock = DockStyle.Fill;
            tabQueue.Location = new Point(0, 0);
            tabQueue.Name = "tabQueue";
            tabQueue.SelectedIndex = 0;
            tabQueue.Size = new Size(784, 561);
            tabQueue.TabIndex = 0;
            // 
            // tabVideos
            // 
            tabVideos.Controls.Add(dgvVideos);
            tabVideos.Location = new Point(4, 22);
            tabVideos.Name = "tabVideos";
            tabVideos.Size = new Size(776, 535);
            tabVideos.TabIndex = 0;
            tabVideos.Text = "Videos";
            // 
            // dgvVideos
            // 
            dgvVideos.Dock = DockStyle.Fill;
            dgvVideos.AllowUserToAddRows = false;
            dgvVideos.AllowUserToDeleteRows = false;
            dgvVideos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvVideos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvVideos.Location = new Point(0, 0);
            dgvVideos.Name = "dgvVideos";
            dgvVideos.Size = new Size(776, 535);
            dgvVideos.TabIndex = 0;
            dgvVideos.DoubleClick += DgvVideos_DoubleClick;
            // Columns (e.g., PK, ImagePath, Status, CreatedDate)
            dgvVideos.Columns.Add(new DataGridViewImageColumn { Name = "Image", HeaderText = "Image", ImageLayout = DataGridViewImageCellLayout.Zoom });
            //dgvVideos.Columns.Add(new DataGridViewTextBoxColumn { Name = "PK", HeaderText = "PK", ReadOnly = true });
            dgvVideos.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", ReadOnly = true });
            dgvVideos.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedDate", HeaderText = "Created", ReadOnly = true });
            dgvVideos.Columns.Add(new DataGridViewTextBoxColumn { Name = "ModifiedDate", HeaderText = "Modified", ReadOnly = true });
            // 
            // tabClips
            // 
            tabClips.Controls.Add(dgvClips);
            tabClips.Location = new Point(4, 22);
            tabClips.Name = "tabClips";
            tabClips.Size = new Size(776, 535);
            tabClips.TabIndex = 1;
            tabClips.Text = "Clips";
            // 
            // dgvClips
            // 
            dgvClips.Dock = DockStyle.Fill;
            dgvClips.AllowUserToAddRows = false;
            dgvClips.AllowUserToDeleteRows = false;
            dgvClips.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvClips.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvClips.Location = new Point(0, 0);
            dgvClips.Name = "dgvClips";
            dgvClips.Size = new Size(776, 535);
            dgvClips.TabIndex = 0;
            dgvClips.DoubleClick += DgvClips_DoubleClick;
            // Columns (e.g., PK, VideoStatePK, Prompt, Status, CreatedDate)
            dgvClips.Columns.Add(new DataGridViewImageColumn { Name = "Image", HeaderText = "Image", ImageLayout = DataGridViewImageCellLayout.Zoom });
            //dgvClips.Columns.Add(new DataGridViewTextBoxColumn { Name = "PK", HeaderText = "PK", ReadOnly = true });
            //dgvClips.Columns.Add(new DataGridViewTextBoxColumn { Name = "VideoStatePK", HeaderText = "Video PK", ReadOnly = true });
            dgvClips.Columns.Add(new DataGridViewTextBoxColumn { Name = "Prompt", HeaderText = "Prompt", ReadOnly = true });
            dgvClips.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", ReadOnly = true });
            dgvClips.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedDate", HeaderText = "Created", ReadOnly = true });
            dgvClips.Columns.Add(new DataGridViewTextBoxColumn { Name = "ModifiedDate", HeaderText = "Modified", ReadOnly = true });
            // 
            // btnAdd
            // 
            btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAdd.Location = new Point(474, 2);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(80, 28);
            btnAdd.TabIndex = 1;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += BtnAdd_Click; ;
            // 
            // btnStart
            // 
            btnStart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnStart.Location = new Point(560, 2);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(80, 28);
            btnStart.TabIndex = 1;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += BtnStart_Click;
            // 
            // btnPause
            // 
            btnPause.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnPause.Location = new Point(646, 2);
            btnPause.Name = "btnPause";
            btnPause.Size = new Size(120, 28);
            btnPause.TabIndex = 2;
            btnPause.Text = "Pause";
            btnPause.UseVisualStyleBackColor = true;
            btnPause.Click += BtnPause_Click;
            // 
            // QueueForm
            // 
            ClientSize = new Size(784, 561);
            Controls.Add(btnAdd);
            Controls.Add(btnPause);
            Controls.Add(btnStart);
            Controls.Add(tabQueue);
            Name = "QueueForm";
            Text = "Queue";
            tabQueue.ResumeLayout(false);
            tabVideos.ResumeLayout(false);
            tabClips.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
    }
}