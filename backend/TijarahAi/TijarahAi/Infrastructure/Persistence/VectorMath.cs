namespace TijarahAi.Infrastructure.Persistence;

public static class VectorMath
{
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0f;

        float dot = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0f || normB <= 0f)
            return 0f;

        return dot / ((float)Math.Sqrt(normA) * (float)Math.Sqrt(normB));
    }
}
