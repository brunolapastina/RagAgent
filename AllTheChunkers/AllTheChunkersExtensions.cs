using System;
using Microsoft.Extensions.DependencyInjection;

namespace AllTheChunkers;

public static class AllTheChunkersExtensions
{
   public static IServiceCollection AddSemanticDoublePassMergingChunker(this IServiceCollection collection) =>
      collection.AddSingleton(service => ActivatorUtilities.CreateInstance<SemanticDoublePassMergingChunker>(service));
}
