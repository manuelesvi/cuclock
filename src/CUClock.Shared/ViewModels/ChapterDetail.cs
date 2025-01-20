using System.ComponentModel;
using Aphorismus.Shared.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CUClock.Shared.ViewModels;

public sealed class TodosSelectedEventArgs(bool isSelected) : EventArgs
{
    public bool IsSelected
    {
        get; set;
    } = isSelected;
}

public partial class ChapterDetail(Capitulo model) : ObservableRecipient
{
    [ObservableProperty]
    private bool _isSelected = true;

    public int NumeroCapitulo => model.NumeroCapitulo;

    public string Nombre => model.Nombre;

    public bool IsChapterNumVisible => NumeroCapitulo != 0;

    public event EventHandler<TodosSelectedEventArgs> TodosSelected;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsSelected) && NumeroCapitulo == 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Chapter '{Nombre}' is selected: {IsSelected}");
            TodosSelected?.Invoke(this, new TodosSelectedEventArgs(IsSelected));
        }
        base.OnPropertyChanged(e);
    }
}
