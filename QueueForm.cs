using iviewer.Helpers;
using iviewer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iviewer.Video
{
    public partial class QueueForm : Form
    {
        private readonly VideoGenerationConfig _config;
        private readonly VideoGenerationService _generationService;

        public QueueForm()
        {
            InitializeComponent();

            _config = new VideoGenerationConfig();
            _generationService = new VideoGenerationService(_config);

            LoadVideos();
            LoadClips();
        }


        private void LoadVideos()
        {
            // TODO: need clever SQL to order these based on Queue state
            dgvVideos.Rows.Clear();
            string sql = "SELECT DISTINCT v.* FROM VideoGenerationStates v ORDER BY CreatedDate";
            var dt = DB.Select(sql);
            foreach (DataRow dr in dt.Rows)
            {
                int rowIndex = dgvVideos.Rows.Add();
                string imgPath = dr["ImagePath"].ToString();
                if (File.Exists(imgPath))
                {
                    dgvVideos.Rows[rowIndex].Cells["Image"].Value = ImageHelper.CreateThumbnail(imgPath, 160, 160);
                }
                dgvVideos.Rows[rowIndex].Cells["PK"].Value = dr["PK"];
                dgvVideos.Rows[rowIndex].Cells["Status"].Value = dr["Status"];
                dgvVideos.Rows[rowIndex].Cells["CreatedDate"].Value = dr["CreatedDate"];
                dgvVideos.Rows[rowIndex].Tag = Guid.Parse(dr["PK"].ToString());
            }
        }

        private void LoadClips()
        {
            dgvClips.Rows.Clear();
            string sql = "SELECT c.* FROM ClipGenerationStates c JOIN VideoGenerationQueue q ON c.PK = q.ClipGenerationStatePK ORDER BY q.CreatedDate";
            var dt = DB.Select(sql);
            foreach (DataRow dr in dt.Rows)
            {
                // TODO: want to get a start image.
                int rowIndex = dgvClips.Rows.Add();
                dgvClips.Rows[rowIndex].Cells["PK"].Value = dr["PK"];
                dgvClips.Rows[rowIndex].Cells["VideoStatePK"].Value = dr["VideoGenerationStatePK"];
                dgvClips.Rows[rowIndex].Cells["Prompt"].Value = dr["Prompt"];
                dgvClips.Rows[rowIndex].Cells["Status"].Value = dr["Status"];
                dgvClips.Rows[rowIndex].Cells["CreatedDate"].Value = dr["CreatedDate"];
                dgvClips.Rows[rowIndex].Tag = Guid.Parse(dr["PK"].ToString());
            }
        }

        private void DgvVideos_DoubleClick(object sender, EventArgs e)
        {
            if (dgvVideos.SelectedRows.Count > 0)
            {
                Guid pk = (Guid)dgvVideos.SelectedRows[0].Tag;
                var form = new VideoGenerator();
                form.LoadState(pk);
                form.Show();
            }
        }

        private void DgvClips_DoubleClick(object sender, EventArgs e)
        {
            if (dgvClips.SelectedRows.Count > 0)
            {
                Guid pk = (Guid)dgvClips.SelectedRows[0].Tag;
                var clipState = ClipGenerationState.Load(pk);
                var form = new VideoGenerator();
                form.LoadState(clipState.VideoGenerationStatePK);
                form.Show();
            }
        }

        // TODO: This should dynamically refresh and process new rows if they are added while generation is in progress
        private async void BtnStart_Click(object sender, EventArgs e)
        {
            cts = new CancellationTokenSource();
            btnStart.Enabled = false;
            btnPause.Enabled = true;

            while (true)
            {
                // Process queue (e.g., all 'Queued' clips from DB)
                string sql = "SELECT TOP 1 PK FROM VideoGenerationQueue WHERE Status = 'Queued' ORDER BY CreatedDate";
                var dt = DB.SelectSingle(sql);
                if (!dt.ContainsKey("PK")) break;

                var pk = Guid.Parse(dt["PK"].ToString());

                var queueItem = VideoQueueItem.Load(pk);
                queueItem.Status = "Generating";
                queueItem.Save();

                // Load clip, gen video (adapt your gen logic)
                var clipState = ClipGenerationState.Load(queueItem.ClipGenerationStatePK);

                // Call gen helper (e.g., _generationService.GenerateClipAsync(clipState))
                string result = await _generationService.GenerateVideoAsync(clipState);

                // On success:
                if (File.Exists(result))
                {
                    clipState.Status = "Generated";
                    queueItem.Status = "Generated";
                }
                else
                {
                    clipState.Status = "Failed";
                    queueItem.Status = "Failed";
                }

                clipState.Save();
                queueItem.Save();

                if (cts.Token.IsCancellationRequested) break;
            }

            btnStart.Enabled = true;
            btnPause.Enabled = false;
            LoadVideos(); // Refresh
            LoadClips();
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            cts?.Cancel();
            btnPause.Enabled = false;
            btnStart.Enabled = true;
        }
    }
}
