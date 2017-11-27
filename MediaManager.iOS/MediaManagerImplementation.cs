using Plugin.MediaManager.Abstractions;

namespace Plugin.MediaManager
{
    public class MediaManagerImplementation : MediaManagerAppleBase
    {
        public MediaManagerImplementation()
        {
            MediaNotificationManager = new MediaNotificationManagerImplementation(this);
        }

        public sealed override IMediaNotificationManager MediaNotificationManager { get; set; }

        public sealed override void UpdateStepSecondsForRemoteControls()
        {
            MediaNotificationManager.UpdateNativeStepInterval();
        }
    }
}