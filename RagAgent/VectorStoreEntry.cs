using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

namespace RagAgent;

public sealed class VectorStoreEntry
{
   [TextSearchResultName]
   [VectorStoreKey]
   public required int Key { get; set; }

   [TextSearchResultValue]
   [VectorStoreData]
   public required string Content { get; set; }

   [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineDistance)]
   public required ReadOnlyMemory<float> Embedding { get; set; }
}
