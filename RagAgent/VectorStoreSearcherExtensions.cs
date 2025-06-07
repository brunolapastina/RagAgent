using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace RagAgent;

public static class VectorStoreSearcherExtensions
{
   /// <summary>
   /// Creates a plugin from an ITextSearch implementation.
   /// </summary>
   /// <remarks>
   /// The plugin will have a single function called `GetSearchResults` which
   /// will return a <see cref="IEnumerable{TextSearchResult}"/>
   /// </remarks>
   /// <param name="textSearch">The instance of ITextSearch to be used by the plugin.</param>
   /// <param name="pluginName">The name for the plugin.</param>
   /// <param name="description">A description of the plugin.</param>
   /// <returns>A <see cref="KernelPlugin"/> instance with a GetTextSearchResults operation that calls the provided <see cref="ITextSearch.GetTextSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.</returns>
   [RequiresUnreferencedCode("Uses reflection to handle various aspects of the function creation and invocation, making it incompatible with AOT scenarios.")]
   [RequiresDynamicCode("Uses reflection to handle various aspects of the function creation and invocation, making it incompatible with AOT scenarios.")]
   public static KernelPlugin CreateWithGetSearchResults<TRecord>(this VectorStoreSearcher<TRecord> searcher, string pluginName, string? description = null) =>
      KernelPluginFactory.CreateFromFunctions(pluginName, description, [searcher.CreateGetSearchResults()]);

   /// <summary>
   /// Create a <see cref="KernelFunction"/> which invokes <see cref="ITextSearch.GetTextSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.
   /// </summary>
   /// <param name="textSearch">The ITextSearch instance to use.</param>
   /// <param name="options">Optional KernelFunctionFromMethodOptions which allow the KernelFunction metadata to be specified.</param>
   /// <param name="searchOptions">Optional TextSearchOptions which override the options provided when the function is invoked.</param>
   /// <returns>A <see cref="KernelFunction"/> instance with a Search operation that calls the provided <see cref="ITextSearch.GetTextSearchResultsAsync(string, SearchOptions?, CancellationToken)"/>.</returns>
   [RequiresUnreferencedCode("Uses reflection to handle various aspects of the function creation and invocation, making it incompatible with AOT scenarios.")]
   [RequiresDynamicCode("Uses reflection to handle various aspects of the function creation and invocation, making it incompatible with AOT scenarios.")]
   public static KernelFunction CreateGetSearchResults<TRecord>(this VectorStoreSearcher<TRecord> searcher, KernelFunctionFromMethodOptions? options = null, SearchOptions? searchOptions = null)
   {
      async Task<IEnumerable<SearchResult>> GetTextSearchResultAsync(Kernel kernel, KernelFunction function, KernelArguments arguments, CancellationToken cancellationToken, int count = 2, int skip = 0)
      {
         arguments.TryGetValue("query", out var query);
         if (string.IsNullOrEmpty(query?.ToString()))
         {
            return [];
         }

         searchOptions ??= new()
         {
            Top = count,
            Skip = skip,
            //Filter = CreateBasicFilter(options, arguments)
         };

         var result = await searcher.GetTextSearchResultsAsync(query?.ToString()!, searchOptions, cancellationToken).ConfigureAwait(false);

         var resultList = new List<SearchResult>();

         await foreach (var item in result.Results.WithCancellation(cancellationToken).ConfigureAwait(false))
         {
            resultList.Add(item);
         }

         return resultList;
      }

      options ??= DefaultGetTextSearchResultsMethodOptions();
      return KernelFunctionFactory.CreateFromMethod(
              GetTextSearchResultAsync,
              options);
   }

   /// <summary>
   /// Create a <see cref="TextSearchFilter" /> for the search based on any additional parameters included in the <see cref="KernelFunctionFromMethodOptions"/>
   /// </summary>
   /// <param name="options">Kernel function method options.</param>
   /// <param name="arguments">Kernel arguments.</param>
   private static TextSearchFilter? CreateBasicFilter(KernelFunctionFromMethodOptions? options, KernelArguments arguments)
   {
      if (options?.Parameters is null)
      {
         return null;
      }

      TextSearchFilter? filter = null;
      foreach (var parameter in options.Parameters)
      {
         // treat non standard parameters as equality filter clauses
         if (!parameter.Name.Equals("query", StringComparison.Ordinal) &&
             !parameter.Name.Equals("count", StringComparison.Ordinal) &&
             !parameter.Name.Equals("skip", StringComparison.Ordinal))
         {
            if (arguments.TryGetValue(parameter.Name, out var value) && value is not null)
            {
               filter ??= new TextSearchFilter();
               filter.Equality(parameter.Name, value);
            }
         }
      }

      return filter;
   }

   /// <summary>
   /// Create the default <see cref="KernelFunctionFromMethodOptions"/> for <see cref="ITextSearch.GetTextSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.
   /// </summary>
   [RequiresUnreferencedCode("Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
   [RequiresDynamicCode("Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
   private static KernelFunctionFromMethodOptions DefaultGetTextSearchResultsMethodOptions() =>
      new()
      {
         FunctionName = "GetSearchResults",
         Description = "Perform a search for content related to the specified query. The search will return the name, value and link for the related content.",
         Parameters = GetDefaultKernelParameterMetadata(),
         ReturnParameter = new() { ParameterType = typeof(KernelSearchResults<TextSearchResult>) },
      };

   [RequiresUnreferencedCode("Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
   [RequiresDynamicCode("Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
   private static IEnumerable<KernelParameterMetadata> GetDefaultKernelParameterMetadata()
   {
      return [
            new KernelParameterMetadata("query") { Description = "What to search for", ParameterType = typeof(string), IsRequired = true },
            new KernelParameterMetadata("count") { Description = "Number of results", ParameterType = typeof(int), IsRequired = false, DefaultValue = 2 },
            new KernelParameterMetadata("skip") { Description = "Number of results to skip", ParameterType = typeof(int), IsRequired = false, DefaultValue = 0 },
        ];
   }
}
