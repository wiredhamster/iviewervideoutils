using System.Diagnostics;

namespace iviewer
{
	public partial class VideoPlayerControl : UserControl
	{
		SafeWindowsMediaPlayer _player;
        private Panel _videoPanel;

        public VideoPlayerControl()
		{
			InitializeComponent();

            // Create video panel
            _videoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            this.Controls.Add(_videoPanel);

            // Initialize player
            _player = new SafeWindowsMediaPlayer(_videoPanel);
            //_player.VideoFinished += (s, e) => MessageBox.Show("Video finished!");
            //_player.ErrorOccurred += (s, msg) => MessageBox.Show($"Error: {msg}");

            this.ResumeLayout();
        }

		public bool Loop { get; set; } = true;

		private void InitializeComponent()
		{
			SuspendLayout();
			// 
			// VideoPlayerControl
			// 
			DoubleBuffered = true;
			Name = "VideoPlayerControl";
			ResumeLayout(false);
		}

		public void Play(Stream stream, double speed = 1, bool loop = false)
		{
			// No stream support I imagine.
            // _player.PlayAsync()
		}

		public void Play(string path, double speed = 1, bool loop = false)
		{
			_player.PlayAsync(path, (float)speed, loop);
		}

        public async Task StopAndHide()
        {
            try
            {
				_player.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StopAndHide: {ex.Message}");
            }
        }
	}
}