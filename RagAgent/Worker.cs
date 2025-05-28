using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAgent;

public class ChatWorker(ILogger<ChatWorker> logger, IHostApplicationLifetime appLifetime, Kernel kernel, IChatCompletionService chatCompletion, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, DataLoader dataLoader) : BackgroundService
{
   private readonly ILogger<ChatWorker> _logger = logger;
   private readonly IHostApplicationLifetime _appLifetime = appLifetime;
   private readonly Kernel _kernel = kernel;
   private readonly IChatCompletionService _chatCompletion = chatCompletion;
   private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator = embeddingGenerator;
   private readonly DataLoader _dataLoader = dataLoader;
   private readonly PromptExecutionSettings _promptExecSettings = new()
   {
      //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(null, true),
   };

   private readonly ChatHistory _chat = new();

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      var collection = await _dataLoader.LoadAsync("Teste", stoppingToken);

      StringBuilder stringBuilder = new();

      while (!stoppingToken.IsCancellationRequested)
      {
         var input = Console.ReadLine();
         if (string.IsNullOrEmpty(input))
         {
            _logger.LogInformation("Terminating worker due to empty input.");

            break;
         }

         stringBuilder.Clear();
         var searchVector = (await _embeddingGenerator.GenerateAsync(input, null, stoppingToken)).Vector;
         var resultRecords = collection.SearchAsync(searchVector, 3, null, stoppingToken);
         await foreach (var record in resultRecords)
         {
            if (record.Score > 0.5f)
            {
               Console.WriteLine($"  Search score: {record.Score}");
               stringBuilder.AppendLine(record.Record.Content);
            }
         }

         int contextToRemove = -1;
         if (stringBuilder.Length != 0)
         {
            stringBuilder.Insert(0, "Here's some additional information: ");
            contextToRemove = _chat.Count;
            _chat.AddUserMessage(stringBuilder.ToString());
         }

         _chat.AddUserMessage(input);

         stringBuilder.Clear();
         await foreach (var message in _chatCompletion.GetStreamingChatMessageContentsAsync(_chat, _promptExecSettings, _kernel, stoppingToken))
         {
            Console.Write(message.Content);
            stringBuilder.Append(message.Content);
         }
         Console.WriteLine();
         _chat.AddAssistantMessage(stringBuilder.ToString());
         if (contextToRemove >= 0)
         {
            _chat.RemoveAt(contextToRemove);
         }
         Console.WriteLine();
      }

      _appLifetime.StopApplication();
      _logger.LogInformation("Worker has stopped.");
   }
}
