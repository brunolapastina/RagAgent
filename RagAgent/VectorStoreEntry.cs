using Microsoft.Extensions.VectorData;

namespace RagAgent;

public sealed class VectorStoreEntry
{
   [VectorStoreKey]
   public required int Key { get; set; }

   [VectorStoreData]
   public required string Content { get; set; }

   [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineDistance)]
   public required ReadOnlyMemory<float> Embedding { get; set; }
}
