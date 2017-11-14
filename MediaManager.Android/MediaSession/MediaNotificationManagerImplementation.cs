using Android;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Support.V4.App;
using Android.Support.V4.Media.Session;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Java.Net;
using Plugin.MediaManager.Abstractions.EventArguments;
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

        private bool _notificationStarted;

        public MediaNotificationManagerImplementation(Context applicationContext, IMediaQueue mediaQueue, Type serviceType)
        {
            _applicationContext = applicationContext;
            MediaQueue = mediaQueue;
            MediaQueue.QueueMediaChanged += QueueMediaChanged;
            MediaQueue.CollectionChanged += MediaQueueCollectionChanged;
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
            Builder.SetSmallIcon(_applicationContext.ApplicationInfo.Icon);
            Builder.SetContentIntent(_pendingIntent);
            Builder.SetOngoing(mediaIsPlaying);
            Builder.SetVisibility(1);

            UpdateNotificationDisplayProperties(mediaFile);
            UpdateNotificationActionButtons(mediaIsPlaying);

            _notificationManagerCompat.Notify(NotificationId, Builder.Build());
            _notificationStarted = true;
        }

        public void StopNotifications()
        {
            _notificationManagerCompat.Cancel(NotificationId);
            _notificationStarted = false;
        }

        public void UpdateNotifications(IMediaFile mediaFile, MediaPlayerStatus status)
        {
            var isPlaying = status == MediaPlayerStatus.Playing || status == MediaPlayerStatus.Buffering;

            try
            {
                if ((mediaFile == null || status == MediaPlayerStatus.Stopped) && _notificationStarted)
                {
                    Builder.SetOngoing(false);
                    return;
                }

                if (_notificationStarted)
                {
                    Builder.SetOngoing(isPlaying);
                    UpdateNotificationActionButtons(isPlaying);
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

        private void QueueMediaChanged(object sender, QueueMediaChangedEventArgs e)
        {
            if (e?.File == null)
            {
                return;
            }

            UpdateNotificationDisplayProperties(e.File);
        }

        private void MediaQueueCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {

        }

        private void UpdateNotificationDisplayProperties(IMediaFile mediaFile)
        {
            if (mediaFile?.Metadata == null)
            {
                return;
            }

            Builder.SetContentTitle(mediaFile.Metadata.Title ?? string.Empty);
            Builder.SetContentText(mediaFile.Metadata.Artist ?? string.Empty);
            Builder.SetContentInfo(mediaFile.Metadata.Album ?? string.Empty);

            TrySettingLargeIconBitmap(mediaFile);
        }

        private Android.Support.V4.App.NotificationCompat.Action GenerateActionCompat(int icon, string title, string intentAction)
        {
            _intent.SetAction(intentAction);

            var flags = PendingIntentFlags.UpdateCurrent;
            if (intentAction.Equals(MediaServiceBase.ActionStop))
            {
                flags = PendingIntentFlags.CancelCurrent;
            }

            var pendingIntent = PendingIntent.GetService(_applicationContext, 1, _intent, flags);

            return new Android.Support.V4.App.NotificationCompat.Action.Builder(icon, title, pendingIntent).Build();
        }

        private void UpdateNotificationActionButtons(bool mediaIsPlaying)
        {
            Builder.MActions.Clear();
            if (MediaQueue?.HasPrevious() ?? false)
            {
                Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaPrevious, nameof(MediaServiceBase.ActionPrevious), MediaServiceBase.ActionPrevious));
            }
            // TODO Change this icon to an appropriate one. It's not a correct one (it's the rewind icon) but there's no other option when it comes to android 'baked in' icons
            Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaRew, nameof(MediaServiceBase.ActionStepBackward), MediaServiceBase.ActionStepBackward));
            Builder.AddAction(mediaIsPlaying ? GenerateActionCompat(Resource.Drawable.IcMediaPause, nameof(MediaServiceBase.ActionPause), MediaServiceBase.ActionPause)
                                             : GenerateActionCompat(Resource.Drawable.IcMediaPlay, nameof(MediaServiceBase.ActionPlay), MediaServiceBase.ActionPlay));
            // TODO Change this icon to an appropriate one. It's not a correct one (it's the fast forward icon) but there's no other option when it comes to android 'baked in' icons
            Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaFf, nameof(MediaServiceBase.ActionStepBackward), MediaServiceBase.ActionStepForward));
            if (MediaQueue?.HasNext() ?? false)
            {
                Builder.AddAction(GenerateActionCompat(Resource.Drawable.IcMediaNext, nameof(MediaServiceBase.ActionNext), MediaServiceBase.ActionNext));
            }

            var numberOfButtonsInCompactView = Enumerable.Range(0, Math.Min(Builder.MActions.Count, 3)).ToArray();
            ((NotificationCompat.MediaStyle)(Builder.MStyle)).SetShowActionsInCompactView(numberOfButtonsInCompactView);
        }

        private async void TrySettingLargeIconBitmap(IMediaFile mediaFile)
        {
            var iconBitmap = mediaFile.Metadata.Art as Bitmap;
            if (iconBitmap == null)
            {
                if (!string.IsNullOrWhiteSpace(mediaFile.Metadata.ArtUri))
                {
                    try
                    {
                        var url = new URL(mediaFile.Metadata.ArtUri);
                        iconBitmap = await Task.Run(() =>
                        {
                            using (var bmpStream = url.OpenStream())
                            {
                                return BitmapFactory.DecodeStream(bmpStream);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }

            if (iconBitmap == null)
            {
                return;
            }

            Builder.SetLargeIcon(iconBitmap);
        }
    }
}
