using System;

namespace RagAgent;

public record DataLoaderConfig
{
   public required string[] Documents { get; init; }
}
