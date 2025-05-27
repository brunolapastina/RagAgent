namespace RagAgent;

public record ModelConfig
{
   public required string ModelId { get; init; }
   public required string Endpoint { get; init; }
   public Uri EndpointUri => new(Endpoint);
}
