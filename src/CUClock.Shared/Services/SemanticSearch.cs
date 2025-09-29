using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CUClock.Shared.Services;

public class SemanticSearch : BackgroundService
{
    private readonly IPhraseProvider _phraseProvider;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<SemanticSearch> _logger;

    public SemanticSearch(
        IPhraseProvider phraseProvider,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<SemanticSearch> logger)
    {
        _phraseProvider = phraseProvider;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var phrases = await GetAllPhrases(_phraseProvider);
        _logger.LogInformation("Generating embeddings...");
        var candidateEmbeddings = await _embeddingGenerator.GenerateAndZipAsync(
            [.. phrases.Select(p => p.Texto)]);
        Debug.Assert(phrases.Count == candidateEmbeddings.Length,
            "# of embbeddings don't match with # of phrases");
        _logger.LogInformation("Embeddings generated successfully.");
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
