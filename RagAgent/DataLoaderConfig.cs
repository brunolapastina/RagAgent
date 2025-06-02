using System;

namespace RagAgent;

public record DataLoaderConfig
{
   public int DegreeOfParallelism { get; init; } = 1;
   public int MaxTokensPerChunk { get; init; } = 400;
   public required string[] Documents { get; init; }
}
