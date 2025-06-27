using System;

namespace AllTheChunkers;

public class DocumentChunk
{
   public int Index { get; set; }
   
   public string Content { get; set; } = string.Empty;

   public int TokenCount { get; set; }

   public ReadOnlyMemory<float>? Embedding { get; set; }
}
