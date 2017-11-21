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

        public Task Pause()
        {
            Player.Pause();
            return Task.CompletedTask;
        }

        public async Task PlayPause()
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

        public override async Task Play(IMediaFile mediaFile = null)
        {
            try
            {
                await base.Play(mediaFile);

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

                Player.Play();
            }
            catch (Exception e)
            {
                HandlePlaybackFailure("Unable to start playback", e);
            }
        }

        public async Task Seek(TimeSpan position)
        {
            Player.PlaybackSession.Position = position;
            await Task.CompletedTask;
        }

        public override async Task Stop()
        {
            await base.Stop();

            Status = MediaPlayerStatus.Stopped;
        }
    }
}