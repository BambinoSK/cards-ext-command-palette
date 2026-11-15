using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CardsExtension.Services;

/// <summary>
/// Token-bucket pacer for background request bursts. Callers reserve their
/// requests up front and wait their turn, so a burst is spread out instead of
/// being fired all at once into the API's rate limit (100 requests per 10
/// seconds per token).
///
/// Reservations are served strictly in call order: a caller that cannot be
/// served immediately takes the bucket into debt and waits until the refill
/// has paid it off. In any window of N seconds at most
/// capacity + N × refillPerSecond requests are let through.
///
/// Hand-rolled because System.Threading.RateLimiting is not part of the base
/// shared framework and this is all the extension needs.
/// </summary>
internal sealed class RequestPacer
{
    private readonly object _gate = new();
    private readonly double _capacity;
    private readonly double _refillPerSecond;
    private readonly Func<TimeSpan> _clock;
    private double _tokens;
    private TimeSpan _lastRefill;

    /// <param name="capacity">Requests that may go out at once after an idle period.</param>
    /// <param name="refillPerSecond">Sustained requests per second once the burst is used up.</param>
    /// <param name="clock">Monotonic clock; injectable for tests.</param>
    public RequestPacer(int capacity, double refillPerSecond, Func<TimeSpan>? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(refillPerSecond);

        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
        if (clock is null)
        {
            var start = Stopwatch.GetTimestamp();
            clock = () => Stopwatch.GetElapsedTime(start);
        }

        _clock = clock;
        _tokens = capacity;
        _lastRefill = _clock();
    }

    /// <summary>
    /// Reserves <paramref name="cost"/> requests and returns how long the
    /// caller must wait before sending them (zero when the bucket covers it).
    /// </summary>
    internal TimeSpan Reserve(int cost)
    {
        lock (_gate)
        {
            var now = _clock();
            _tokens = Math.Min(_capacity, _tokens + ((now - _lastRefill).TotalSeconds * _refillPerSecond));
            _lastRefill = now;

            _tokens -= cost;
            return _tokens >= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(-_tokens / _refillPerSecond);
        }
    }

    /// <summary>Gives back a reservation whose requests were never sent.</summary>
    internal void Refund(int cost)
    {
        lock (_gate)
        {
            _tokens = Math.Min(_capacity, _tokens + cost);
        }
    }

    /// <summary>
    /// Reserves <paramref name="cost"/> requests and completes when they may
    /// be sent. The reservation is made synchronously, before the first await,
    /// so callers are served in the order they called. A cancelled wait gives
    /// its reservation back.
    /// </summary>
    public Task WaitAsync(int cost, CancellationToken ct)
    {
        var delay = Reserve(cost);
        return delay == TimeSpan.Zero ? Task.CompletedTask : DelayAsync(delay, cost, ct);
    }

    private async Task DelayAsync(TimeSpan delay, int cost, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Refund(cost);
            throw;
        }
    }
}
