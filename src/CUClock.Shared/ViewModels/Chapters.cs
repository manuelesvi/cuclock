using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CUClock.Shared.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace CUClock.Shared.ViewModels;

public partial class Chapters : BaseViewModel
{
    private const string FileName = "chapters.json";

    private readonly IFileService _fileService;
    private readonly ILogger<Chapters> _logger;
    private readonly IPhraseProvider _phraseProvider;
    private bool _isBatchSelect;

    [ObservableProperty]
    private IList<ChapterDetail> _items;

    public Chapters(IPhraseProvider phraseProvider,
        IFileService fileService,
        ILogger<Chapters> logger)
    {
        _fileService = fileService;
        _logger = logger;
        _phraseProvider = phraseProvider;
        Load();
    }

    internal void Save()
    {
        if (_isBatchSelect)
        {
            return;
        }

        _logger.LogInformation("Saving chapters");

        var content = new Dictionary<int, bool>();
        foreach (var d in Items)
        {
            if (d.NumeroCapitulo == 0)
            {
                continue;
            }
            content[d.NumeroCapitulo] = d.IsSelected;
        }

        _fileService.Save(FileName, content);
    }

    private void Load()
    {
        // read chapters.json file into content
        Dictionary<int, bool> content;
        try
        {
            content = _fileService
                .Read<Dictionary<int, bool>>(FileName) ?? [];
        }
        catch
        {
            content = [];
        }

        var allIncluded = content.All(x => x.Value);
        var todos = new ChapterDetail(this,
            new Capitulo { Nombre = "Todos" },
            _logger, allIncluded);
        todos.TodosSelected += Todos_TodosSelected;

        var chapters = new List<ChapterDetail>
        {
            todos
        };
        for (var i = 0; i < _phraseProvider.NumberOfChapters; i++)
        {
            var chapter = i + 1;
            chapters.Add(new ChapterDetail(this, new Capitulo
            {
                NumeroCapitulo = chapter,
                Nombre = _phraseProvider.GetChapterName(chapter)
            }, _logger, GetSelectionState(chapter)));
        }
        chapters.Add(todos);
        Items = chapters; // done

        bool GetSelectionState(int chapter)
            => !content.ContainsKey(chapter) || content[chapter];
    }

    private void Todos_TodosSelected(object sender, TodosSelectedEventArgs e)
    {
        _isBatchSelect = true;
        foreach (var d in Items)
        {
            if (d.NumeroCapitulo == 0)
            {
                continue;
            }
            d.IsSelected = e.IsSelected;
        }
        _isBatchSelect = false;
        Save();
        OnPropertyChanged(nameof(Items));
    }
}