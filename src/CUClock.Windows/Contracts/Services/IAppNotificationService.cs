using System.Collections.Specialized;

namespace CUClock.Windows.Contracts.Services;

public interface IAppNotificationService
{
    void Initialize();

    /// <summary>
    /// Shows a Toast notification with <paramref name="payload"/> as content.
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    bool Show(string payload);

    NameValueCollection ParseArguments(string arguments);

    void Unregister();
}
