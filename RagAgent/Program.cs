using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0070, SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace RagAgent;

public class Program
{
   protected Program()
   {
      // This constructor is intentionally empty.
      // It is used to prevent instantiation of the Program class.
      // The Main method is static and serves as the entry point for the application.
   }

   public static void Main(string[] args)
   {
      var builder = Host.CreateApplicationBuilder(args);
      builder.Configuration.AddUserSecrets<Program>();
      builder.Services.AddHostedService<Worker>();
      builder.Services.AddInMemoryVectorStore();

      var embeddingConfig = builder.Configuration.GetSection("EmbeddingService").Get<ModelConfig>() ??
         throw new InvalidOperationException("EmbeddingGenerator configuration is missing.");

      var chatConfig = builder.Configuration.GetSection("ChatService").Get<ModelConfig>() ??
         throw new InvalidOperationException("EmbeddingGenerator configuration is missing.");

      builder.Services.AddKernel()
         .AddOllamaEmbeddingGenerator(embeddingConfig.ModelId, embeddingConfig.EndpointUri)
         .AddOllamaChatCompletion(chatConfig.ModelId, embeddingConfig.EndpointUri);

      var host = builder.Build();
      host.Run();
   }
}