using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace RagAgent;

public class ChatWorker(
   ILogger<ChatWorker> _logger,
   IHostApplicationLifetime _appLifetime,
   Kernel _kernel,
   IChatCompletionService _chatCompletion,
   IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator,
   DataLoader _dataLoader) : BackgroundService
{
   private readonly PromptExecutionSettings _promptExecSettings = new()
   {
      //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(null, true),
   };

   private readonly ChatHistory _chat = new();

   private readonly HandlebarsPromptTemplateFactory _templateFactory = new();

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      var collection = await _dataLoader.LoadAsync("Teste", stoppingToken);

      // Create a text search instance using the vector store collection.
      var textSearch = new VectorStoreTextSearch<VectorStoreEntry>(collection, _embeddingGenerator);

      // Build a text search plugin with vector store search and add to the kernel
      //var searchPlugin = textSearch.CreateWithGetTextSearchResults("SearchPlugin");
      var searchOptions = new TextSearchOptions()
      {
         Top = 5,
         Skip = 0,
      };
      var searchPlugin = KernelPluginFactory.CreateFromFunctions("SearchPlugin", "Performs search on the provided documents", [textSearch.CreateGetTextSearchResults(null, searchOptions)]);

      _kernel.Plugins.Add(searchPlugin);

      var handlebarsPromptYaml = File.ReadAllText("Resources/Prompt.yaml");
      var function = _kernel.CreateFunctionFromPromptYaml(handlebarsPromptYaml, _templateFactory);

      var arguments = new KernelArguments()
      {
         { "history", _chat },
      };

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
         arguments["CurrentQuestion"] = input;

         stringBuilder.Clear();
         var response = _kernel.InvokeStreamingAsync(function, arguments, stoppingToken);
         await foreach (var message in response)
         {
            Console.Write(message);
            stringBuilder.Append(message);
         }
         Console.WriteLine();
         _chat.AddAssistantMessage(stringBuilder.ToString());
         Console.WriteLine();
      }

      _appLifetime.StopApplication();
      _logger.LogInformation("Worker has stopped.");
   }
}
