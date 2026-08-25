namespace Billing.Lambda;

public class RefillRequest
{
    /// <summary>
    /// Billing period being refilled, "yyyy-MM". Defaults to the current UTC month, which is
    /// correct for the scheduled 1st-of-month run; override for manual re-runs of a past period.
    /// </summary>
    public string Period { get; set; }

    /// <summary>When true, scans and reports eligible users without writing anything.</summary>
    public bool DryRun { get; set; }
}
