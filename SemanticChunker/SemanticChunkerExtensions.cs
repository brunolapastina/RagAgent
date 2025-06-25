using System;
using Microsoft.Extensions.DependencyInjection;

namespace SemanticChunker;

public static class SemanticChunkerExtensions
{
   public static IServiceCollection UseSemanticChunker(this IServiceCollection collection) =>
      collection.UseSemanticChunker(new TextChunkerOptions());

   public static IServiceCollection UseSemanticChunker(this IServiceCollection collection, TextChunkerOptions options) =>
      collection.AddSingleton(service => ActivatorUtilities.CreateInstance<TextChunker>(service, options));
}
