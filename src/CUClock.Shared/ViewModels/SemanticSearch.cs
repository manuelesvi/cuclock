using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SemanticSearchSVC = CUClock.Shared.Services.SemanticSearch;

namespace CUClock.Shared.ViewModels;

public partial class SemanticSearch : BaseViewModel,
    IRecipient<SearchResultMessage>
{
    private readonly ILogger<SemanticSearch> _logger;
    private readonly SemanticSearchSVC _searchService;

    public SemanticSearch(
        SemanticSearchSVC searchService,
        ILogger<SemanticSearch> logger) : base(logger)
    {
        _searchService = searchService;
        _logger = logger;
        Search = new RelayCommand<string>(async args =>
        {
            if (await _searchService.SearchChannel.WaitToWriteAsync())
                await _searchService.SearchChannel.WriteAsync(args);
        });
        Clean = new RelayCommand(() => Phrases = []);
        WeakReferenceMessenger.Default.Register(this);
    }

    [ObservableProperty]
    public partial IList<Frase> Phrases { get; set; }

    public IRelayCommand<string> Search { get; private set; }

    public IRelayCommand Clean { get; private set; }

    public void Receive(SearchResultMessage message)
    {
        _logger.LogInformation("{count} matches found.",
            message.Value.Count);
        Phrases = message.Value;
    }
}
