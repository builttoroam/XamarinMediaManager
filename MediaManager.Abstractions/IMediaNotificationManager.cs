using Plugin.MediaManager.Abstractions.Enums;

namespace Plugin.MediaManager.Abstractions
{
    /// <summary>
    /// Manages the notifications to the native platform
    /// </summary>
    public interface IMediaNotificationManager
    {
        /// <summary>
        /// Starts the notification.
        /// </summary>
        /// <param name="mediaFile">The media file.</param>
        void StartNotification(IMediaFile mediaFile);

        /// <summary>
        /// Stops the notifications.
        /// </summary>
        void StopNotifications();

        /// <summary>
        /// Updates the notifications.
        /// </summary>
        /// <param name="mediaFile">The media file.</param>
        /// <param name="status">The status.</param>
        void UpdateNotifications(IMediaFile mediaFile, MediaPlayerStatus status);

        /// <summary>
        /// Forces the iOS native step controls to update their step interval from the value that is set in IPlaybackController
        /// </summary>
        void UpdateNativeStepInterval();
    }
}