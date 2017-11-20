using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using MediaPlayer;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using UIKit;

namespace Plugin.MediaManager
{
    public class RemoteControlNotificationManager : NSObject, IMediaNotificationManager
    {
        private readonly IPlaybackController _playbackController;

        private NSObject _playCommandUnsubscribeToken;
        private NSObject _pauseCommandUnsubscribeToken;
        private NSObject _skipForwardCommandUnsubscribeToken;
        private NSObject _skipBackwardCommandUnsubscribeToken;
        private NSObject _changePlaybackPositionCommandUnsubscribeToken;
        private NSObject _nextCommandUnsubscribeToken;
        private NSObject _previousCommandUnsubscribeToken;

        protected bool IsRemoteControlTriggeredSeeking;

        public RemoteControlNotificationManager(IPlaybackController playbackController)
        {
            _playbackController = playbackController;

            MPRemoteCommandCenter.Shared.PlayCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.PauseCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.SkipForwardCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.SkipForwardCommand.PreferredIntervals = new double[] { _playbackController.StepSeconds };
            MPRemoteCommandCenter.Shared.SkipBackwardCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.SkipBackwardCommand.PreferredIntervals = new double[] { _playbackController.StepSeconds };
            MPRemoteCommandCenter.Shared.ChangePlaybackPositionCommand.Enabled = true;
        }

        public virtual void StartNotification(IMediaFile mediaFile)
        {
            _playCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.PlayCommand.AddTarget(OnPlayCommand);
            _pauseCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.PauseCommand.AddTarget(OnPauseCommand);
            _skipForwardCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.SkipForwardCommand.AddTarget(OnSkipForwardCommand);
            _skipBackwardCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.SkipBackwardCommand.AddTarget(OnSkipBackwardCommand);
            _changePlaybackPositionCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.ChangePlaybackPositionCommand.AddTarget(OnChangePlaybackPositionCommand);
            _nextCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.NextTrackCommand.AddTarget(OnNextCommand);
            _previousCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.PreviousTrackCommand.AddTarget(OnPreviousCommand);
        }

        public virtual void StopNotifications()
        {
            MPRemoteCommandCenter.Shared.PlayCommand.RemoveTarget(_playCommandUnsubscribeToken);
            MPRemoteCommandCenter.Shared.PauseCommand.RemoveTarget(_pauseCommandUnsubscribeToken);
            MPRemoteCommandCenter.Shared.SkipForwardCommand.RemoveTarget(_skipForwardCommandUnsubscribeToken);
            MPRemoteCommandCenter.Shared.SkipBackwardCommand.RemoveTarget(_skipBackwardCommandUnsubscribeToken);
            MPRemoteCommandCenter.Shared.ChangePlaybackPositionCommand.RemoveTarget(_changePlaybackPositionCommandUnsubscribeToken);
            MPRemoteCommandCenter.Shared.NextTrackCommand.RemoveTarget(_nextCommandUnsubscribeToken);
            MPRemoteCommandCenter.Shared.PreviousTrackCommand.RemoveTarget(_previousCommandUnsubscribeToken);
        }

        public virtual void UpdateNotifications(IMediaFile mediaFile, MediaPlayerStatus status)
        {
        }

        private MPRemoteCommandHandlerStatus OnPlayCommand(MPRemoteCommandEvent e)
        {
            _playbackController.Play();

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnPauseCommand(MPRemoteCommandEvent e)
        {
            _playbackController.Pause();

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnNextCommand(MPRemoteCommandEvent e)
        {
            _playbackController.PlayNext();

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnPreviousCommand(MPRemoteCommandEvent e)
        {
            _playbackController.PlayPreviousOrSeekToStart();

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnSkipForwardCommand(MPRemoteCommandEvent e)
        {
            if (e is MPSkipIntervalCommandEvent skip)
            {
                _playbackController.StepForward();
            }

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnSkipBackwardCommand(MPRemoteCommandEvent e)
        {
            if (e is MPSkipIntervalCommandEvent skip)
            {
                _playbackController.StepBackward();
            }

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnChangePlaybackPositionCommand(MPRemoteCommandEvent e)
        {
            if (e is MPChangePlaybackPositionCommandEvent playback)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        IsRemoteControlTriggeredSeeking = true;

                        await _playbackController.SeekTo(playback.PositionTime);
                    }
                    finally
                    {
                        IsRemoteControlTriggeredSeeking = false;
                    }
                });
            }

            return MPRemoteCommandHandlerStatus.Success;
        }
    }
}