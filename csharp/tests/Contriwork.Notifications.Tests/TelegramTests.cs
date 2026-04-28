using System.Text.Json;
using Contriwork.Notifications.Adapters;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Contriwork.Notifications.Tests;

public sealed class TelegramTests : IDisposable
{
    private const string Token = "BOT_TOKEN";
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    private TelegramAdapter Adapter() => new(Token, "123", apiBase: _server.Url!);
    private static readonly Payload SamplePayload = new("hi", "world");

    [Fact]
    public async Task Delivers_On_2xx()
    {
        _server.Given(Request.Create().WithPath($"/bot{Token}/sendMessage").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"ok\":true}"));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.Delivered, result.Status);
    }

    [Fact]
    public async Task Auth_Failed_On_401()
    {
        _server.Given(Request.Create().WithPath($"/bot{Token}/sendMessage").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(401));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.PermanentFailure, result.Status);
        Assert.Equal(ErrorCode.AuthFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Rate_Limited_On_429()
    {
        _server.Given(Request.Create().WithPath($"/bot{Token}/sendMessage").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(429));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.RateLimited, result.ErrorCode);
    }

    [Fact]
    public async Task Upstream_Error_On_503()
    {
        _server.Given(Request.Create().WithPath($"/bot{Token}/sendMessage").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.UpstreamError, result.ErrorCode);
    }

    [Fact]
    public async Task Silent_Sets_Disable_Notification()
    {
        _server.Given(Request.Create().WithPath($"/bot{Token}/sendMessage").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"ok\":true}"));
        await Adapter().DeliverAsync(Severity.Warn, SamplePayload, silent: true);

        var entries = _server.LogEntries.ToList();
        Assert.Single(entries);
        using var doc = JsonDocument.Parse(entries[0].RequestMessage!.Body!);
        Assert.True(doc.RootElement.GetProperty("disable_notification").GetBoolean());
    }

    [Fact]
    public async Task Url_Appended_To_Text()
    {
        _server.Given(Request.Create().WithPath($"/bot{Token}/sendMessage").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"ok\":true}"));
        var payload = new Payload("t", "b", Url: "https://example.com", UrlTitle: "link");
        await Adapter().DeliverAsync(Severity.Info, payload, silent: false);

        var entries = _server.LogEntries.ToList();
        using var doc = JsonDocument.Parse(entries[0].RequestMessage!.Body!);
        var text = doc.RootElement.GetProperty("text").GetString()!;
        Assert.EndsWith("link: https://example.com", text, StringComparison.Ordinal);
    }
}
