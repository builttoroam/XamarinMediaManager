using Plugin.MediaManager;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Abstractions.Implementations;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MediaForms
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HomePage : ContentPage
    {
        private bool isPlaybackProgressingNaturally;

        public HomePage()
        {
            InitializeComponent();
            this.volumeLabel.Text = "Volume (0-" + CrossMediaManager.Current.VolumeManager.MaxVolume + ")";
            //Initialize Volume settings to match interface
            int.TryParse(this.volumeEntry.Text, out var vol);
            CrossMediaManager.Current.VolumeManager.CurrentVolume = vol;
            CrossMediaManager.Current.VolumeManager.Muted = false;
        }

        private void MainBtn_OnClicked(object sender, EventArgs e)
        {
            Navigation.PushAsync(new MediaFormsPage());
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            CrossMediaManager.Current.PlayingChanged += PlaybackChanged;
            CrossMediaManager.Current.StatusChanged += CurrentOnStatusChanged;

            CrossMediaManager.Current.MediaFinished += MediaFinished;
            CrossMediaManager.Current.MediaQueue.CollectionChanged += MediaQueueCollectionChanged;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            CrossMediaManager.Current.PlayingChanged -= PlaybackChanged;
            CrossMediaManager.Current.StatusChanged -= CurrentOnStatusChanged;

            CrossMediaManager.Current.MediaFinished -= MediaFinished;
            CrossMediaManager.Current.MediaQueue.CollectionChanged -= MediaQueueCollectionChanged;
        }

        private void PlaybackChanged(object sender, PlayingChangedEventArgs e)
        {
            if (e == null || double.IsNaN(e.Progress))
            {
                return;
            }

            Device.BeginInvokeOnMainThread(() =>
            {
                isPlaybackProgressingNaturally = true;
                PlaybackSlider.Value = e.Progress;
                isPlaybackProgressingNaturally = false;
            });
        }

        private void CurrentOnStatusChanged(object sender, StatusChangedEventArgs e)
        {
            Debug.WriteLine($"MediaManager Status: {e.Status}");

            Device.BeginInvokeOnMainThread(() =>
            {
                PlayButton.IsEnabled = e.Status == MediaPlayerStatus.Paused;
                StopButton.IsEnabled = e.Status == MediaPlayerStatus.Playing;
                PauseButton.IsEnabled = e.Status == MediaPlayerStatus.Playing;

                PlayerStatus.Text = e.Status.ToString();
                IsBufferingIndicator.IsVisible = e.Status == MediaPlayerStatus.Buffering || e.Status == MediaPlayerStatus.Loading;

                if (e.Status == MediaPlayerStatus.Stopped)
                {
                    NextButton.IsEnabled = false;
                    PreviousButton.IsEnabled = false;
                }
            });
        }

        private void MediaFinished(object sender, MediaFinishedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                NextButton.IsEnabled = CrossMediaManager.Current.MediaQueue.HasNext();
                PreviousButton.IsEnabled = CrossMediaManager.Current.MediaQueue.HasPrevious();
            });
        }

        private void MediaQueueCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                NextButton.IsEnabled = CrossMediaManager.Current.MediaQueue.HasNext();
                PreviousButton.IsEnabled = CrossMediaManager.Current.MediaQueue.HasPrevious();
            });
        }

        private async void PlayButton_OnClicked(object sender, EventArgs e)
        {
            await CrossMediaManager.Current.Play();
        }

        private async void PauseButton_OnClicked(object sender, EventArgs e)
        {
            await CrossMediaManager.Current.Pause();
        }

        private async void StopButton_OnClicked(object sender, EventArgs e)
        {
            await CrossMediaManager.Current.Stop();
        }

        private async void PreviousButton_OnClicked(object sender, EventArgs e)
        {
            await CrossMediaManager.Current.PlayPrevious();
        }

        private async void NextButton_OnClicked(object sender, EventArgs e)
        {
            await CrossMediaManager.Current.PlayNext();
        }

        private async void PlayAudio_OnClicked(object sender, EventArgs e)
        {
            var mediaFile = new MediaFile
            {
                Type = MediaFileType.Audio,
                Availability = ResourceAvailability.Remote,
                Url = "https://audioboom.com/posts/5766044-follow-up-305.mp3",
                ExtractMetadata = true
            };
            await CrossMediaManager.Current.Play(mediaFile);
        }

        private async void PlayAudioMyTrack_OnClicked(object sender, EventArgs e)
        {
            var mediaFile = new MediaFile
            {
                Type = MediaFileType.Audio,
                Availability = ResourceAvailability.Remote,
                Url = "https://audioboom.com/posts/5766044-follow-up-305.mp3",
                Metadata = new MediaFileMetadata() { Title = "My Title", Artist = "My Artist", Album = "My Album" },
                ExtractMetadata = false
            };
            await CrossMediaManager.Current.Play(mediaFile);
        }

        private async void PlaylistButton_OnClicked(object sender, EventArgs e)
        {
            var playlist = RetrievePlaylist();
            await CrossMediaManager.Current.Play(playlist);

            foreach (var child in PlaylistActionContainer.Children)
            {
                child.IsEnabled = true;
            }
        }

        private async void PlayAudioListFromSecond_OnClicked(object sender, EventArgs e)
        {
            var playlist = RetrievePlaylist();
            await CrossMediaManager.Current.Play(playlist, 1);

            foreach (var child in PlaylistActionContainer.Children)
            {
                child.IsEnabled = true;
            }
        }

        private void SetVolumeBtn_OnClicked(object sender, EventArgs e)
        {
            int.TryParse(this.volumeEntry.Text, out var vol);
            CrossMediaManager.Current.VolumeManager.CurrentVolume = vol;
        }

        private void MutedBtn_OnClicked(object sender, EventArgs e)
        {
            if (CrossMediaManager.Current.VolumeManager.Muted)
            {
                CrossMediaManager.Current.VolumeManager.Muted = false;
                mutedBtn.Text = "Mute";
            }
            else
            {
                CrossMediaManager.Current.VolumeManager.Muted = true;
                mutedBtn.Text = "Unmute";
            }
        }

        private void AddToPlaylistClicked(object sender, EventArgs e)
        {
            CrossMediaManager.Current.MediaQueue.Add(new MediaFile
            {
                Url = "https://audioboom.com/posts/5723344-ep-304-the-4th-dimension.mp3?source=rss&amp;stitched=1",
                Type = MediaFileType.Audio,
                Metadata = new MediaFileMetadata
                {
                    Title = "Test4",
                    DisplayIconUri = "https://d15mj6e6qmt1na.cloudfront.net/i/30739475.jpg"
                }
            });
        }

        private void RemoveLastFromPlaylistClicked(object sender, EventArgs e)
        {
            if (!(CrossMediaManager.Current.MediaQueue?.Any() ?? false))
            {
                return;
            }

            CrossMediaManager.Current.MediaQueue.RemoveAt(CrossMediaManager.Current.MediaQueue.Count - 1);
        }

        private void ShuffleClicked(object sender, EventArgs e)
        {
            CrossMediaManager.Current.MediaQueue.IsShuffled = !CrossMediaManager.Current.MediaQueue.IsShuffled;
        }

        private static List<MediaFile> RetrievePlaylist()
        {
            var list = new List<MediaFile>
            {
                new MediaFile
                {
                    Url = "https://audioboom.com/posts/5766044-follow-up-305.mp3?source=rss&amp;stitched=1",
                    Type = MediaFileType.Audio,
                    Metadata = new MediaFileMetadata
                    {
                        Title = "Test1"
                    }
                },
                new MediaFile
                {
                    Url = "https://media.acast.com/mydadwroteaporno/s3e1-london-thursday15.55localtime/media.mp3",
                    Type = MediaFileType.Audio,
                    Metadata = new MediaFileMetadata
                    {
                        Title = "Test2",
                        ArtUri = "https://d15mj6e6qmt1na.cloudfront.net/i/8457198.jpg"
                    }
                },
                new MediaFile
                {
                    Url = "https://audioboom.com/posts/5770261-ep-306-a-theory-of-evolution.mp3?source=rss&amp;stitched=1",
                    Type = MediaFileType.Audio,
                    Metadata = new MediaFileMetadata
                    {
                        Title = "Test3",
                        ArtUri = "https://d15mj6e6qmt1na.cloudfront.net/i/30739475.jpg"
                    }
                }
            };
            return list;
        }

        private void PlaybackSlideValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (isPlaybackProgressingNaturally)
            {
                return;
            }

            if (e == null || PlaybackSlider.Maximum == 0)
            {
                return;
            }

            var progressInPercent = e.NewValue / PlaybackSlider.Maximum;

            var seekTo = CrossMediaManager.Current.AudioPlayer.Duration.TotalMilliseconds * progressInPercent;
            CrossMediaManager.Current.Seek(TimeSpan.FromMilliseconds(seekTo));
        }
    }
}