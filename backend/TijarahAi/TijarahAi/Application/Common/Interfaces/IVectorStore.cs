using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Application.Common.Interfaces;

public interface IVectorStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task RemoveDocumentChunksAsync(string documentSource, CancellationToken cancellationToken = default);
    Task SaveChunksAsync(List<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);
    Task<List<KnowledgeChunk>> SearchAsync(Madhab madhab, float[] queryEmbedding, int topK = 4, CancellationToken cancellationToken = default);
    Task<DocumentHash?> GetDocumentHashAsync(string fileName, CancellationToken cancellationToken = default);
    Task SaveDocumentHashAsync(DocumentHash docHash, CancellationToken cancellationToken = default);
    Task<int> GetTotalChunksCountAsync(Madhab? madhab = null);
}
