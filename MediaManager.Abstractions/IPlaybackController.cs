using System.Threading.Tasks;

namespace Plugin.MediaManager.Abstractions
{
    public interface IPlaybackController
    {
        /// <summary>
        /// Amount of seconds to step when skipped forward or backward
        /// </summary>
        double StepSeconds { get; }

        /// <summary>
        /// Plays or pauses the currentl MediaFile
        /// </summary>
        Task PlayPause();

        /// <summary>
        /// Plays the current MediaFile
        /// </summary>
        Task Play();

        /// <summary>
        /// Pauses the current MediaFile
        /// </summary>
        Task Pause();

        /// <summary>
        /// Stops playing
        /// </summary>
        Task Stop();

        /// <summary>
        /// Plays the previous MediaFile or seeks to start if far enough into the current one.
        /// </summary>
        Task PlayPreviousOrSeekToStart();

        /// <summary>
        /// Plays the previous MediaFile
        /// </summary>
        Task PlayPrevious();

        /// <summary>
        /// Plays the next MediaFile
        /// </summary>
        /// <returns></returns>
        Task PlayNext();

        /// <summary>
        /// Seeks to the start of the current MediaFile
        /// </summary>
        Task SeekToStart();

        /// <summary>
        /// Seeks forward a fixed amount of seconds of the current MediaFile
        /// </summary>
        Task StepForward();

        /// <summary>
        /// Seeks backward a fixed amount of seconds of the current MediaFile
        /// </summary>
        Task StepBackward();

        /// <summary>
        /// Seeks to the specified amount of seconds
        /// </summary>
        /// <param name="seconds"></param>
        Task SeekTo(double seconds);

        /// <summary>
        /// Toggles between the different repeat: modes None, RepeatOne and RepeatAll
        /// </summary>
        void ToggleRepeat();

        /// <summary>
        /// Enables or disables shuffling
        /// </summary>
        void ToggleShuffle();

        /// <summary>
        /// Overrides the StepSeconds value from the default 10 seconds
        /// </summary>
        /// <param name="newValue">The new step value in seconds</param>
        void SetStepSeconds(double newValue);
    }
}