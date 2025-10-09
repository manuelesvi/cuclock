using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Messages;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Numerics.Tensors;
using System.Threading.Channels;

namespace CUClock.Shared.Services;

public class SemanticSearch : BackgroundService
{
    private const byte MatchCount = 50;
    private readonly IPhraseProvider _phraseProvider;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<SemanticSearch> _logger;
    private readonly Channel<string> _channel;

    private Frase[] _phrases;
    private (string Value, Embedding<float> Embedding)[] _embeddings;

    public SemanticSearch(
        IPhraseProvider phraseProvider,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<SemanticSearch> logger)
    {
        _phraseProvider = phraseProvider;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
        _channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(1)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = true
            });
    }

    public ChannelWriter<string> SearchChannel => _channel.Writer;

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // load all phrases
        _phrases = (await GetAllPhrases(_phraseProvider))
            .ToArray();

        _logger.LogInformation("Generating embeddings...");
        _embeddings = await _embeddingGenerator.GenerateAndZipAsync(
            [.. _phrases.Select(p => p.Texto)]);
        
        Debug.Assert(_phrases.Length == _embeddings.Length,
            "# of embbeddings don't match with # of phrases");
        _logger.LogInformation("Embeddings generated successfully.");

        _logger.BeginScope("Listening for search queries...");
        while (true)
        {
            while (!stoppingToken.IsCancellationRequested &&
                await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                // read from channel the query string
                if (_channel.Reader.TryRead(out string query)
                    && !string.IsNullOrWhiteSpace(query))
                {
                    _logger.LogInformation("Received search query: {query}", query);
                    await PerformSearch(query);
                    _logger.LogInformation("Search completed.");
                }
            }
        }
    }

    private async Task PerformSearch(string query)
    {
        // Generate embedding for the user's input.
        var userEmbedding = await _embeddingGenerator.GenerateAsync(query);

        // find matches by similarity
        var matches = _embeddings
            .Index()
            .Where(x => x.Item.Value.Length > 0)
            .Select(embedding => new
            {
                embedding.Index,
                //Distance = TensorPrimitives.Distance(
                //    x.Item.Embedding.Vector.Span,
                //    userEmbedding.Vector.Span),
                Similarity = TensorPrimitives.CosineSimilarity(
                    embedding.Item.Embedding.Vector.Span,
                    userEmbedding.Vector.Span)
            })
            .OrderByDescending(match => match.Similarity)
            .Take(MatchCount);

        var results = new List<Frase>(MatchCount);
        foreach (var m in matches)
        {
            _logger.LogInformation("Similarity: {similarity}", m.Similarity);
            results.Add(_phrases[m.Index]);
        }

        // send message
        WeakReferenceMessenger.Default.Send(
            new SearchResultMessage(results.ToArray()));
        _logger.LogInformation("SearchResultMessage sent.");
    }

    private static async Task<List<Frase>> GetAllPhrases(IPhraseProvider phraseProvider)
    {
        List<Frase> phrases = [];
        var capitulos = phraseProvider.Chapters;
        for (int chapter = 1; chapter <= phraseProvider.NumberOfChapters; chapter++)
        {
            int numberOfPhrases = phraseProvider.GetNumberOfPhrases(chapter);
            var capitulo = capitulos.First(c => c.NumeroCapitulo == chapter);
            for (int phrase = 1; phrase <= numberOfPhrases; phrase++)
            {
                var text = await phraseProvider.GetPhrase(chapter, phrase);
                phrases.Add(new Frase
                {
                    Capitulo = capitulo,
                    ID = phrase,
                    Texto = text
                });
            }
        }
        return phrases;
    }

    public override void Dispose()
    {
        _embeddingGenerator.Dispose();
        base.Dispose();
    }
}
