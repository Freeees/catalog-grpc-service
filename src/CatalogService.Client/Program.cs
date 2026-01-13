using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using CatalogService.Grpc.Contracts.V1;
using Grpc.Core;
using Grpc.Net.ClientFactory;


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
            .AddGrpcClient<CatalogApi.CatalogApiClient>("CatalogApiClientV1", o =>
            {
                o.Address = new Uri(grpcAddress);
            })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);

        services
            .AddGrpcClient<CatalogService.Grpc.Contracts.V2.CatalogApi.CatalogApiClient>("CatalogApiClientV2", o =>
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
    private readonly CatalogApi.CatalogApiClient _clientV1;
    private readonly CatalogService.Grpc.Contracts.V2.CatalogApi.CatalogApiClient _clientV2;
    private readonly ILogger<App> _logger;

    public App(GrpcClientFactory grpcClientFactory, ILogger<App> logger)
    {
        _clientV1 = grpcClientFactory.CreateClient<CatalogApi.CatalogApiClient>("CatalogApiClientV1");
        _clientV2 = grpcClientFactory.CreateClient<CatalogService.Grpc.Contracts.V2.CatalogApi.CatalogApiClient>("CatalogApiClientV2");
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("1) Ping...");
        var reply = await _clientV1.PingAsync(new PingRequest { Message = "hello from client" });
        _logger.LogInformation("Ping reply: {Message}", reply.Message);

        _logger.LogInformation("1.1) Ping v2...");
        var replyV2 = await _clientV2.PingAsync(new CatalogService.Grpc.Contracts.V2.PingRequest
        {
            Message = "hello from client",
            CorrelationId = Guid.NewGuid().ToString("N")
        });
        _logger.LogInformation("Ping v2 reply: {Message}; corr={Corr}; code={Code}",
            replyV2.Message, replyV2.CorrelationId, replyV2.Code);

        _logger.LogInformation("2) Server streaming: WatchCatalog...");
        using (var call = _clientV1.WatchCatalog(new WatchCatalogRequest { IntervalMs = 300, MaxUpdates = 5 }))
        {
            await foreach (var update in call.ResponseStream.ReadAllAsync())
            {
                _logger.LogInformation("Update: #{Seq} {Text}", update.Sequence, update.Text);
            }
        }

        _logger.LogInformation("3) Client streaming: UploadCatalogEvents...");
        using (var call = _clientV1.UploadCatalogEvents())
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
        using (var call = _clientV1.Chat())
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
