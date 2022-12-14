using System;
using Xamarin.Forms;

namespace VideoStream.Controls.VideoCore
{
	public sealed class MediaSourceConverter : TypeConverter
	{
		public override object? ConvertFromInvariantString(string value)
		{
			if (value == null)
				throw new InvalidOperationException($"Cannot convert \"{value}\" into {typeof(MediaSource)}");

			return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme != "file"
				? MediaSource.FromUri(uri)
				: MediaSource.FromFile(value);
		}
	}
}
