using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RagAgent;

public class DataLoader(ILogger<DataLoader> logger, IConfiguration configuration, VectorStore vectorStore, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
   private readonly ILogger<DataLoader> _logger = logger;
   private readonly VectorStore _vectorStore = vectorStore;
   private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator = embeddingGenerator;
   private readonly DataLoaderConfig _config = configuration.GetSection("DataLoader").Get<DataLoaderConfig>()
      ?? throw new InvalidOperationException("DataLoader configuration is missing.");

   public async Task<VectorStoreCollection<int, VectorStoreEntry>> LoadAsync(string collectionName, CancellationToken cancellationToken = default)
   {
      _logger.LogInformation("Starting data loading...");

      var collection = vectorStore.GetCollection<int, VectorStoreEntry>(collectionName);
      await collection.EnsureCollectionExistsAsync(cancellationToken);

      int key = 0;
      foreach (var document in _config.Documents)
      {
         var sw = Stopwatch.StartNew();
         var entries = new ConcurrentBag<VectorStoreEntry>();
         var linesInDocument = LoadTxtFile(document, cancellationToken);
         var options = new ParallelOptions
         {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 2
         };
         await Parallel.ForEachAsync(linesInDocument, options, async (line, cs) =>
         {
            var vse = new VectorStoreEntry
            {
               Key = key++,
               Content = line,
               Embedding = (await embeddingGenerator.GenerateAsync(line, null, cs)).Vector
            };
            entries.Add(vse);
         });

         await collection.UpsertAsync(entries, cancellationToken);
         sw.Stop();

         _logger.LogInformation("Loaded {Count} entries from {Document} in {Time}", entries.Count, document, sw.Elapsed);
      }

      _logger.LogInformation("Data loading completed.");

      return collection;
   }

   private IEnumerable<string> LoadPdfFile(string pdfPath, CancellationToken cancellationToken)
   {
      _logger.LogInformation("Loading PDF text from {PdfPath}", pdfPath);

      using var pdf = PdfDocument.Open(pdfPath);
      foreach (var page in pdf.GetPages())
      {
         if (cancellationToken.IsCancellationRequested)
         {
            _logger.LogWarning("PDF text loading was cancelled.");
            yield break;
         }

         // Extract text using the content order extractor.
         var text = ContentOrderTextExtractor.GetText(page);
         yield return text;
      }
   }

   private IEnumerable<string> LoadTxtFile(string path, CancellationToken cancellationToken)
   {
      _logger.LogInformation("Loading TXT text from {TxtPath}", path);

      return File.ReadLines(path);
   }

   private async Task LoadDocumentAsync(string content, CancellationToken cancellationToken)
   {
      _logger.LogInformation("Loading document content...");

      //var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(content, cancellationToken);
      //await _vectorStore.AddAsync(embedding, content, cancellationToken);

      _logger.LogInformation("Document content loaded successfully.");
   }
}
