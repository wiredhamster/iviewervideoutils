using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System;
using System.IO;
using System.Windows.Forms;

namespace iviewer
{
	public partial class VideoPlayerControl : UserControl
	{
		VideoView videoView;
		LibVLC libVLC;
		MediaPlayer mediaPlayer;
		Media currentMedia;
		Stream currentStream;

		public VideoPlayerControl()
		{
			InitializeComponent();
		}

		public bool Loop { get; set; } = true;

		private void InitializeComponent()
		{
			this.SuspendLayout();
			libVLC = new LibVLC();
			mediaPlayer = new MediaPlayer(libVLC);
			videoView = new VideoView { MediaPlayer = mediaPlayer };
			videoView.Dock = DockStyle.Fill;
			this.Controls.Add(videoView);

			//
			// VideoPlayerControl
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Name = "VideoPlayerControl";
			this.Size = new System.Drawing.Size(300, 300);
			this.ResumeLayout(false);
			mediaPlayer.EnableKeyInput = false;
		}

		public Stream VideoStream
		{
			get => currentStream;
			set
			{
				if (value == null) return;
				if (mediaPlayer.IsPlaying)
				{
					currentMedia?.Dispose();
				}
				currentStream = value;

                Visible = true;
                BringToFront();
                Show();

                Play();
			}
		}

		void Play()
		{
			currentStream.Position = 0; // Reset position
			var streamMediaInput = new StreamMediaInput(currentStream);
			currentMedia = new Media(libVLC, streamMediaInput);

			if (Loop)
			{
				currentMedia.AddOption(":input-repeat=10000"); // Enable native looping
			}

			mediaPlayer.Play(currentMedia);
		}

		public async void StopAndHide()
		{
			try
			{
				if (mediaPlayer.IsPlaying)
				{
					var stopTask = Task.Run(() =>
					{
						mediaPlayer.Stop();
					});

					// Wait with timeout (e.g., 5 seconds)
					if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
					{
						// Timeout: Force dispose or handle hang (can't kill VLC thread, but continue app)
						// Log "Stop timed out"
					}
				}

				currentMedia?.Dispose();
				currentMedia = null;
			}
			catch (Exception ex)
			{
				// Log ex if needed
			}
        }

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				mediaPlayer?.Dispose();
				libVLC?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}