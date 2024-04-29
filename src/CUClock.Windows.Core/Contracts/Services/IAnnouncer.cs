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
    /// Includes milliseconds in announcement.
    /// </param>
    public void Announce(bool sayMilliseconds = true);
}