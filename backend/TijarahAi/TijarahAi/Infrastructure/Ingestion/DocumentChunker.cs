using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Infrastructure.Ingestion;

public class DocumentChunker
{
    public static string ComputeSha256(string content)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    public static List<KnowledgeChunk> ChunkStructuredDocument(string rawContent, Madhab madhab, string sourceFileName)
    {
        var chunks = new List<KnowledgeChunk>();
        string docHash = ComputeSha256(rawContent);

        // Split by standard section dividers (=== or --- or [STANDARD]: or [CHAPTER]:)
        var sections = Regex.Split(rawContent, @"(?=(?:\[STANDARD\]:|\[CHAPTER\]:|\[SECTION\]:))", RegexOptions.Multiline)
            .Where(sec => Regex.IsMatch(sec, @"(?:\[STANDARD\]:|\[CHAPTER\]:|\[SECTION\]:)", RegexOptions.IgnoreCase))
            .ToList();

        foreach (var sec in sections)
        {
            if (string.IsNullOrWhiteSpace(sec) || sec.Trim().Length < 50)
                continue;

            string standardOrBook = ExtractHeader(sec, @"(?:\[STANDARD\]|\[CHAPTER\]|\[BOOK\]):\s*([^\r\n]+)");
            string topic = ExtractHeader(sec, @"\[CORE TOPIC\]:\s*([^\r\n]+)");
            string citations = ExtractHeader(sec, @"\[EVIDENCE\]:\s*([\s\S]+?)(?=\n\[|\Z)");

            // If section is moderately sized, keep as coherent chunk. If oversized, split cleanly.
            if (sec.Length <= 2000)
            {
                chunks.Add(new KnowledgeChunk
                {
                    Madhab = madhab,
                    DocumentSource = sourceFileName,
                    StandardOrBook = string.IsNullOrWhiteSpace(standardOrBook) ? "Islamic Jurisprudence Source" : standardOrBook,
                    Topic = string.IsNullOrWhiteSpace(topic) ? "Commercial Transactions" : topic,
                    SectionTitle = standardOrBook,
                    Content = sec.Trim(),
                    Citations = citations.Trim(),
                    DocumentHash = docHash
                });
            }
            else
            {
                // Sub-chunk by paragraphs
                var paragraphs = sec.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                var currentBatch = new StringBuilder();

                foreach (var p in paragraphs)
                {
                    if (currentBatch.Length + p.Length > 1500)
                    {
                        chunks.Add(new KnowledgeChunk
                        {
                            Madhab = madhab,
                            DocumentSource = sourceFileName,
                            StandardOrBook = standardOrBook,
                            Topic = topic,
                            SectionTitle = standardOrBook,
                            Content = currentBatch.ToString().Trim(),
                            Citations = citations.Trim(),
                            DocumentHash = docHash
                        });
                        currentBatch.Clear();
                    }
                    currentBatch.AppendLine(p);
                }

                if (currentBatch.Length > 0)
                {
                    chunks.Add(new KnowledgeChunk
                    {
                        Madhab = madhab,
                        DocumentSource = sourceFileName,
                        StandardOrBook = standardOrBook,
                        Topic = topic,
                        SectionTitle = standardOrBook,
                        Content = currentBatch.ToString().Trim(),
                        Citations = citations.Trim(),
                        DocumentHash = docHash
                    });
                }
            }
        }

        return chunks;
    }

    private static string ExtractHeader(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
