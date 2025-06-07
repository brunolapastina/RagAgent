using System;

namespace RagAgent;

/// <summary>
/// Represents a search result retrieved from a <see cref="IVectorSearchable" /> instance.
/// </summary>
/// <remarks>
/// An instance of <see cref="SearchResult"/> is a normalized search result which provides access to:
/// - Key associated with the search result
/// - Value associated with the search result
/// - Score associated with the search result
/// </remarks>
/// <param name="value">The text search result value.</param>
public sealed class SearchResult(string value)
{
    /// <summary>
    /// The text search result name.
    /// </summary>
    /// <remarks>
    /// This represents the name associated with the result.
    /// If the text search was for a web search engine this would typically be the name of the web page associated with the search result.
    /// </remarks>
    public string? Key { get; init; }

    /// <summary>
    /// The link reference associated with the text search result.
    /// </summary>
    /// <remarks>
    /// This represents a possible link associated with the result.
    /// If the text search was for a web search engine this would typically be the URL of the web page associated with the search result.
    /// </remarks>
    public double? Score { get; init; }

    /// <summary>
    /// The text search result value.
    /// </summary>
    /// <remarks>
    /// This represents the text value associated with the result.
    /// If the text search was for a web search engine this would typically be the snippet describing the web page associated with the search result.
    /// </remarks>
    public string Value { get; init; } = value;
}
