using Aphorismus.Shared.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Messages;
using Microsoft.Extensions.Logging;
using SemanticSearchSVC = CUClock.Shared.Services.SemanticSearch;

namespace CUClock.Shared.ViewModels;

public partial class SemanticSearch : BaseViewModel,
    IRecipient<SearchResultMessage>
{
    private readonly IAnnouncer _announcer;
    private readonly ILogger<SemanticSearch> _logger;
    private readonly SemanticSearchSVC _searchService;

    public SemanticSearch(
        IAnnouncer announcer,
        SemanticSearchSVC searchService,
        ILogger<SemanticSearch> logger) : base(logger)
    {
        _announcer = announcer;
        _searchService = searchService;
        _logger = logger;
        Search = new RelayCommand<string>(async args =>
        {
            if (await _searchService.SearchChannel.WaitToWriteAsync())
                await _searchService.SearchChannel.WriteAsync(args);
        });
        SpeakPhrase = new RelayCommand<Frase>((args) =>
        {
            _logger.LogInformation(
                $"Speaking phrase: {args.Capitulo.NumeroCapitulo}.{args.ID}");
            _announcer.SpeakPhrase(args);
            _logger.LogInformation("Phrase speaked.");
        });
        Clean = new RelayCommand(() => Phrases = []);
        WeakReferenceMessenger.Default.Register(this);
    }

    [ObservableProperty]
    public partial IList<Frase> Phrases { get; set; }

    public IRelayCommand<string> Search { get; private set; }

    public IRelayCommand Clean { get; private set; }

    public IRelayCommand<Frase> SpeakPhrase { get; private set; }

    public void Receive(SearchResultMessage message)
    {
        _logger.LogInformation("{count} matches found.",
            message.Value.Count());
        Phrases = [.. message.Value];
    }
}
