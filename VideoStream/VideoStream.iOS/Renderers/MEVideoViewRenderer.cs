using System;
using System.IO;
using AVFoundation;
using AVKit;
using CoreMedia;
using Foundation;
using VideoStream.Enums;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Platform.iOS;
using ToolKitMediaElement = VideoStream.Controls.MEVideoView;
using ToolKitMediaElementRenderer = VideoStream.iOS.Renderers.MEVideoViewRenderer;
using XCT = VideoStream.Controls.VideoCore;

[assembly: ExportRenderer(typeof(ToolKitMediaElement), typeof(ToolKitMediaElementRenderer))]
namespace VideoStream.iOS.Renderers
{
	public class MEVideoViewRenderer: ViewRenderer<ToolKitMediaElement, UIView>
	{
		VideoStream.Controls.IMediaElementController Controller => Element;

		protected readonly AVPlayerViewController avPlayerViewController = new AVPlayerViewController();
		protected NSObject? playedToEndObserver;
		protected IDisposable? statusObserver;
		protected IDisposable? rateObserver;
		protected IDisposable? volumeObserver;
		bool idleTimerDisabled = false;
		AVPlayerItem? playerItem;

		public MEVideoViewRenderer() => AddPlayedToEndObserver();

		public ToolKitMediaElement meVideoView;

		protected virtual void SetKeepScreenOn(bool value)
		{
			if (value)
			{
				if (!UIApplication.SharedApplication.IdleTimerDisabled)
				{
					idleTimerDisabled = true;
					UIApplication.SharedApplication.IdleTimerDisabled = true;
				}
			}
			else if (idleTimerDisabled)
			{
				idleTimerDisabled = false;
				UIApplication.SharedApplication.IdleTimerDisabled = false;
			}
		}

		protected virtual void UpdateSource()
		{
			if (Element.Source != null)
			{
				AVAsset? asset = null;

				if (Element.Source is XCT.UriMediaSource uriSource)
				{
					if (uriSource.Uri?.Scheme is "ms-appx")
					{
						if (uriSource.Uri.LocalPath.Length <= 1)
							return;

						// used for a file embedded in the application package
						asset = AVAsset.FromUrl(NSUrl.FromFilename(uriSource.Uri.LocalPath.Substring(1)));
					}
					else if (uriSource.Uri?.Scheme == "ms-appdata")
					{
						var filePath = ResolveMsAppDataUri(uriSource.Uri);

						if (string.IsNullOrEmpty(filePath))
							throw new ArgumentException("Invalid Uri", "Source");

						asset = AVAsset.FromUrl(NSUrl.FromFilename(filePath));
					}
					else if (uriSource.Uri != null)
					{
						asset = AVUrlAsset.Create(NSUrl.FromString(uriSource.Uri.AbsoluteUri));
					}
					else
					{
						throw new InvalidOperationException($"{nameof(uriSource.Uri)} is not initialized");
					}
				}
				else
				{
					if (Element.Source is XCT.FileMediaSource fileSource)
						asset = AVAsset.FromUrl(NSUrl.FromFilename(fileSource.File));
				}

				_ = asset ?? throw new NullReferenceException();

				playerItem = new AVPlayerItem(asset);
				AddStatusObserver();

				if (avPlayerViewController.Player != null)
					avPlayerViewController.Player.ReplaceCurrentItemWithPlayerItem(playerItem);
				else
				{
					avPlayerViewController.Player = new AVPlayer(playerItem);
					//AddRateObserver();
					//AddVolumeObserver();
				}

				//UpdateVolume();
				MutedVolume();

				if (Element.AutoPlay)
					Play();
			}
			else
			{
				avPlayerViewController.Player?.Pause();
				avPlayerViewController.Player?.ReplaceCurrentItemWithPlayerItem(null);
				DestroyStatusObserver();
				Controller.CurrentState = MediaElementState.Stopped;
			}
		}

		protected string ResolveMsAppDataUri(Uri uri)
		{
			if (uri.Scheme is "ms-appdata")
			{
				string filePath;

				if (uri.LocalPath.StartsWith("/local"))
				{
					var libraryPath = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.LibraryDirectory, NSSearchPathDomain.User)[0].Path;
					filePath = Path.Combine(libraryPath, uri.LocalPath.Substring(7));
				}
				else if (uri.LocalPath.StartsWith("/temp"))
					filePath = Path.Combine(Path.GetTempPath(), uri.LocalPath.Substring(6));
				else
					throw new ArgumentException("Invalid Uri", "Source");

				return filePath;
			}
			else
				throw new ArgumentException("uri");
		}

		protected virtual void ObserveRate(NSObservedChange e)
		{
			if (Controller is object)
			{
				switch (avPlayerViewController.Player?.Rate)
				{
					case 0.0f:
						Controller.CurrentState = MediaElementState.Paused;
						break;

					case 1.0f:
						Controller.CurrentState = MediaElementState.Playing;
						break;
				}

				Controller.Position = Position;
			}
		}

		void ObserveVolume(NSObservedChange e)
		{
			if (Controller == null || avPlayerViewController.Player == null)
				return;

			Controller.Volume = avPlayerViewController.Player.Volume;
		}

		protected void ObserveStatus(NSObservedChange e)
		{
			if(avPlayerViewController.Player?.CurrentItem == null)
			{
				return;
			}
			Controller.Volume = avPlayerViewController.Player.Volume;
			meVideoView.ShowPlaceHolder = true;
			switch (avPlayerViewController.Player.Status)
			{
				case AVPlayerStatus.Failed:
					Controller.OnMediaFailed();
					break;

				case AVPlayerStatus.ReadyToPlay:
					var duration = avPlayerViewController.Player.CurrentItem.Duration;
					meVideoView.ShowPlaceHolder = false;
					if (duration.IsIndefinite)
						Controller.Duration = TimeSpan.Zero;
					else
						Controller.Duration = TimeSpan.FromSeconds(duration.Seconds);

					Controller.VideoHeight = (int)avPlayerViewController.Player.CurrentItem.Asset.NaturalSize.Height;
					Controller.VideoWidth = (int)avPlayerViewController.Player.CurrentItem.Asset.NaturalSize.Width;
					Controller.OnMediaOpened();
					Controller.Position = Position;
					break;
			}
		}

		TimeSpan Position
		{
			get
			{
				if (avPlayerViewController?.Player?.CurrentTime.IsInvalid ?? true)
					return TimeSpan.Zero;

				return TimeSpan.FromSeconds(avPlayerViewController.Player.CurrentTime.Seconds);
			}
		}

		void PlayedToEnd(NSNotification notification)
		{
			if (Element == null || notification.Object != avPlayerViewController.Player?.CurrentItem)
				return;

			if (Element.IsLooping)
			{
				avPlayerViewController.Player?.Seek(CMTime.Zero);
				//Controller.Position = Position;
				avPlayerViewController.Player?.Play();
				meVideoView.ShowPlaceHolder = false;
			}
			else
			{
				SetKeepScreenOn(false);
				Controller.Position = Position;

				try
				{
					Device.BeginInvokeOnMainThread(Controller.OnMediaEnded);
				}
				catch (Exception e)
				{
					Log.Warning("MediaElement", $"Failed to play media to end: {e}");
				}
			}
		}

		protected override void OnElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(ToolKitMediaElement.Aspect):
					avPlayerViewController.VideoGravity = AspectToGravity(Element.Aspect);
					break;

				case nameof(ToolKitMediaElement.KeepScreenOn):
					if (!Element.KeepScreenOn)
						SetKeepScreenOn(false);
					else if (Element.CurrentState == MediaElementState.Playing)
					{
						// only toggle this on if property is set while video is already running
						SetKeepScreenOn(true);
					}
					break;

				case nameof(ToolKitMediaElement.ShowsPlaybackControls):
					avPlayerViewController.ShowsPlaybackControls = Element.ShowsPlaybackControls;
					break;

				case nameof(ToolKitMediaElement.Source):
					UpdateSource();
					break;

				case nameof(ToolKitMediaElement.IsMuted):
					MutedVolume();
					break;
			}
		}

		void MediaElementSeekRequested(object? sender, Controls.SeekRequested e)
		{
			if (avPlayerViewController.Player?.CurrentItem == null || avPlayerViewController.Player.Status != AVPlayerStatus.ReadyToPlay)
				return;

			var ranges = avPlayerViewController.Player.CurrentItem.SeekableTimeRanges;
			var seekTo = new CMTime(Convert.ToInt64(e.Position.TotalMilliseconds), 1000);
			foreach (var v in ranges)
			{
				if (seekTo >= v.CMTimeRangeValue.Start && seekTo < (v.CMTimeRangeValue.Start + v.CMTimeRangeValue.Duration))
				{
					avPlayerViewController.Player.Seek(seekTo, SeekComplete);
					break;
				}
			}
		}

		protected virtual void Play()
		{
			var audioSession = AVAudioSession.SharedInstance();
			var err = audioSession.SetCategory(AVAudioSession.CategoryPlayback);

			if (err != null)
				Log.Warning("MediaElement", "Failed to set AVAudioSession Category {0}", err.Code);

			audioSession.SetMode(AVAudioSession.ModeMoviePlayback, out err);
			if (err != null)
				Log.Warning("MediaElement", "Failed to set AVAudioSession Mode {0}", err.Code);

			err = audioSession.SetActive(true);
			if (err != null)
				Log.Warning("MediaElement", "Failed to set AVAudioSession Active {0}", err.Code);

			if (avPlayerViewController.Player != null)
			{
				avPlayerViewController.Player.Play();
				Controller.CurrentState = MediaElementState.Playing;
			}

			if (Element.KeepScreenOn)
				SetKeepScreenOn(true);
		}

		void UpdateVolume()
		{
			if (avPlayerViewController.Player != null)
				avPlayerViewController.Player.Volume = (float)Element.Volume;
		}

		void MutedVolume()
		{
			if (avPlayerViewController.Player != null)
				avPlayerViewController.Player.Muted = (bool)Element.IsMuted;
		}

		void MediaElementStateRequested(object? sender, Controls.StateRequested e)
		{
			switch (e.State)
			{
				case MediaElementState.Playing:
					Play();
					break;

				case MediaElementState.Paused:
					if (Element.KeepScreenOn)
						SetKeepScreenOn(false);

					if (avPlayerViewController.Player != null)
					{
						avPlayerViewController.Player.Pause();
						Controller.CurrentState = MediaElementState.Paused;
					}
					break;

				case MediaElementState.Stopped:
					if (Element.KeepScreenOn)
						SetKeepScreenOn(false);

					// iOS has no stop...
					avPlayerViewController.Player?.Pause();
					avPlayerViewController.Player?.Seek(CMTime.Zero);
					Controller.CurrentState = MediaElementState.Stopped;

					var err = AVAudioSession.SharedInstance().SetActive(false);

					if (err != null)
						Log.Warning("MediaElement", "Failed to set AVAudioSession Inactive {0}", err.Code);

					break;
			}

			Controller.Position = Position;
		}

		static AVLayerVideoGravity AspectToGravity(Aspect aspect) =>
			AVLayerVideoGravity.ResizeAspect;

		void SeekComplete(bool finished)
		{
			if (finished)
				Controller?.OnSeekCompleted();
		}

		void MediaElementPositionRequested(object? sender, EventArgs e) => Controller.Position = Position;

		protected override void OnElementChanged(ElementChangedEventArgs<Controls.MEVideoView> e)
		{
			base.OnElementChanged(e);

			if (e.OldElement != null)
			{
				e.OldElement.PropertyChanged -= OnElementPropertyChanged;
				//e.OldElement.SeekRequested -= MediaElementSeekRequested;
				e.OldElement.StateRequested -= MediaElementStateRequested;
				e.OldElement.PositionRequested -= MediaElementPositionRequested;
				SetKeepScreenOn(false);

				// stop video if playing
				if (avPlayerViewController?.Player?.CurrentItem != null)
				{
					if (avPlayerViewController?.Player?.Rate > 0)
						avPlayerViewController?.Player?.Pause();

					avPlayerViewController?.Player?.ReplaceCurrentItemWithPlayerItem(null);
					AVAudioSession.SharedInstance().SetActive(false);
				}

				//DestroyPlayedToEndObserver();
				//DestroyRateObserver();
				//DestroyVolumeObserver();
				//DestroyStatusObserver();
			}

			if (e.NewElement != null)
			{
				SetNativeControl(avPlayerViewController?.View ?? throw new NullReferenceException());

				Element.PropertyChanged += OnElementPropertyChanged;
				//Element.SeekRequested += MediaElementSeekRequested;
				Element.StateRequested += MediaElementStateRequested;
				Element.PositionRequested += MediaElementPositionRequested;

				avPlayerViewController.ShowsPlaybackControls = Element.ShowsPlaybackControls;
				avPlayerViewController.VideoGravity = AspectToGravity(Element.Aspect);

				if (Element.KeepScreenOn)
					SetKeepScreenOn(true);
				if(Control!= null)
				{
					meVideoView = e.NewElement;
				}
				AddPlayedToEndObserver();
				MutedVolume();
				UpdateBackgroundColor();
				UpdateSource();
			}
		}

		protected virtual void UpdateBackgroundColor() => BackgroundColor = Element.BackgroundColor.ToUIColor();

		protected void DisposeObservers(ref IDisposable? disposable)
		{
			disposable?.Dispose();
			disposable = null;
		}

		protected void DisposeObservers(ref NSObject? disposable)
		{
			disposable?.Dispose();
			disposable = null;
		}

		void AddVolumeObserver()
		{
			DestroyVolumeObserver();
			volumeObserver = avPlayerViewController.Player?.AddObserver("volume", NSKeyValueObservingOptions.New,
					ObserveVolume);
		}

		void AddRateObserver()
		{
			DestroyRateObserver();
			rateObserver = avPlayerViewController.Player?.AddObserver("rate", NSKeyValueObservingOptions.New,
					ObserveRate);
		}

		void AddStatusObserver()
		{
			DestroyStatusObserver();
			statusObserver = playerItem?.AddObserver("status", NSKeyValueObservingOptions.New, ObserveStatus);
		}

		void AddPlayedToEndObserver()
		{
			DestroyPlayedToEndObserver();
			playedToEndObserver =
				NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification, PlayedToEnd);
		}

		void DestroyVolumeObserver() => DisposeObservers(ref volumeObserver);

		void DestroyRateObserver() => DisposeObservers(ref rateObserver);

		void DestroyStatusObserver() => DisposeObservers(ref statusObserver);

		void DestroyPlayedToEndObserver()
		{
			if (playedToEndObserver == null)
			{
				return;
			}

			NSNotificationCenter.DefaultCenter.RemoveObserver(playedToEndObserver);
			DisposeObservers(ref playedToEndObserver);
		}
	}
}
