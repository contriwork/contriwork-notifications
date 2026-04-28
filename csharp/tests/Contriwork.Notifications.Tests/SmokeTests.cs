using Xunit;

namespace Contriwork.Notifications.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Port_Interface_Is_Public()
    {
        var t = typeof(INotificationsPort);
        Assert.True(t.IsPublic);
        Assert.True(t.IsInterface);
    }

    [Fact]
    public void Port_Declares_Example_Method()
    {
        var method = typeof(INotificationsPort).GetMethod(nameof(INotificationsPort.ExampleAsync));
        Assert.NotNull(method);
    }
}
