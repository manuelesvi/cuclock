using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CUClock.Shared.Contracts.Services;
using Microsoft.Extensions.Logging;
using Quartz.Logging;

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

        _fileService.Save(".", FileName, content);
    }

    private void Load()
    {
        Dictionary<int, bool> content;
        try
        {
            content = _fileService.Read<Dictionary<int, bool>>(".", FileName);
        }
        catch
        {
            content = [];
        }

        var todosSelected = content.All(x => x.Value);
        var chapters = new List<ChapterDetail>();
        var todos = new ChapterDetail(this,
            new Capitulo { Nombre = "Todos" }, _logger, todosSelected);
        todos.TodosSelected += Todos_TodosSelected;
        chapters.Add(todos);

        for (var i = 0; i < _phraseProvider.NumberOfChapters; i++)
        {
            chapters.Add(new ChapterDetail(this, new Capitulo
            {
                NumeroCapitulo = i + 1,
                Nombre = _phraseProvider.GetChapterName(i + 1)
            }, _logger, content[i + 1]));
        }
        chapters.Add(todos);
        Items = chapters;
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