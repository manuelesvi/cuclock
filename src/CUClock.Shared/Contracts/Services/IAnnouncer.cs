namespace CUClock.Shared.Contracts.Services;

/// <summary>
/// An interface describing a time announcer.
/// </summary>
public interface IAnnouncer : IDisposable
{
    event EventHandler<CaptionChangedEventArgs> CaptionChanged;

    /// <summary>
    /// Announces current time.
    /// </summary>
    /// <param name="sayMilliseconds">
    /// Include milliseconds.
    /// </param>
    void Announce(bool sayMilliseconds = true);

    /// <summary>
    /// Speaks a random phrase (no time).
    /// </summary>
    void SpeakPhrase(bool conGallo = true);

    /// <summary>
    /// Silences audio output.
    /// </summary>
    void Silence();
}
