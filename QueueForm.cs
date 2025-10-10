using iviewer.Helpers;
using iviewer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
            //EventBus.ClipQueued += OnClipQueued;
            EventBus.ClipStatusChanged += OnClipStatusChanged;
            EventBus.ClipDeleted += OnClipDeleted;
            EventBus.VideoStatusChanged += OnVideoStatusChanged;
            EventBus.VideoDeleted += OnVideoDeleted;
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
                var video = new VideoGenerationState();
                video.LoadFromRow(dr);
                CreateVideoRow(video);                
            }
        }

        void CreateVideoRow(VideoGenerationState video)
        {
            int rowIndex = dgvVideos.Rows.Add();

            string imgPath = video.ImagePath;
            if (File.Exists(imgPath))
            {
                dgvVideos.Rows[rowIndex].Cells["Image"].Value = ImageHelper.CreateThumbnail(imgPath, null, 160);
            }
            //dgvVideos.Rows[rowIndex].Cells["PK"].Value = dr["PK"];
            dgvVideos.Rows[rowIndex].Cells["Status"].Value = video.Status;
            dgvVideos.Rows[rowIndex].Cells["CreatedDate"].Value = video.CreatedDate;
            dgvVideos.Rows[rowIndex].Cells["ModifiedDate"].Value = video.ModifiedDate;
            dgvVideos.Rows[rowIndex].Tag = video.PK;
            dgvVideos.Rows[rowIndex].MinimumHeight = 160;
        }

        private void LoadClips()
        {
            dgvClips.Rows.Clear();
            string sql = @"
SELECT c.* 
FROM ClipGenerationStates c
    JOIN VideoGenerationStates v ON c.VideoGenerationStatePK = v.PK
WHERE c.Status != 'Queue'
ORDER BY v.CreatedDate, c.OrderIndex";
            var dt = DB.Select(sql);
            foreach (DataRow dr in dt.Rows)
            {
                var clip = new ClipGenerationState();
                clip.LoadFromRow(dr);
                CreateClipRow(clip);
            }
        }

        private void CreateClipRow(ClipGenerationState clip)
        {
            int rowIndex = dgvClips.Rows.Add();

            string imgPath = clip.ImagePath;
            if (File.Exists(imgPath))
            {
                dgvClips.Rows[rowIndex].Cells["Image"].Value = ImageHelper.CreateThumbnail(imgPath, null, 160);
            }

            dgvClips.Rows[rowIndex].Cells["Prompt"].Value = clip.Prompt;
            dgvClips.Rows[rowIndex].Cells["Status"].Value = clip.Status;
            dgvClips.Rows[rowIndex].Cells["CreatedDate"].Value = clip.CreatedDate;
            dgvClips.Rows[rowIndex].Cells["ModifiedDate"].Value = clip.ModifiedDate;
            dgvClips.Rows[rowIndex].Tag = clip.PK;
            dgvClips.Rows[rowIndex].MinimumHeight = 160;
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
WHERE c.Status = 'Queued' OR v.Status = 'Export'
ORDER BY v.CreatedDate, c.OrderIndex";
                    var dt = DB.SelectSingle(sql);
                    if (!dt.ContainsKey("PK")) break;

                    var pk = Guid.Parse(dt["PK"].ToString());
                    var videoPK = Guid.Parse(dt["VideoGenerationStatePK"].ToString());

                    var video = VideoGenerationState.Load(videoPK);
                    var clipState = video.ClipGenerationStates.FirstOrDefault(c => c.PK == pk);

                    // Generate or Export
                    if (video.Status == "Export")
                    {
                        await ExportVideo(video);
                    }
                    else if (clipState.Status == "Queued")
                    {
                        await GenerateVideo(clipState, video);
                    }
                    else
                    {
                        // What???
                        throw new Exception("Why no clip or video?");
                    }

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
                if (!video.ClipGenerationStates.Any())
                {
                    video.Delete();
                    EventBus.RaiseVideoDeleted(pk, new HashSet<Guid>());
                    return;
                }
                else if (video.ClipGenerationStates.Any(c => c.Status == "Failed"))
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
                else if (video.ClipGenerationStates.Any(c => c.Status == "Generated") || video.ClipGenerationStates.Any(c => c.Status == "Requeue"))
                {
                    video.Status = "Generated";
                }
                else
                {
                    video.Status = "Unknown";
                }

                if (video.HasChanges)
                {
                    video.Save();
                    EventBus.RaiseVideoStatusChanged(pk, video.Status);
                }
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

        #region Generate Video

        async Task GenerateVideo(ClipGenerationState clipState, VideoGenerationState video)
        {
            video.Status = "Generating";
            clipState.Status = "Generating";
            video.Save();

            EventBus.RaiseVideoStatusChanged(video.PK, video.Status);
            EventBus.RaiseClipStatusChanged(clipState.PK, video.PK, "Generating");

            string result = await _generationService.GenerateVideoAsync(clipState);

            // On success:
            if (File.Exists(result))
            {
                clipState.Status = "Generated";

                var sql = $"SELECT TOP 1 * From ClipGenerationStates WHERE VideoGenerationStatePK = {DB.FormatDBValue(clipState.VideoGenerationStatePK)} AND OrderIndex = {DB.FormatDBValue(clipState.OrderIndex + 1)}";
                var nextClip = ClipGenerationState.Load(sql);
                if (nextClip != null && nextClip.ImagePath == "")
                {
                    var lastFrame = VideoUtils.ExtractLastFrame(result, _config.TempDir, true);
                    nextClip.ImagePath = lastFrame;
                    nextClip.Save();

                    EventBus.RaiseClipStatusChanged(clipState.PK, video.PK, nextClip.Status);
                }
            }
            else
            {
                clipState.Status = "Failed";
            }

            clipState.Save();
            EventBus.RaiseClipStatusChanged(clipState.PK, video.PK, clipState.Status);
            UpdateVideoState(clipState.VideoGenerationStatePK);
        }

        #endregion

        #region Export Video

        async Task ExportVideo(VideoGenerationState videoGenerationState)
        {
            if (videoGenerationState.ClipGenerationStates.Any(c => c.Status == "Queued" || c.Status == "Generating"))
            {
                videoGenerationState.Status = "Failed";
                videoGenerationState.Save();

                EventBus.RaiseVideoStatusChanged(videoGenerationState.PK, "Failed");

                return;
            }

            videoGenerationState.Status = "Exporting";
            videoGenerationState.Save();

            EventBus.RaiseVideoStatusChanged(videoGenerationState.PK, "Exporting");

            var generationService = new VideoGenerationService(_config);

            var clipsToExport = videoGenerationState.ClipGenerationStates.Where(c => !string.IsNullOrEmpty(c.VideoPath)).OrderBy(c => c.OrderIndex).ToList();

            var path = await generationService.StitchVideos(clipsToExport, true);
            videoGenerationState.TempFiles = videoGenerationState.TempFiles + "," + path;

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new Exception("Stitch failed");
                }

                string filename = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string tempPath = Path.Combine(_config.TempDir, filename + ".mp4");
                string exportPath = Path.Combine(
                    _config.ExportDir,
                    filename + ".mp4");
                string metaPath = Path.Combine(
                    Path.GetDirectoryName(exportPath) ?? "",
                    filename + ".json");

                // Ensure export directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath) ?? "");

                // Apply upscaling and interpolation to the stitched video
                bool success = await VideoUtils.UpscaleAndInterpolateVideoAsync(path, tempPath);

                if (!success)
                {
                    throw new Exception("Video upscaling and interpolation failed");
                }

                // Export metadata
                await ExportMetadataAsync(GenerateClipInfos(clipsToExport), metaPath);

                // Copy to final location
                File.Copy(tempPath, exportPath, overwrite: true);

                // Cleanup temp file
                File.Delete(tempPath);

                videoGenerationState.Status = "Exported";
                videoGenerationState.Save();

                EventBus.RaiseVideoStatusChanged(videoGenerationState.PK, videoGenerationState.Status);

                await generationService.DeleteAndCleanUp(videoGenerationState);
            }
            catch (Exception ex)
            {
                videoGenerationState.Status = "Failed";
                videoGenerationState.Save();

                EventBus.RaiseVideoStatusChanged(videoGenerationState.PK, videoGenerationState.Status);
            }
        }

        private List<VideoClipInfo> GenerateClipInfos(List<ClipGenerationState> clipStates)
        {
            var clipInfos = new List<VideoClipInfo>();
            var metadataService = new VideoMetadataService();

            foreach (var clipState in clipStates)
            {
                if (File.Exists(clipState.VideoPath))
                {
                    var clipInfo = metadataService.ExtractClipInfo(clipState);
                    clipInfos.Add(clipInfo);
                }
            }

            return clipInfos;
        }

        private async Task ExportMetadataAsync(List<VideoClipInfo> clipInfos, string metaPath)
        {
            var jsonData = new
            {
                Source = Guid.NewGuid(),
                ClipInfos = clipInfos,
                // TODO: Export ClipGenerationStates. Or at least relevant info from them.
                ExportTimestamp = DateTime.Now
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);
            await File.WriteAllTextAsync(metaPath, json);
        }

        #endregion

        #region Data Refresh

        //private void OnClipQueued(Guid clipPK)
        //{
        //    if (InvokeRequired)
        //    {
        //        Invoke(new Action<Guid>(OnClipQueued), clipPK);
        //        return;
        //    }

        //    var clip = ClipGenerationState.Load(clipPK);
        //    if (clip != null)
        //    {
        //        var found = false;

        //        for (var i = 0; i < dgvClips.Rows.Count; i++)
        //        {
        //            if (((Guid)dgvClips.Rows[i].Tag) == clipPK)
        //            {
        //                found = true;
        //                dgvClips.Rows[i].Cells["Status"].Value = clip.Status;

        //                break;
        //            }
        //        }

        //        if (!found)
        //        {
        //            CreateClipRow(clip);
        //        }

        //        UpdateVideoState(clip.VideoGenerationStatePK);
        //        UpdateVideoRowState(clip.VideoGenerationStatePK);
        //    }
        //    else
        //    {
        //        // Time for a refresh
        //        LoadVideos();
        //        LoadClips();
        //    }
        //}

        private void OnClipStatusChanged(Guid clipPK, Guid videoPK, string newStatus)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Guid, Guid, string>(OnClipStatusChanged), clipPK, newStatus);
                return;
            }

            // Update status in dgvClips (find row by PK, update Status cell)
            var found = false;
            var clip = ClipGenerationState.Load(clipPK);
            if (clip != null)
            {

                foreach (DataGridViewRow row in dgvClips.Rows)
                {
                    if ((Guid)row.Tag == clipPK) // Assuming Tag = PK
                    {
                        row.Cells["Status"].Value = newStatus;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    CreateClipRow(clip);
                }

                UpdateVideoState(clip.VideoGenerationStatePK);
                UpdateVideoRowState(clip.VideoGenerationStatePK);
            }
            else
            {
                // Time for a refresh
                LoadVideos();
                LoadClips();
            }
        }

        private void OnClipDeleted(Guid clipPK, Guid videoPK)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Guid, Guid>(OnClipDeleted), clipPK, videoPK);
                return;
            }

            foreach (DataGridViewRow row in dgvClips.Rows)
            {
                if ((Guid)row.Tag == clipPK)
                {
                    dgvClips.Rows.Remove(row);
                    break;
                }
            }

            UpdateVideoState(videoPK);
        }

        private void OnVideoStatusChanged(Guid videoPK, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Guid, string>(OnVideoStatusChanged), videoPK, status);
                return;
            }

            UpdateVideoRowState(videoPK, status);
        }

        private void OnVideoDeleted(Guid videoPK, HashSet<Guid> clipPKs)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Guid, HashSet<Guid>>(OnVideoDeleted), videoPK, clipPKs);
                return;
            }

            foreach (DataGridViewRow row in dgvVideos.Rows)
            {
                if (row.Tag != null && (Guid)row.Tag == videoPK)
                {
                    dgvVideos.Rows.Remove(row);
                }
            }

            foreach (DataGridViewRow row in dgvClips.Rows)
            {
                if (clipPKs.Contains((Guid)row.Tag))
                {
                    var pk = (Guid)row.Tag;
                    dgvClips.Rows.Remove(row);
                    clipPKs.Remove(pk);

                    if (!clipPKs.Any())
                    {
                        break;
                    }
                }
            }
        }

        void UpdateVideoRowState(Guid videoPK, string status = null)
        {
            var video = VideoGenerationState.Load(videoPK);

            if (status == null && video != null)
            {
                status = video.Status;
            }

            bool found = false;

            foreach (DataGridViewRow row in dgvVideos.Rows)
            {
                if (row.Tag != null && (Guid)row.Tag == videoPK)
                {
                    found = true;
                    row.Cells["Status"].Value = status;
                    break;
                }
            }

            if (!found)
            {
                CreateVideoRow(video);
            }
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Unsubscribe to avoid leaks
            //EventBus.ClipQueued -= OnClipQueued;
            EventBus.ClipStatusChanged -= OnClipStatusChanged;
            EventBus.ClipDeleted -= OnClipDeleted;
            EventBus.VideoStatusChanged -= OnVideoStatusChanged;
            EventBus.VideoDeleted -= OnVideoDeleted;
            base.OnFormClosing(e);
        }
    }
}
