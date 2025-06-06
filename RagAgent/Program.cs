using Microsoft.Extensions.Http.Resilience;
using Microsoft.SemanticKernel;
using Serilog;

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
      var configuration = new ConfigurationBuilder()
         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
         .Build();

      Log.Logger = new LoggerConfiguration()
         .ReadFrom.Configuration(configuration)
         .CreateLogger();

      try
      {
         Log.Information("Starting RagAgent application...");

         var builder = Host.CreateApplicationBuilder(args);
         builder.Services.AddSerilog(Log.Logger);
         builder.Services.AddSingleton<DataLoader>();
         builder.Services.AddHostedService<ChatWorker>();
         builder.Services.AddInMemoryVectorStore();
         builder.Services.AddHttpClient().ConfigureHttpClientDefaults(conf =>
         {
            conf.AddStandardResilienceHandler().Configure(o =>
            {
               o.AttemptTimeout = new HttpTimeoutStrategyOptions
               {
                  Timeout = TimeSpan.FromMinutes(3),
                  Name = "AttemptTimeout"
               };
               o.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
               {
                  SamplingDuration = TimeSpan.FromMinutes(6),
                  MinimumThroughput = 10,
                  Name = "CircuitBreaker"
               };
               o.Retry = new HttpRetryStrategyOptions
               {
                  MaxRetryAttempts = 1,
                  Delay = TimeSpan.FromSeconds(2),
                  Name = "Retry"
               };
               o.TotalRequestTimeout = new HttpTimeoutStrategyOptions
               {
                  Timeout = TimeSpan.FromMinutes(7),
                  Name = "TotalRequestTimeout"
               };
            });
         });

         ConfigureKernel(builder);

         var host = builder.Build();
         host.Run();
      }
      catch (Exception ex)
      {
         Log.Fatal(ex, "An unhandled exception occurred during application startup.");
      }
      finally
      {
         Log.CloseAndFlush();
      }
   }
   
   private static void ConfigureKernel(HostApplicationBuilder builder)
   {
      var embeddingConfig = builder.Configuration.GetSection("EmbeddingService").Get<ModelConfig>() ??
            throw new InvalidOperationException("EmbeddingGenerator configuration is missing.");

         var chatConfig = builder.Configuration.GetSection("ChatService").Get<ModelConfig>() ??
            throw new InvalidOperationException("EmbeddingGenerator configuration is missing.");

         var httpClientFactory = builder.Services.BuildServiceProvider().GetService<IHttpClientFactory>();

         var embeddingClient = httpClientFactory?.CreateClient() ?? new HttpClient();
         embeddingClient.BaseAddress = embeddingConfig.EndpointUri;

         var chatClient = httpClientFactory?.CreateClient() ?? new HttpClient();
         chatClient.BaseAddress = chatConfig.EndpointUri;

         builder.Services.AddKernel()
            .AddOllamaEmbeddingGenerator(embeddingConfig.ModelId, embeddingClient)
            .AddOllamaChatCompletion(chatConfig.ModelId, chatClient);
   }
}