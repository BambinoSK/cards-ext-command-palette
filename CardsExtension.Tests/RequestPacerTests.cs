// RequestPacer spreads background request bursts (card detail fetches) so
// they stay under the API rate limit. These tests drive it with a fake clock.

using CardsExtension.Services;
using Xunit;

namespace CardsExtension.Tests;

public sealed class RequestPacerTests
{
    [Fact]
    public void Reserve_WithinCapacity_NeedsNoWait()
    {
        var now = TimeSpan.Zero;
        var pacer = new RequestPacer(capacity: 3, refillPerSecond: 2, clock: () => now);

        Assert.Equal(TimeSpan.Zero, pacer.Reserve(1));
        Assert.Equal(TimeSpan.Zero, pacer.Reserve(2));
    }

    [Fact]
    public void Reserve_BeyondCapacity_QueuesBehindEarlierReservations()
    {
        var now = TimeSpan.Zero;
        var pacer = new RequestPacer(capacity: 3, refillPerSecond: 2, clock: () => now);
        pacer.Reserve(3);

        // 2 requests per second: the 4th request waits 0.5s, and the two after
        // it wait behind that one - not alongside it.
        Assert.Equal(TimeSpan.FromSeconds(0.5), pacer.Reserve(1));
        Assert.Equal(TimeSpan.FromSeconds(1.5), pacer.Reserve(2));
    }

    [Fact]
    public void Reserve_AfterIdleTime_RefillsUpToCapacityOnly()
    {
        var now = TimeSpan.Zero;
        var pacer = new RequestPacer(capacity: 3, refillPerSecond: 2, clock: () => now);
        pacer.Reserve(3);

        now = TimeSpan.FromMinutes(10);

        Assert.Equal(TimeSpan.Zero, pacer.Reserve(3));
        Assert.Equal(TimeSpan.FromSeconds(0.5), pacer.Reserve(1));
    }

    [Fact]
    public void Refund_GivesAnUnusedReservationBack()
    {
        var now = TimeSpan.Zero;
        var pacer = new RequestPacer(capacity: 3, refillPerSecond: 2, clock: () => now);
        pacer.Reserve(3);
        pacer.Reserve(2);

        pacer.Refund(2);

        Assert.Equal(TimeSpan.FromSeconds(0.5), pacer.Reserve(1));
    }

    [Fact]
    public async Task WaitAsync_Cancelled_ThrowsAndGivesItsReservationBack()
    {
        var now = TimeSpan.Zero;
        var pacer = new RequestPacer(capacity: 1, refillPerSecond: 0.01, clock: () => now);
        await pacer.WaitAsync(1, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var waiting = pacer.WaitAsync(1, cts.Token);
        Assert.False(waiting.IsCompleted);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        // The cancelled reservation was refunded, so the next caller waits 100s, not 200s.
        Assert.Equal(TimeSpan.FromSeconds(100), pacer.Reserve(1));
    }
}
