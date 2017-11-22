using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Interfaces;

namespace Plugin.MediaManager
{
    public class VideoPlayerImplementation : BasePlayerImplementation, IVideoPlayer
    {
        private SpriteVisual _spriteVisual;
        private IVideoSurface _renderSurface;

        public VideoPlayerImplementation(IMediaQueue mediaQueue, IMediaPlyerPlaybackController mediaPlyerPlaybackController, IVolumeManager volumeManager)
            : base(mediaQueue, mediaPlyerPlaybackController, volumeManager)
        {
        }

        public Dictionary<string, string> RequestHeaders { get; set; }

        public TimeSpan Buffered
        {
            get
            {
                if (Player == null) return TimeSpan.Zero;
                return
                    TimeSpan.FromMilliseconds(Player.PlaybackSession.BufferingProgress *
                                              Player.PlaybackSession.NaturalDuration.TotalMilliseconds);
            }
        }

        public TimeSpan Duration => Player?.PlaybackSession.NaturalDuration ?? TimeSpan.Zero;
        public TimeSpan Position => Player?.PlaybackSession.Position ?? TimeSpan.Zero;

        /// <summary>
        /// True when RenderSurface has been initialized and ready for rendering
        /// </summary>
        public bool IsReadyRendering => RenderSurface != null && !RenderSurface.IsDisposed;

        public IVideoSurface RenderSurface
        {
            get { return _renderSurface; }
            set
            {
                if (!(value is VideoSurface))
                    throw new ArgumentException("Not a valid video surface");

                _renderSurface = (VideoSurface)value;

                SetVideoSurface((VideoSurface)_renderSurface);

                var canvas = _renderSurface as Canvas;
                if (canvas != null)
                {
                    canvas.SizeChanged -= ResizeVideoSurface;
                }
                _renderSurface = value;
                canvas = _renderSurface as Canvas;
                if (canvas != null)
                {
                    canvas.SizeChanged += ResizeVideoSurface;
                }
            }
        }

        private void ResizeVideoSurface(object sender, SizeChangedEventArgs e)
        {
            var newSize = new Size(e.NewSize.Width, e.NewSize.Height);
            Player.SetSurfaceSize(newSize);
            _spriteVisual.Size = new Vector2((float)newSize.Width, (float)newSize.Height);
        }

        private void SetVideoSurface(VideoSurface canvas)
        {
            var size = new Size(canvas.ActualWidth, canvas.ActualHeight);
            Player.SetSurfaceSize(size);

            var compositor = ElementCompositionPreview.GetElementVisual(canvas).Compositor;
            var surface = Player.GetSurface(compositor);

            _spriteVisual = compositor.CreateSpriteVisual();
            _spriteVisual.Size =
                new Vector2((float)canvas.ActualWidth, (float)canvas.ActualHeight);

            CompositionBrush brush = compositor.CreateSurfaceBrush(surface.CompositionSurface);
            _spriteVisual.Brush = brush;

            var container = compositor.CreateContainerVisual();
            container.Children.InsertAtTop(_spriteVisual);

            ElementCompositionPreview.SetElementChildVisual(canvas, container);
        }

        public VideoAspectMode AspectMode { get; set; }

        public bool IsMuted
        {
            get;
            set;
        }

        public void SetVolume(float leftVolume, float rightVolume)
        {
        }
    }
}