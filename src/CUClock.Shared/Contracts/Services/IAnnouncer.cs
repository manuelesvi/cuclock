using Aphorismus.Shared.Entities;
using CUClock.Shared.Services;

namespace CUClock.Shared.Contracts.Services;

/// <summary>
/// An interface describing a time announcer.
/// </summary>
public interface IAnnouncer : IDisposable
{
    /// <summary>
    /// Specifies whether an aphorism should be mentioned within
    /// scheduled tasks.
    /// </summary>
    bool EnableAphorisms
    {
        get; set;
    }

    /// <summary>
    /// Count of Phrases in previous stack
    /// </summary>
    int PreviousCount
    {
        get;
    }

    /// <summary>
    /// Count of Phrases in next stack
    /// </summary>
    int NextCount
    {
        get;
    }

    CancellationToken SilenceToken
    {
        get;
    }

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
    /// Reads aloud the <paramref name="phrase"/>.
    /// </summary>
    /// <param name="frase"></param>
    void SpeakPhrase(Frase phrase);

    /// <summary>
    /// Silences audio output.
    /// </summary>
    void Silence();

    void Previous();

    void Next();

    Announcer.Schedule GetScheduleFor(string cronExpression);
}
