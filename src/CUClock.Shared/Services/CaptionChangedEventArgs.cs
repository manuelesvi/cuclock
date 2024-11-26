namespace CUClock.Shared;

public class CaptionChangedEventArgs(string text) : EventArgs
{
    public string Text
    {
        get;
    } = text;
}