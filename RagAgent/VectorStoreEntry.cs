using Microsoft.Extensions.VectorData;

namespace RagAgent;

public sealed class VectorStoreEntry
{
   [VectorStoreKey]
   public required int Key { get; set; }

   [VectorStoreData]
   public required string Content { get; set; }

   [VectorStoreVector(1536)]
   public required ReadOnlyMemory<float> Embedding { get; set; }
}
