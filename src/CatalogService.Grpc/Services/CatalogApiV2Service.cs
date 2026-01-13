using Grpc.Core;
using ContractsV2 = CatalogService.Grpc.Contracts.V2;

namespace CatalogService.Grpc.Services;

public sealed class CatalogApiV2Service : ContractsV2.CatalogApi.CatalogApiBase
{
    public override Task<ContractsV2.PingResponse> Ping(ContractsV2.PingRequest request, ServerCallContext context)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;

        return Task.FromResult(new ContractsV2.PingResponse
        {
            Message = $"pong(v2): {request.Message}",
            ServerTimeUnixMs = now,
            CorrelationId = correlationId,
            Code = 0
        });
    }

    public override async Task WatchCatalog(
        ContractsV2.WatchCatalogRequest request,
        IServerStreamWriter<ContractsV2.CatalogUpdate> responseStream,
        ServerCallContext context)
    {
        var intervalMs = request.IntervalMs <= 0 ? 500 : request.IntervalMs;
        var max = request.MaxUpdates <= 0 ? 10 : request.MaxUpdates;

        for (long i = 1; i <= max; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            await responseStream.WriteAsync(new ContractsV2.CatalogUpdate
            {
                Sequence = i,
                Text = string.IsNullOrWhiteSpace(request.Filter)
                    ? $"Update(v2) #{i}"
                    : $"Update(v2) #{i}, filter={request.Filter}",
                ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Version = "v2"
            });

            await Task.Delay(intervalMs, context.CancellationToken);
        }
    }

    public override async Task<ContractsV2.UploadSummary> UploadCatalogEvents(
        IAsyncStreamReader<ContractsV2.CatalogEvent> requestStream,
        ServerCallContext context)
    {
        var count = 0;

        await foreach (var _ in requestStream.ReadAllAsync(context.CancellationToken))
        {
            count++;
        }

        return new ContractsV2.UploadSummary
        {
            ReceivedCount = count,
            ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Version = "v2"
        };
    }

    public override async Task Chat(
        IAsyncStreamReader<ContractsV2.ChatMessage> requestStream,
        IServerStreamWriter<ContractsV2.ChatMessage> responseStream,
        ServerCallContext context)
    {
        await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
        {
            var corr = string.IsNullOrWhiteSpace(msg.CorrelationId) ? "-" : msg.CorrelationId;

            await responseStream.WriteAsync(new ContractsV2.ChatMessage
            {
                Sender = "server",
                Text = $"echo(v2): [{corr}] {msg.Sender}: {msg.Text}",
                TimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CorrelationId = corr
            });
        }
    }
}
