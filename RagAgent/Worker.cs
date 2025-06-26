using System.Text;
using AllTheChunkers;
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
   DataLoader _dataLoader,
   SemanticDoublePassMergingChunker textChunker) : BackgroundService
{
   private readonly ChatHistory _chat = [];

   private readonly HandlebarsPromptTemplateFactory _templateFactory = new();
   private readonly SemanticDoublePassMergingChunker _textChunker = textChunker;

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      var text = """
Babies learning to walk is one of the most exciting milestones in early development! It’s a process that blends curiosity, trial and error, and a lot of determination. Every baby approaches it differently, making each journey unique.

It all starts with muscle development and coordination... Before they take their first steps, babies spend months building strength by lifting their heads, rolling over, and eventually crawling. These foundational movements are critical—they help babies gain control over their muscles and balance systems.

Around 8 to 12 months, many babies begin pulling themselves up to stand. They grab onto furniture, a parent’s leg, or anything within reach. This moment sparks their realization: standing upright is possible! With wide eyes and wobbly legs, they often surprise themselves—what a thrill!

Once standing becomes more comfortable, babies begin to cruise... This means walking while holding onto something for support. Tables, couches, and even walls become makeshift handrails. It’s a time of exploration and often a few tumbles—but that’s part of the learning process, isn’t it?

Balance plays a huge role in walking readiness. Babies practice shifting their weight from one foot to the other (sometimes unintentionally) while standing still. You’ll often see them bend their knees and sway side to side—looks funny, but it’s actually important motor training!

Confidence is the next ingredient. After many cruising sessions, babies start daring to let go... For a few seconds, they stand without holding anything. Parents gasp. Cameras come out. Then—plop!—the baby falls right down, followed by giggles (or sometimes tears)!

The first independent steps are usually hesitant... Babies take a step, maybe two, and then drop to the floor. But each attempt builds muscle memory and spatial awareness. Within days—or sometimes weeks—they string together more steps until they’re truly walking.

Encouragement plays a big part in this phase! Parents often cheer loudly: "You can do it!" or "Come to Mama!" Positive reinforcement helps babies feel safe and excited to try again. Who wouldn’t want an applause just for putting one foot in front of the other?

It’s important to remember that every baby has their own timeline. Some walk at 9 months... others at 15 months... and both are perfectly normal! There’s no "right" time, only the baby’s time. Comparison with other children can create unnecessary worry (so avoid it)!

In the end, learning to walk is a beautiful mix of biology, practice, and love. It marks the start of a lifetime of movement and independence. And once babies master walking—watch out... they’ll soon be running everywhere!
""";
      var opts = new SemanticDoublePassMergingChunkerOptions()
      {
         InitialThreshold = 0.5f,
         AppendingThreshold = 0.6f,
         MergingThreshold = 0.6f,
         MaxLength = 1000
      };
      var slices = await _textChunker.Slice(text, opts, stoppingToken);
      foreach (var str in slices)
      {
         Console.WriteLine("-------------------");
         Console.WriteLine(str);
         Console.WriteLine("-------------------");
         Console.WriteLine();
      }

      _appLifetime.StopApplication();
      return;




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
