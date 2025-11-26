using iviewer.Helpers;

namespace iviewer
{
	public static class ThumbnailCache
	{
		private static System.Drawing.Image _placeholderImage;

		public static System.Drawing.Image PlaceholderImage
		{
			get
			{
				if (_placeholderImage == null)
				{
					// Create a simple gray placeholder
					var bmp = new Bitmap(160, 160);
					using (var g = Graphics.FromImage(bmp))
					{
						g.Clear(Color.LightGray);
						using (var font = new Font("Arial", 10))
						using (var brush = new SolidBrush(Color.Gray))
						{
							var text = "Loading...";
							var size = g.MeasureString(text, font);
							g.DrawString(text, font, brush,
								(160 - size.Width) / 2,
								(160 - size.Height) / 2);
						}
					}
					_placeholderImage = bmp;
				}
				return _placeholderImage;
			}
		}

		private static Dictionary<string, System.Drawing.Image> _cache = new Dictionary<string, System.Drawing.Image>();

		public static System.Drawing.Image GetThumbnail(string imagePath)
		{
			if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
				return null;

			if (_cache.ContainsKey(imagePath))
				return _cache[imagePath];

			var thumbnail = ImageHelper.CreateThumbnail(imagePath, null, 160);
			_cache[imagePath] = thumbnail;
			return thumbnail;
		}

		public static void Clear()
		{
			foreach (var img in _cache.Values)
			{
				img?.Dispose();
			}
			_cache.Clear();
		}
	}
}