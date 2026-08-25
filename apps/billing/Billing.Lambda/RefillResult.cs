namespace Billing.Lambda;

public class RefillResult
{
    public string Period { get; set; }
    public bool DryRun { get; set; }
    public float FreeGrant { get; set; }

    /// <summary>Users matched by the scan (free-tier role, no active subscription).</summary>
    public int Eligible { get; set; }

    public int Refilled { get; set; }

    /// <summary>Users whose ledger row for this period already exists (re-run no-ops).</summary>
    public int AlreadyRefilled { get; set; }

    /// <summary>Users who stopped being eligible between scan and write (e.g. subscribed mid-run).</summary>
    public int SkippedConcurrentChange { get; set; }

    public int Failed { get; set; }
}
