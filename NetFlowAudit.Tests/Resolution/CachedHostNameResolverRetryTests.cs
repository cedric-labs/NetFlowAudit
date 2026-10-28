using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

/// <summary>
/// A slow DNS server must not leave an address unnamed for good: timeouts are retried, and an address without PTR
/// record is asked again once the retry delay has passed, including entries read from an older cache file.
/// </summary>
public sealed class CachedHostNameResolverRetryTests : IDisposable
{
    private static readonly IPAddress Server = IPAddress.Parse("192.0.2.79");
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
    private readonly string _directory;

    public CachedHostNameResolverRetryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "NetFlowAudit_DnsRetry_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A file still held by the OS must not fail the test run; the folder lives in the temp directory.
        }
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenDnsAnswersWithinTimeout_ReturnsNameFromSlowServer()
    {
        // Arrange
        var dns = new ScriptedReverseDnsLookup(_ => DnsAnswer.After(TimeSpan.FromMilliseconds(300), "SVLVBDD67"));
        await using var resolver = CreateResolver(dns, TimeSpan.FromSeconds(3));

        // Act
        var name = await resolver.ResolveHostNameAsync(Server);

        // Assert
        name.Should().Be("SVLVBDD67");
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenDnsExceedsTimeout_DoesNotCacheTheFailure()
    {
        // Arrange
        var dns = new ScriptedReverseDnsLookup(call => call == 1 ? DnsAnswer.Never() : DnsAnswer.Now("SVLVBDD67"));
        await using var resolver = CreateResolver(dns, TimeSpan.FromMilliseconds(50));

        // Act
        var first = await resolver.ResolveHostNameAsync(Server);
        var cachedAfterTimeout = resolver.TryGetCachedEntry(Server, out _);
        var second = await resolver.ResolveHostNameAsync(Server);

        // Assert
        first.Should().BeEmpty();
        cachedAfterTimeout.Should().BeFalse();
        second.Should().Be("SVLVBDD67");
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenNoPtrRecord_RetriesOnlyAfterRetryDelay()
    {
        // Arrange
        var dns = new ScriptedReverseDnsLookup(call => call == 1 ? DnsAnswer.NotFound() : DnsAnswer.Now("SVLVBDD67"));
        await using var resolver = CreateResolver(dns, TimeSpan.FromSeconds(1));

        // Act
        var first = await resolver.ResolveHostNameAsync(Server);
        _clock.Advance(TimeSpan.FromHours(1));
        var withinDelay = await resolver.ResolveHostNameAsync(Server);
        _clock.Advance(CachedHostNameResolver.NegativeResultRetryDelay);
        var afterDelay = await resolver.ResolveHostNameAsync(Server);

        // Assert
        first.Should().BeEmpty();
        withinDelay.Should().BeEmpty();
        afterDelay.Should().Be("SVLVBDD67");
        dns.Calls.Should().Be(2);
    }

    [Fact]
    public async Task QueueResolution_WhenCacheFileHoldsOldFailedEntry_ResolvesTheAddressAgain()
    {
        // Arrange
        var cachePath = Path.Combine(_directory, "netflow_dns_cache.json");
        await File.WriteAllTextAsync(cachePath, "{\"192.0.2.79\": {\"HostName\": \"\", \"Source\": \"None\", \"ResolvedAtUtc\": \"2026-10-25T07:05:52Z\"}}");
        var dns = new ScriptedReverseDnsLookup(_ => DnsAnswer.Now("SVLVBDD67"));
        await using var resolver = CreateResolver(dns, TimeSpan.FromSeconds(1), new PersistentDnsCacheStore(cachePath));
        var resolved = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        resolver.HostNameResolved += (_, e) => resolved.TrySetResult(e.HostName);

        // Act
        resolver.QueueResolution(Server);
        var name = await resolved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        name.Should().Be("SVLVBDD67");
    }

    [Fact]
    public async Task QueueResolution_WhenDnsTimedOut_WaitsTheTransientDelayBeforeAskingAgain()
    {
        // Arrange
        var dns = new ScriptedReverseDnsLookup(_ => DnsAnswer.Never());
        await using var resolver = CreateResolver(dns, TimeSpan.FromMilliseconds(50));
        resolver.QueueResolution(Server);
        await WaitUntilAsync(() => dns.Calls == 1 && !resolver.IsQueued(Server));

        // Act
        resolver.QueueResolution(Server);
        await Task.Delay(300);
        var callsWithinDelay = dns.Calls;
        _clock.Advance(CachedHostNameResolver.TransientFailureRetryDelay);
        resolver.QueueResolution(Server);
        await WaitUntilAsync(() => dns.Calls == 2);

        // Assert
        callsWithinDelay.Should().Be(1);
        dns.Calls.Should().Be(2);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the background resolution should have progressed");
            await Task.Delay(20);
        }
    }

    private CachedHostNameResolver CreateResolver(IReverseDnsLookup dns, TimeSpan timeout, PersistentDnsCacheStore? store = null) =>
        new(timeout, cacheStore: store, reverseDnsLookup: dns, timeProvider: _clock);

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan delay) => _now += delay;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed record DnsAnswer(TimeSpan Delay, string? HostName, bool Hangs)
    {
        public static DnsAnswer Now(string hostName) => new(TimeSpan.Zero, hostName, Hangs: false);

        public static DnsAnswer After(TimeSpan delay, string hostName) => new(delay, hostName, Hangs: false);

        public static DnsAnswer NotFound() => new(TimeSpan.Zero, null, Hangs: false);

        public static DnsAnswer Never() => new(TimeSpan.Zero, null, Hangs: true);
    }

    private sealed class ScriptedReverseDnsLookup(Func<int, DnsAnswer> script) : IReverseDnsLookup
    {
        private int _calls;

        public int Calls => _calls;

        public async Task<string> LookupAsync(IPAddress address, CancellationToken cancellationToken)
        {
            var answer = script(Interlocked.Increment(ref _calls));
            if (answer.Hangs)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            await Task.Delay(answer.Delay, cancellationToken);
            return answer.HostName ?? throw new SocketException((int)SocketError.HostNotFound);
        }
    }
}
