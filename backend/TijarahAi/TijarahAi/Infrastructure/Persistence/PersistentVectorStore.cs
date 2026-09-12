using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Infrastructure.Persistence;

public class PersistentVectorStore : IVectorStore
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PersistentVectorStore> _logger;
    private readonly string _localJsonPath;
    private readonly float _minimumSimilarity;
    private readonly List<KnowledgeChunk> _memoryCache = new();
    private readonly Dictionary<string, DocumentHash> _hashCache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isInitialized = false;

    public PersistentVectorStore(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<PersistentVectorStore> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _localJsonPath = _configuration["VectorStore:JsonStoragePath"] ?? "data/knowledge_base_vectors.json";
        _minimumSimilarity = _configuration.GetValue("VectorStore:MinimumSimilarity", 0.55f);
    }

    public async Task RemoveDocumentChunksAsync(string documentSource, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _memoryCache.RemoveAll(c => string.Equals(c.DocumentSource, documentSource, StringComparison.OrdinalIgnoreCase));

            string? connString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connString))
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TijarahDbContext>();
                var dbChunks = await db.KnowledgeChunks
                    .Where(c => c.DocumentSource == documentSource)
                    .ToListAsync(cancellationToken);
                db.KnowledgeChunks.RemoveRange(dbChunks);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            // Strategy 1: Check PostgreSQL if ConnectionString is available
            string? connString = _configuration.GetConnectionString("DefaultConnection");
            bool dbSuccess = false;

            if (!string.IsNullOrWhiteSpace(connString))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TijarahDbContext>();
                    await db.Database.EnsureCreatedAsync(cancellationToken);

                    var dbChunks = await db.KnowledgeChunks.AsNoTracking().ToListAsync(cancellationToken);
                    if (dbChunks.Count > 0)
                    {
                        _memoryCache.Clear();
                        _memoryCache.AddRange(dbChunks);

                        var hashes = await db.DocumentHashes.AsNoTracking().ToListAsync(cancellationToken);
                        foreach (var h in hashes)
                            _hashCache[h.FileName] = h;

                        _logger.LogInformation("Loaded {Count} knowledge chunks from PostgreSQL database.", _memoryCache.Count);
                        dbSuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to query PostgreSQL vector store. Falling back to persistent JSON storage.");
                }
            }

            // Strategy 2: If DB was empty or not configured, load from persisted local JSON file
            if (!dbSuccess && File.Exists(_localJsonPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(_localJsonPath, cancellationToken);
                    var persistedData = JsonSerializer.Deserialize<PersistedDataModel>(json);
                    if (persistedData != null)
                    {
                        _memoryCache.Clear();
                        _memoryCache.AddRange(persistedData.Chunks);
                        foreach (var h in persistedData.Hashes)
                            _hashCache[h.FileName] = h;

                        _logger.LogInformation("Loaded {Count} knowledge chunks from persistent JSON file '{Path}'.", _memoryCache.Count, _localJsonPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading persistent JSON vector file '{Path}'.", _localJsonPath);
                }
            }

            _isInitialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveChunksAsync(List<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Update in-memory cache
            foreach (var chunk in chunks)
            {
                var existing = _memoryCache.FirstOrDefault(c => c.Id == chunk.Id);
                if (existing != null)
                    _memoryCache.Remove(existing);
                _memoryCache.Add(chunk);
            }

            // Persist to PostgreSQL if configured
            string? connString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connString))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TijarahDbContext>();
                    await db.Database.EnsureCreatedAsync(cancellationToken);

                    foreach (var chunk in chunks)
                    {
                        var existing = await db.KnowledgeChunks.FindAsync(new object[] { chunk.Id }, cancellationToken);
                        if (existing != null)
                            db.Entry(existing).CurrentValues.SetValues(chunk);
                        else
                            await db.KnowledgeChunks.AddAsync(chunk, cancellationToken);
                    }
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist chunks to PostgreSQL. Saving to local JSON file.");
                }
            }

            // Persist to JSON file for zero-delay instant restarts and file persistence
            var dataToSave = new PersistedDataModel
            {
                Chunks = _memoryCache,
                Hashes = _hashCache.Values.ToList()
            };

            string directory = Path.GetDirectoryName(_localJsonPath) ?? "data";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string serialized = JsonSerializer.Serialize(dataToSave, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(_localJsonPath, serialized, cancellationToken);
            _logger.LogInformation("Saved {Count} chunks to persistent storage '{Path}'.", chunks.Count, _localJsonPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<KnowledgeChunk>> SearchAsync(Madhab madhab, float[] queryEmbedding, int topK = 4, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        // Filter chunks strictly by madhab to prevent cross-contamination
        var candidates = _memoryCache
            .Where(c => c.Madhab == madhab &&
                        !string.Equals(c.StandardOrBook, "Islamic Jurisprudence Source", StringComparison.OrdinalIgnoreCase) &&
                        c.Embedding != null &&
                        c.Embedding.Length > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogWarning("No vector candidates found for Madhab: {Madhab}", madhab);
            return new List<KnowledgeChunk>();
        }

        // Rank by Cosine Similarity
        var ranked = candidates
            .Select(c => new
            {
                Chunk = c,
                Score = VectorMath.CosineSimilarity(queryEmbedding, c.Embedding)
            })
            .Where(x => x.Score >= _minimumSimilarity)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} relevant {Madhab} chunks using minimum similarity {MinimumSimilarity}.",
            ranked.Count,
            madhab,
            _minimumSimilarity);

        return ranked;
    }

    public async Task<DocumentHash?> GetDocumentHashAsync(string fileName, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        _hashCache.TryGetValue(fileName, out var docHash);
        return docHash;
    }

    public async Task SaveDocumentHashAsync(DocumentHash docHash, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        _hashCache[docHash.FileName] = docHash;

        // Persist
        var dataToSave = new PersistedDataModel
        {
            Chunks = _memoryCache,
            Hashes = _hashCache.Values.ToList()
        };
        string directory = Path.GetDirectoryName(_localJsonPath) ?? "data";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(_localJsonPath, JsonSerializer.Serialize(dataToSave), cancellationToken);
    }

    public async Task<int> GetTotalChunksCountAsync(Madhab? madhab = null)
    {
        await InitializeAsync();
        if (madhab.HasValue)
            return _memoryCache.Count(c => c.Madhab == madhab.Value);
        return _memoryCache.Count;
    }

    private class PersistedDataModel
    {
        public List<KnowledgeChunk> Chunks { get; set; } = new();
        public List<DocumentHash> Hashes { get; set; } = new();
    }
}
