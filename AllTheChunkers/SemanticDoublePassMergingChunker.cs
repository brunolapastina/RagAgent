using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AllTheChunkers;

public class SemanticDoublePassMergingChunker
{
   private readonly ILogger<SemanticDoublePassMergingChunker> _logger;
   private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenrator;

   public SemanticDoublePassMergingChunker(ILogger<SemanticDoublePassMergingChunker> logger, IEmbeddingGenerator<string, Embedding<float>> embeddingGenrator)
   {
      _logger = logger;
      _embeddingGenrator = embeddingGenrator;
   }

   public async Task<IEnumerable<string>> Slice(string text, SemanticDoublePassMergingChunkerOptions options, CancellationToken cancellationToken)
   {
      var slices = SentenceSlicer.Slice(text);
      var sliceEmbeddingPairs = (await _embeddingGenrator.GenerateAndZipAsync(slices, null, cancellationToken)).ToList();

      //--[] First pass: classical semantic chunking ]---
      int i = 0;
      bool isInitialMerge = true;
      while (i < sliceEmbeddingPairs.Count - 1)
      {
         if (options.TokenCounter(sliceEmbeddingPairs[i].Value) >= options.MaxLength)
         {  // Reached the token count limit. Start a new chunk
            _logger.LogTrace("[First pass] Chunk {ChunkNumber} -> Reached limit", i);
            isInitialMerge = true;    // We`ll start a new chunk
            i++;
            continue;
         }

         var score = CompareEmbeddings(sliceEmbeddingPairs[i].Embedding.Vector.Span, sliceEmbeddingPairs[i + 1].Embedding.Vector.Span, options.DistanceFunction);
         if (CheckSemanticChunkingScore(score, isInitialMerge, options))
         {  // Shoud merge, so merge the slices and do it again, with the merged chunk and the next slice
            sliceEmbeddingPairs[i] = await MergeSlicesAndCalculateEmbedding(cancellationToken, sliceEmbeddingPairs[i].Value, sliceEmbeddingPairs[i + 1].Value);
            sliceEmbeddingPairs.RemoveAt(i + 1);
            isInitialMerge = false;    // We`ll continue appending to an existing chunk
            _logger.LogTrace("[First pass] Chunks {ChunkNumberBegin}:{ChunkNumberEnd} -> {Score} -> Merged", i, i + 1, score);
         }
         else
         {  // Should not merge. Advance to the next slice and do it all over again
            _logger.LogTrace("[First pass] Chunks {ChunkNumberBegin}:{ChunkNumberEnd} -> {Score} -> NOT Merged", i, i + 1, score);
            isInitialMerge = true;     // We`ll start a new chunk
            i++;
         }
      }

      //---[ Second Pass: a semantic merging ]---
      i = 0;
      while (i < sliceEmbeddingPairs.Count - 1)
      {
         if (options.TokenCounter(sliceEmbeddingPairs[i].Value) > options.MaxLength)
         {  // Chunk is already too big. Go to next
            _logger.LogTrace("First pass] Chunk {ChunkNumber} -> Reached limit", i);
            i++;
            continue;
         }

         var score = CompareEmbeddings(sliceEmbeddingPairs[i].Embedding.Vector.Span, sliceEmbeddingPairs[i + 1].Embedding.Vector.Span, options.DistanceFunction);
         if (score > options.MergingThreshold)
         {  // Shoud merge, so merge the slices and do it again, with the merged chunk and the next slice
            sliceEmbeddingPairs[i] = await MergeSlicesAndCalculateEmbedding(cancellationToken, sliceEmbeddingPairs[i].Value, sliceEmbeddingPairs[i + 1].Value);
            sliceEmbeddingPairs.RemoveAt(i + 1);
            _logger.LogTrace("[Second pass] Chunks {ChunkNumberBegin}:{ChunkNumberEnd} -> {Score} - > Merged one", i, i + 1, score);
         }
         else if (i + 2 < sliceEmbeddingPairs.Count)
         {  // Should not merge, but there are mor slices, so check the similarity with the next slice
            score = CompareEmbeddings(sliceEmbeddingPairs[i].Embedding.Vector.Span, sliceEmbeddingPairs[i + 2].Embedding.Vector.Span, options.DistanceFunction);
            if (score > options.MergingThreshold)
            {  // Shoud merge, so merge the slices and do it again, with the merged chunk and the next slice
               sliceEmbeddingPairs[i] = await MergeSlicesAndCalculateEmbedding(cancellationToken, sliceEmbeddingPairs[i].Value, sliceEmbeddingPairs[i + 1].Value, sliceEmbeddingPairs[i + 2].Value);
               sliceEmbeddingPairs.RemoveAt(i + 1);
               sliceEmbeddingPairs.RemoveAt(i + 2);
               _logger.LogTrace("[Second pass] Chunks {ChunkNumberBegin}:{ChunkNumberEnd} -> {Score} -> Merged two", i, i + 2, score);
            }
            else
            {  // Really should not merge. So leave this chunk as it is and go to the next one
               _logger.LogTrace("[Second pass] Chunks {ChunkNumberBegin}:{ChunkNumberEnd} -> {Score} -> NOT merged two", i, i + 2, score);
               i++;
            }
         }
         else
         {  // Should not merge and reached the end
            _logger.LogTrace("[Second pass] Chunks {ChunkNumberBegin}:{ChunkNumberEnd} -> {Score} -> NOT merged one", i, i + 1, score);
            i++;
         }
      }

      return sliceEmbeddingPairs.Select(s => s.Value);
   }

   private static bool CheckSemanticChunkingScore(float score, bool isInitialMerge, SemanticDoublePassMergingChunkerOptions options)
   {
      var threshold = isInitialMerge ? options.InitialThreshold : options.AppendingThreshold;
      return score > threshold;
   }

   private async Task<(string Value, Embedding<float> Embedding)> MergeSlicesAndCalculateEmbedding(CancellationToken cancellationToken, params string[] strs)
   {
      var newValue = string.Join(' ', strs);
      var newEmbedding = await _embeddingGenrator.GenerateAsync(newValue, null, cancellationToken);
      return (Value: newValue, Embedding: newEmbedding);
   }

   private static float CompareEmbeddings(ReadOnlySpan<float> x, ReadOnlySpan<float> y, DistanceFunctions distanceFunction) =>
      distanceFunction switch
      {
         DistanceFunctions.CosineSimilarity => TensorPrimitives.CosineSimilarity(x, y),
         DistanceFunctions.DotProductSimilarity => TensorPrimitives.Dot(x, y),
         DistanceFunctions.EuclideanDistance => TensorPrimitives.Distance(x, y),
         _ => throw new NotSupportedException($"The distance function '{distanceFunction}' is not supported")
      };
}
