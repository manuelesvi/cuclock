using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CUClock.Shared.ViewModels;

public partial class Chapters : BaseViewModel
{
    [ObservableProperty]
    private IList<ChapterDetail> _items;

    public Chapters(IPhraseProvider phraseProvider)
    {
        var chapters = new List<ChapterDetail>();
        var todos = new ChapterDetail(new Capitulo { Nombre = "Todos" });
        todos.TodosSelected += Todos_TodosSelected;
        chapters.Add(todos);
        for (var i = 0; i < phraseProvider.NumberOfChapters; i++)
        {
            chapters.Add(new ChapterDetail(new Capitulo
            {
                NumeroCapitulo = i + 1,
                Nombre = phraseProvider.GetChapterName(i + 1)
            }));
        }
        chapters.Add(todos);
        Items = chapters;
    }

    private void Todos_TodosSelected(object sender, TodosSelectedEventArgs e)
    {
        foreach (var d in Items)
        {
            if (d.NumeroCapitulo == 0)
            {
                continue;
            }
            d.IsSelected = e.IsSelected;
        }

        OnPropertyChanged(nameof(Items));
    }
}