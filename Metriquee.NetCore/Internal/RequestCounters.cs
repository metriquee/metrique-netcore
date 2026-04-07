namespace Metriquee.NetCore.Internal;

internal sealed class RequestCounters
{
    private long _totalRequests;

    public void Increment()
    {
        Interlocked.Increment(ref _totalRequests);
    }

    public long SnapshotAndReset()
    {
        return Interlocked.Exchange(ref _totalRequests, 0);
    }
}