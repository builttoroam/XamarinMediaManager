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
        private const int MaxNumberOfActionButtons = 5;

        private NotificationCompat.Builder _builder;
        private NotificationCompat.Builder Builder
        {
            get
            {
                if (_builder != null)
                {
                    return _builder;
                }

                _notificationStyle.SetMediaSession(SessionToken);
                _builder = new NotificationCompat.Builder(_applicationContext)
                {
                    MStyle = _notificationStyle
                };

                return _builder;
            }
        }

        private Android.Support.V4.App.NotificationCompat.Action _previousButton;
        private Android.Support.V4.App.NotificationCompat.Action PreviousButton => _previousButton ?? (_previousButton = GenerateActionCompat(Resource.Drawable.IcMediaPrevious, nameof(MediaServiceBase.ActionPrevious), MediaServiceBase.ActionPrevious));

        private Android.Support.V4.App.NotificationCompat.Action _stepBackwardButton;
        private Android.Support.V4.App.NotificationCompat.Action StepBackwardButton => _stepBackwardButton ?? (_stepBackwardButton = GenerateActionCompat(Resource.Drawable.IcMediaRew, nameof(MediaServiceBase.ActionStepBackward), MediaServiceBase.ActionStepBackward));

        private Android.Support.V4.App.NotificationCompat.Action _playButton;
        private Android.Support.V4.App.NotificationCompat.Action PlayButton => _playButton ?? (_playButton = GenerateActionCompat(Resource.Drawable.IcMediaPlay, nameof(MediaServiceBase.ActionPlay), MediaServiceBase.ActionPlay));

        private Android.Support.V4.App.NotificationCompat.Action _pauseButton;
        private Android.Support.V4.App.NotificationCompat.Action PauseButton => _pauseButton ?? (_pauseButton = GenerateActionCompat(Resource.Drawable.IcMediaPause, nameof(MediaServiceBase.ActionPause), MediaServiceBase.ActionPause));

        private Android.Support.V4.App.NotificationCompat.Action _stepForwardButton;
        private Android.Support.V4.App.NotificationCompat.Action StepForwardButton => _stepForwardButton ?? (_stepForwardButton = GenerateActionCompat(Resource.Drawable.IcMediaFf, nameof(MediaServiceBase.ActionStepBackward), MediaServiceBase.ActionStepForward));

        private Android.Support.V4.App.NotificationCompat.Action _nextButton;
        private Android.Support.V4.App.NotificationCompat.Action NextButton => _nextButton ?? (_nextButton = GenerateActionCompat(Resource.Drawable.IcMediaNext, nameof(MediaServiceBase.ActionNext), MediaServiceBase.ActionNext));

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
            if (e == null)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Reset:
                    UpdateActionButtonVisibility(PreviousButton, MediaQueue?.HasPrevious() ?? false, 0);
                    UpdateActionButtonVisibility(NextButton, MediaQueue?.HasNext() ?? false, Builder.MActions.Count + 1);
                    _notificationManagerCompat.Notify(NotificationId, Builder.Build());
                    break;
            }
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
            UpdateActionButtonVisibility(PreviousButton, MediaQueue?.HasPrevious() ?? false, 1);
            UpdateActionButtonVisibility(StepBackwardButton, true, 2);
            UpdateActionButtonVisibility(PlayButton, !mediaIsPlaying, 3);
            UpdateActionButtonVisibility(PauseButton, mediaIsPlaying, 3);
            UpdateActionButtonVisibility(StepForwardButton, true, 4);
            UpdateActionButtonVisibility(NextButton, MediaQueue?.HasNext() ?? false, 5);

            var numberOfButtonsInCompactView = Enumerable.Range(0, Math.Min(Builder.MActions.Count, 3)).ToArray();
            ((NotificationCompat.MediaStyle)(Builder.MStyle)).SetShowActionsInCompactView(numberOfButtonsInCompactView);
        }

        private void UpdateActionButtonVisibility(Android.Support.V4.App.NotificationCompat.Action actionbutton, bool isVisible, int buttonPositionStartingFromLeft = 1)
        {
            if (actionbutton == null || buttonPositionStartingFromLeft > MaxNumberOfActionButtons)
            {
                return;
            }

            var isAlreadyVisible = IsButtonVisible(actionbutton);
            if (isAlreadyVisible && isVisible ||
                !isAlreadyVisible && !isVisible)
            {
                return;
            }

            // Show
            if (!isAlreadyVisible && isVisible)
            {
                if (buttonPositionStartingFromLeft - 1 >= Builder.MActions.Count)
                {
                    Builder.MActions.Add(actionbutton);
                }
                else
                {
                    Builder.MActions.Insert(buttonPositionStartingFromLeft - 1, actionbutton);
                }
            }
            // Hide
            if (isAlreadyVisible && !isVisible)
            {
                Builder.MActions.Remove(actionbutton);
            }
        }

        private bool IsButtonVisible(Android.Support.V4.App.NotificationCompat.Action actionBbutton)
        {
            if (actionBbutton == null)
            {
                return false;
            }

            return Builder.MActions?.Contains(actionBbutton) ?? false;
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
