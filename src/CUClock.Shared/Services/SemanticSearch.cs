using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Messages;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics.Tensors;
using System.Threading.Channels;

namespace CUClock.Shared.Services;

using EmbeddingTuple = (string Value, Embedding<float> Embedding);

public class SemanticSearch : BackgroundService
{
    private const byte MatchCount = 50;

    private readonly IServiceProvider _services;
    private readonly IPhraseProvider _phraseProvider;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<SemanticSearch> _logger;
    private readonly Channel<string> _channel;

    private Frase[] _phrases;
    private EmbeddingTuple[] _embeddings;

    public SemanticSearch(IServiceProvider services)
    {
        _services = services;
        _phraseProvider = services.GetService<IPhraseProvider>();
        _embeddingGenerator = services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        _logger = services.GetService<ILogger<SemanticSearch>>();
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
        _phrases = [.. await GetAllPhrases(_phraseProvider, _logger)];
        await GenerateEmbeddings();
        _logger.LogInformation("Listening for search queries...");
        while (!stoppingToken.IsCancellationRequested)
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                await ReadChannel();
            }
        }
    }

    private async Task GenerateEmbeddings()
    {
        var time = new Stopwatch();
        time.Start();
        _logger.LogInformation("Generating embeddings...");
        var chunkSize = _phrases.Count() / Environment.ProcessorCount;
        _logger.LogInformation("Parallel.ForEach chunk size: {size} phrases.", chunkSize);
        var chunks = _phrases.Index()
            .Select(p => new
            {
                p.Index,
                p.Item.Texto
            })
            .Chunk(chunkSize)
            .Index();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };
        var results = new ConcurrentDictionary<int, EmbeddingTuple[]>();
        await Parallel.ForEachAsync(chunks, options,
            body: async (chunk, token) =>
            {
                var generator = _services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
                _logger.LogInformation("Processing chunk #{index}. Size = {size}. Thread {threadID}",
                    chunk.Index, chunk.Item.Length, Thread.CurrentThread.ManagedThreadId);
                try
                {
                    results[chunk.Index] = await generator
                        .GenerateAndZipAsync(chunk.Item
                            .Select(x => x.Texto.Length > 256
                                ? x.Texto[..256] : x.Texto), cancellationToken: token);
                    _logger.LogInformation("Chunk #{index} processed. Results Count = {count}. Thread {threadID}",
                        chunk.Index, results.Count, Thread.CurrentThread.ManagedThreadId);
                }
                catch
                {
                    _logger.LogError("Error generating embeddings for chunk #{index}",
                        chunk.Index);
                    foreach (var length in chunk.Item.Index()
                        //.OrderByDescending(x => x.Item.Texto.Length)
                        // .Take(3)
                        .Select(x => new
                        {
                            x.Item.Index,
                            x.Item.Texto.Length,
                            Phrase = _phrases[x.Item.Index]
                        }))
                    {
                        _logger.LogInformation("  Phrase #{index} Length = {length}. Phrase: {capitulo}.{phrase}",
                            length.Index, length.Length,
                            length.Phrase.Capitulo.NumeroCapitulo, length.Phrase.ID);
                        _logger.LogInformation("  Text = {text}", length.Phrase.Texto);
                        try
                        {
                            var text = _phrases[length.Index].Texto;
                            var v = await generator.GenerateAsync(text, cancellationToken: token);
                            Debug.WriteLine("Vector Length: {0}", v.Vector.Length);
                        }
                        catch
                        {
                            _logger.LogError("  Error generating embedding for phrase #{index}. Text Length {length}",
                                length.Index, length.Length);
                            Debugger.Break();
                        }
                    }
                    results[chunk.Index] = [];
                }
                generator.Dispose();
            });
        _embeddings = [];
        for (int i = 0; i < chunks.Count(); i++)
        {
            _embeddings = [.. _embeddings, .. results[i]];
        }
        Debug.Assert(_phrases.Length == _embeddings.Length,
            "# of embbeddings don't match with # of phrases");
        time.Stop();
        _logger.LogInformation("Embeddings generated successfully. Took: {time}", time.Elapsed);
    }

    private async Task ReadChannel()
    {
        // read from channel the query string
        if (_channel.Reader.TryRead(out string query)
            && !string.IsNullOrWhiteSpace(query))
        {
            using var scope = _logger.BeginScope("Semantic Search");
            _logger.LogInformation("Received search query: {query}", query);
            await PerformSearch(query);
            _logger.LogInformation("Search completed.");
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

    private static async Task<List<Frase>> GetAllPhrases(IPhraseProvider phraseProvider,
        ILogger<SemanticSearch> logger)
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
        logger.LogInformation("Total phrases loaded: {count}", phrases.Count);
        return phrases;
    }

    public override void Dispose()
    {
        _embeddingGenerator.Dispose();
        base.Dispose();
    }
}
