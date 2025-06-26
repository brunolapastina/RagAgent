using System;
using Microsoft.Extensions.DependencyInjection;

namespace SemanticChunker;

public static class SemanticChunkerExtensions
{
   public static IServiceCollection AddSemanticChunker(this IServiceCollection collection) =>
      collection.AddSingleton(service => ActivatorUtilities.CreateInstance<TextChunker>(service));
}
