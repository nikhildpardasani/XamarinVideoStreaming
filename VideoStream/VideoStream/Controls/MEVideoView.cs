using System;
using VideoStream.Controls.VideoCore;
using VideoStream.Enums;
using Xamarin.Forms;

namespace VideoStream.Controls
{
    public class MEVideoView : View, IMediaElementController
    {
        public MEVideoView()
        {
            MessagingCenter.Subscribe<object>(this, "Mute", (sender) =>
            {
                if (Device.RuntimePlatform == Device.iOS)
                {
                    Stop();
                }
                else
                {
                    Pause();
                }
            });
            MessagingCenter.Subscribe<object>(this, "Resume", (sender) =>
            {
                IsMuted = true;
                ShowPlaceHolder = true;
                if (Device.RuntimePlatform == Device.iOS)
                {
                    Stop();
                }
                else
                {
                    Pause();
                }
                Resume();
            });
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
        }

        public static readonly BindableProperty AspectProperty =
          BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(MEVideoView), Aspect.AspectFit);

        public static readonly BindableProperty AutoPlayProperty =
          BindableProperty.Create(nameof(AutoPlay), typeof(bool), typeof(MEVideoView), true);

        public static readonly BindableProperty ShowPlaceHolderProperty =
          BindableProperty.Create(nameof(ShowPlaceHolder), typeof(bool), typeof(MEVideoView), true);

        public static readonly BindableProperty BufferingProgressProperty =
          BindableProperty.Create(nameof(BufferingProgress), typeof(double), typeof(MEVideoView), 0.0, propertyChanged: BufferChanged);

        public static readonly BindableProperty CurrentStateProperty =
          BindableProperty.Create(nameof(CurrentState), typeof(MediaElementState), typeof(MEVideoView), MediaElementState.Closed, propertyChanged: CurrentStateChanged);

        public static readonly BindableProperty DurationProperty =
          BindableProperty.Create(nameof(Duration), typeof(TimeSpan?), typeof(MEVideoView), null);

        public static readonly BindableProperty IsLoopingProperty =
          BindableProperty.Create(nameof(IsLooping), typeof(bool), typeof(MEVideoView), false);

        public static readonly BindableProperty KeepScreenOnProperty =
          BindableProperty.Create(nameof(KeepScreenOn), typeof(bool), typeof(MEVideoView), false);

        public static readonly BindableProperty PositionProperty =
          BindableProperty.Create(nameof(Position), typeof(TimeSpan), typeof(MEVideoView), TimeSpan.Zero, propertyChanged: PositionChanged);

        public static readonly BindableProperty ShowsPlaybackControlsProperty =
          BindableProperty.Create(nameof(ShowsPlaybackControls), typeof(bool), typeof(MEVideoView), true);

        public static readonly BindableProperty SourceProperty =
          BindableProperty.Create(nameof(Source), typeof(MediaSource), typeof(MEVideoView),
              propertyChanging: OnSourcePropertyChanging, propertyChanged: OnSourcePropertyChanged);

        public static readonly BindableProperty VideoHeightProperty =
          BindableProperty.Create(nameof(VideoHeight), typeof(int), typeof(MEVideoView));

        public static readonly BindableProperty VideoWidthProperty =
          BindableProperty.Create(nameof(VideoWidth), typeof(int), typeof(MEVideoView));

        public static readonly BindableProperty VolumeProperty =
          BindableProperty.Create(nameof(Volume), typeof(double), typeof(MEVideoView), 1.0, BindingMode.TwoWay, new BindableProperty.ValidateValueDelegate(ValidateVolume));

        public static readonly BindableProperty IsMutedProperty =
          BindableProperty.Create(nameof(IsMuted), typeof(bool), typeof(MEVideoView), false);

        public Aspect Aspect
        {
            get => (Aspect)GetValue(AspectProperty);
            set => SetValue(AspectProperty, value);
        }

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public bool ShowPlaceHolder
        {
            get => (bool)GetValue(ShowPlaceHolderProperty);
            set => SetValue(ShowPlaceHolderProperty, value);
        }

        public double BufferingProgress => (double)GetValue(BufferingProgressProperty);

        public bool CanSeek => Source != null && Duration.HasValue;

        public MediaElementState CurrentState => (MediaElementState)GetValue(CurrentStateProperty);

        public TimeSpan? Duration => (TimeSpan?)GetValue(DurationProperty);

        public bool IsLooping
        {
            get => (bool)GetValue(IsLoopingProperty);
            set => SetValue(IsLoopingProperty, value);
        }

        public bool KeepScreenOn
        {
            get => (bool)GetValue(KeepScreenOnProperty);
            set => SetValue(KeepScreenOnProperty, value);
        }

        public bool ShowsPlaybackControls
        {
            get => (bool)GetValue(ShowsPlaybackControlsProperty);
            set => SetValue(ShowsPlaybackControlsProperty, value);
        }

        public bool IsMuted
        {
            get => (bool)GetValue(IsMutedProperty);
            set => SetValue(IsMutedProperty, value);
        }

        public TimeSpan Position
        {
            get
            {
                PositionRequested?.Invoke(this, EventArgs.Empty);
                return (TimeSpan)GetValue(PositionProperty);
            }

            set
            {
                var currentValue = (TimeSpan)GetValue(PositionProperty);
                if (Math.Abs(value.Subtract(currentValue).TotalMilliseconds) > 300 && !isSeeking)
                    RequestSeek(value);
            }
        }

        [TypeConverter(typeof(MediaSourceConverter))]
        public MediaSource? Source
        {
            get => (MediaSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public int VideoHeight => (int)GetValue(VideoHeightProperty);

        public int VideoWidth => (int)GetValue(VideoWidthProperty);

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        public event EventHandler<SeekRequested>? SeekRequested;

        public event EventHandler<StateRequested>? StateRequested;

        public event EventHandler? PositionRequested;

        public event EventHandler? MediaEnded;

        public event EventHandler? MediaFailed;

        public event EventHandler? MediaOpened;

        public event EventHandler? SeekCompleted;

        public void Stop() => StateRequested?.Invoke(this, new StateRequested(MediaElementState.Stopped));

        public void Play() => StateRequested?.Invoke(this, new StateRequested(MediaElementState.Playing));

        public void Resume() => StateRequested?.Invoke(this, new StateRequested(MediaElementState.Playing));

        public void Pause() => StateRequested?.Invoke(this, new StateRequested(MediaElementState.Paused));

        double IMediaElementController.BufferingProgress
        {
            get => (double)GetValue(BufferingProgressProperty);
            set => SetValue(BufferingProgressProperty, value);
        }

        MediaElementState IMediaElementController.CurrentState
        {
            get => (MediaElementState)GetValue(CurrentStateProperty);
            set => SetValue(CurrentStateProperty, value);
        }

        TimeSpan? IMediaElementController.Duration
        {
            get => (TimeSpan?)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        TimeSpan IMediaElementController.Position
        {
            get => (TimeSpan)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        int IMediaElementController.VideoHeight
        {
            get => (int)GetValue(VideoHeightProperty);
            set => SetValue(VideoHeightProperty, value);
        }

        int IMediaElementController.VideoWidth
        {
            get => (int)GetValue(VideoWidthProperty);
            set => SetValue(VideoWidthProperty, value);
        }

        double IMediaElementController.Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        void IMediaElementController.OnMediaEnded()
        {
            SetValue(CurrentStateProperty, MediaElementState.Stopped);
            MediaEnded?.Invoke(this, EventArgs.Empty);
        }

        void IMediaElementController.OnMediaFailed() => MediaFailed?.Invoke(this, EventArgs.Empty);

        //void IMediaElementController.OnMediaOpened() => MediaOpened?.Invoke(this, EventArgs.Empty);

        void IMediaElementController.OnMediaOpened()
        {
            ShowPlaceHolder = false;
            MediaOpened?.Invoke(this, EventArgs.Empty);
        }

        void IMediaElementController.OnSeekCompleted()
        {
            isSeeking = false;
            SeekCompleted?.Invoke(this, EventArgs.Empty);
        }

        bool isSeeking = false;

        void RequestSeek(TimeSpan newPosition)
        {
            isSeeking = true;
            SeekRequested?.Invoke(this, new SeekRequested(newPosition));
        }

        protected override void OnBindingContextChanged()
        {
            if (Source != null)
                SetInheritedBindingContext(Source, BindingContext);

            base.OnBindingContextChanged();
        }

        void OnSourceChanged(object? sender, EventArgs eventArgs)
        {
            OnPropertyChanged(SourceProperty.PropertyName);
            InvalidateMeasure();
        }

        static void OnSourcePropertyChanged(BindableObject bindable, object oldvalue, object newvalue) =>
            ((MEVideoView)bindable).OnSourcePropertyChanged((MediaSource)newvalue);

        void OnSourcePropertyChanged(MediaSource newvalue)
        {
            if (newvalue != null)
            {
                newvalue.SourceChanged += OnSourceChanged;
                SetInheritedBindingContext(newvalue, BindingContext);
            }

            InvalidateMeasure();
        }

        static void OnSourcePropertyChanging(BindableObject bindable, object oldvalue, object newvalue) =>
            ((MEVideoView)bindable).OnSourcePropertyChanging((MediaSource)oldvalue);

        void OnSourcePropertyChanging(MediaSource oldvalue)
        {
            if (oldvalue == null)
                return;

            oldvalue.SourceChanged -= OnSourceChanged;
        }

        static void CurrentStateChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var element = (MEVideoView)bindable;

            switch ((MediaElementState)newValue)
            {
                case MediaElementState.Playing:
                    // start a timer to poll the native control position while playing
                    Device.StartTimer(TimeSpan.FromMilliseconds(200), () =>
                    {
                        if (!element.isSeeking)
                        {
                            Device.BeginInvokeOnMainThread(() =>
                            {
                                element.PositionRequested?.Invoke(element, EventArgs.Empty);
                            });
                        }

                        return element.CurrentState == MediaElementState.Playing;
                    });
                    break;
            }
        }

        static void PositionChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var element = (MEVideoView)bindable;
            var oldval = (TimeSpan)oldValue;
            var newval = (TimeSpan)newValue;
            element.ShowPlaceHolder = !(newval.TotalSeconds > TimeSpan.Zero.TotalSeconds);
            if (Math.Abs(newval.Subtract(oldval).TotalMilliseconds) > 300 && !element.isSeeking)
                element.RequestSeek(newval);
        }

        static void BufferChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var element = (MEVideoView)bindable;
            var oldval = (double)oldValue;
            var newval = (double)newValue;
        }

        static bool ValidateVolume(BindableObject o, object newValue)
        {
            var d = (double)newValue;

            return d >= 0.0 && d <= 1.0;
        }

        public event EventHandler FullScreenTapped;

        public void SendFullScreenTapped(bool isFullScreen)
        {
            EventHandler eventHandler = this.FullScreenTapped;
            eventHandler?.Invoke(isFullScreen, EventArgs.Empty);
        }

        public event EventHandler VideoExited;

        public void SendVideoExited()
        {
            EventHandler eventHandler = this.VideoExited;
            eventHandler?.Invoke(this, EventArgs.Empty);
        }
    }

    public class SeekRequested : EventArgs
    {
        public TimeSpan Position { get; }

        public SeekRequested(TimeSpan position) => Position = position;
    }

    public class StateRequested : EventArgs
    {
        public MediaElementState State { get; }

        public StateRequested(MediaElementState state) => State = state;
    }

    public interface IMediaElementController
    {
        double BufferingProgress { get; set; }

        MediaElementState CurrentState { get; set; }

        TimeSpan? Duration { get; set; }

        TimeSpan Position { get; set; }

        int VideoHeight { get; set; }

        int VideoWidth { get; set; }

        double Volume { get; set; }

        void OnMediaEnded();

        void OnMediaFailed();

        void OnMediaOpened();

        void OnSeekCompleted();
    }
}

