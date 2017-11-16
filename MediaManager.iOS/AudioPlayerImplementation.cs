using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AVFoundation;
using CoreFoundation;
using CoreMedia;
using Foundation;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;

namespace Plugin.MediaManager
{
    public class AudioPlayerImplementation : NSObject, IAudioPlayer
    {
        private readonly NSString _statusObservationKey = new NSString(Constants.StatusObservationKey);
        private readonly NSString _rateObservationKey = new NSString(Constants.RateObservationKey);
        private readonly NSString _loadedTimeRangesObservationKey = new NSString(Constants.LoadedTimeRangesObservationKey);

        private readonly AVPlayer _player = new AVPlayer();
        private readonly IVolumeManager _volumeManager;
        private readonly IVersionHelper _versionHelper;

        private IMediaFile _currentMediaFile;
        private MediaPlayerStatus _status;

        public AudioPlayerImplementation(IVolumeManager volumeManager)
        {
            _volumeManager = volumeManager;
            _versionHelper = new VersionHelper();

            InitializePlayer();

            _status = MediaPlayerStatus.Stopped;

            // Watch the buffering status. If it changes, we may have to resume because the playing stopped because of bad network-conditions.
            BufferingChanged += (sender, e) =>
            {
                // If the player is ready to play, it's paused and the status is still on PLAYING, go on!
                var isPlaying = Status == MediaPlayerStatus.Playing;
                if (CurrentItem.Status == AVPlayerItemStatus.ReadyToPlay && Rate == 0.0f && isPlaying)
                    _player.Play();
            };
            _volumeManager.Muted = _player.Muted;
            _volumeManager.CurrentVolume = (int)_player.Volume * 100;
            _volumeManager.MaxVolume = 100;
            _volumeManager.VolumeChanged += VolumeManagerOnVolumeChanged;
        }

        public event StatusChangedEventHandler StatusChanged;
        public event PlayingChangedEventHandler PlayingChanged;
        public event BufferingChangedEventHandler BufferingChanged;
        public event MediaFinishedEventHandler MediaFinished;
        public event MediaFailedEventHandler MediaFailed;

        public Dictionary<string, string> RequestHeaders { get; set; }

        public float Rate
        {
            get => _player?.Rate ?? 0.0f;
            set
            {
                if (_player != null)
                    _player.Rate = value;
            }
        }

        public TimeSpan Position
        {
            get
            {
                if (CurrentItem == null)
                    return TimeSpan.Zero;
                return TimeSpan.FromSeconds(CurrentItem.CurrentTime.Seconds);
            }
        }

        public TimeSpan Duration
        {
            get
            {
                if (CurrentItem == null || CurrentItem.Duration.IsIndefinite ||
                    CurrentItem.Duration.IsInvalid)
                    return TimeSpan.Zero;
                return TimeSpan.FromSeconds(CurrentItem.Duration.Seconds);
            }
        }

        public TimeSpan Buffered
        {
            get
            {
                var buffered = TimeSpan.Zero;

                var currentItem = CurrentItem;

                var loadedTimeRanges = currentItem?.LoadedTimeRanges;

                if (currentItem != null && loadedTimeRanges.Any())
                {
                    var loadedSegments = loadedTimeRanges
                        .Select(timeRange =>
                        {
                            var timeRangeValue = timeRange.CMTimeRangeValue;

                            var startSeconds = timeRangeValue.Start.Seconds;
                            var durationSeconds = timeRangeValue.Duration.Seconds;

                            return startSeconds + durationSeconds;
                        });

                    var loadedSeconds = loadedSegments.Max();

                    buffered = TimeSpan.FromSeconds(loadedSeconds);
                }

                Console.WriteLine("Buffered size: " + buffered);

                return buffered;
            }
        }

        public MediaPlayerStatus Status
        {
            get => _status;
            private set
            {
                _status = value;
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(_status));
            }
        }

        private AVPlayerItem CurrentItem => _player.CurrentItem;

        private NSUrl NsUrl { get; set; }

        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            Console.WriteLine("Observer triggered for {0}", keyPath);

            switch (keyPath)
            {
                case Constants.StatusObservationKey:
                    HandlePlaybackStatusChange();
                    return;
                case Constants.LoadedTimeRangesObservationKey:
                    HandleLoadedTimeRangesChange();
                    return;
                case Constants.RateObservationKey:
                    HandlePlaybackRateChange();
                    return;
            }
        }

        public async Task Play(IMediaFile mediaFile = null)
        {
            var sameMediaFile = mediaFile == null || mediaFile.Equals(_currentMediaFile);

            if (Status == MediaPlayerStatus.Paused && sameMediaFile)
            {
                _player.Play();
                Status = MediaPlayerStatus.Playing;
                return;
            }

            if (mediaFile != null)
            {
                NsUrl = MediaFileUrlHelper.GetUrlFor(mediaFile);
                _currentMediaFile = mediaFile;
            }

            try
            {
                Status = MediaPlayerStatus.Buffering;

                var playerItem = GetPlayerItem(NsUrl);
                if (playerItem == null)
                {
                    Status = MediaPlayerStatus.Failed;
                    return;
                }

                CurrentItem?.RemoveObserver(this, _statusObservationKey);

                _player.ReplaceCurrentItemWithPlayerItem(playerItem);
                // ReSharper disable once PossibleNullReferenceException
                CurrentItem.AddObserver(this, _loadedTimeRangesObservationKey, NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New, _loadedTimeRangesObservationKey.Handle);
                CurrentItem.AddObserver(this, _statusObservationKey, NSKeyValueObservingOptions.New | NSKeyValueObservingOptions.Initial, _statusObservationKey.Handle);

                NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification, HandleFinshedPlaying, CurrentItem);

                _player.Play();
            }
            catch (Exception ex)
            {
                HandleMediaPlaybackFailure();
                Status = MediaPlayerStatus.Stopped;

                //unable to start playback log error
                Console.WriteLine("Unable to start playback: " + ex);
            }

            await Task.CompletedTask;
        }

        public async Task Stop()
        {
            if (CurrentItem == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                _player.Pause();
                CurrentItem.Seek(CMTime.FromSeconds(0d, 1));

                Status = MediaPlayerStatus.Stopped;
            });
        }

        public async Task Pause()
        {
            if (CurrentItem == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                _player.Pause();
                Status = MediaPlayerStatus.Paused;
            });
        }

        public async Task Seek(TimeSpan position)
        {
            if (CurrentItem == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                CurrentItem?.Seek(CMTime.FromSeconds(position.TotalSeconds, 1));
            });
        }

        private void InitializePlayer()
        {
            if (_versionHelper.SupportsAutomaticWaitPlayerProperty)
            {
                _player.AutomaticallyWaitsToMinimizeStalling = false;
            }

#if __IOS__ || __TVOS__
            var avSession = AVAudioSession.SharedInstance();

            // By setting the Audio Session category to AVAudioSessionCategorPlayback, audio will continue to play when the silent switch is enabled, or when the screen is locked.
            avSession.SetCategory(AVAudioSessionCategory.Playback);
            avSession.SetActive(true, out var activationError);
            if (activationError != null)
            {
                Console.WriteLine("Could not activate audio session {0}", activationError.LocalizedDescription);
            }
#endif
            _player.AddObserver(this, (NSString)"rate", NSKeyValueObservingOptions.New | NSKeyValueObservingOptions.Initial, _rateObservationKey.Handle);
            _player.AddPeriodicTimeObserver(new CMTime(1, 4), DispatchQueue.MainQueue, delegate
            {
                if (CurrentItem.Duration.IsInvalid || CurrentItem.Duration.IsIndefinite || double.IsNaN(CurrentItem.Duration.Seconds))
                {
                    PlayingChanged?.Invoke(this, new PlayingChangedEventArgs(0, Position, Duration));
                }
                else
                {
                    var totalDuration = TimeSpan.FromSeconds(CurrentItem.Duration.Seconds);
                    var totalProgress = Position.TotalMilliseconds /
                                        totalDuration.TotalMilliseconds;
                    PlayingChanged?.Invoke(this, new PlayingChangedEventArgs(
                        !double.IsInfinity(totalProgress) ? totalProgress : 0,
                        Position,
                        Duration
                    ));
                }
            });
        }

        private void VolumeManagerOnVolumeChanged(object sender, VolumeChangedEventArgs volumeChangedEventArgs)
        {
            _player.Volume = (float)volumeChangedEventArgs.NewVolume / 100;
            _player.Muted = volumeChangedEventArgs.Muted;
        }

        private AVPlayerItem GetPlayerItem(NSUrl url)
        {
            AVAsset asset;

            if (RequestHeaders?.Any() ?? false)
            {
                var options = MediaFileUrlHelper.GetOptionsWithHeaders(RequestHeaders);
                asset = AVUrlAsset.Create(url, options);
            }
            else
            {
                asset = AVAsset.FromUrl(url);
            }

            var playerItem = AVPlayerItem.FromAsset(asset);
            return playerItem;
        }

        private void HandlePlaybackStatusChange()
        {
            Console.WriteLine("Status Observed Method {0}", CurrentItem.Status);

            var isBuffering = Status == MediaPlayerStatus.Buffering;
            if (CurrentItem.Status == AVPlayerItemStatus.ReadyToPlay && isBuffering)
            {
                Status = MediaPlayerStatus.Playing;
                _player.Play();
            }
            else if (CurrentItem.Status == AVPlayerItemStatus.Failed)
            {
                HandleMediaPlaybackFailure();
                Status = MediaPlayerStatus.Stopped;
            }
        }

        private void HandlePlaybackRateChange()
        {
            var stoppedPlaying = Rate == 0.0;
            if (stoppedPlaying && Status == MediaPlayerStatus.Playing)
            {
                //Update the status becuase the system changed the rate.
                Status = MediaPlayerStatus.Paused;
            }
        }

        private void HandleLoadedTimeRangesChange()
        {
            var loadedTimeRanges = CurrentItem.LoadedTimeRanges;
            var hasLoadedAnyTimeRanges = loadedTimeRanges != null && loadedTimeRanges.Length > 0;

            if (hasLoadedAnyTimeRanges)
            {
                var range = loadedTimeRanges[0].CMTimeRangeValue;
                var duration = double.IsNaN(range.Duration.Seconds) ? TimeSpan.Zero : TimeSpan.FromSeconds(range.Duration.Seconds);
                var totalDuration = CurrentItem.Duration;
                var bufferProgress = duration.TotalSeconds / totalDuration.Seconds;

                BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(!double.IsInfinity(bufferProgress) ? bufferProgress : 0, duration));
            }
            else
            {
                BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(0, TimeSpan.Zero));
            }
        }

        private void HandleMediaPlaybackFailure()
        {
            var error = CurrentItem.Error;

            MediaFailed?.Invoke(this, new MediaFailedEventArgs(error.LocalizedDescription, new NSErrorException(error)));
        }

        private void HandleFinshedPlaying(NSNotification notification)
        {
            MediaFinished?.Invoke(this, new MediaFinishedEventArgs(_currentMediaFile));
        }
    }
}