namespace CUClock.Windows.Core;

public class CaptionChangedEventArgs(string text) : EventArgs
{
    public string Text
    {
        get; init;
    } = text;
}

