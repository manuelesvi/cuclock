namespace CUClock.Windows.Core.Contracts.Services;

/// <summary>
/// An interface describing a time announcer.
/// </summary>
public interface IAnnouncer : IDisposable
{
    /// <summary>
    /// Announces current time.
    /// </summary>
    /// <param name="sayMilliseconds">
    /// Include milliseconds.
    /// </param>
    void Announce(bool sayMilliseconds = true);

    void SpeakPhrase();

    /// <summary>
    /// Silences audio output.
    /// </summary>
    void Silence();
}
