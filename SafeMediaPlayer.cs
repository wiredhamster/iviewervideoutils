using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

public class SafeMediaPlayer : IDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly LibVLC _libVLC;
    private readonly SynchronizationContext _syncContext; // For marshalling to UI thread
    private Media _currentMedia;
    private string _currentFilePath;
    private Stream _currentStream; // Owned by caller, but tracked for disposal safety
    private bool _disposed = false;
    private bool _isPlaying = false;

    public event EventHandler VideoFinished;

    public bool IsPlaying => _isPlaying;

    public MediaPlayer InternalMediaPlayer => _mediaPlayer;

    public SafeMediaPlayer()
    {
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        _mediaPlayer.EndReached += OnEndReached;
    }

    // Play from file path
    public void Play(string filePath, double speed = 1, bool loop = false)
    {
        ExecuteOnSyncThread(() =>
        {
            var newMedia = new Media(_libVLC, filePath, FromType.FromPath);
            StopInternal();
            _currentMedia = newMedia;
            ConfigureMedia(speed, loop);
            _mediaPlayer.Media = _currentMedia;
            _mediaPlayer.Play();
            _isPlaying = true;
            _currentFilePath = filePath;
            _currentStream = null;
        });
    }

    // Play from stream (caller owns the stream, do not dispose it)
    public void Play(Stream stream, double speed = 1, bool loop = false)
    {
        ExecuteOnSyncThread(() =>
        {
            StopInternal();
            var streamMediaInput = new StreamMediaInput(_currentStream);
            _currentMedia = new Media(_libVLC, streamMediaInput);
            ConfigureMedia(speed, loop);
            _mediaPlayer.Media = _currentMedia;
            _mediaPlayer.Play();
            _isPlaying = true;
            _currentFilePath = null;
            _currentStream = stream; // Track but don't dispose
        });
    }

    public void Pause()
    {
        ExecuteOnSyncThread(() =>
        {
            if (_isPlaying)
            {
                _mediaPlayer.Pause();
                _isPlaying = false;
            }
        });
    }

    public void Resume()
    {
        ExecuteOnSyncThread(() =>
        {
            if (!_isPlaying && _mediaPlayer.CanPause)
            {
                _mediaPlayer.Play();
                _isPlaying = true;
            }
        });
    }

    private void StopInternal()
    {
        _mediaPlayer.Stop();
        _isPlaying = false;
        _currentMedia?.Dispose();
        _currentMedia = null;
        _currentFilePath = null;
        _currentStream = null;
    }

    public void Stop()
    {
        ExecuteOnSyncThread(StopInternal);
    }

    public bool ExtractCurrentFrame(string outputPath)
    {
        bool success = false;
        ExecuteOnSyncThread(() =>
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
            }
            success = _mediaPlayer.TakeSnapshot(0, outputPath, 0, 0); // 0 for default size
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Play();
            }
        });
        return success;
    }

    private void ConfigureMedia(double speed, bool loop)
    {
        if (loop)
        {
            _currentMedia.AddOption(":input-repeat=10000"); // Loop for a long time
        }

        _mediaPlayer.SetRate((float)speed);
    }

    private void OnEndReached(object sender, EventArgs e)
    {
        ExecuteOnSyncThread(() =>
        {
            _isPlaying = false;
            VideoFinished?.Invoke(this, EventArgs.Empty);
        });
    }

    private void ExecuteOnSyncThread(Action action)
    {
        if (_syncContext == SynchronizationContext.Current)
        {
            action();
        }
        else
        {
            _syncContext.Post(_ => action(), null);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _mediaPlayer.EndReached -= OnEndReached;
                StopInternal();
                _mediaPlayer.Dispose();
                _libVLC.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~SafeMediaPlayer()
    {
        Dispose(false);
    }
}