using Contriwork.Notifications.Adapters;
using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;
using MimeKit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Contriwork.Notifications.Tests;

public sealed class SmtpTests
{
    private static readonly Payload SamplePayload = new("hi", "world");

    private static SmtpAdapter MakeAdapter(IMailTransport transport, bool withCreds = true)
    {
        return new SmtpAdapter(
            host: "smtp.example.com",
            port: 587,
            fromAddr: "alerts@example.com",
            toAddrs: ["dest@example.com"],
            username: withCreds ? "alerts" : null,
            password: withCreds ? "secret" : null)
        {
            TransportFactory = () => transport,
        };
    }

    [Fact]
    public async Task Delivers_On_Send_Success()
    {
        var transport = Substitute.For<IMailTransport>();
        var result = await MakeAdapter(transport).DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.Delivered, result.Status);
    }

    [Fact]
    public async Task Auth_Failed()
    {
        var transport = Substitute.For<IMailTransport>();
        transport.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new AuthenticationException("bad creds"));

        var result = await MakeAdapter(transport).DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.PermanentFailure, result.Status);
        Assert.Equal(ErrorCode.AuthFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Recipients_Refused()
    {
        var transport = Substitute.For<IMailTransport>();
        transport.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Throws(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "nope"));

        var result = await MakeAdapter(transport).DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.PermanentFailure, result.Status);
        Assert.Equal(ErrorCode.AdapterInvalidPayload, result.ErrorCode);
    }

    [Fact]
    public async Task Upstream_Error_On_Generic_Failure()
    {
        var transport = Substitute.For<IMailTransport>();
        transport.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Throws(new IOException("connection reset"));

        var result = await MakeAdapter(transport).DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.UpstreamError, result.ErrorCode);
    }

    [Fact]
    public async Task Is_Available_Requires_Required_Fields()
    {
        var transport = Substitute.For<IMailTransport>();
        Assert.True(await MakeAdapter(transport).IsAvailableAsync());

        var emptyHost = new SmtpAdapter("", 587, "a@b.c", ["d@e.f"])
        {
            TransportFactory = () => transport,
        };
        Assert.False(await emptyHost.IsAvailableAsync());

        var noRecipients = new SmtpAdapter("smtp.example.com", 587, "a@b.c", Array.Empty<string>())
        {
            TransportFactory = () => transport,
        };
        Assert.False(await noRecipients.IsAvailableAsync());
    }

    [Fact]
    public async Task Authenticate_Skipped_When_No_Credentials()
    {
        var transport = Substitute.For<IMailTransport>();
        var result = await MakeAdapter(transport, withCreds: false)
            .DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.Delivered, result.Status);
        await transport.DidNotReceive().AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
