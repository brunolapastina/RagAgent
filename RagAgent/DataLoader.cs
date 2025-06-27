using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AllTheChunkers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using SemanticSlicer;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to 

namespace RagAgent;

public class DataLoader(ILogger<DataLoader> logger, IConfiguration configuration, VectorStore vectorStore, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, SemanticDoublePassMergingChunker textChunker)
{
   private readonly ILogger<DataLoader> _logger = logger;
   private readonly VectorStore _vectorStore = vectorStore;
   private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator = embeddingGenerator;
   private readonly SemanticDoublePassMergingChunker _textChunker = textChunker;
   private readonly DataLoaderConfig _config = configuration.GetSection("DataLoader").Get<DataLoaderConfig>()
      ?? throw new InvalidOperationException("DataLoader configuration is missing.");


   public async Task<VectorStoreCollection<int, VectorStoreEntry>> LoadAsync(string collectionName, CancellationToken cancellationToken = default)
   {
      _logger.LogInformation("Starting data loading...");

      var collection = _vectorStore.GetCollection<int, VectorStoreEntry>(collectionName);
      await collection.EnsureCollectionExistsAsync(cancellationToken);

      var parallelOpts = new ParallelOptions
      {
         CancellationToken = cancellationToken,
         MaxDegreeOfParallelism = _config.DegreeOfParallelism
      };

      var opts = new SemanticDoublePassMergingChunkerOptions()
      {
         InitialThreshold = 0.7f,
         AppendingThreshold = 0.8f,
         MergingThreshold = 0.64f,
         MaxLength = _config.MaxTokensPerChunk
      };

      foreach (var document in _config.Documents)
      {
         var sw = Stopwatch.StartNew();

         var fileContent = await LoadFile(document, cancellationToken);
         var documentChunks = await _textChunker.Slice(fileContent, opts, cancellationToken);

         var entries = new ConcurrentBag<VectorStoreEntry>();

         await Parallel.ForEachAsync(documentChunks, parallelOpts, async (chunk, cs) =>
         {
            var vse = new VectorStoreEntry
            {
               Key = chunk.Index,
               Content = chunk.Content,
               Embedding = chunk.Embedding ?? (await _embeddingGenerator.GenerateAsync(chunk.Content, null, cs)).Vector
            };
            entries.Add(vse);
         });

         await collection.UpsertAsync(entries, cancellationToken);
         sw.Stop();

         _logger.LogInformation("Loaded {ChunkCount} chunks from {Document} in {Time}", entries.Count, document, sw.Elapsed);
      }

      _logger.LogInformation("Data loading completed.");

      return collection;
   }

   private async ValueTask<string> LoadFile(string path, CancellationToken cancellationToken)
   {
      var extension = Path.GetExtension(path).ToLowerInvariant();
      return extension switch
      {
         ".pdf" => await LoadPdfFile(path, cancellationToken),
         ".txt" => await LoadTxtFile(path, cancellationToken),
         _ => throw new NotSupportedException($"Unsupported file type: {extension}")
      };
   }

   private ValueTask<string> LoadPdfFile(string pdfPath, CancellationToken cancellationToken)
   {
      _logger.LogInformation("Loading PDF text from {PdfPath}", pdfPath);

      var sb = new StringBuilder();
      using var pdf = PdfDocument.Open(pdfPath);
      foreach (var page in pdf.GetPages())
      {
         if (cancellationToken.IsCancellationRequested)
         {
            _logger.LogWarning("PDF text loading was cancelled.");
            return ValueTask.FromCanceled<string>(cancellationToken);
         }

         // Extract text using the content order extractor.
         var text = ContentOrderTextExtractor.GetText(page);
         sb.AppendLine(text);
      }

      return ValueTask.FromResult(sb.ToString());
   }

   private async ValueTask<string> LoadTxtFile(string path, CancellationToken cancellationToken)
   {
      _logger.LogInformation("Loading TXT text from {TxtPath}", path);

      return await File.ReadAllTextAsync(path, cancellationToken);
   }
}
