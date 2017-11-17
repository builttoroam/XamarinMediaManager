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

        protected bool IsSeeking;

        public RemoteControlNotificationManager(IPlaybackController playbackController)
        {
            _playbackController = playbackController;

            MPRemoteCommandCenter.Shared.PlayCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.PauseCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.SkipForwardCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.SkipForwardCommand.PreferredIntervals = new double[] { _playbackController.StepSeconds };
            MPRemoteCommandCenter.Shared.SkipBackwardCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.SkipBackwardCommand.PreferredIntervals = new double[] { _playbackController.StepSeconds };
            MPRemoteCommandCenter.Shared.NextTrackCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.PreviousTrackCommand.Enabled = true;
            MPRemoteCommandCenter.Shared.ChangePlaybackPositionCommand.Enabled = true;
        }

        public virtual void StartNotification(IMediaFile mediaFile)
        {
            _playCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.PlayCommand.AddTarget(OnPlayCommand);
            _pauseCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.PauseCommand.AddTarget(OnPauseCommand);
            _skipForwardCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.SkipForwardCommand.AddTarget(OnSkipCommand);
            _skipBackwardCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.SkipBackwardCommand.AddTarget(OnSkipCommand);
            _changePlaybackPositionCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.ChangePlaybackPositionCommand.AddTarget(OnChangePlaybackPositionCommand);
            _changePlaybackPositionCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.NextTrackCommand.AddTarget(OnNextCommand);
            _changePlaybackPositionCommandUnsubscribeToken = MPRemoteCommandCenter.Shared.PreviousTrackCommand.AddTarget(OnPreviousCommand);
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
            _playbackController.PlayPrevious();

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnSkipCommand(MPRemoteCommandEvent e)
        {
            if (e is MPSkipIntervalCommandEvent skip)
            {
                if (skip.Interval > 0)
                {
                    _playbackController.StepForward();
                }
                else
                {
                    _playbackController.StepBackward();
                }
            }

            return MPRemoteCommandHandlerStatus.Success;
        }

        private MPRemoteCommandHandlerStatus OnChangePlaybackPositionCommand(MPRemoteCommandEvent e)
        {
            if (e is MPChangePlaybackPositionCommandEvent playback)
            {
                IsSeeking = true;
                try
                {
                    _playbackController.SeekTo(playback.PositionTime);
                }
                finally
                {
                    IsSeeking = false;
                }
            }

            return MPRemoteCommandHandlerStatus.Success;
        }
    }
}