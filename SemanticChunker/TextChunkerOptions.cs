using System;

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
}
