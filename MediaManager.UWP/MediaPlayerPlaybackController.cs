using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Plugin.MediaManager.Interfaces;

namespace Plugin.MediaManager
{
    internal class MediaPlayerPlaybackController : IMediaPlyerPlaybackController
    {
        private readonly IPlaybackControllerProvider _playbackControllerProvider;

        private readonly SemaphoreSlim _createPlayerSemaphor = new SemaphoreSlim(1);

        private readonly CoreDispatcher _dispatcher;

        public MediaPlayerPlaybackController(IPlaybackControllerProvider playbackControllerProvider)
        {
            _playbackControllerProvider = playbackControllerProvider;

            Player = new MediaPlayer();
            PlaybackList = new MediaPlaybackList();

            Player.SystemMediaTransportControls.ButtonPressed += ButtonPressed;

            _dispatcher = Window.Current.Dispatcher;
        }

        public MediaPlayer Player { get; private set; }

        public MediaPlaybackList PlaybackList { get; private set; }

        public async Task CreatePlayerIfDoesntExist()
        {
            if (Player == null)
            {
                try
                {
                    await Task.Run(async () =>
                    {
                        if (await _createPlayerSemaphor.WaitAsync(-1))
                        {
                            if (Player != null)
                            {
                                return;
                            }

                            await _dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                            {
                                Player = new MediaPlayer();
                                PlaybackList = new MediaPlaybackList();
                            });
                        }
                    });
                }
                finally
                {
                    _createPlayerSemaphor.Release(1);
                }
            }
        }

        public void StopPlayer()
        {
            DisposePlayer();
        }

        public void Dispose()
        {
            DisposePlayer();
        }

        private async void ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Next:
                    await _playbackControllerProvider.PlaybackController.PlayNext();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    await _playbackControllerProvider.PlaybackController.PlayPreviousOrSeekToStart();
                    break;
                case SystemMediaTransportControlsButton.Play:
                    await _playbackControllerProvider.PlaybackController.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    await _playbackControllerProvider.PlaybackController.Pause();
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    await _playbackControllerProvider.PlaybackController.Stop();
                    break;
            }
        }

        private void DisposePlayer()
        {
            if (Player == null)
            {
                return;
            }

            Player.SystemMediaTransportControls.ButtonPressed -= ButtonPressed;

            DisposePlayerSources();
            Player.Dispose();
            Player = null;
            PlaybackList = null;
        }

        private void DisposePlayerSources()
        {
            if (Player == null)
            {
                return;
            }

            if (Player.Source != null)
            {
                var source = Player.Source as MediaSource;
                source?.Dispose();

                var playbackItem = Player.Source as MediaPlaybackItem;
                DisposePlaybackItemSource(playbackItem);

                var playbackList = Player.Source as MediaPlaybackList;
                if (playbackList?.Items?.Any() ?? false)
                {
                    foreach (var playbackListItem in playbackList.Items)
                    {
                        DisposePlaybackItemSource(playbackListItem);
                    }
                }
            }

            Player.Source = null;
        }

        private static void DisposePlaybackItemSource(MediaPlaybackItem item)
        {
            if (item?.Source == null)
            {
                return;
            }

            if (item.BreakSchedule.PrerollBreak != null)
            {
                foreach (var mpItem in item.BreakSchedule.PrerollBreak.PlaybackList.Items)
                {
                    mpItem.Source?.Dispose();
                }
            }
            if (item.BreakSchedule.MidrollBreaks.Count != 0)
            {
                foreach (var mrBreak in item.BreakSchedule.MidrollBreaks)
                {
                    foreach (var mpItem in mrBreak.PlaybackList.Items)
                    {
                        mpItem.Source?.Dispose();
                    }
                }
            }
            if (item.BreakSchedule.PostrollBreak != null)
            {
                foreach (var mpItem in item.BreakSchedule.PostrollBreak.PlaybackList.Items)
                {
                    mpItem.Source?.Dispose();
                }
            }
            item.Source.Dispose();
        }
    }
}
