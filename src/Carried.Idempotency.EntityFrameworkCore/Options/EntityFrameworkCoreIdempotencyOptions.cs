namespace Carried.Idempotency.EntityFrameworkCore.Options;

/// <summary>
/// Configures the Entity Framework Core persistence model used by Carried.Idempotency.
/// </summary>
public sealed class EntityFrameworkCoreIdempotencyOptions
{
    /// <summary>
    /// Gets or sets the name of the table used to store idempotency entries.
    /// </summary>
    /// <remarks>
    /// The default value is <c>__CarriedIdempotency</c>.
    /// </remarks>
    public string TableName { get; set; } = "__CarriedIdempotency";

    /// <summary>
    /// Gets or sets the schema containing the idempotency table.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value uses the database provider's default schema.
    /// </remarks>
    public string? Schema { get; set; }
}