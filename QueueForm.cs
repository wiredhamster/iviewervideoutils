using iviewer.Helpers;
using iviewer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

namespace iviewer.Video
{
    public partial class QueueForm : Form
    {
        public bool Running { get; private set; } = false;

        private readonly VideoGenerationConfig _config;
        private readonly VideoGenerationService _generationService;

        public QueueForm()
        {
            InitializeComponent();

            _config = new VideoGenerationConfig();
            _generationService = new VideoGenerationService(_config);

            SyncStates();
            LoadVideos();
            LoadClips();

            // Subscribe to events
            EventBus.ItemQueued += OnItemQueued;
            EventBus.ClipStatusChanged += OnClipStatusChanged;
        }

        public void Start()
        {
            BtnStart_Click(this, EventArgs.Empty);
        }

        private void LoadVideos()
        {
            dgvVideos.Rows.Clear();
            string sql = "SELECT DISTINCT v.* FROM VideoGenerationStates v ORDER BY CreatedDate";
            var dt = DB.Select(sql);
            foreach (DataRow dr in dt.Rows)
            {
                int rowIndex = dgvVideos.Rows.Add();
                string imgPath = dr["ImagePath"].ToString();
                if (File.Exists(imgPath))
                {
                    dgvVideos.Rows[rowIndex].Cells["Image"].Value = ImageHelper.CreateThumbnail(imgPath, null, 160);
                }
                //dgvVideos.Rows[rowIndex].Cells["PK"].Value = dr["PK"];
                dgvVideos.Rows[rowIndex].Cells["Status"].Value = dr["Status"];
                dgvVideos.Rows[rowIndex].Cells["CreatedDate"].Value = dr["CreatedDate"];
                dgvVideos.Rows[rowIndex].Cells["ModifiedDate"].Value = dr["ModifiedDate"];
                dgvVideos.Rows[rowIndex].Tag = Guid.Parse(dr["PK"].ToString());
                dgvVideos.Rows[rowIndex].MinimumHeight = 160;
            }
        }

        private void LoadClips()
        {
            dgvClips.Rows.Clear();
            string sql = "SELECT * FROM ClipGenerationStates WHERE Status IN ('Queued', 'Generating', 'Generated') ORDER BY CreatedDate, OrderIndex";
            var dt = DB.Select(sql);
            foreach (DataRow dr in dt.Rows)
            {
                int rowIndex = dgvClips.Rows.Add();
                //dgvClips.Rows[rowIndex].Cells["PK"].Value = dr["PK"];
                //dgvClips.Rows[rowIndex].Cells["VideoStatePK"].Value = dr["VideoGenerationStatePK"];
                string imgPath = dr["ImagePath"].ToString();
                if (File.Exists(imgPath))
                {
                    dgvClips.Rows[rowIndex].Cells["Image"].Value = ImageHelper.CreateThumbnail(imgPath, null, 160);
                }

                dgvClips.Rows[rowIndex].Cells["Prompt"].Value = dr["Prompt"];
                dgvClips.Rows[rowIndex].Cells["Status"].Value = dr["Status"];
                dgvClips.Rows[rowIndex].Cells["CreatedDate"].Value = dr["CreatedDate"];
                dgvClips.Rows[rowIndex].Cells["ModifiedDate"].Value = dr["ModifiedDate"];
                dgvClips.Rows[rowIndex].Tag = Guid.Parse(dr["PK"].ToString());
                dgvClips.Rows[rowIndex].MinimumHeight = 160;
            }
        }

        private void DgvVideos_DoubleClick(object sender, EventArgs e)
        {
            if (dgvVideos.SelectedRows.Count > 0)
            {
                Guid pk = (Guid)dgvVideos.SelectedRows[0].Tag;

                var form = FindForm(pk);
                if (form != null)
                {
                    form.BringToFront();
                    form.Activate();
                    form.Focus();
                    form.Show();
                }
                else
                {
                    var newForm = new VideoGenerator(pk);
                    //newForm.LoadState(pk);
                    newForm.Show();
                }
            }
        }

        private void DgvClips_DoubleClick(object sender, EventArgs e)
        {
            if (dgvClips.SelectedRows.Count > 0)
            {
                Guid pk = (Guid)dgvClips.SelectedRows[0].Tag;
                var clipState = ClipGenerationState.Load(pk);

                var form = FindForm(clipState.VideoGenerationStatePK);
                if (form != null)
                {
                    form.BringToFront();
                    form.Activate();
                    form.Focus();
                }
                else
                {
                    var newForm = new VideoGenerator(clipState.VideoGenerationStatePK);
                    //newForm.LoadState(clipState.VideoGenerationStatePK);
                    newForm.Show();
                }
            }
        }

        Form FindForm(Guid pk)
        {
            foreach (Form form in Application.OpenForms)
            {
                // Check if form has PK property (use reflection for safety)
                var pkProp = form.GetType().GetProperty("VideoGenerationStatePK");
                if (pkProp != null && pkProp.PropertyType == typeof(Guid))
                {
                    Guid formPK = (Guid)pkProp.GetValue(form);
                    if (formPK == pk)
                    {
                        return form;
                    }
                }
            }

            return null;
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var form = new VideoGenerator();
            form.Show();
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            cts = new CancellationTokenSource();
            btnStart.Enabled = false;
            btnPause.Enabled = true;
            Running = true;

            try
            {
                while (true)
                {
                    // Process queue (e.g., all 'Queued' clips from DB)
                    string sql = @"
SELECT TOP 1 c.PK, c.VideoGenerationStatePK 
FROM ClipGenerationStates c
    JOIN VideoGenerationStates v ON c.VideoGenerationStatePK = v.PK
WHERE c.Status = 'Queued' 
ORDER BY v.CreatedDate, c.OrderIndex";
                    var dt = DB.SelectSingle(sql);
                    if (!dt.ContainsKey("PK")) break;

                    var pk = Guid.Parse(dt["PK"].ToString());
                    var videoPK = Guid.Parse(dt["VideoGenerationStatePK"].ToString());

                    var video = VideoGenerationState.Load(videoPK);
                    var clipState = video.ClipGenerationStates.FirstOrDefault(c => c.PK == pk);

                    video.Status = "Generating";
                    clipState.Status = "Generating";
                    video.Save();

                    EventBus.RaiseClipStatusChanged(clipState.PK, "Generating");

                    string result = await _generationService.GenerateVideoAsync(clipState);

                    // On success:
                    if (File.Exists(result))
                    {
                        clipState.Status = "Generated";

                        sql = $"SELECT TOP 1 * From ClipGenerationStates WHERE VideoGenerationStatePK = {DB.FormatDBValue(clipState.VideoGenerationStatePK)} AND OrderIndex = {DB.FormatDBValue(clipState.OrderIndex + 1)}";
                        var nextClip = ClipGenerationState.Load(sql);
                        if (nextClip != null && nextClip.ImagePath == "")
                        {
                            var lastFrame = VideoUtils.ExtractLastFrame(result, _config.TempDir, true);
                            nextClip.ImagePath = lastFrame;
                            nextClip.Save();

                            EventBus.RaiseClipStatusChanged(nextClip.PK, nextClip.Status);
                        }
                    }
                    else
                    {
                        clipState.Status = "Failed";
                    }

                    clipState.Save();
                    UpdateVideoState(clipState.VideoGenerationStatePK);

                    EventBus.RaiseClipStatusChanged(clipState.PK, clipState.Status);

                    if (cts.Token.IsCancellationRequested) break;
                }
            }
            finally
            {
                btnStart.Enabled = true;
                btnPause.Enabled = false;
                btnPause.Text = "Pause";
                LoadVideos(); // Refresh
                LoadClips();

                Running = false;
            }
        }

        void UpdateVideoState(Guid pk)
        {
            var video = VideoGenerationState.Load(pk);
            if (video != null)
            {
                if (video.ClipGenerationStates.Any(c => c.Status == "Failed"))
                {
                    video.Status = "Failed";
                }
                else if (video.ClipGenerationStates.Any(c => c.Status == "Generating"))
                {
                    video.Status = "Generating";
                }
                else if (video.ClipGenerationStates.Any(c => c.Status == "Queued"))
                {
                    video.Status = "Queued";
                }
                else if (video.ClipGenerationStates.Any(c => c.Status == "Generated"))
                {
                    video.Status = "Generated";
                }
                else
                {
                    video.Status = "Unknown";
                }

                video.Save();
            }
        }

        void SyncStates()
        {
            var sql = "SELECT PK FROM VideoGenerationStates";
            using (var table = DB.Select(sql))
            {
                for (var i = 0; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    var pk = Guid.Parse(row[0].ToString());
                    UpdateVideoState(pk);
                }
            }
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            cts?.Cancel();
            btnPause.Text = "Pausing...";
            btnPause.Enabled = false;
            btnStart.Enabled = false;
        }

        #region Data Refresh

        private void OnItemQueued(Guid clipPK)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Guid>(OnItemQueued), clipPK);
                return;
            }

            // Reload grids to show new item
            LoadVideos();
            LoadClips();
        }

        private void OnClipStatusChanged(Guid clipPK, string newStatus)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Guid, string>(OnClipStatusChanged), clipPK, newStatus);
                return;
            }

            //// Update status in dgvClips (find row by PK, update Status cell)
            //foreach (DataGridViewRow row in dgvClips.Rows)
            //{
            //    if ((Guid)row.Tag == clipPK) // Assuming Tag = PK
            //    {
            //        row.Cells["Status"].Value = newStatus;
            //        break;
            //    }
            //}

            LoadVideos();
            LoadClips();
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Unsubscribe to avoid leaks
            EventBus.ItemQueued -= OnItemQueued;
            EventBus.ClipStatusChanged -= OnClipStatusChanged;
            base.OnFormClosing(e);
        }
    }
}
