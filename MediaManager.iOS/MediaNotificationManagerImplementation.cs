using Foundation;
using MediaPlayer;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using UIKit;

namespace Plugin.MediaManager
{
    public class MediaNotificationManagerImplementation : RemoteControlNotificationManager
    {
        private readonly IMediaManager _mediaManager;

        public MediaNotificationManagerImplementation(IMediaManager mediaManager)
            : base(mediaManager.PlaybackController)
        {
            _mediaManager = mediaManager;
        }

        private IMediaQueue Queue => _mediaManager.MediaQueue;

        private MPNowPlayingInfo NowPlaying
        {
            set => MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = value;
        }

        public override void StartNotification(IMediaFile mediaFile)
        {
            TrySetNowPlayingInfo(mediaFile);

            base.StartNotification(mediaFile);
        }

        public override void UpdateNotifications(IMediaFile mediaFile, MediaPlayerStatus status)
        {
            TrySetNowPlayingInfo(mediaFile);

            base.UpdateNotifications(mediaFile, status);
        }

        public override void StopNotifications()
        {
            NowPlaying = null;

            base.StopNotifications();
        }

        private void TrySetNowPlayingInfo(IMediaFile mediaFile)
        {
            if (mediaFile == null) return;

            var nowPlaying = CreateNowPlayingInfo(mediaFile);
            if (nowPlaying != null)
            {
                NowPlaying = nowPlaying;
            }
        }

        private MPNowPlayingInfo CreateNowPlayingInfo(IMediaFile mediaFile)
        {
            if (mediaFile?.Metadata == null)
            {
                return null;
            }

            var metadata = mediaFile.Metadata;
            var nowPlayingInfo = new MPNowPlayingInfo
            {
                Title = metadata.Title,
                AlbumTitle = metadata.Album,
                AlbumTrackNumber = metadata.TrackNumber,
                AlbumTrackCount = metadata.NumTracks,
                Artist = metadata.Artist,
                Composer = metadata.Composer,
                DiscNumber = metadata.DiscNumber,
                Genre = metadata.Genre,
                ElapsedPlaybackTime = _mediaManager.Position.TotalSeconds,
                PlaybackDuration = _mediaManager.Duration.TotalSeconds,
                PlaybackQueueIndex = Queue.Index,
                PlaybackQueueCount = Queue.Count,
                PlaybackRate = _mediaManager.Status == MediaPlayerStatus.Playing ? 1f : 0f
            };

            var cover = metadata.AlbumArt as UIImage;
            if (cover != null)
            {
                nowPlayingInfo.Artwork = new MPMediaItemArtwork(cover);
            }

            return nowPlayingInfo;
        }        
    }
}
