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

        public TimeSpan Duration => Player?.PlaybackSession.NaturalDuration ?? TimeSpan.Zero;
        public TimeSpan Position => Player?.PlaybackSession.Position ?? TimeSpan.Zero;

        public async Task Seek(TimeSpan position)
        {
            Player.PlaybackSession.Position = position;
            await Task.CompletedTask;
        }        
    }
}