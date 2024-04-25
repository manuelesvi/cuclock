namespace CUClock.Windows.Core.Contracts.Services;

public interface IAnnouncer : IDisposable
{
    public void Announce();
}