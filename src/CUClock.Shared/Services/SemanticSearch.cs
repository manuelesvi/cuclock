using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Shared.Messages;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace CUClock.Shared.Services;

using EmbeddingTuple = (string Value, Embedding<float> Embedding);
using TextEmbeddingGenerator = IEmbeddingGenerator<string, Embedding<float>>;

/// <summary>
/// Elastic Search document representing a phrase with its embedding.
/// </summary>
/// <param name="Index">
/// Zero-based index of the phrase in the <see cref="SemanticSearch._phrases"/> arrat.
/// </param>
/// <param name="Chapter">
/// Chapter number of the phrase.
/// </param>
/// <param name="Phrase">
/// Phrase number within the chapter.
/// </param>
/// <param name="Text">
/// Text of the phrase.
/// </param>
/// <param name="Embedding">
/// A 384-dimensional dense vector representation of the phrase text.
/// </param>
public record ElasticPhrase(int Index,
    byte Chapter,
    byte Phrase,
    string Text,
    ReadOnlyMemory<float> Embedding
);

public class SemanticSearch : BackgroundService
{
    /// <summary>
    /// Number of results to return for ElasticSearch queries.
    /// </summary>
    private const byte MatchCount = 50;

    /// <summary>
    /// Name of the ElasticSearch index to use for storing phrases and their embeddings.
    /// </summary>
    private const string IndexName = "cuclock";

    // dependencies
    private readonly IServiceProvider _services;
    private readonly IPhraseProvider _phraseProvider;
    private readonly TextEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<SemanticSearch> _logger;
    private readonly ElasticsearchClient _elastic;

    private readonly Channel<string> _channel;

    // local members
    private bool _bulkIngest;
    private Frase[] _phrases;
    private EmbeddingTuple[] _embeddings;

    /// <summary>
    /// Default constructor for a Semantic Search Service.
    /// </summary>
    /// <param name="phraseProvider"></param>
    /// <param name="logger"></param>
    /// <param name="serviceProvider"></param>
    public SemanticSearch(
        IPhraseProvider phraseProvider,
        ILogger<SemanticSearch> logger,
        IServiceProvider serviceProvider)
    {
        _services = serviceProvider;
        _phraseProvider = phraseProvider;
        _embeddingGenerator = serviceProvider.GetService<TextEmbeddingGenerator>();
        _logger = logger;
        // create bounded channel to read search queries
        _channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(1)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = true
            });

        // TODO: move to Program.cs to register as a dependency in ServiceCollection and inject it in constructor
        var settings = new ElasticsearchClientSettings(new Uri("http://bacanoraX:9200"))
            // NOTE: include certificate fingerprint if needed (https)
            // .CertificateFingerprint("<FINGERPRINT>")
            .Authentication(new ApiKey("U3RYT2c1b0JKeFVDSUQ2ZkE1bWc6RUtPakxYQmFRdkVEb2JWWGJMdWpMUQ=="));
        _elastic = new ElasticsearchClient(settings);
        if (!IndexExists)
        {
            CreateIndex();
        }
    }

    public ChannelWriter<string> SearchChannel => _channel.Writer;

    private bool IndexExists
    {
        get
        {
            var i = _elastic.Indices.Get(new GetIndexRequest(Indices.Index(IndexName)));
            bool exists = i.IsValidResponse && (i.Indices?.ContainsKey(IndexName) ?? false);
            _logger.LogInformation(exists switch
            {
                true => "Index {name} found.",
                false => "Index {name} NOT found."
            }, IndexName);
            return exists;
        }
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _phrases = [.. await GetAllPhrases(_phraseProvider, _logger)];
        if (_bulkIngest)
        {
            await GenerateEmbeddings();
            await IndexPhrases();
        }
        _logger.LogInformation("Listening for search queries...");
        while (!stoppingToken.IsCancellationRequested)
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                await ReadChannel();
            }
        }
    }

    private void CreateIndex()
    {
        var response = _elastic.Indices.Create(IndexName, c => c
        .Mappings(m => m
            .Properties<ElasticPhrase>(p => p
                .IntegerNumber(b => b.Index) // 889 phrases, so int is fine
                .ByteNumber(b => b.Chapter) // 31 chapters, so byte is fine
                .ByteNumber(b => b.Phrase) // max 255 phrases per chapter, so byte is fine
                .Text(t => t.Text) // store the phrase for full-text search
                .DenseVector(v => v.Embedding, p => p // 384-dimensional embedding vector (all-MiniLM)
                    .Dims(384)
                    .Index(true)
                    .Similarity(DenseVectorSimilarity.Cosine)))));

        if (!response.IsValidResponse)
        {
            var ex = new ApplicationException(
                $"Failed to create ElasticSearch index. Error: {response.DebugInformation}");
            _logger.LogError(ex, "Failed to create index: {error}", response.DebugInformation);
            throw ex;
        }

        _logger.LogInformation("Index created successfully.");
        _bulkIngest = true;
    }

    private async Task IndexPhrases()
    {
#if DEBUG
        var sw = new Stopwatch();
        sw.Start();
#endif
        var documents = GetDocuments();
        var bulkResponse = await _elastic
            .BulkAsync(b => b.Index(IndexName)
            .CreateMany(documents));
        if (!bulkResponse.Errors)
        {
#if DEBUG
            _logger.LogInformation("Bulk insert successful! Finished in: {time}", sw.Elapsed);
#else
            _logger.LogInformation("Bulk insert successful!");
#endif
            return;
        }

        // Handle errors, iterate through bulkResponse.ItemsWithErrors
        foreach (var itemWithError in bulkResponse.ItemsWithErrors)
        {
            _logger.LogInformation("Error indexing document {id}: {reason}",
                itemWithError.Id, itemWithError.Error.Reason);
        }
        
        // Local functions
        IEnumerable<ElasticPhrase> GetDocuments() => _phrases
            .Index()
            .Select(p =>
                ConvertToDoc(p.Index, p.Item));

        ElasticPhrase ConvertToDoc(int index, Frase phrase) => new(index,
            Chapter: (byte)phrase.Capitulo.NumeroCapitulo,
            Phrase: (byte)phrase.ID,
            Text: phrase.Texto,
            _embeddings[index].Embedding.Vector);
    }

    private async Task GenerateEmbeddings()
    {
#if DEBUG
        var time = new Stopwatch();
        time.Start();
#endif
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
        await Parallel.ForEachAsync(chunks, options, body: async (chunk, token) =>
        {
            var generator = _services.GetService<TextEmbeddingGenerator>();
            _logger.LogInformation(
                "Processing chunk #{index}. Size = {size}. Thread {threadID}",
                chunk.Index, chunk.Item.Length, Thread.CurrentThread.ManagedThreadId);
            try
            {
                results[chunk.Index] = await generator
                    .GenerateAndZipAsync(chunk.Item
                        .Select(x => x.Texto.Length > 256
                            ? x.Texto[..256] : x.Texto), cancellationToken: token);
                _logger.LogInformation(
                    "Chunk #{index} processed. Results Count = {count}. Thread {threadID}",
                    chunk.Index, results.Count, Thread.CurrentThread.ManagedThreadId);
            }
            catch
            {
                _logger.LogError("Error generating embeddings for chunk #{index}",
                    chunk.Index);
                foreach (var length in chunk.Item.Index()
                    .Select(x => new
                    {
                        x.Item.Index,
                        x.Item.Texto.Length,
                        Phrase = _phrases[x.Item.Index]
                    }))
                {
                    _logger.LogInformation(
                        "  Phrase #{index} Length = {length}. Phrase: {capitulo}.{phrase}",
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
                        _logger.LogError(
                            "  Error generating embedding for phrase #{index}. Text Length {length}",
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
#if DEBUG
        Debug.Assert(_phrases.Length == _embeddings.Length,
            "# of embbeddings don't match with # of phrases");
        time.Stop();
        _logger.LogInformation("Embeddings generated successfully. Took: {time}",
            time.Elapsed);
#endif
    }

    private async Task ReadChannel()
    {
        // read from channel the query string
        if (_channel.Reader.TryRead(out string query)
            && !string.IsNullOrWhiteSpace(query))
        {
            using var scope = _logger.BeginScope("Semantic Search Query received.");
            _logger.LogInformation("Query: '{query}'.", query);
            var msg = await PerformSearch(query);
            _logger.LogInformation("Search completed.");

            var results = msg.Value.ToArray();
            _logger.LogInformation  ("Found {count} results.", results.Length);
        }
    }

    /// <summary>
    /// Performs a search using the specified query string in <paramref name="query"/>,
    /// combining semantic and full-text relevance to retrieve matching phrases.
    /// </summary>
    /// <remarks>
    /// This method uses both semantic vector similarity and full-text matching to rank results,
    /// with a weighted combination favoring semantic relevance.
    /// Upon completion, the search results are sent to higher-level
    /// components via a <see cref="SearchResultMessage"/>.
    /// </remarks>
    /// <param name="query">The search query string used to find relevant phrases. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous search operation.</returns>
    private async Task<SearchResultMessage> PerformSearch(string query)
    {
        // generate embedding for input query
        var queryEmbedding = await _embeddingGenerator.GenerateAsync(query);
        var queryVector = queryEmbedding.Vector;
#if DEBUG
        var timer = new Stopwatch();
        timer.Start();
#endif
        // 75% semantic search, 25% full-text search
        var response = await _elastic.SearchAsync<ElasticPhrase>(search => search
            .Indices(IndexName)
            .Query(q => q.ScriptScore(ss => ss
                .Query(qs => qs.Match(m => m
                    .Field(f => f.Text)
                    .Query(query)
                ))
                .Script(sc => sc
                    // Combine text relevance (_score) with vector similarity & adjust the weighting;
                    // cosines' range goes between -1 and 1, add 1 to avoid NEGATIVE scores:
                    .Source(
                    "0.75 * (cosineSimilarity(params.query, 'embedding') + 1) + 0.25 * _score")                    
                    .Params(p => p
                        .Add("query", queryVector)
                ))
            ))
            .Size(MatchCount));
#if DEBUG
        timer.Stop();
        _logger.LogInformation("Search query executed in: {time}.",
            timer.Elapsed);
#endif
        _logger.LogInformation("ElasticSearch returned {total} results.",
            response.Total);
        var results = new Frase[((int)response.Total)];
        foreach (var document in response.Documents.Index())
        {
            var phrase = document.Item;
            _logger.LogInformation("Phrase {chapter}.{phrase}: {text}",
                phrase.Chapter, phrase.Phrase, phrase.Text);
            results[document.Index] = _phrases[phrase.Index];
        }

        var msg = new SearchResultMessage(results);
        // Send message to upper layer(s)
        WeakReferenceMessenger.Default.Send(msg);
        _logger.LogInformation("SearchResultMessage sent.");
        return msg;
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
