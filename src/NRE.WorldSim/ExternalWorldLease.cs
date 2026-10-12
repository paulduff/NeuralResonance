namespace NRE.WorldSim;

/// <summary>One physical world may feed the brain. An expired lease leaves the
/// old world paused until an operator explicitly resumes it.</summary>
public sealed class ExternalWorldLease(TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private string? token;
    private string? owner;
    private DateTimeOffset expires;
    public bool IsHeld => token is not null && clock.GetUtcNow() < expires;
    public bool TryAcquire(string requestedOwner, out string? leaseToken)
    {
        leaseToken = null;
        if (string.IsNullOrWhiteSpace(requestedOwner) || requestedOwner.Length > 64 || IsHeld) return false;
        owner = requestedOwner;
        token = Guid.NewGuid().ToString("N");
        expires = clock.GetUtcNow().AddSeconds(10);
        leaseToken = token;
        return true;
    }
    public bool Renew(string requestedToken)
    {
        if (!IsHeld || !string.Equals(token, requestedToken, StringComparison.Ordinal)) return false;
        expires = clock.GetUtcNow().AddSeconds(10);
        return true;
    }
    public bool Release(string requestedToken)
    {
        if (!string.Equals(token, requestedToken, StringComparison.Ordinal)) return false;
        token = null; owner = null;
        return true;
    }
    public string? Owner => IsHeld ? owner : null;
}
