namespace Carried.Idempotency.Options;

public sealed class IdempotencyOptions
{
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CompletedRetention { get; set; } = TimeSpan.FromHours(24);

    internal void Validate()
    {
        if (LeaseDuration.Ticks < 3)
            throw new ArgumentOutOfRangeException(
                nameof(LeaseDuration),
                "Lease duration must be positive and large enough for lease renewal.");

        if (CompletedRetention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(CompletedRetention),
                "Completed retention must be greater than zero.");
    }
}