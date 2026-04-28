using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Contriwork.Notifications.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Public_Types_Are_Public()
    {
        Assert.True(typeof(INotificationPort).IsPublic);
        Assert.True(typeof(NotificationClient).IsPublic);
        Assert.True(typeof(IAdapter).IsPublic);
        Assert.True(typeof(Payload).IsPublic);
        Assert.True(typeof(SendResult).IsPublic);
        Assert.True(typeof(AdapterOutcome).IsPublic);
        Assert.True(typeof(AdapterDeliverResult).IsPublic);
        Assert.True(typeof(NotificationConfig).IsPublic);
        Assert.True(typeof(RetryConfig).IsPublic);
        Assert.True(typeof(QuietHoursConfig).IsPublic);
        Assert.True(typeof(RateLimitPolicy).IsPublic);
    }

    [Fact]
    public void Severity_Icons_Are_Stable()
    {
        Assert.Equal("🔍", Severity.Debug.Icon());
        Assert.Equal("ℹ️", Severity.Info.Icon());
        Assert.Equal("⚠️", Severity.Warn.Icon());
        Assert.Equal("❌", Severity.Error.Icon());
        Assert.Equal("⛔", Severity.Critical.Icon());
    }

    [Fact]
    public void Severity_Wire_Strings_Round_Trip()
    {
        foreach (var s in Enum.GetValues<Severity>())
        {
            Assert.Equal(s, SeverityExtensions.FromWireString(s.ToWireString()));
        }
    }

    [Fact]
    public void ErrorCode_Wire_Strings_Round_Trip()
    {
        foreach (var c in Enum.GetValues<ErrorCode>())
        {
            Assert.Equal(c, ErrorCodeExtensions.FromWireString(c.ToWireString()));
        }
    }

    [Fact]
    public async Task Empty_Adapter_List_Send_Is_Noop()
    {
        var client = new NotificationClient(Array.Empty<IAdapter>());
        var result = await client.SendAsync(Severity.Info, new Payload("hi", "world"));
        Assert.True(result.Ok);
        Assert.Empty(result.Results);
        Assert.Null(result.ErrorCode);
        Assert.Equal(0, result.Attempts);
    }

    [Fact]
    public async Task Invalid_Payload_Short_Circuits()
    {
        var client = new NotificationClient(Array.Empty<IAdapter>());
        var oversize = new string('A', 201);
        var result = await client.SendAsync(Severity.Info, new Payload(oversize, "ok"));
        Assert.False(result.Ok);
        Assert.Equal(ErrorCode.InvalidPayload, result.ErrorCode);
    }

    [Fact]
    public async Task Invalid_Payload_NonHttps_Url_Short_Circuits()
    {
        var client = new NotificationClient(Array.Empty<IAdapter>());
        var result = await client.SendAsync(Severity.Info,
            new Payload("hi", "ok", Url: "http://example.com"));
        Assert.False(result.Ok);
        Assert.Equal(ErrorCode.InvalidPayload, result.ErrorCode);
    }

    [Fact]
    public void DI_Extension_Registers_Singleton_Port()
    {
        var services = new ServiceCollection();
        services.AddContriworkNotifications(_ => Array.Empty<IAdapter>());
        using var sp = services.BuildServiceProvider();
        var port = sp.GetRequiredService<INotificationPort>();
        Assert.IsType<NotificationClient>(port);
    }
}
