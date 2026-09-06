namespace Carried.Idempotency.Options;

/// <summary>
/// Configures the behavior of the idempotency engine.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    /// Gets or sets how long an acquired idempotency key remains owned
    /// without a successful lease renewal.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets how long a completed result is retained for replay.
    /// </summary>
    public TimeSpan CompletedRetention { get; set; } = TimeSpan.FromHours(24);

    internal void Validate()
    {
        if (LeaseDuration.Ticks < 3)
            throw new ArgumentOutOfRangeException(
                nameof(LeaseDuration),
                "Lease duration must be at least three ticks.");

        if (CompletedRetention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(CompletedRetention),
                "Completed retention must be greater than zero.");
    }
}