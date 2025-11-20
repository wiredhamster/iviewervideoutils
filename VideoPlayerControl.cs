using System.Diagnostics;

namespace iviewer
{
	public partial class VideoPlayerControl : UserControl
	{
		private WebView2VideoPlayer _player;
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
			_player = new WebView2VideoPlayer(_videoPanel);

			this.ResumeLayout();
		}

		public bool Loop { get; set; } = true;

		// Expose playlist events
		public event EventHandler<int> PlaylistItemChanged
		{
			add { _player.PlaylistItemChanged += value; }
			remove { _player.PlaylistItemChanged -= value; }
		}

		public event EventHandler VideoFinished
		{
			add { _player.VideoFinished += value; }
			remove { _player.VideoFinished -= value; }
		}

		public event EventHandler<string> ErrorOccurred
		{
			add { _player.ErrorOccurred += value; }
			remove { _player.ErrorOccurred -= value; }
		}

		// Expose playlist properties
		public int CurrentPlaylistIndex => _player.CurrentPlaylistIndex;
		public int PlaylistCount => _player.PlaylistCount;
		public bool IsPlayingPlaylist => _player.IsPlayingPlaylist;

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
			_player.PlayAsync(stream, (float)speed, loop);
		}

		public void Play(string path, double speed = 1, bool loop = false)
		{
			_player.PlayAsync(path, (float)speed, loop);
		}

		public async Task<bool> PlayPlaylistAsync(List<PlaylistItem> playlist, int startIndex = 0)
		{
			return await _player.PlayPlaylistAsync(playlist, startIndex);
		}

		public void StopPlaylist()
		{
			_player.StopPlaylist();
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

		public void TogglePlayPause()
		{
			_player.TogglePlayPause();
		}

		public async Task ExtractCurrentFrameAsync(string path, int width = 0, int height = 0)
		{
			if (width == 0 || height == 0)
			{
				await _player.ExtractCurrentFrameAsync(path);
			}
			else
			{
				await _player.ExtractCurrentFrameAsync(path, width, height);
			}
		}

		public async Task<double> GetCurrentTimeAsync()
		{
			return await _player.GetCurrentTimeAsync();
		}

		public async Task<bool> IsPausedAsync()
		{
			return await _player.IsPausedAsync();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_player?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}