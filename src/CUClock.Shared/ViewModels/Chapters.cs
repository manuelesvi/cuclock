using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CUClock.Shared.ViewModels;

public partial class Chapters : BaseViewModel
{
    [ObservableProperty]
    private IList<Capitulo> _items;

    public Chapters(IPhraseProvider phraseProvider)
    {
        var chapters = new List<Capitulo>();
        for (var i = 0; i < phraseProvider.NumberOfChapters; i++)
        {
            chapters.Add(new Capitulo
            {
                NumeroCapitulo = i + 1,
                Nombre = phraseProvider.GetChapterName(i + 1)
            });
        }
        Items = chapters;
    }
}