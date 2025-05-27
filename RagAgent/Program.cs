using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0070, SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace RagChatBot;

public class Program
{
   public static void Main(string[] args)
   {
      var builder = Host.CreateApplicationBuilder(args);
      builder.Configuration.AddUserSecrets<Program>();
      builder.Services.AddHostedService<Worker>();
      builder.Services.AddInMemoryVectorStore();

      var kernel = builder.Services.AddKernel()
          .AddOllamaEmbeddingGenerator("nomic-embed-text", new Uri("http://localhost:11434"))
          .AddOllamaChatCompletion("llama3.2", new Uri("http://localhost:11434"));

      var host = builder.Build();
      host.Run();
   }
}