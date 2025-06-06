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
   IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator,
   DataLoader _dataLoader) : BackgroundService
{
   private readonly ChatHistory _chat = [];

   private readonly HandlebarsPromptTemplateFactory _templateFactory = new();

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      var collection = await _dataLoader.LoadAsync("Teste", stoppingToken);

      // Create a text search instance using the vector store collection.
      var textSearch = new VectorStoreTextSearch<VectorStoreEntry>(collection, _embeddingGenerator);

      // Build a text search plugin with vector store search and add to the kernel
      var searchOptions = new TextSearchOptions()
      {
         Top = 5,
         Skip = 0,
      };
      var searchPlugin = KernelPluginFactory.CreateFromFunctions("SearchPlugin", "Performs search on the provided documents", [textSearch.CreateGetTextSearchResults(null, searchOptions)]);

      _kernel.Plugins.Add(searchPlugin);

      var handlebarsPromptYaml = await File.ReadAllTextAsync("Resources/Prompt.yaml", stoppingToken);
      var function = _kernel.CreateFunctionFromPromptYaml(handlebarsPromptYaml, _templateFactory);

      var arguments = new KernelArguments()
      {
         { "history", _chat.TakeLast(5) },
      };

      StringBuilder stringBuilder = new();

      while (!stoppingToken.IsCancellationRequested)
      {
         try
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
         catch (Exception ex)
         {
            _logger.LogError(ex, "An error occurred while processing input. Please try again.");
         }
      }

      _appLifetime.StopApplication();
      _logger.LogInformation("Worker has stopped.");
   }
}
