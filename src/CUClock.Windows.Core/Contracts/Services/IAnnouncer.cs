namespace CUClock.Windows.Core.Contracts.Services;

/// <summary>
/// An interface describing an announcer.
/// </summary>
public interface IAnnouncer : IDisposable
{
    /// <summary>
    /// Announces the current time.
    /// </summary>
    /// <param name="sayMilliseconds">
    /// Include milliseconds in the announcement.
    /// </param>
    public void Announce(bool sayMilliseconds = true);

    /// <summary>
    /// Silences ALL audio output.
    /// </summary>
    public void Silence();
}
