using System.Text;
using Microsoft.Extensions.AI;
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

   private readonly ChatHistory _chat = new("You are an AI assistant that helps people find information aboute episodes of a podcast.");

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      var collection = await _dataLoader.LoadAsync("Teste", stoppingToken);

      // Create a text search instance using the vector store collection.
      var textSearch = new VectorStoreTextSearch<VectorStoreEntry>(collection, _embeddingGenerator);

      // Build a text search plugin with vector store search and add to the kernel
      var searchPlugin = textSearch.CreateWithGetTextSearchResults("SearchPlugin");
      _kernel.Plugins.Add(searchPlugin);

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
            //if (record.Score > 0.5f)
            {
               Console.WriteLine($"  Search score: {record.Score}");
               stringBuilder.AppendLine(record.Record.Content);
            }
         }

         int contextToRemove = -1;
         if (stringBuilder.Length != 0)
         {
            stringBuilder.Insert(0, "Please use this information to answer the questions: ");
            contextToRemove = _chat.Count;
            _chat.AddUserMessage(stringBuilder.ToString());
         }

         _chat.AddUserMessage(input);

         stringBuilder.Clear();
         /*await foreach (var message in _chatCompletion.GetStreamingChatMessageContentsAsync(_chat, _promptExecSettings, _kernel, stoppingToken))
         {
            Console.Write(message.Content);
            stringBuilder.Append(message.Content);
         }*/

         var response = _kernel.InvokePromptStreamingAsync(
                promptTemplate: """
                    Please use this information to answer the question:
                    {{#with (SearchPlugin-GetTextSearchResults question)}}  
                      {{#each this}}  
                        Name: {{Name}}
                        Value: {{Value}}
                        -----------------
                      {{/each}}
                    {{/with}}

                    Include citations to the relevant information where it is referenced in the response.
                    
                    Question: {{question}}
                    """,
                arguments: new KernelArguments()
                {
                    { "question", input },
                },
                templateFormat: "handlebars",
                promptTemplateFactory: new HandlebarsPromptTemplateFactory(),
                cancellationToken: stoppingToken);
         await foreach (var message in response.ConfigureAwait(false))
         {
            Console.Write(message);
            stringBuilder.Append(message);
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
