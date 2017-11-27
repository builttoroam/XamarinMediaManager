using System;
using System.Threading.Tasks;
using Windows.Media.Playback;

namespace Plugin.MediaManager.Interfaces
{
    public interface IMediaPlyerPlaybackController : IDisposable
    {
        MediaPlayer Player { get; }
        MediaPlaybackList PlaybackList { get; }

        Task CreatePlayerIfDoesntExist();

        void StopPlayer();
    }
}
