using System.ComponentModel;
using Aphorismus.Shared.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace CUClock.Shared.ViewModels;

public partial class ChapterDetail(Capitulo model,
    ILogger logger) : ObservableRecipient
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
            logger.LogInformation(
                "Todos changed, triggering {event}. IsSelected = {isSelected}",
                nameof(TodosSelected), IsSelected);
            TodosSelected?.Invoke(this, new TodosSelectedEventArgs(IsSelected));
        }
        base.OnPropertyChanged(e);
    }
}
