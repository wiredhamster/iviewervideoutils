using AxWMPLib;
using CefSharp.DevTools.LayerTree;
using iviewer;
using System.Diagnostics;

public class SafeWindowsMediaPlayer : IDisposable
{
    private AxWMPLib.AxWindowsMediaPlayer _player;
    private AxWMPLib.AxWindowsMediaPlayer _inactivePlayer;
    private Panel _container;
    private bool _isDisposed;

    private string _nextFile;
    private float _nextSpeed;
    private bool _nextLoop;

    private System.Windows.Forms.Timer _endTimer;

    public event EventHandler VideoFinished;
    public event EventHandler<string> ErrorOccurred;

    public bool IsPlaying => _player?.playState == WMPLib.WMPPlayState.wmppsPlaying;

    public float PlaybackSpeed
    {
        get => (float)(_player?.settings.rate ?? 1.0);
        set
        {
            if (_player != null)
                _player.settings.rate = value;
        }
    }

    public bool Loop
    {
        get => _player?.settings.getMode("loop") ?? false;
        set
        {
            if (_player != null)
                _player.settings.setMode("loop", value);
        }
    }

    /// <summary>
    /// Creates a Windows Media Player in the specified panel
    /// </summary>
    public SafeWindowsMediaPlayer(Panel videoPanel)
    {
        try
		{
			_container = videoPanel;
			_inactivePlayer = InitPlayer(videoPanel);
			_player = InitPlayer(videoPanel);

			_endTimer = new System.Windows.Forms.Timer();
			_endTimer.Enabled = false;
			_endTimer.Interval = 1; // Small interval for frequent checks (effective ~15 ms)
			_endTimer.Tick += OnEndTimerTick;

			Debug.WriteLine("SafeWindowsMediaPlayer initialized");
		}
		catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Failed to initialize: {ex.Message}");
            throw;
        }
    }

	private AxWindowsMediaPlayer InitPlayer(Panel videoPanel)
	{
        var player = new AxWindowsMediaPlayer();

        ((System.ComponentModel.ISupportInitialize)(player)).BeginInit();

		player.Dock = DockStyle.Fill;
        player.Visible = false;
		player.Enabled = true;

		videoPanel.Controls.Add(player);

		((System.ComponentModel.ISupportInitialize)(player)).EndInit();

		player.uiMode = "none"; // Hide default controls
		player.stretchToFit = true;

		// Subscribe to events
		player.PlayStateChange += OnPlayStateChange;

        return player;
	}

	public async Task<bool> PlayAsync(string filePath, float speed = 1.0f, bool loop = false)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                ErrorOccurred?.Invoke(this, $"File not found: {filePath}");
                return false;
            }

            var oldPlayer = _player;
            var newPlayer = _inactivePlayer;

			newPlayer.URL = filePath;
            newPlayer.settings.rate = speed;
            newPlayer.settings.setMode("loop", loop);
            newPlayer.Ctlcontrols.play();

			_endTimer.Start();

			await Task.Delay(70); // Gives time for first frame to buffer without flicker

			_container.SuspendLayout();

			newPlayer.Size = _container.Size;
            newPlayer.BringToFront();
			newPlayer.Visible = true;

            oldPlayer.Ctlcontrols.pause();

			_player = newPlayer;
            _inactivePlayer = oldPlayer;

            _container.ResumeLayout();

            Debug.WriteLine($"Playing: {Path.GetFileName(filePath)} at {speed}x");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error playing: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Playback failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> QueueNextAsync(string filePath, float speed = 1.0f, bool loop = false)
    {
        try
        {
			if (!File.Exists(filePath))
			{
				ErrorOccurred?.Invoke(this, $"File not found: {filePath}");
				return false;
			}

            _nextFile = filePath;
            _nextSpeed = speed;
            _nextLoop = loop;

            return await Task.FromResult(true);
		}
        catch (Exception ex)
        {
			Debug.WriteLine($"Error playing: {ex.Message}");
			ErrorOccurred?.Invoke(this, $"Playback failed: {ex.Message}");
			return false;
		}
    }

    public void BringToFront()
    {
        _container.BringToFront();
    }

    public void Pause()
    {
        try
        {
            _player?.Ctlcontrols.pause();
            _endTimer.Stop();
            Debug.WriteLine("Paused");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error pausing: {ex.Message}");
        }
    }

    public void Resume()
    {
        try
        {
            _player?.Ctlcontrols.play();

            if (!Loop)
            {
                _endTimer.Start();
            }

            Debug.WriteLine("Resumed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error resuming: {ex.Message}");
        }
    }

    public void TogglePlayPause()
    {
        if (IsPlaying)
            Pause();
        else
            Resume();
    }

    public void Stop()
    {
        try
        {
            _player?.Ctlcontrols.stop();
            _endTimer.Stop();
            Debug.WriteLine("Stopped");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping: {ex.Message}");
        }
    }

    public async Task<bool> ExtractCurrentFrameAsync(string outputPath)
    {
        // Windows Media Player doesn't support direct frame extraction
        // You'll need to use FFmpeg or a screenshot approach
        await Task.CompletedTask;

        // Workaround: Take a screenshot of the control
        try
        {
            var bmp = new System.Drawing.Bitmap(_player.Width, _player.Height);
            _player.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, _player.Width, _player.Height));
            bmp.Save(outputPath);
            bmp.Dispose();

            Debug.WriteLine($"Frame saved to: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error extracting frame: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Frame extraction failed: {ex.Message}");
            return false;
        }
    }

    private void OnPlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
    {
        if (e.newState == (int)WMPLib.WMPPlayState.wmppsMediaEnded)
        {
            //Debug.WriteLine("Video finished");
            VideoFinished?.Invoke(this, EventArgs.Empty);
        }
        else if (e.newState == (int)WMPLib.WMPPlayState.wmppsReady)
        {
            //Debug.WriteLine("Player ready");
        }
    }

    private void OnEndTimerTick(object sender, EventArgs e)
    {
        try
        {
            if (_player?.currentMedia == null) return;

            if (Loop && string.IsNullOrEmpty(_nextFile)) return;

            const int PauseThresholdMs = 100; // Adjust as needed (e.g., 100-500 ms)

            double duration = _player.currentMedia.duration;
            double position = _player.Ctlcontrols.currentPosition;

            if (duration > 0 && position > 0 && (duration - position) * 1000 < PauseThresholdMs)
            {
                if (_nextFile != null)
                {
                    PlayAsync(_nextFile, _nextSpeed, _nextLoop);
                    _nextFile = "";
                    _nextSpeed = 1;
                    _nextLoop = false;
                }
                else
                {
                    _player.Ctlcontrols.pause();
                    _endTimer.Stop();
                    Debug.WriteLine($"Paused near last frame (threshold: {PauseThresholdMs} ms)");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Timer error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            DisposePlayer(_player);
            Debug.WriteLine("Disposed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disposing: {ex.Message}");
        }
    }

    void DisposePlayer(AxWindowsMediaPlayer player)
    {
        if (player != null)
        {
			_container.Controls.Remove(player);

			player.Visible = false;
			player.PlayStateChange -= OnPlayStateChange;
            player.Ctlcontrols?.stop();
            player.Dispose();
            player = null;
        }
    }
}
