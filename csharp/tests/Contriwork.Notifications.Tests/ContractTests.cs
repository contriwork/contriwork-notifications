using System.Text.Json;
using Contriwork.Notifications.Adapters;
using Xunit;

namespace Contriwork.Notifications.Tests;

/// <summary>
/// Cross-language contract test runner. Loads
/// contract-tests/test_cases.json from the repo root and asserts each
/// fixture's expected_output against the result NotificationClient produces
/// when driven with InMemoryAdapter instances built from the per-fixture
/// behavior sequence. The same JSON drives the Python and TypeScript
/// runners; if a behavior here changes, all three runners change in the same PR.
/// </summary>
public sealed class ContractTests
{
    private static readonly string FixturePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "..",
        "contract-tests", "test_cases.json"));

    [Fact]
    public void Fixture_Is_WellFormed()
    {
        Assert.True(File.Exists(FixturePath), $"fixture missing at {FixturePath}");
        using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath));
        Assert.Equal(1, doc.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("v1", doc.RootElement.GetProperty("contract_revision").GetString());
        Assert.True(doc.RootElement.TryGetProperty("cases", out _));
    }

    public static IEnumerable<object[]> LoadCases()
    {
        if (!File.Exists(FixturePath))
        {
            yield break;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath));
        if (doc.RootElement.GetProperty("schema_version").GetInt32() != 1)
        {
            throw new InvalidOperationException(
                "contract fixture schema_version changed -- update all three language runners");
        }
        if (!doc.RootElement.TryGetProperty("cases", out var casesEl))
        {
            yield break;
        }

        foreach (var caseEl in casesEl.EnumerateArray())
        {
            if (caseEl.TryGetProperty("skip_languages", out var skipEl)
                && skipEl.EnumerateArray().Any(s => s.GetString() == "csharp"))
            {
                continue;
            }
            yield return new object[]
            {
                caseEl.GetProperty("name").GetString() ?? "unnamed",
                caseEl.GetRawText(),
            };
        }
    }

    [Theory]
    [MemberData(nameof(LoadCases))]
    public async Task Run_Case(string name, string caseJson)
    {
        _ = name; // surfaced through xUnit's [Theory] DisplayName
        using var doc = JsonDocument.Parse(caseJson);
        var caseEl = doc.RootElement;
        var input = caseEl.GetProperty("input");
        var expected = caseEl.GetProperty("expected_output");

        var adapters = BuildAdapters(input.GetProperty("adapters"));
        var config = BuildConfig(input.GetProperty("config"));
        var send = input.GetProperty("send");
        var severity = SeverityExtensions.FromWireString(send.GetProperty("severity").GetString()!);
        var payload = BuildPayload(send.GetProperty("payload"));

        var client = new NotificationClient(adapters, config);
        var result = await client.SendAsync(severity, payload);

        Assert.Equal(expected.GetProperty("ok").GetBoolean(), result.Ok);
        AssertOptionalErrorCode(expected, "error_code", result.ErrorCode);
        Assert.Equal(expected.GetProperty("attempts").GetInt32(), result.Attempts);

        var expectedResults = expected.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(expectedResults.Count, result.Results.Count);

        for (var i = 0; i < expectedResults.Count; i++)
        {
            var exp = expectedResults[i];
            var actual = result.Results[i];
            Assert.Equal(exp.GetProperty("adapter").GetString(), actual.Adapter);
            Assert.Equal(
                OutcomeStatusExtensions.FromWireString(exp.GetProperty("status").GetString()!),
                actual.Status);
            Assert.Equal(exp.GetProperty("attempts").GetInt32(), actual.Attempts);
            AssertOptionalErrorCode(exp, "error_code", actual.ErrorCode);
        }
    }

    private static void AssertOptionalErrorCode(JsonElement element, string property, ErrorCode? actual)
    {
        if (!element.TryGetProperty(property, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(actual);
            return;
        }
        Assert.Equal(ErrorCodeExtensions.FromWireString(prop.GetString()!), actual);
    }

    private static List<IAdapter> BuildAdapters(JsonElement spec)
    {
        var list = new List<IAdapter>();
        foreach (var item in spec.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var sequence = new List<AdapterDeliverResult>();
            if (item.TryGetProperty("behavior", out var behavior)
                && behavior.TryGetProperty("sequence", out var seqEl))
            {
                foreach (var entry in seqEl.EnumerateArray())
                {
                    var status = AdapterStatusExtensions.FromWireString(
                        entry.GetProperty("status").GetString()!);
                    ErrorCode? error = null;
                    if (entry.TryGetProperty("error_code", out var errEl)
                        && errEl.ValueKind != JsonValueKind.Null)
                    {
                        error = ErrorCodeExtensions.FromWireString(errEl.GetString()!);
                    }
                    sequence.Add(new AdapterDeliverResult(status, error));
                }
            }
            list.Add(new InMemoryAdapter(name, sequence));
        }
        return list;
    }

    private static NotificationConfig BuildConfig(JsonElement spec)
    {
        RetryConfig? retry = null;
        if (TryGetNonNull(spec, "retry", out var retryEl))
        {
            retry = new RetryConfig(
                retryEl.GetProperty("max_attempts").GetInt32(),
                retryEl.GetProperty("base_delay_ms").GetInt32(),
                retryEl.GetProperty("max_delay_ms").GetInt32(),
                retryEl.GetProperty("jitter_ratio").GetDouble());
        }

        QuietHoursConfig? quiet = null;
        if (TryGetNonNull(spec, "quiet_hours", out var qhEl))
        {
            quiet = new QuietHoursConfig(
                qhEl.GetProperty("start").GetString()!,
                qhEl.GetProperty("end").GetString()!,
                qhEl.GetProperty("timezone").GetString()!,
                qhEl.GetProperty("bypass_severities").EnumerateArray()
                    .Select(s => SeverityExtensions.FromWireString(s.GetString()!))
                    .ToList());
        }

        Dictionary<string, RateLimitPolicy>? rates = null;
        if (TryGetNonNull(spec, "rate_limits", out var rlEl))
        {
            rates = new Dictionary<string, RateLimitPolicy>(StringComparer.Ordinal);
            foreach (var prop in rlEl.EnumerateObject())
            {
                var p = prop.Value;
                rates[prop.Name] = new RateLimitPolicy(
                    p.GetProperty("max_count").GetInt32(),
                    p.GetProperty("window_seconds").GetInt32(),
                    p.GetProperty("bypass_severities").EnumerateArray()
                        .Select(s => SeverityExtensions.FromWireString(s.GetString()!))
                        .ToList());
            }
        }

        return new NotificationConfig(retry, quiet, rates);
    }

    private static Payload BuildPayload(JsonElement spec)
    {
        var url = TryGetNonNull(spec, "url", out var urlEl) ? urlEl.GetString() : null;
        var urlTitle = TryGetNonNull(spec, "url_title", out var utEl) ? utEl.GetString() : null;
        return new Payload(
            spec.GetProperty("title").GetString()!,
            spec.GetProperty("body").GetString()!,
            url,
            urlTitle);
    }

    private static bool TryGetNonNull(JsonElement parent, string property, out JsonElement value)
    {
        if (parent.TryGetProperty(property, out value) && value.ValueKind != JsonValueKind.Null)
        {
            return true;
        }
        return false;
    }
}
