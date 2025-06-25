using System;
using System.Diagnostics;

namespace SemanticChunker;

public class TextChunkerOptions
{
   /// <summary>
   /// Specifies the similarity needed for initial sentences to form a new chunk. A higher value creates more focused chunks but may result in smaller chunks.
   /// </summary>
   public float InitialThreshold { get; set; }

   /// <summary>
   /// Determines the minimum similarity required for adding sentences to an existing chunk. A higher value promotes cohesive chunks but may result in fewer sentences being added.
   /// </summary>
   public float AppendingThreshold { get; set; }

   /// <summary>
   /// Sets the similarity level for merging chunks. Higher value consolidates related chunks but risks merging unrelated ones.
   /// </summary>
   public float MergingThreshold { get; set; }

   /// <summary>
   /// Maximum number of tokens allowed in a chunk
   /// </summary>
   public int MaxLength { get; set; }

   public delegate int TokenCounterDelegate(string input);

   /// <summary>
   /// A function to be used to count the number of tokens in a string
   /// </summary>
   public TokenCounterDelegate TokenCounter { get; set; } = DefaultTokenCounter;

   /// <summary>
   /// A default function to count the number of tokens in a string
   /// It only devides the length by 4
   /// </summary>
   private static int DefaultTokenCounter(string input)
   {
      return input.Length >> 2;
   }

   public enum DistanceFunctions
   {
      /// <summary>Computes the cosine similarity between the two embeddings.</summary>
      CosineSimilarity,

      /// <summary>Computes the distance in Euclidean space.</summary>
      EuclideanDistance,

      /// <sumary>Computes the dot product of two embeddings</summary>
      DotProductSimilarity,
   }

   public DistanceFunctions DistanceFunction { get; set; } = DistanceFunctions.CosineSimilarity;
}
