using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Interfaces;

namespace Plugin.MediaManager
{
    public class AudioPlayerImplementation : BasePlayerImplementation, IAudioPlayer
    {
        public AudioPlayerImplementation(IMediaQueue mediaQueue, IMediaPlyerPlaybackController mediaPlyerPlaybackController, IVolumeManager volumeManager)
            : base(mediaQueue, mediaPlyerPlaybackController, volumeManager)
        {
        }
    }
}