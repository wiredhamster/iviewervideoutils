using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
            try
            {
                // Verify media player is valid
                if (mediaPlayer == null || mediaPlayer.NativeReference == IntPtr.Zero)
                {
                    Debug.WriteLine("MediaPlayer is null or disposed, cannot play");
                    return;
                }

                // Verify stream is valid
                if (currentStream == null || !currentStream.CanRead)
                {
                    Debug.WriteLine("Stream is null or cannot be read");
                    return;
                }

                // Quick stop without timeout - don't wait for completion
                try
                {
                    if (mediaPlayer.IsPlaying)
                    {
                        // Use Pause instead of Stop - it's instant
                        mediaPlayer.Pause();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error pausing: {ex.Message}");
                }

                // Dispose old media
                try
                {
                    currentMedia?.Dispose();
                    currentMedia = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing media: {ex.Message}");
                }

                // Small delay for cleanup
                System.Threading.Thread.Sleep(50);

                // Reset stream position
                try
                {
                    if (currentStream.CanSeek)
                    {
                        currentStream.Position = 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error resetting stream position: {ex.Message}");
                    return;
                }

                // Create and configure media
                var streamMediaInput = new StreamMediaInput(currentStream);
                currentMedia = new Media(libVLC, streamMediaInput);

                if (Loop)
                {
                    currentMedia.AddOption(":input-repeat=10000");
                }

                // Play the media
                if (mediaPlayer.Play(currentMedia))
                {
                    mediaPlayer.SetRate(1f);
                    Debug.WriteLine("Playback started successfully");
                }
                else
                {
                    Debug.WriteLine("Failed to start playback");
                }
            }
            catch (AccessViolationException ex)
            {
                Debug.WriteLine($"Access violation in Play(): {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Play(): {ex.Message}");
            }
        }

        public void SetSpeed(double speed)
		{
            if (speed != 1)
            {
                mediaPlayer.SetRate((float)speed);
            }
        }

        public async Task StopAndHide()
        {
            try
            {
                // Check if invoke is even possible
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() => StopMediaPlayer()));
                    }
                    else
                    {
                        StopMediaPlayer();
                    }
                }
                else
                {
                    // Handle not created or disposed, just stop directly
                    StopMediaPlayer();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StopAndHide: {ex.Message}");
            }
        }

        private void StopMediaPlayer()
        {
            try
            {
                if (mediaPlayer != null && mediaPlayer.NativeReference != IntPtr.Zero)
                {
                    try
                    {
                        // Try Pause first (more reliable)
                        if (mediaPlayer.IsPlaying)
                        {
                            var pauseTask = Task.Run(() =>
                            {
                                try
                                {
                                    mediaPlayer.Pause();
                                }
                                catch { }
                            });

                            if (!pauseTask.Wait(500))
                            {
                                Debug.WriteLine("Pause timed out, trying Stop");

                                // If Pause times out, try Stop with timeout
                                var stopTask = Task.Run(() =>
                                {
                                    try
                                    {
                                        mediaPlayer.Stop();
                                    }
                                    catch { }
                                });

                                if (!stopTask.Wait(1000))
                                {
                                    Debug.WriteLine("Stop also timed out, forcing cleanup");
                                }
                            }
                        }
                    }
                    catch (AccessViolationException)
                    {
                        Debug.WriteLine("MediaPlayer already disposed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error stopping playback: {ex.Message}");
                    }
                }

                // Always try to dispose media
                try
                {
                    currentMedia?.Dispose();
                    currentMedia = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing media: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StopMediaPlayer: {ex.Message}");
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