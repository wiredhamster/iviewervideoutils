using CefSharp.DevTools.LayerTree;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace iviewer
{
	public partial class VideoPlayerControl : UserControl
	{
		VideoView videoView;
		SafeMediaPlayer mediaPlayer;

		public VideoPlayerControl()
		{
			InitializeComponent();
		}

		public bool Loop { get; set; } = true;

		private void InitializeComponent()
		{
			this.SuspendLayout();
            mediaPlayer = new SafeMediaPlayer();
			videoView = new VideoView();
			videoView.MediaPlayer = mediaPlayer.InternalMediaPlayer;
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
			//mediaPlayer.EnableKeyInput = false;
		}

		public void Play(Stream stream, double speed = 1, bool loop = false)
		{
			mediaPlayer.Play(stream, speed, loop);
		}

		public void Play(string path, double speed = 1, bool loop = false)
		{
			mediaPlayer.Play(path, speed, loop);
		}

        public async Task StopAndHide()
        {
            try
            {
				mediaPlayer.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StopAndHide: {ex.Message}");
            }
        }
	}
}