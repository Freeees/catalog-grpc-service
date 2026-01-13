using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using CatalogService.Grpc.Contracts.V1;
using Grpc.Core;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureServices((context, services) =>
    {
        var grpcAddress = context.Configuration["Grpc:CatalogServiceAddress"] ?? "http://localhost:5144";

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * attempt)
            );

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(2));

        services
            .AddGrpcClient<CatalogApi.CatalogApiClient>(o =>
            {
                o.Address = new Uri(grpcAddress);
            })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);

        services.AddTransient<App>();
    })
    .Build();

await host.Services.GetRequiredService<App>().RunAsync();

public sealed class App
{
    private readonly CatalogApi.CatalogApiClient _client;
    private readonly ILogger<App> _logger;

    public App(CatalogApi.CatalogApiClient client, ILogger<App> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("1) Ping...");
        var reply = await _client.PingAsync(new PingRequest { Message = "hello from client" });
        _logger.LogInformation("Ping reply: {Message}", reply.Message);

        _logger.LogInformation("2) Server streaming: WatchCatalog...");
        using (var call = _client.WatchCatalog(new WatchCatalogRequest { IntervalMs = 300, MaxUpdates = 5 }))
        {
            await foreach (var update in call.ResponseStream.ReadAllAsync())
            {
                _logger.LogInformation("Update: #{Seq} {Text}", update.Sequence, update.Text);
            }
        }

        _logger.LogInformation("3) Client streaming: UploadCatalogEvents...");
        using (var call = _client.UploadCatalogEvents())
        {
            for (var i = 1; i <= 5; i++)
            {
                await call.RequestStream.WriteAsync(new CatalogEvent
                {
                    EventId = Guid.NewGuid().ToString("N"),
                    Type = "DemoEvent",
                    PayloadJson = $$"""{"n": {{i}}}""",
                    ClientTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }

            await call.RequestStream.CompleteAsync();
            var summary = await call.ResponseAsync;
            _logger.LogInformation("Upload summary: received={Count}", summary.ReceivedCount);
        }

        _logger.LogInformation("4) Bidirectional streaming: Chat...");
        using (var call = _client.Chat())
        {
            var readTask = Task.Run(async () =>
            {
                await foreach (var serverMsg in call.ResponseStream.ReadAllAsync())
                {
                    _logger.LogInformation("Chat <- {Text}", serverMsg.Text);
                }
            });

            for (var i = 1; i <= 3; i++)
            {
                await call.RequestStream.WriteAsync(new ChatMessage
                {
                    Sender = "client",
                    Text = $"hi {i}",
                    TimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                await Task.Delay(200);
            }

            await call.RequestStream.CompleteAsync();
            await readTask;
        }
    }

}
