namespace RagChatBot;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var input = Console.ReadLine();
            Console.WriteLine("You entered: " + input);
        }
    }
}
