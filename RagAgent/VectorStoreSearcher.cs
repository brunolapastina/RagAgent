using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using System.Runtime.CompilerServices;

namespace RagAgent;

public class VectorStoreSearcher<TRecord>
{
   private readonly IVectorSearchable<TRecord> _vectorSearchable;
   private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
   private readonly Func<TRecord, double?, SearchResult> _mapper;

   public VectorStoreSearcher(
        IVectorSearchable<TRecord> vectorSearchable,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        Func<TRecord, double?, SearchResult> mapper)
   {
      _vectorSearchable = vectorSearchable;
      _embeddingGenerator = embeddingGenerator;
      _mapper = mapper;
   }

   public async Task<KernelSearchResults<SearchResult>> GetTextSearchResultsAsync(string query, SearchOptions searchOptions, CancellationToken cancellationToken)
   {
      var vectorSearchOptions = new VectorSearchOptions<TRecord>()
      {
         Skip = searchOptions.Skip,
         IncludeVectors = false
      };

      var searchVector = await _embeddingGenerator.GenerateAsync(query, cancellationToken: cancellationToken);
      var resultRecords = _vectorSearchable.SearchAsync(searchVector.Vector, searchOptions.Top, vectorSearchOptions, cancellationToken);
      var validResult = resultRecords.Where(r => r.Score >= searchOptions.MinScore); 

      return new KernelSearchResults<SearchResult>(GetResultsAsStringAsync(validResult, cancellationToken));
   }

   /// <summary>
   /// Return the search results as instances of <see cref="SearchResult"/>.
   /// </summary>
   /// <param name="searchResponse">Response containing the web pages matching the query.</param>
   /// <param name="cancellationToken">Cancellation token</param>
   private async IAsyncEnumerable<SearchResult> GetResultsAsStringAsync(IAsyncEnumerable<VectorSearchResult<TRecord>>? searchResponse, [EnumeratorCancellation] CancellationToken cancellationToken)
   {
      if (searchResponse is null)
      {
         yield break;
      }

      await foreach (var result in searchResponse.Where(result => result.Record is not null)
                                                .WithCancellation(cancellationToken)
                                                .ConfigureAwait(false))
      {
         yield return _mapper(result.Record, result.Score);
         await Task.Yield();
      }
   }
}
