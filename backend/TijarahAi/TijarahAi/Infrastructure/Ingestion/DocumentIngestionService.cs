using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Application.DTOs;
using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Infrastructure.Ingestion;

public class DocumentIngestionService
{
    private readonly IVectorStore _vectorStore;
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IVectorStore vectorStore,
        IGeminiClient geminiClient,
        ILogger<DocumentIngestionService> logger)
    {
        _vectorStore = vectorStore;
        _geminiClient = geminiClient;
        _logger = logger;
    }

    public async Task<IngestionResponse> IngestDocumentsAsync(string dataDirectory, bool forceRebuild = false, CancellationToken cancellationToken = default)
    {
        var response = new IngestionResponse();

        string hanafiPath = Path.Combine(dataDirectory, "knowledge_base_hanafi.txt");
        string ahleHadithPath = Path.Combine(dataDirectory, "knowledge_base_ahlehadith.txt");

        // 1. Ingest Hanafi KB
        if (File.Exists(hanafiPath))
        {
            int added = await ProcessFileAsync(hanafiPath, Madhab.Hanafi, forceRebuild, response.SkippedFiles, cancellationToken);
            response.HanafiChunksAdded = added;
        }

        // 2. Ingest Ahl-e-Hadith KB
        if (File.Exists(ahleHadithPath))
        {
            int added = await ProcessFileAsync(ahleHadithPath, Madhab.AhleHadith, forceRebuild, response.SkippedFiles, cancellationToken);
            response.AhleHadithChunksAdded = added;
        }

        response.TotalChunksInStore = await _vectorStore.GetTotalChunksCountAsync();
        response.Success = true;
        response.Message = $"Ingestion completed. Added {response.HanafiChunksAdded} Hanafi chunks and {response.AhleHadithChunksAdded} Ahl-e-Hadith chunks. Total in store: {response.TotalChunksInStore}.";

        return response;
    }

    private async Task<int> ProcessFileAsync(
        string filePath,
        Madhab madhab,
        bool forceRebuild,
        List<string> skippedFiles,
        CancellationToken cancellationToken)
    {
        string fileName = Path.GetFileName(filePath);
        string fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        string currentHash = DocumentChunker.ComputeSha256(fileContent);

        var existingHash = await _vectorStore.GetDocumentHashAsync(fileName, cancellationToken);

        // Check if file has changed
        if (!forceRebuild && existingHash != null && existingHash.Sha256Hash == currentHash)
        {
            _logger.LogInformation("File {FileName} is unchanged (SHA256: {Hash}). Skipping embedding recalculation.", fileName, currentHash[..8]);
            skippedFiles.Add($"{fileName} (unchanged)");
            return 0;
        }

        _logger.LogInformation("Chunking and generating embeddings for {FileName} (Madhab: {Madhab})...", fileName, madhab);

        var chunks = DocumentChunker.ChunkStructuredDocument(fileContent, madhab, fileName);
        if (chunks.Count == 0) return 0;

        await _vectorStore.RemoveDocumentChunksAsync(fileName, cancellationToken);

        // Generate embeddings in batches of 10 to respect API rate limits
        int batchSize = 10;
        for (int i = 0; i < chunks.Count; i += batchSize)
        {
            var batch = chunks.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(c => $"{c.StandardOrBook}\n{c.Topic}\n{c.Content}").ToList();

            var embeddings = await _geminiClient.GenerateBatchEmbeddingsAsync(texts, cancellationToken);
            for (int k = 0; k < batch.Count; k++)
            {
                batch[k].Embedding = embeddings[k];
            }

            // Save progress
            await _vectorStore.SaveChunksAsync(batch, cancellationToken);
            _logger.LogInformation("Embedded and saved {Count}/{Total} chunks for {FileName}.", Math.Min(i + batchSize, chunks.Count), chunks.Count, fileName);

            // Small delay to maintain standard free quota
            await Task.Delay(200, cancellationToken);
        }

        // Save updated hash
        await _vectorStore.SaveDocumentHashAsync(new DocumentHash
        {
            FileName = fileName,
            Sha256Hash = currentHash,
            TotalChunks = chunks.Count,
            LastIngestedUtc = DateTime.UtcNow
        }, cancellationToken);

        return chunks.Count;
    }
}
