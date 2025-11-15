using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Messages;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;
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

public record ElasticDocument(
    byte Index,
    byte Chapter, byte Phrase, string Text,
    ReadOnlyMemory<float> Embedding);

public class SemanticSearch : BackgroundService
{
    private const byte MatchCount = 50;
    private const string IndexName = "cuclock";

    private readonly IServiceProvider _services;
    private readonly IPhraseProvider _phraseProvider;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<SemanticSearch> _logger;
    private readonly Channel<string> _channel;
    private readonly ElasticsearchClient _elastic;
    private readonly IndexState _index;

    private bool _bulkIngest;
    private Frase[] _phrases;
    private EmbeddingTuple[] _embeddings;

    public SemanticSearch(
        IPhraseProvider phraseProvider,
        ILogger<SemanticSearch> logger,
        IServiceProvider serviceProvider)
    {
        _services = serviceProvider;
        _phraseProvider = phraseProvider;
        _embeddingGenerator = serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        _logger = logger;
        _channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(1)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = true
            });

        var settings = new ElasticsearchClientSettings(new Uri("http://bacanoraX:9200"))
            // .CertificateFingerprint("<FINGERPRINT>")
            .Authentication(new ApiKey("U3RYT2c1b0JKeFVDSUQ2ZkE1bWc6RUtPakxYQmFRdkVEb2JWWGJMdWpMUQ=="));

        _elastic = new ElasticsearchClient(settings);
        var (exists, i) = DoesIndexExist();
        if (!exists)
        {
            var response = _elastic.Indices.Create(IndexName, c => c
                .Mappings(m => m
                    .Properties<ElasticDocument>(p => p
                        .ByteNumber(b => b.Chapter)
                        .ByteNumber(b => b.Phrase)
                        .Text(t => t.Text)
                        .DenseVector(v => v.Embedding, p => p
                            .Dims(384)
                            .Index(true)
                            .Similarity(DenseVectorSimilarity.Cosine)))));

            if (response.IsValidResponse)
            {
                _logger.LogInformation("Index created successfully.");
                _index = FetchIndex();
                _bulkIngest = true;
            }
            else
            {
                var ex = new ApplicationException(
                    $"Failed to create ElasticSearch index. Error: {response.DebugInformation}");
                _logger.LogError(ex, "Failed to create index: {error}", response.DebugInformation);
                throw ex;
            }
        }
        else
        {
            _index = i;
        }

        (bool Exists, IndexState Index) DoesIndexExist()
        {
            var i = _elastic.Indices.Get(new GetIndexRequest(Indices.Index(IndexName)));
            bool exists = i.IsValidResponse && (i.Indices?.ContainsKey(IndexName) ?? false);
            var result = (exists, exists ? i.Indices[IndexName] : null);
            _logger.LogInformation(result.Item1 switch
            {
                true => "Index {name} found.",
                false => "Index {name} NOT found."
            }, IndexName);
            return result;
        }

        IndexState FetchIndex()
        {
            var response = _elastic.Indices.Get(new GetIndexRequest(Indices.Index(IndexName)));
            return response.Indices[IndexName];
        }
    }

    public ChannelWriter<string> SearchChannel => _channel.Writer;

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

    private async Task IndexPhrases()
    {
#if DEBUG
        var sw = new Stopwatch();
        sw.Start();
#endif
        var docs = new ElasticDocument[_phrases.Length];
        foreach (var phrase in _phrases.Index())
        {
            docs[phrase.Index] = ConvertToDoc(phrase.Index, phrase.Item);
        }

        var bulkResponse = await _elastic
            .BulkAsync(b => b.Index(IndexName)
            .CreateMany(docs));

        if (bulkResponse.Errors)
        {
            // Handle errors, iterate through bulkResponse.ItemsWithErrors
            foreach (var itemWithError in bulkResponse.ItemsWithErrors)
            {
                _logger.LogInformation("Error indexing document {id}: {reason}",
                    itemWithError.Id, itemWithError.Error.Reason);
            }
        }
        else
        {
#if DEBUG
            _logger.LogInformation("Bulk insert successful! Finished in: {time}", sw.Elapsed);
#else
            _logger.LogInformation("Bulk insert successful!");
#endif
        }

        ElasticDocument ConvertToDoc(int index, Frase phrase) => new((byte)index,
            (byte)phrase.Capitulo.NumeroCapitulo, (byte)phrase.ID,
            phrase.Texto, _embeddings[index].Embedding.Vector);
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
#if DEBUG
        Debug.Assert(_phrases.Length == _embeddings.Length,
            "# of embbeddings don't match with # of phrases");
        time.Stop();
        _logger.LogInformation("Embeddings generated successfully. Took: {time}", time.Elapsed);
#endif
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
        var queryVector = await _embeddingGenerator.GenerateAsync(query);
        // Use it to query ElasticSearch
        var response = await _elastic.SearchAsync<ElasticDocument>(search => search
            .Indices(IndexName)
            .Query(q => q
                .ScriptScore(ss => ss
                    .Query(qs => qs.Match(m => m
                        .Field(f => f.Text)
                        .Query(query)))
                .Script(sc => sc
                    .Source("cosineSimilarity(params.queryVector, 'embedding') + 1.0")
                    .Params(p => p
                        .Add("queryVector", queryVector.Vector)))))
            .Size(MatchCount));

        var results = new List<Frase>(MatchCount);
        foreach (var document in response.Documents)
        {
            _logger.LogInformation("Phrase {chapter}.{phrase}: {text}",
                document.Chapter, document.Phrase, document.Text);
            results.Add(_phrases[document.Index]);
        }

        // Send message to upper layer(s)
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
