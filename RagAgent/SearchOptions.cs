using System;

namespace RagAgent;

public sealed class SearchOptions
{
   /// <summary>
   /// Default number of search results to return.
   /// </summary>
   public static readonly int DefaultTop = 5;

   /// <summary>
   /// Number of search results to return.
   /// </summary>
   public int Top { get; init; } = DefaultTop;

   /// <summary>
   /// The index of the first result to return.
   /// </summary>
   public int Skip { get; init; } = 0;

   /// <summary>
   /// The minimum score a result has to have to be returned
   /// </summary>
   public double MinScore { get; init; } = 0.0f;
}
