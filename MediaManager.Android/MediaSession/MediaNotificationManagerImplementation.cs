using Android;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Support.V4.App;
using Android.Support.V4.Media.Session;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using System;
using NotificationCompat = Android.Support.V7.App.NotificationCompat;

namespace Plugin.MediaManager
{
    internal class MediaNotificationManagerImplementation : IAndroidMediaNotificationManager
    {
        public IMediaQueue MediaQueue { get; set; }
        public MediaSessionCompat.Token SessionToken { get; set; }

        private const int NotificationId = 1;

        private NotificationCompat.Builder _builder;
        private NotificationCompat.Builder Builder
        {
            get
            {
                if (_builder == null)
                {
                    _notificationStyle.SetMediaSession(SessionToken);
                    _builder = new NotificationCompat.Builder(_applicationContext)
                    {
                        MStyle = _notificationStyle
                    };
                }

                return _builder;
            }
        }

        private readonly Context _applicationContext;
        private readonly Intent _intent;
        private readonly PendingIntent _pendingIntent;
        private readonly NotificationManagerCompat _notificationManagerCompat;
        private readonly NotificationCompat.MediaStyle _notificationStyle = new NotificationCompat.MediaStyle();

        private bool _hasNotificationStarted;

        public MediaNotificationManagerImplementation(Context applicationContext, Type serviceType)
        {
            _applicationContext = applicationContext;
            _intent = new Intent(_applicationContext, serviceType);

            var mainActivity = _applicationContext.PackageManager.GetLaunchIntentForPackage(_applicationContext.PackageName);
            _pendingIntent = PendingIntent.GetActivity(_applicationContext, 0, mainActivity, PendingIntentFlags.UpdateCurrent);

            _notificationManagerCompat = NotificationManagerCompat.From(_applicationContext);
        }

        /// <summary>
        /// Starts the notification.
        /// </summary>
        /// <param name="mediaFile">The media file.</param>
        public void StartNotification(IMediaFile mediaFile)
        {
            StartNotification(mediaFile, true, false);
        }

        /// <summary>
        /// When we start on the foreground we will present a notification to the user
        /// When they press the notification it will take them to the main page so they can control the music
        /// </summary>
        public void StartNotification(IMediaFile mediaFile, bool mediaIsPlaying, bool canBeRemoved)
        {
            var icon = (_applicationContext.Resources?.GetIdentifier("xam_mediamanager_notify_ic", "drawable", _applicationContext?.PackageName)).GetValueOrDefault(0);

            Builder.SetSmallIcon(icon != 0 ? icon : _applicationContext.ApplicationInfo.Icon);
            Builder.SetContentIntent(_pendingIntent);
            Builder.SetOngoing(mediaIsPlaying);
            Builder.SetVisibility(1);

            SetMetadata(mediaFile);
            AddActionButtons(mediaIsPlaying);

            if (Builder.MActions.Count >= 3)
                ((NotificationCompat.MediaStyle)(Builder.MStyle)).SetShowActionsInCompactView(0, 1, 2);
            if (Builder.MActions.Count == 2)
                ((NotificationCompat.MediaStyle)(Builder.MStyle)).SetShowActionsInCompactView(0, 1);
            if (Builder.MActions.Count == 1)
                ((NotificationCompat.MediaStyle)(Builder.MStyle)).SetShowActionsInCompactView(0);

            _notificationManagerCompat.Notify(NotificationId, Builder.Build());
            _hasNotificationStarted = true;
        }

        public void StopNotifications()
        {
            _notificationManagerCompat.Cancel(NotificationId);
            _hasNotificationStarted = false;
        }

        public void UpdateNotifications(IMediaFile mediaFile, MediaPlayerStatus status)
        {
            try
            {
                var isPlaying = status == MediaPlayerStatus.Playing || status == MediaPlayerStatus.Buffering;
                if (_hasNotificationStarted)
                {
                    SetMetadata(mediaFile);
                    AddActionButtons(isPlaying);
                    Builder.SetOngoing(isPlaying);

                    _notificationManagerCompat.Notify(NotificationId, Builder.Build());
                }
                else
                {
                    StartNotification(mediaFile, isPlaying, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                StopNotifications();
            }
        }

        private void SetMetadata(IMediaFile mediaFile)
        {
            Builder.SetContentTitle(mediaFile?.Metadata?.Title ?? string.Empty);
            Builder.SetContentText(mediaFile?.Metadata?.Artist ?? string.Empty);
            Builder.SetContentInfo(mediaFile?.Metadata?.Album ?? string.Empty);
            Builder.SetLargeIcon(mediaFile?.Metadata?.Art as Bitmap);
        }

        private Android.Support.V4.App.NotificationCompat.Action GenerateActionCompat(int icon, string title, string intentAction)
        {
            _intent.SetAction(intentAction);

            PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
            if (intentAction.Equals(MediaServiceBase.ActionStop))
                flags = PendingIntentFlags.CancelCurrent;

            PendingIntent pendingIntent = PendingIntent.GetService(_applicationContext, 1, _intent, flags);

            return new Android.Support.V4.App.NotificationCompat.Action.Builder(icon, title, pendingIntent).Build();
        }

        private void AddActionButtons(bool mediaIsPlaying)
        {
            // Add previous/next button based on media queue
            var canGoPrevious = MediaQueue?.HasPrevious() ?? false;
            var canGoNext = MediaQueue?.HasNext() ?? false;

            Builder.MActions.Clear();
            if (canGoPrevious)
            {
                Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaPrevious, "Previous",
                    MediaServiceBase.ActionPrevious));
            }
            // TODO Change this icon to an appropriate one. It's not a correct one (it's the rewind icon) but there's no other option when it comes to android 'baked in' icons
            Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaRew, "StepBackward", MediaServiceBase.ActionStepBackward));
            Builder.AddAction(mediaIsPlaying
                ? GenerateActionCompat(Resource.Drawable.IcMediaPause, "Pause", MediaServiceBase.ActionPause)
                : GenerateActionCompat(Resource.Drawable.IcMediaPlay, "Play", MediaServiceBase.ActionPlay));
            // TODO Change this icon to an appropriate one. It's not a correct one (it's the fast forward icon) but there's no other option when it comes to android 'baked in' icons
            Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaFf, "StepForward", MediaServiceBase.ActionStepForward));
            if (canGoNext)
            {
                Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaNext, "Next",
                    MediaServiceBase.ActionNext));
            }
        }
    }
}