using Contriwork.Notifications.Adapters;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Contriwork.Notifications.Tests;

public sealed class PushoverTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    private string ApiUrl => _server.Url + "/messages.json";
    private static readonly Payload SamplePayload = new("hi", "world");

    [Fact]
    public async Task Delivers_On_2xx()
    {
        _server.Given(Request.Create().WithPath("/messages.json").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"status\":1}"));
        var adapter = new PushoverAdapter("u", "t", apiUrl: ApiUrl);
        var result = await adapter.DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.Delivered, result.Status);
    }

    [Fact]
    public async Task Auth_Failed_On_401()
    {
        _server.Given(Request.Create().WithPath("/messages.json").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(401));
        var adapter = new PushoverAdapter("u", "t", apiUrl: ApiUrl);
        var result = await adapter.DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.PermanentFailure, result.Status);
        Assert.Equal(ErrorCode.AuthFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Invalid_Payload_On_400()
    {
        _server.Given(Request.Create().WithPath("/messages.json").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(400));
        var adapter = new PushoverAdapter("u", "t", apiUrl: ApiUrl);
        var result = await adapter.DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.PermanentFailure, result.Status);
        Assert.Equal(ErrorCode.AdapterInvalidPayload, result.ErrorCode);
    }

    [Fact]
    public async Task Rate_Limited_On_429()
    {
        _server.Given(Request.Create().WithPath("/messages.json").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(429));
        var adapter = new PushoverAdapter("u", "t", apiUrl: ApiUrl);
        var result = await adapter.DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.RateLimited, result.ErrorCode);
    }

    [Fact]
    public async Task Upstream_Error_On_500()
    {
        _server.Given(Request.Create().WithPath("/messages.json").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(502));
        var adapter = new PushoverAdapter("u", "t", apiUrl: ApiUrl);
        var result = await adapter.DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.UpstreamError, result.ErrorCode);
    }

    [Fact]
    public async Task Silent_Forces_Priority_Minus_One()
    {
        _server.Given(Request.Create().WithPath("/messages.json").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        var adapter = new PushoverAdapter("u", "t", apiUrl: ApiUrl);
        await adapter.DeliverAsync(Severity.Warn, SamplePayload, silent: true);

        var requests = _server.LogEntries.ToList();
        Assert.Single(requests);
        var body = requests[0].RequestMessage!.Body!;
        Assert.Contains("priority=-1", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Is_Available_Requires_Credentials()
    {
        Assert.True(await new PushoverAdapter("u", "t").IsAvailableAsync());
        Assert.False(await new PushoverAdapter("", "t").IsAvailableAsync());
        Assert.False(await new PushoverAdapter("u", "").IsAvailableAsync());
    }

    [Fact]
    public void Constructor_Rejects_Invalid_Error_Priority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PushoverAdapter("u", "t", errorPriority: 2));
    }
}
