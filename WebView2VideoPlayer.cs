using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text.Json;

namespace iviewer
{
	public class PlaylistItem
	{
		public string FilePath { get; set; }
		public float Speed { get; set; } = 1.0f;
		public bool Loop { get; set; } = false;
	}

	public class WebView2VideoPlayer : IDisposable
	{
		private WebView2 _webView;
		private Panel _container;
		private bool _isDisposed;
		private bool _isInitialized;
		private TaskCompletionSource<bool> _initializationTcs;

		private string _nextFile;
		private float _nextSpeed;
		private bool _nextLoop;

		// Store streams for serving
		private Dictionary<string, Stream> _streamCache = new Dictionary<string, Stream>();
		private int _streamCounter = 0;

		// Playlist support
		private List<PlaylistItem> _playlist = new List<PlaylistItem>();
		private int _currentPlaylistIndex = -1;
		private bool _isPlayingPlaylist = false;

		public event EventHandler VideoFinished;
		public event EventHandler<string> ErrorOccurred;
		public event EventHandler<int> PlaylistItemChanged;

		public bool IsPlaying { get; private set; }

		public float PlaybackSpeed { get; private set; } = 1.0f;

		public bool Loop { get; private set; }

		public int CurrentPlaylistIndex => _currentPlaylistIndex;
		public int PlaylistCount => _playlist.Count;
		public bool IsPlayingPlaylist => _isPlayingPlaylist;

		public WebView2VideoPlayer(Panel videoPanel)
		{
			try
			{
				_container = videoPanel;
				_initializationTcs = new TaskCompletionSource<bool>();

				_webView = new WebView2
				{
					Dock = DockStyle.Fill
				};

				_container.Controls.Add(_webView);

				InitializeAsync();

				Debug.WriteLine("WebView2VideoPlayer initialized");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error initializing: {ex.Message}");
				ErrorOccurred?.Invoke(this, $"Failed to initialize: {ex.Message}");
				throw;
			}
		}

		private async void InitializeAsync()
		{
			try
			{
				await _webView.EnsureCoreWebView2Async(null);

				_webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
				_webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
				_webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

				_webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
				_webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

				string html = GetVideoPlayerHtml();
				_webView.CoreWebView2.NavigateToString(html);

				await Task.Delay(100);

				_isInitialized = true;
				_initializationTcs.SetResult(true);

				Debug.WriteLine("WebView2 initialized successfully");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error during WebView2 initialization: {ex.Message}");
				ErrorOccurred?.Invoke(this, $"Initialization failed: {ex.Message}");
				_initializationTcs.SetResult(false);
			}
		}

		private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
		{
			try
			{
				var uri = e.Request.Uri;
				Debug.WriteLine($"Resource requested: {uri}");

				if (uri.StartsWith("http://video.local/"))
				{
					var resourcePath = Uri.UnescapeDataString(uri.Substring("http://video.local/".Length));
					Debug.WriteLine($"Loading video resource: {resourcePath}");

					Stream contentStream = null;
					string mimeType = "video/mp4";

					if (resourcePath.StartsWith("stream_"))
					{
						if (_streamCache.ContainsKey(resourcePath))
						{
							contentStream = _streamCache[resourcePath];
							if (contentStream.CanSeek)
								contentStream.Position = 0;
							Debug.WriteLine($"Serving from stream cache: {resourcePath}");
						}
					}
					else if (File.Exists(resourcePath))
					{
						contentStream = new FileStream(resourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

						string ext = Path.GetExtension(resourcePath).ToLower();
						if (ext == ".webm") mimeType = "video/webm";
						else if (ext == ".ogg" || ext == ".ogv") mimeType = "video/ogg";
						else if (ext == ".mov") mimeType = "video/quicktime";

						Debug.WriteLine($"Serving file: {resourcePath}");
					}

					if (contentStream != null)
					{
						Debug.WriteLine($"Serving with MIME type: {mimeType}");

						var response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
							contentStream,
							200,
							"OK",
							$"Content-Type: {mimeType}\nAccess-Control-Allow-Origin: *\nAccept-Ranges: bytes"
						);

						e.Response = response;
					}
					else
					{
						Debug.WriteLine($"Resource not found: {resourcePath}");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error in WebResourceRequested: {ex.Message}");
			}
		}

		private string GetVideoPlayerHtml()
		{
			return @"
<!DOCTYPE html>
<html>
<head>
    <style>
        * { margin: 0; padding: 0; }
        body { 
            background: black; 
            overflow: hidden;
            width: 100vw;
            height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        video {
            width: 100%;
            height: 100%;
            object-fit: contain;
            display: block;
        }
        #debug {
            position: absolute;
            top: 10px;
            left: 10px;
            color: white;
            font-family: monospace;
            font-size: 12px;
            background: rgba(0,0,0,0.3);
            padding: 10px;
            display: none;
            cursor: pointer;
        }
        #debug:hover {
            background: rgba(0,0,0,0.5);
        }
    </style>
</head>
<body>
    <video id='videoPlayer' preload='auto'></video>
    <div id='debug'></div>
    <script>
        const video = document.getElementById('videoPlayer');
        const debug = document.getElementById('debug');
        let debugVisible = false;
        
        document.addEventListener('keydown', (e) => {
            if (e.key === 'd' || e.key === 'D') {
                debugVisible = !debugVisible;
                debug.style.display = debugVisible ? 'block' : 'none';
            }
        });
        
        debug.addEventListener('click', () => {
            debugVisible = false;
            debug.style.display = 'none';
        });
        
        function log(msg) {
            console.log(msg);
            debug.innerHTML += msg + '<br>';
        }
        
        log('Video player loaded');
        
        video.addEventListener('loadstart', () => {
            log('Video loadstart');
        });
        
        video.addEventListener('loadeddata', () => {
            log('Video loaded data');
        });
        
        video.addEventListener('canplay', () => {
            log('Video can play');
        });
        
        video.addEventListener('ended', () => {
            log('Video ended');
            window.chrome.webview.postMessage(JSON.stringify({ type: 'ended' }));
        });
        
        video.addEventListener('error', (e) => {
            const errorMsg = video.error ? 
                'Code: ' + video.error.code + ', Message: ' + video.error.message : 
                'Unknown error';
            log('Video error: ' + errorMsg);
            window.chrome.webview.postMessage(JSON.stringify({ 
                type: 'error', 
                message: errorMsg
            }));
        });
        
        video.addEventListener('play', () => {
            log('Video playing at speed: ' + video.playbackRate);
            window.chrome.webview.postMessage(JSON.stringify({ type: 'playing' }));
        });
        
        video.addEventListener('pause', () => {
            log('Video paused');
            window.chrome.webview.postMessage(JSON.stringify({ type: 'paused' }));
        });

        window.playVideo = function(path, speed, loop) {
            log('Play command received: ' + path);
            log('Speed: ' + speed + ', Loop: ' + loop);
            
            const encodedPath = encodeURIComponent(path);
            const videoUrl = 'http://video.local/' + encodedPath;
            
            log('Setting video source: ' + videoUrl);
            
            video.src = videoUrl;
            video.loop = loop || false;
            
            video.load();
            
            video.play().then(() => {
                // Set speed AFTER play starts for better reliability
                video.playbackRate = speed || 1.0;
                log('Play successful, playbackRate set to: ' + video.playbackRate);
            }).catch(err => {
                log('Play failed: ' + err.message);
                window.chrome.webview.postMessage(JSON.stringify({ 
                    type: 'error', 
                    message: 'Play failed: ' + err.message 
                }));
            });
        };
        
        window.pauseVideo = function() {
            log('Pause command');
            video.pause();
        };
        
        window.resumeVideo = function() {
            log('Resume command');
            video.play().catch(err => {
                log('Resume failed: ' + err.message);
            });
        };
        
        window.stopVideo = function() {
            log('Stop command');
            video.pause();
            video.currentTime = 0;
            video.src = '';
        };
        
        window.setSpeed = function(speed) {
            log('Set speed: ' + speed);
            video.playbackRate = speed;
        };
        
        window.setLoop = function(loop) {
            log('Set loop: ' + loop);
            video.loop = loop;
        };
        
        log('All functions registered');
    </script>
</body>
</html>";
		}

		private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			try
			{
				var json = e.TryGetWebMessageAsString();
				Debug.WriteLine($"Message from JS: {json}");

				var message = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

				if (message == null) return;

				switch (message["type"])
				{
					case "ended":
						IsPlaying = false;

						if (_isPlayingPlaylist && _currentPlaylistIndex < _playlist.Count - 1)
						{
							_currentPlaylistIndex++;
							PlayCurrentPlaylistItem();
						}
						else if (!string.IsNullOrEmpty(_nextFile))
						{
							PlayAsync(_nextFile, _nextSpeed, _nextLoop);
							_nextFile = null;
						}
						else
						{
							_isPlayingPlaylist = false;
						}

						VideoFinished?.Invoke(this, EventArgs.Empty);
						Debug.WriteLine("Video finished");
						break;

					case "playing":
						IsPlaying = true;
						Debug.WriteLine("Video playing");
						break;

					case "paused":
						IsPlaying = false;
						Debug.WriteLine("Video paused");
						break;

					case "error":
						IsPlaying = false;
						string errorMsg = message.ContainsKey("message") ? message["message"] : "Unknown error";
						Debug.WriteLine($"Video error: {errorMsg}");
						ErrorOccurred?.Invoke(this, errorMsg);
						break;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error handling web message: {ex.Message}");
			}
		}

		public async Task<bool> PlayAsync(string filePath, float speed = 1.0f, bool loop = false)
		{
			try
			{
				if (!File.Exists(filePath))
				{
					string error = $"File not found: {filePath}";
					Debug.WriteLine(error);
					ErrorOccurred?.Invoke(this, error);
					return false;
				}

				if (!_isInitialized)
				{
					Debug.WriteLine("Waiting for initialization...");
					await _initializationTcs.Task;
					if (!_isInitialized)
					{
						Debug.WriteLine("Initialization failed");
						return false;
					}
				}

				PlaybackSpeed = speed;
				Loop = loop;

				Debug.WriteLine($"Playing: {filePath} at {speed}x, loop={loop}");

				string escapedPath = filePath.Replace("\\", "\\\\").Replace("'", "\\'");
				string script = $"playVideo('{escapedPath}', {speed.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {loop.ToString().ToLower()});";

				Debug.WriteLine($"Executing script: {script}");

				string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
				Debug.WriteLine($"Script result: {result}");

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error playing: {ex.Message}");
				Debug.WriteLine($"Stack trace: {ex.StackTrace}");
				ErrorOccurred?.Invoke(this, $"Playback failed: {ex.Message}");
				return false;
			}
		}

		public async Task<bool> PlayAsync(Stream stream, float speed = 1.0f, bool loop = false)
		{
			try
			{
				if (stream == null)
				{
					string error = "Stream is null";
					Debug.WriteLine(error);
					ErrorOccurred?.Invoke(this, error);
					return false;
				}

				if (!_isInitialized)
				{
					Debug.WriteLine("Waiting for initialization...");
					await _initializationTcs.Task;
					if (!_isInitialized)
					{
						Debug.WriteLine("Initialization failed");
						return false;
					}
				}

				PlaybackSpeed = speed;
				Loop = loop;

				string streamId = $"stream_{_streamCounter++}";
				_streamCache[streamId] = stream;

				Debug.WriteLine($"Playing from stream: {streamId} at {speed}x, loop={loop}");

				string script = $"playVideo('{streamId}', {speed.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {loop.ToString().ToLower()});";

				Debug.WriteLine($"Executing script: {script}");

				string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
				Debug.WriteLine($"Script result: {result}");

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error playing stream: {ex.Message}");
				Debug.WriteLine($"Stack trace: {ex.StackTrace}");
				ErrorOccurred?.Invoke(this, $"Stream playback failed: {ex.Message}");
				return false;
			}
		}

		public async Task<bool> PlayPlaylistAsync(List<PlaylistItem> playlist, int startIndex = 0)
		{
			try
			{
				if (playlist == null || playlist.Count == 0)
				{
					ErrorOccurred?.Invoke(this, "Playlist is empty");
					return false;
				}

				if (startIndex < 0 || startIndex >= playlist.Count)
				{
					ErrorOccurred?.Invoke(this, $"Invalid start index: {startIndex}");
					return false;
				}

				if (!_isInitialized)
				{
					Debug.WriteLine("Waiting for initialization...");
					await _initializationTcs.Task;
					if (!_isInitialized)
					{
						Debug.WriteLine("Initialization failed");
						return false;
					}
				}

				_playlist = new List<PlaylistItem>(playlist);
				_currentPlaylistIndex = startIndex;
				_isPlayingPlaylist = true;

				Debug.WriteLine($"Starting playlist with {playlist.Count} items at index {startIndex}");

				return await PlayCurrentPlaylistItem();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error starting playlist: {ex.Message}");
				ErrorOccurred?.Invoke(this, $"Playlist playback failed: {ex.Message}");
				return false;
			}
		}

		private async Task<bool> PlayCurrentPlaylistItem()
		{
			if (_currentPlaylistIndex < 0 || _currentPlaylistIndex >= _playlist.Count)
				return false;

			var item = _playlist[_currentPlaylistIndex];

			Debug.WriteLine($"Playing playlist item {_currentPlaylistIndex + 1}/{_playlist.Count}: {item.FilePath} at {item.Speed}x");

			PlaylistItemChanged?.Invoke(this, _currentPlaylistIndex);

			return await PlayAsync(item.FilePath, item.Speed, item.Loop);
		}

		public void StopPlaylist()
		{
			_isPlayingPlaylist = false;
			_playlist.Clear();
			_currentPlaylistIndex = -1;
			Stop();
			Debug.WriteLine("Playlist stopped");
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
				Debug.WriteLine($"Error queueing: {ex.Message}");
				ErrorOccurred?.Invoke(this, $"Queue failed: {ex.Message}");
				return false;
			}
		}

		public void BringToFront()
		{
			_container.BringToFront();
		}

		public async void Pause()
		{
			try
			{
				if (!_isInitialized) return;
				await _webView.CoreWebView2.ExecuteScriptAsync("pauseVideo();");
				Debug.WriteLine("Paused");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error pausing: {ex.Message}");
			}
		}

		public async void Resume()
		{
			try
			{
				if (!_isInitialized) return;
				await _webView.CoreWebView2.ExecuteScriptAsync("resumeVideo();");
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

		public async void Stop()
		{
			try
			{
				if (!_isInitialized) return;
				await _webView.CoreWebView2.ExecuteScriptAsync("stopVideo();");
				IsPlaying = false;
				Debug.WriteLine("Stopped");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error stopping: {ex.Message}");
			}
		}

		public async Task<bool> ExtractCurrentFrameAsync(string outputPath)
		{
			try
			{
				if (!_isInitialized) return false;

				var bmp = new System.Drawing.Bitmap(_webView.Width, _webView.Height);
				_webView.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, _webView.Width, _webView.Height));
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

		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;

			try
			{
				foreach (var stream in _streamCache.Values)
				{
					try
					{
						stream?.Dispose();
					}
					catch { }
				}
				_streamCache.Clear();

				if (_webView != null)
				{
					_container.Controls.Remove(_webView);
					_webView.Dispose();
					_webView = null;
				}

				Debug.WriteLine("Disposed");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error disposing: {ex.Message}");
			}
		}
	}
}