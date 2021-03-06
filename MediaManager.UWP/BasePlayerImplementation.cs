using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Plugin.MediaManager
{
    public class BasePlayerImplementation : IDisposable
    {
        private readonly IVolumeManager _volumeManager;

        private readonly IMediaPlyerPlaybackController _mediaPlyerPlaybackController;
        private readonly IDictionary<Guid, MediaPlaybackItem> _playbackItemByMediaFileIdDict = new Dictionary<Guid, MediaPlaybackItem>();

        private readonly SemaphoreSlim _playbackListChangesSemaphor = new SemaphoreSlim(1);

        private MediaPlayerStatus _status;
        private Timer _playProgressTimer;

        public BasePlayerImplementation(IMediaQueue mediaQueue, IMediaPlyerPlaybackController mediaPlyerPlaybackController, IVolumeManager volumeManager)
        {
            _mediaPlyerPlaybackController = mediaPlyerPlaybackController;
            MediaQueue = mediaQueue;
            _volumeManager = volumeManager;

            SetupPlaybackProgressTimer();
            SubscribeToPlayerEvents();
            Player.Source = PlaybackList;

            MediaQueue.QueueMediaChanged += MediaQueueMediaChanged;
            MediaQueue.CollectionChanged += MediaQueueCollectionChanged;

            _volumeManager.CurrentVolume = (int)Player.Volume * 100;
            _volumeManager.Muted = Player.IsMuted;
            _volumeManager.VolumeChanged += VolumeChanged;
        }

        public event StatusChangedEventHandler StatusChanged;

        public event PlayingChangedEventHandler PlayingChanged;

        public event BufferingChangedEventHandler BufferingChanged;

        public event MediaFinishedEventHandler MediaFinished;

        public event MediaFailedEventHandler MediaFailed;

        protected MediaPlayer Player => _mediaPlyerPlaybackController.Player;

        protected MediaPlaybackList PlaybackList => _mediaPlyerPlaybackController.PlaybackList;

        public TimeSpan Duration => Player?.PlaybackSession.NaturalDuration ?? TimeSpan.Zero;

        public TimeSpan Position => Player?.PlaybackSession.Position ?? TimeSpan.Zero;

        public Dictionary<string, string> RequestHeaders { get; set; }

        public TimeSpan Buffered
        {
            get
            {
                if (Player == null)
                {
                    return TimeSpan.Zero;
                }

                return TimeSpan.FromMilliseconds(Player.PlaybackSession.BufferingProgress * Player.PlaybackSession.NaturalDuration.TotalMilliseconds);
            }
        }

        public MediaPlayerStatus Status
        {
            get => _status;
            protected set
            {
                _status = value;
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(_status));
            }
        }

        protected IMediaQueue MediaQueue { get; private set; }

        public virtual async Task Play(IMediaFile mediaFile = null)
        {
            try
            {
                if (Player == null)
                {
                    await _mediaPlyerPlaybackController.CreatePlayerIfDoesntExist();

                    SubscribeToPlayerEvents();
                    Player.Source = PlaybackList;
                }

                var sameMediaFile = mediaFile == null || mediaFile.Equals(MediaQueue.Current);
                var currentMediaPosition = Player.PlaybackSession?.Position;

                // This variable will determine whether you will resume your playback or not
                var resumeMediaFile = Status == MediaPlayerStatus.Paused && sameMediaFile ||
                                      currentMediaPosition?.TotalSeconds > 0 && sameMediaFile;
                if (resumeMediaFile)
                {
                    // TODO: PlaybackRate needs to be configurable rather than hard-coded here
                    //Player.PlaybackSession.PlaybackRate = 1;
                    Player.Play();
                    return;
                }

                try
                {
                    if (await _playbackListChangesSemaphor.WaitAsync(-1))
                    {
                        var mediaToPlay = RetrievePlaylistItem(mediaFile);
                        if (mediaToPlay == null)
                        {
                            PlaybackList.Items.Clear();
                            var mediaPlaybackItem = await CreateMediaPlaybackItem(mediaFile);
                            if (mediaPlaybackItem != null)
                            {
                                PlaybackList.Items.Add(mediaPlaybackItem);
                            }
                        }
                        else
                        {
                            var mediaToPlayIndex = PlaybackList.Items.IndexOf(mediaToPlay);
                            if (mediaToPlayIndex < 0)
                            {
                                Debug.WriteLine($"Specified media file not present in the playback list. Media file title: {mediaFile?.Metadata?.Title}");
                                return;
                            }

                            if (PlaybackList.CurrentItem != null && mediaToPlayIndex != PlaybackList.CurrentItemIndex)
                            {
                                PlaybackList.MoveTo((uint)mediaToPlayIndex);
                            }
                        }
                    }
                }
                finally
                {
                    _playbackListChangesSemaphor.Release(1);
                }

                Player.Play();
            }
            catch (Exception e)
            {
                HandlePlaybackFailure("Unable to start playback", e);
            }
        }

        public virtual async Task PlayPause()
        {
            if (Status == MediaPlayerStatus.Paused || Status == MediaPlayerStatus.Stopped)
            {
                await Play();
            }
            else
            {
                await Pause();
            }
        }

        public virtual Task Pause()
        {
            Player.Pause();

            return Task.CompletedTask;
        }

        public virtual Task Seek(TimeSpan position)
        {
            Player.PlaybackSession.Position = position;

            return Task.CompletedTask;
        }

        public virtual Task Stop()
        {
            if (Player == null)
            {
                return Task.CompletedTask;
            }

            _playProgressTimer.Change(0, int.MaxValue);
            Player.Pause();

            _playbackItemByMediaFileIdDict.Clear();
            UnsubscribeFromPlayerEvents();
            _mediaPlyerPlaybackController.StopPlayer();

            Status = MediaPlayerStatus.Stopped;

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            UnsubscribeFromPlayerEvents();

            MediaQueue.CollectionChanged -= MediaQueueCollectionChanged;
            _volumeManager.VolumeChanged -= VolumeChanged;

            _mediaPlyerPlaybackController?.Dispose();
        }

        protected async Task<MediaPlaybackItem> CreateMediaPlaybackItem(IMediaFile mediaFile)
        {
            if (string.IsNullOrWhiteSpace(mediaFile?.Url))
            {
                return null;
            }

            var mediaSource = await CreateMediaSource(mediaFile);
            if (mediaSource == null)
            {
                return null;
            }

            var playbackItem = new MediaPlaybackItem(mediaSource);
            UpdatePlaybackItemDisplayProperties(mediaFile, playbackItem);
            return playbackItem;
        }

        protected async Task<MediaSource> CreateMediaSource(IMediaFile mediaFile)
        {
            switch (mediaFile.Availability)
            {
                case ResourceAvailability.Remote:
                    return MediaSource.CreateFromUri(new Uri(mediaFile.Url));

                case ResourceAvailability.Local:
                    var du = Player.SystemMediaTransportControls.DisplayUpdater;
                    var storageFile = await StorageFile.GetFileFromPathAsync(mediaFile.Url);
                    var playbackType = mediaFile.Type == MediaFileType.Audio
                        ? MediaPlaybackType.Music
                        : MediaPlaybackType.Video;
                    await du.CopyFromFileAsync(playbackType, storageFile);
                    du.Update();
                    return MediaSource.CreateFromStorageFile(storageFile);
            }

            return MediaSource.CreateFromUri(new Uri(mediaFile.Url));
        }

        protected MediaPlaybackItem RetrievePlaylistItem(IMediaFile mediaFile)
        {
            if (mediaFile == null)
            {
                return null;
            }

            if (_playbackItemByMediaFileIdDict.ContainsKey(mediaFile.Id))
            {
                return _playbackItemByMediaFileIdDict[mediaFile.Id];
            }

            return null;
        }

        protected void HandlePlaybackFailure(string errorMsg, Exception exception)
        {
            _status = MediaPlayerStatus.Failed;
            _playProgressTimer.Change(0, int.MaxValue);

            MediaFailed?.Invoke(this, new MediaFailedEventArgs(errorMsg, exception));
        }

        private void PlayerMediaFailed(MediaPlayer mediaPlayer, MediaPlayerFailedEventArgs e)
        {
            HandlePlaybackFailure(e.ErrorMessage, e.ExtendedErrorCode);
        }

        private void PlaybackSessionStateChanged(MediaPlaybackSession playbackSession, object o)
        {
            Debug.WriteLine($"[Player] State changed {playbackSession.PlaybackState}");

            switch (playbackSession.PlaybackState)
            {
                case MediaPlaybackState.None:
                    _playProgressTimer.Change(0, int.MaxValue);
                    break;

                case MediaPlaybackState.Opening:
                    Status = MediaPlayerStatus.Loading;
                    _playProgressTimer.Change(0, int.MaxValue);
                    break;

                case MediaPlaybackState.Buffering:
                    Status = MediaPlayerStatus.Buffering;
                    _playProgressTimer.Change(0, int.MaxValue);
                    break;

                case MediaPlaybackState.Playing:
                    if (playbackSession.PlaybackRate <= 0 && playbackSession.Position == TimeSpan.Zero)
                    {
                        Status = MediaPlayerStatus.Stopped;
                    }
                    else
                    {
                        Status = MediaPlayerStatus.Playing;
                        _playProgressTimer.Change(0, 50);
                    }
                    break;

                case MediaPlaybackState.Paused:
                    Status = MediaPlayerStatus.Paused;
                    _playProgressTimer.Change(0, int.MaxValue);
                    break;
            }
        }

        private void PlaybackSessionBufferingStarted(MediaPlaybackSession playbackSession, object o)
        {
            var bufferedTime = TimeSpan.FromSeconds(playbackSession.BufferingProgress * playbackSession.NaturalDuration.TotalSeconds);
            BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(playbackSession.BufferingProgress, bufferedTime));
        }

        private void PlaybackSessionBufferingProgressChanged(MediaPlaybackSession playbackSession, object args)
        {
            var bufferedTime = TimeSpan.FromSeconds(Player.PlaybackSession.BufferingProgress * Player.PlaybackSession.NaturalDuration.TotalSeconds);
            BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(Player.PlaybackSession.BufferingProgress, bufferedTime));
        }

        private void MediaQueueMediaChanged(object sender, QueueMediaChangedEventArgs e)
        {
            if (e?.File == null || Player == null || PlaybackList == null)
            {
                return;
            }

            var playlistItemToPlay = RetrievePlaylistItem(e.File);
            if (playlistItemToPlay == null)
            {
                return;
            }

            var mediaToPlayIndex = PlaybackList.Items.IndexOf(playlistItemToPlay);
            if (mediaToPlayIndex < 0)
            {
                Debug.WriteLine($"Specified media file not present in the playback list. Media file title: {e.File.Metadata?.Title}");
                return;
            }

            PlaybackList.MoveTo((uint)mediaToPlayIndex);
        }

        private async void MediaQueueCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            await _mediaPlyerPlaybackController.CreatePlayerIfDoesntExist();

            try
            {
                if (await _playbackListChangesSemaphor.WaitAsync(-1))
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            await HandleMediaQueueAddAction(e);
                            break;

                        case NotifyCollectionChangedAction.Move:

                            // The reality is that this scenario is never going to happen. Even when re-ordering or shuffling happens, the list is being regenerated (Reset)
                            break;

                        case NotifyCollectionChangedAction.Remove:
                            HandleMediaQueueRemoveAction(e);
                            break;

                        case NotifyCollectionChangedAction.Replace:
                            await HandleMediaQueueReplaceAction(e);
                            break;

                        case NotifyCollectionChangedAction.Reset:
                            await HandleMediaQueueResetAction(sender as IEnumerable<IMediaFile>);
                            break;
                    }
                }
            }
            finally
            {
                _playbackListChangesSemaphor.Release(1);
            }
        }

        private void VolumeChanged(object sender, VolumeChangedEventArgs volumeChangedEventArgs)
        {
            Player.Volume = (double)volumeChangedEventArgs.NewVolume;
            Player.IsMuted = volumeChangedEventArgs.Muted;
        }

        private async Task HandleMediaQueueAddAction(NotifyCollectionChangedEventArgs e)
        {
            if (e?.NewItems == null)
            {
                return;
            }

            var newMediaFiles = new List<IMediaFile>();
            foreach (var newItem in e.NewItems)
            {
                if (newItem is IMediaFile mediaFile)
                {
                    newMediaFiles.Add(mediaFile);
                }
                else if (newItem is IEnumerable<IMediaFile> mediaFiles)
                {
                    newMediaFiles.AddRange(mediaFiles);
                }

                foreach (var newMediaFile in newMediaFiles)
                {
                    if (_playbackItemByMediaFileIdDict.ContainsKey(newMediaFile.Id))
                    {
                        continue;
                    }

                    var newPlaybackItem = await CreateMediaPlaybackItem(newMediaFile);

                    if (e.NewStartingIndex < 0)
                    {
                        PlaybackList.Items.Add(newPlaybackItem);
                    }
                    else
                    {
                        if (e.NewStartingIndex <= PlaybackList.Items.Count)
                        {
                            PlaybackList.Items.Insert(e.NewStartingIndex, newPlaybackItem);
                        }
                    }
                    _playbackItemByMediaFileIdDict.Add(newMediaFile.Id, newPlaybackItem);
                }
            }
        }

        private async Task HandleMediaQueueReplaceAction(NotifyCollectionChangedEventArgs e)
        {
            if (e?.NewItems == null || e.OldItems == null)
            {
                return;
            }

            try
            {
                var newMediaFile = e.NewItems[0] as IMediaFile;
                var oldMediaFile = e.OldItems[0] as IMediaFile;

                if (newMediaFile == null || oldMediaFile == null)
                {
                    return;
                }

                var mediaFileInPlaylist = RetrievePlaylistItem(oldMediaFile);
                if (mediaFileInPlaylist == null)
                {
                    return;
                }

                if (newMediaFile == oldMediaFile || newMediaFile.Url == oldMediaFile.Url)
                {
                    // Update same media file
                }
                else
                {
                    // Replace playlist media file with new one
                    var mediaFileInPlaylistIndex = PlaybackList.Items.IndexOf(mediaFileInPlaylist);
                    if (mediaFileInPlaylistIndex < 0)
                    {
                        Debug.WriteLine($"Specified media file not present in the playback list. Media file title: {oldMediaFile.Metadata?.Title}");
                        return;
                    }

                    if (mediaFileInPlaylistIndex == PlaybackList.CurrentItemIndex)
                    {
                        Player.Pause();
                    }

                    PlaybackList.Items.RemoveAt(mediaFileInPlaylistIndex);
                    _playbackItemByMediaFileIdDict.Remove(oldMediaFile.Id);

                    var newPlaybackItem = await CreateMediaPlaybackItem(newMediaFile);

                    PlaybackList.Items.Insert(mediaFileInPlaylistIndex, newPlaybackItem);
                    _playbackItemByMediaFileIdDict.Add(oldMediaFile.Id, newPlaybackItem);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void HandleMediaQueueRemoveAction(NotifyCollectionChangedEventArgs e)
        {
            if (e?.OldItems == null)
            {
                return;
            }

            foreach (var oldItem in e.OldItems)
            {
                if (oldItem is IMediaFile mediaFile)
                {
                    var mediaFileInPlaylist = RetrievePlaylistItem(mediaFile);
                    if (mediaFileInPlaylist == null)
                    {
                        continue;
                    }

                    var mediaFileInPlaylistIndex = PlaybackList.Items.IndexOf(mediaFileInPlaylist);
                    if (mediaFileInPlaylistIndex < 0)
                    {
                        Debug.WriteLine($"Specified media file not present in the playback list. Media file title: {mediaFile.Metadata?.Title}");
                        continue;
                    }

                    var isMediaFileInPlaylistIndexCurrentlyPlaying = mediaFileInPlaylistIndex == PlaybackList.CurrentItemIndex;
                    if (isMediaFileInPlaylistIndexCurrentlyPlaying)
                    {
                        Player.Pause();
                    }

                    PlaybackList.Items.RemoveAt(mediaFileInPlaylistIndex);
                    _playbackItemByMediaFileIdDict.Remove(mediaFile.Id);
                    if (PlaybackList.Items.Any() && isMediaFileInPlaylistIndexCurrentlyPlaying)
                    {
                        if (mediaFileInPlaylistIndex == 0)
                        {
                            PlaybackList.MoveNext();
                        }
                        else
                        {
                            PlaybackList.MovePrevious();
                        }
                    }
                }
            }
        }

        private async Task HandleMediaQueueResetAction(IEnumerable<IMediaFile> mediaFiles)
        {
            if (mediaFiles == null)
            {
                return;
            }

            if (Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                Player.Pause();
            }

            PlaybackList.Items.Clear();
            _playbackItemByMediaFileIdDict.Clear();
            foreach (var mediaFile in mediaFiles)
            {
                var newPlaybackItem = await CreateMediaPlaybackItem(mediaFile);

                PlaybackList.Items.Add(newPlaybackItem);
                _playbackItemByMediaFileIdDict.Add(mediaFile.Id, newPlaybackItem);
            }
        }

        private void UpdatePlaybackItemDisplayProperties(IMediaFile mediaFile, MediaPlaybackItem playbackItem)
        {
            if (mediaFile?.Metadata == null || playbackItem == null)
            {
                return;
            }

            var playbaItemDisplayProperties = playbackItem.GetDisplayProperties();
            playbaItemDisplayProperties.Type = mediaFile.Type == MediaFileType.Audio ? MediaPlaybackType.Music : MediaPlaybackType.Video;
            switch (playbaItemDisplayProperties.Type)
            {
                case MediaPlaybackType.Music:
                    if (!string.IsNullOrWhiteSpace(mediaFile.Metadata.Title))
                    {
                        playbaItemDisplayProperties.MusicProperties.Title = mediaFile.Metadata.Title;
                    }
                    break;

                case MediaPlaybackType.Video:
                    if (!string.IsNullOrWhiteSpace(mediaFile.Metadata.Title))
                    {
                        playbaItemDisplayProperties.VideoProperties.Title = mediaFile.Metadata.Title;
                    }
                    break;
            }
            if (!string.IsNullOrWhiteSpace(mediaFile.Metadata?.ArtUri))
            {
                playbaItemDisplayProperties.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(mediaFile.Metadata.ArtUri));
            }
            playbackItem.ApplyDisplayProperties(playbaItemDisplayProperties);
        }

        private void SubscribeToPlayerEvents()
        {
            Player.MediaFailed += PlayerMediaFailed;
            Player.PlaybackSession.PlaybackStateChanged += PlaybackSessionStateChanged;
            Player.PlaybackSession.BufferingStarted += PlaybackSessionBufferingStarted;
            Player.PlaybackSession.BufferingProgressChanged += PlaybackSessionBufferingProgressChanged;

            PlaybackList.CurrentItemChanged += PlaybackListCurrentItemChanged;
        }

        private void UnsubscribeFromPlayerEvents()
        {
            if (Player == null)
            {
                return;
            }

            Player.MediaFailed -= PlayerMediaFailed;
            Player.PlaybackSession.PlaybackStateChanged -= PlaybackSessionStateChanged;
            Player.PlaybackSession.BufferingStarted -= PlaybackSessionBufferingStarted;
            Player.PlaybackSession.BufferingProgressChanged -= PlaybackSessionBufferingProgressChanged;

            PlaybackList.CurrentItemChanged -= PlaybackListCurrentItemChanged;
        }

        private void PlaybackListCurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            switch (e.Reason)
            {
                case MediaPlaybackItemChangedReason.EndOfStream:
                    HandleMediaFinishedPlayback();
                    break;
            }
        }

        private void HandleMediaFinishedPlayback()
        {
            MediaFinished?.Invoke(this, new MediaFinishedEventArgs(MediaQueue.Current));
        }

        private void SetupPlaybackProgressTimer()
        {
            _playProgressTimer = new Timer(state =>
            {
                if (Player?.PlaybackSession?.PlaybackState != MediaPlaybackState.Playing)
                {
                    return;
                }
                var progress = Player.PlaybackSession.Position.TotalSeconds / Player.PlaybackSession.NaturalDuration.TotalSeconds;
                if (double.IsInfinity(progress))
                {
                    progress = 0;
                }
                PlayingChanged?.Invoke(this, new PlayingChangedEventArgs(progress, Player.PlaybackSession.Position, Player.PlaybackSession.NaturalDuration));
            }, null, 0, int.MaxValue);
        }
    }
}