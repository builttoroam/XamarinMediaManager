using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
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