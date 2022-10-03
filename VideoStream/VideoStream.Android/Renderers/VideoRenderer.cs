using System;
using System.ComponentModel;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.Content;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Source.Hls;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.UI;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;
using VideoStream.Controls;
using VideoStream.Droid.Renderers;
using VideoStream.Enums;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using static Android.Views.View;
using ARelativeLayout = Android.Widget.RelativeLayout;
using XCT = VideoStream.Controls.VideoCore;

[assembly: ExportRenderer(typeof(MEVideoView), typeof(VideoRenderer))]
namespace VideoStream.Droid.Renderers
{
	public class VideoRenderer : ViewRenderer<MEVideoView, PlayerView> , IOnClickListener
	{
        //VideoView videoView;
        //Android.Widget.ProgressBar progressBar;
        //MediaController mediaController;    // Used to display transport controls
        bool isPrepared;
        Context _context;
        AppCompatImageView fullscreenButton;
		static double deviceWidth;
        static double deviceHeight;
        public bool fullScreen;
        double playerHeight;

        private PlayerView _playerView;
        private SimpleExoPlayer _player;
        public IDataSourceFactory DataSourceFactory { get; set; }
        protected ConcatenatingMediaSource MediaSource { get; set; }
        public MEVideoView meVideoView;
        IMediaElementController? Controller => meVideoView;

        public event EventHandler<bool> FullScreenStatusChanged;

        public StatusBarVisibility currentState;

        public VideoRenderer(Context context) : base(context)
		{
            _context = context;
            deviceWidth = (int)(context.Resources.DisplayMetrics.WidthPixels / context.Resources.DisplayMetrics.Density);
            deviceHeight = (int)(context.Resources.DisplayMetrics.HeightPixels / context.Resources.DisplayMetrics.Density);
        }

        protected override void OnElementChanged(Xamarin.Forms.Platform.Android.ElementChangedEventArgs<MEVideoView> e)
        {
            base.OnElementChanged(e);
            if (e.NewElement != null)
            {
                if(Control == null)
				{
                    meVideoView = e.NewElement;
                    InitializePlayer();
                    meVideoView.StateRequested += StateRequested;
                }
            }
            else if(e.OldElement != null)
			{
                if(meVideoView != null)
				{
                    meVideoView.StateRequested -= StateRequested;
                }
			}
        }

        private void InitializePlayer()
        {
            var UserAgent = Util.GetUserAgent(Context, Context.PackageName);
            var HttpDataSourceFactory = new DefaultHttpDataSourceFactory(UserAgent);
            DataSourceFactory = new DefaultDataSourceFactory(Context, HttpDataSourceFactory);
            MediaSource = new ConcatenatingMediaSource();
            _player = new SimpleExoPlayer.Builder(Context).Build();
            _player.PlayWhenReady = true;
            _playerView = new PlayerView(Context) { Player = _player };
			if (meVideoView.IsLooping)
			{
                _player.RepeatMode = 2;
            }
			if (meVideoView.IsMuted)
			{
                _player.Volume = 0;
            }
			if (!meVideoView.ShowsPlaybackControls)
			{
                _playerView.UseController = false;
            }

            MediaEvents mediaEvents = new MediaEvents(meVideoView);
            _player.AddListener(mediaEvents);

            fullscreenButton = _playerView.FindViewById<AppCompatImageView>(Resource.Id.exo_fullscreen_icon);

            fullscreenButton.SetOnClickListener(this);
			meVideoView.VideoExited += MeVideoView_VideoExited;
            SetNativeControl(_playerView);
            if (meVideoView.Source is XCT.UriMediaSource uriSource)
            {
                string uri = uriSource.Uri.AbsoluteUri;
                UpdateSource(meVideoView, uriSource.Uri.AbsoluteUri);
            }
        }

		private void MeVideoView_VideoExited(object sender, EventArgs e)
		{
			if (fullScreen)
			{
                var window = _context.GetActivity().Window;
                window.DecorView.SystemUiVisibility = currentState;
                _context.GetActivity().RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
			}
		}

		public void OnClick(Android.Views.View v)
		{
			if (fullScreen)
			{
                fullscreenButton.SetImageDrawable(ContextCompat.GetDrawable(_context, Resource.Drawable.ic_fullscreen_open));
				var window = _context.GetActivity().Window;
                window.DecorView.SystemUiVisibility = currentState;
                _context.GetActivity().RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
                fullScreen = false;
            }
			else
			{
                fullscreenButton.SetImageDrawable(ContextCompat.GetDrawable(_context, Resource.Drawable.ic_fullscreen_close));
                var window = _context.GetActivity().Window;
                var uiOpts = SystemUiFlags.Fullscreen 
                | SystemUiFlags.HideNavigation 
                
                | SystemUiFlags.ImmersiveSticky;
                currentState = window.DecorView.SystemUiVisibility;
                window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOpts;
                _context.GetActivity().RequestedOrientation = Android.Content.PM.ScreenOrientation.Sensor;
                
                fullScreen = true;
            }
            meVideoView.SendFullScreenTapped(fullScreen);
        }

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base.OnElementPropertyChanged(sender, e);
            if (this.Element == null || this.Control == null)
                return;

            if (e.PropertyName == MEVideoView.SourceProperty.PropertyName)
            {
                if(sender is MEVideoView videoView && videoView.Source is XCT.UriMediaSource uriSource)
				{
                    UpdateSource((MEVideoView)sender, uriSource.Uri.AbsoluteUri);
                }
            }
            else if (e.PropertyName == MEVideoView.IsMutedProperty.PropertyName)
            {
                if (sender is MEVideoView videoView && videoView.IsMuted)
                {
                    _player.Volume = 0;
                }
                else
                {
                    _player.Volume = 1;
                }
            }
        }

        private void UpdateSource(MEVideoView MediaElement , string url)
        {
            if (_player == null)
                return;

            if (MediaElement.Source != null)
            {
                IMediaSource mediaSource = new HlsMediaSource.Factory(DataSourceFactory)
                        .SetAllowChunklessPreparation(true)
                        .CreateMediaSource(Android.Net.Uri.Parse(url));
                MediaSource.Clear();
                MediaSource.AddMediaSource(mediaSource);
                _player.Prepare(MediaSource);

                _player.PlayWhenReady = true;
            }
            else
            {
                _player.Stop();
                _player.SeekTo(0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Control != null && _player != null)
            {
                _player.Release();
                if(meVideoView != null)
				{
                    meVideoView.StateRequested -= StateRequested;
                }
            }

            base.Dispose(disposing);
        }

        void StateRequested(object? sender, StateRequested e)
        {
            if (_player == null)
                return;

            _ = Controller ?? throw new NullReferenceException();
            switch (e.State)
            {
                case MediaElementState.Playing:
                    _player.PlayWhenReady = true;
                    Controller.CurrentState = _player.IsPlaying ? MediaElementState.Playing : MediaElementState.Stopped;
                    break;

                case MediaElementState.Paused:
                    _player.PlayWhenReady = false;
                    Controller.CurrentState = MediaElementState.Paused;
                    break;

                case MediaElementState.Stopped:
                    _player.Stop();
                    _player.SeekTo(0);
                    Controller.CurrentState = _player.IsPlaying ? MediaElementState.Playing : MediaElementState.Stopped;
                    break;
            }

            //UpdateLayoutParameters();
            Controller.Position = new TimeSpan(_player.CurrentPosition);
        }
    }

    public class MediaEvents : Java.Lang.Object, IPlayerEventListener
    {
        public MEVideoView managerVideoView;
        public MediaEvents(MEVideoView managerVideoView)
        {
            this.managerVideoView = managerVideoView;
        }

        protected MediaEvents(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
        {
        }

        public void OnIsPlayingChanged(bool isPlaying)
        {
            managerVideoView.ShowPlaceHolder = !isPlaying;
        }

        public void OnTracksChanged(TrackGroupArray p0, TrackSelectionArray p1)
        {
        }

        public void OnPositionDiscontinuity(int reason)
        {
        }

        public void OnPlayerStateChanged(bool playWhenReady, int playbackState)
        {
        }

        public void OnPlayerError(ExoPlaybackException error)
        {
        }

        public void OnLoadingChanged(bool isLoading)
        {
        }

        public void OnPlaybackParametersChanged(PlaybackParameters playbackParameters)
        {
        }

        public void OnRepeatModeChanged(int repeatMode)
        {
        }

        public void OnSeekProcessed()
        {
        }

        public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled)
        {
        }

        public void OnTimelineChanged(Timeline timeline, int reason)
        {
        }

        public void OnPlaybackSuppressionReasonChanged(int playbackSuppressionReason)
        {
        }
    }
}
