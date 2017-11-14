using Android;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Support.V4.App;
using Android.Support.V4.Media.Session;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

        private bool IsPreviousActionButtonVisible => MediaQueue?.HasPrevious() ?? false;
        private bool IsNextActionButtonVisible => MediaQueue?.HasNext() ?? false;

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
            MediaQueue.QueueMediaChanged += MediaQueueItemChanged;
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

            ApplyChangesToMediaControls();
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
                    ApplyChangesToMediaControls();
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

        private void MediaQueueItemChanged(object sender, QueueMediaChangedEventArgs e)
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
                    UpdateActionButtonVisibility(NextButton, MediaQueue?.HasNext() ?? false, 4);
                    UpdateNotificationActionButtonsCompactView();
                    ApplyChangesToMediaControls();
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
            UpdateActionButtonVisibility(PreviousButton, IsPreviousActionButtonVisible, 0);
            UpdateActionButtonVisibility(StepBackwardButton, true, 1);

            var stepBackwardButtonPosition = Builder.MActions.IndexOf(StepBackwardButton);
            UpdateActionButtonVisibility(PlayButton, !mediaIsPlaying, stepBackwardButtonPosition + 1);
            UpdateActionButtonVisibility(PauseButton, mediaIsPlaying, stepBackwardButtonPosition + 1);
            UpdateActionButtonVisibility(StepForwardButton, true, 3);
            UpdateActionButtonVisibility(NextButton, IsNextActionButtonVisible, 4);

            UpdateNotificationActionButtonsCompactView();
        }

        private void UpdateNotificationActionButtonsCompactView()
        {
            var compactVersionActionButtons = new List<int>();
            var previousButtonIndex = Builder.MActions.IndexOf(PreviousButton);
            if (previousButtonIndex >= 0)
            {
                compactVersionActionButtons.Add(previousButtonIndex);
            }
            var playOrPauseButtonIdex = Builder.MActions.IndexOf(PlayButton);
            if (playOrPauseButtonIdex < 0)
            {
                playOrPauseButtonIdex = Builder.MActions.IndexOf(PauseButton);
            }
            if (playOrPauseButtonIdex >= 0)
            {
                compactVersionActionButtons.Add(playOrPauseButtonIdex);
            }
            var nextButtonIndex = Builder.MActions.IndexOf(NextButton);
            if (nextButtonIndex >= 0)
            {
                compactVersionActionButtons.Add(nextButtonIndex);
            }

            if (previousButtonIndex < 0 && nextButtonIndex < 0)
            {
                var stepBackwardButtonIndex = Builder.MActions.IndexOf(StepBackwardButton);
                var stepForwardButtonIndex = Builder.MActions.IndexOf(StepForwardButton);
                if (stepBackwardButtonIndex >= 0 && compactVersionActionButtons.Count < 3)
                {
                    compactVersionActionButtons.Insert(0, stepBackwardButtonIndex);
                }
                if (stepForwardButtonIndex >= 0 && compactVersionActionButtons.Count < 3)
                {
                    compactVersionActionButtons.Add(stepForwardButtonIndex);
                }
            }

            ((NotificationCompat.MediaStyle)(Builder.MStyle)).SetShowActionsInCompactView(compactVersionActionButtons.ToArray());
        }

        private void UpdateActionButtonVisibility(Android.Support.V4.App.NotificationCompat.Action actionbutton, bool isVisible, int buttonPositionStartingFromLeft = 0)
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
                if (Builder.MActions.Count >= MaxNumberOfActionButtons)
                {
                    return;
                }

                if (buttonPositionStartingFromLeft > Builder.MActions.Count)
                {
                    Builder.MActions.Add(actionbutton);
                }
                else
                {
                    Builder.MActions.Insert(buttonPositionStartingFromLeft, actionbutton);
                }
            }
            // Hide
            if (isAlreadyVisible && !isVisible)
            {
                Builder.MActions.Remove(actionbutton);
            }
        }

        private void ApplyChangesToMediaControls()
        {
            _notificationManagerCompat.Notify(NotificationId, Builder.Build());
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
