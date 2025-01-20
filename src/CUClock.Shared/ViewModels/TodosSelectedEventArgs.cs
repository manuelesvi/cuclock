namespace CUClock.Shared.ViewModels;

public sealed class TodosSelectedEventArgs(bool isSelected) : EventArgs
{
    public bool IsSelected
    {
        get; set;
    } = isSelected;
}
