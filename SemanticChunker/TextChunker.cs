using System.Net.Http.Headers;
using System.Numerics.Tensors;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace SemanticChunker;

public class TextChunker
{
   private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenrator;
   private readonly TextChunkerOptions _options;

   public TextChunker(IEmbeddingGenerator<string, Embedding<float>> embeddingGenrator, TextChunkerOptions options)
   {
      _embeddingGenrator = embeddingGenrator;
      _options = options;
   }

   public async Task<IEnumerable<string>> Slice(string text, CancellationToken cancellationToken)
   {
      var sentenceSlicer = new SentenceSlicer();
      var slices = sentenceSlicer.Slice(text);
      var sliceEmbeddingPairs = (await _embeddingGenrator.GenerateAndZipAsync(slices, null, cancellationToken)).ToList();

      //--[] First pass: classical semantic chunking ]---
      int i = 0;
      bool isInitialMerge = true;
      while (i < sliceEmbeddingPairs.Count - 1)
      {
         if (CalculateTokens(sliceEmbeddingPairs[i].Value) >= _options.MaxLength)
         {  // Reached the token count limit. Start a new chunk
            Console.WriteLine($"Frst {i} -> Reached limit");
            isInitialMerge = true;    // We`ll start a new chunk
            i++;
            continue;
         }

         var score = CompareEmbeddings(sliceEmbeddingPairs[i].Embedding.Vector.Span, sliceEmbeddingPairs[i + 1].Embedding.Vector.Span);
         if (CheckSemanticChunkingScore(score, isInitialMerge))
         {  // Shoud merge, so merge the slices and do it again, with the merged chunk and the next slice
            sliceEmbeddingPairs[i] = await MergeSlicesAndCalculateEmbedding(cancellationToken, sliceEmbeddingPairs[i].Value, sliceEmbeddingPairs[i + 1].Value);
            sliceEmbeddingPairs.RemoveAt(i + 1);
            isInitialMerge = false;    // We`ll continue appending to an existing chunk
            Console.WriteLine($"Frst {i}:{i + 1} -> {score} -> Merged");
         }
         else
         {  // Should not merge. Advance to the next slice and do it all over again
            Console.WriteLine($"Frst {i}:{i + 1} -> {score} -> NOT Merged");
            isInitialMerge = true;     // We`ll start a new chunk
            i++;
         }
      }

      //---[ Second Pass: a semantic merging ]---
      i = 0;
      while (i < sliceEmbeddingPairs.Count - 1)
      {
         if (CalculateTokens(sliceEmbeddingPairs[i].Value) > _options.MaxLength)
         {  // Chunk is already too big. Go to next
            Console.WriteLine($"Scnd {i} -> Reached limit");
            i++;
            continue;
         }

         var score = CompareEmbeddings(sliceEmbeddingPairs[i].Embedding.Vector.Span, sliceEmbeddingPairs[i + 1].Embedding.Vector.Span);
         if (score > _options.MergingThreshold)
         {  // Shoud merge, so merge the slices and do it again, with the merged chunk and the next slice
            sliceEmbeddingPairs[i] = await MergeSlicesAndCalculateEmbedding(cancellationToken, sliceEmbeddingPairs[i].Value, sliceEmbeddingPairs[i + 1].Value);
            sliceEmbeddingPairs.RemoveAt(i + 1);
            Console.WriteLine($"Scnd {i}:{i + 1} -> {score} - > Merged one");
         }
         else if (i + 2 < sliceEmbeddingPairs.Count)
         {  // Should not merge, but there are mor slices, so check the similarity with the next slice
            score = CompareEmbeddings(sliceEmbeddingPairs[i].Embedding.Vector.Span, sliceEmbeddingPairs[i + 2].Embedding.Vector.Span);
            if (score > _options.MergingThreshold)
            {  // Shoud merge, so merge the slices and do it again, with the merged chunk and the next slice
               sliceEmbeddingPairs[i] = await MergeSlicesAndCalculateEmbedding(cancellationToken, sliceEmbeddingPairs[i].Value, sliceEmbeddingPairs[i + 1].Value, sliceEmbeddingPairs[i + 2].Value);
               sliceEmbeddingPairs.RemoveAt(i + 1);
               sliceEmbeddingPairs.RemoveAt(i + 2);
               Console.WriteLine($"Scnd {i}:{i + 2} -> {score} -> Merged two");
            }
            else
            {  // Really should not merge. So leave this chunk as it is and go to the next one
               Console.WriteLine($"Scnd {i}:{i + 2} -> {score} -> NOT merged two");
               i++;
            }
         }
         else
         {  // Should not merge and reached the end
            Console.WriteLine($"Scnd {i}:{i + 1} -> {score} -> NOT merged one");
            i++;
         }
      }

      return sliceEmbeddingPairs.Select(s => s.Value);
   }

   private bool CheckSemanticChunkingScore(float score, bool isInitialMerge)
   {
      var threshold = isInitialMerge ? _options.InitialThreshold : _options.AppendingThreshold;
      return score > threshold;
   }

   private async Task<(string Value, Embedding<float> Embedding)> MergeSlicesAndCalculateEmbedding(CancellationToken cancellationToken, params string[] strs)
   {
      var newValue = string.Join(' ', strs);
      var newEmbedding = await _embeddingGenrator.GenerateAsync(newValue, null, cancellationToken);
      return (Value: newValue, Embedding: newEmbedding);
   }

   private static int CalculateTokens(string str)
   {
      return str.Length;
   }

   private static float CompareEmbeddings(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
   {
      return TensorPrimitives.CosineSimilarity(x, y);
   }
}
