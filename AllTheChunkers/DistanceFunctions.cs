namespace AllTheChunkers;

public enum DistanceFunctions
{
   /// <summary>Computes the cosine similarity between the two embeddings.</summary>
   CosineSimilarity,

   /// <summary>Computes the distance in Euclidean space.</summary>
   EuclideanDistance,

   /// <sumary>Computes the dot product of two embeddings</summary>
   DotProductSimilarity,
}
