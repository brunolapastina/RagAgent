using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAgent;

public class Worker(ILogger<Worker> logger, IHostApplicationLifetime appLifetime, Kernel kernel, IChatCompletionService chatCompletion) : BackgroundService
{
   private readonly ILogger<Worker> _logger = logger;
   private readonly IHostApplicationLifetime _appLifetime = appLifetime;
   private readonly Kernel _kernel = kernel;
   private readonly IChatCompletionService _chatCompletion = chatCompletion;
   
   private readonly PromptExecutionSettings _promptExecSettings = new()
   {
      //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(null, true),
   };

   private readonly ChatHistory _chat = new();

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      StringBuilder stringBuilder = new();

      while (!stoppingToken.IsCancellationRequested)
      {
         var input = Console.ReadLine();
         if (string.IsNullOrEmpty(input))
         {
            _logger.LogInformation("Terminating worker due to empty input.");

            break;
         }

         _chat.AddUserMessage(input);

         await foreach (var message in _chatCompletion.GetStreamingChatMessageContentsAsync(_chat, _promptExecSettings, _kernel, stoppingToken))
         {
            Console.Write(message.Content);
            stringBuilder.Append(message.Content);
         }
         Console.WriteLine();
         _chat.AddAssistantMessage(stringBuilder.ToString());
         Console.WriteLine();
      }

      _appLifetime.StopApplication();
      _logger.LogInformation("Worker has stopped.");
   }
}
