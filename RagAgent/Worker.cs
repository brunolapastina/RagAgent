using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
      var textSearch = new VectorStoreSearcher<VectorStoreEntry>(collection, _embeddingGenerator, (entry, score) => new SearchResult(entry.Content) { Key = entry.Key.ToString(), Score = score });

      // Build a text search plugin with vector store search and add to the kernel
      var searchOptions = new SearchOptions()
      {
         Top = 5,
         Skip = 0,
         MinScore = 0.6f
      };
      var searchPlugin = textSearch.CreateWithGetSearchResults("SearchPlugin", null, null, searchOptions);

      _kernel.Plugins.Add(searchPlugin);

      var handlebarsPromptYaml = await LoadPromptText();
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

   private static async Task<string> LoadPromptText()
   {
      using var stream = typeof(ChatWorker).Assembly.GetManifestResourceStream("RagAgent.Resources.Prompt.yaml") ??
         throw new InvalidOperationException("Prompt YAML resource not found in assembly.");

      using var sR = new StreamReader(stream);
      var promptText = await sR.ReadToEndAsync();
      sR.Close();

      stream.Close();

      return promptText;
   }
}
