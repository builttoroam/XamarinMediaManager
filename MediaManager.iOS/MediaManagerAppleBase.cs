using System;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Implementations;

namespace Plugin.MediaManager
{
    /// <summary>
    ///     Implementation for MediaManager that is common for MacOS, tvOS and iOS.
    /// </summary>
    public class MediaManagerAppleBase : MediaManagerBase
    {
        private IAudioPlayer _audioPlayer;
        private IVideoPlayer _videoPlayer;

        public override IAudioPlayer AudioPlayer
        {
            get => _audioPlayer ?? (_audioPlayer = new AudioPlayerImplementation(MediaQueue, VolumeManager));
            set => _audioPlayer = value;
        }

        public override IVideoPlayer VideoPlayer
        {
            get => _videoPlayer ?? (_videoPlayer = new VideoPlayerImplementation(VolumeManager));
            set => _videoPlayer = value;
        }

        public override IMediaExtractor MediaExtractor { get; set; } = new MediaExtractorImplementation();

        public override IVolumeManager VolumeManager { get; set; } = new VolumeManagerImplementation();

        public override IMediaNotificationManager MediaNotificationManager { get; set; }
    }
}