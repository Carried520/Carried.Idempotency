    using Carried.Idempotency.AspNet.Metadata;
    using Microsoft.AspNetCore.Builder;

    namespace Carried.Idempotency.AspNet.Extensions;

    /// <summary>
    /// Provides extension methods for requiring idempotency on ASP.NET Core endpoints.
    /// </summary>
    public static class IdempotencyEndpointConventionBuilderExtensions
    {
        extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder
        {
            /// <summary>
            /// Requires idempotency using default idempotency policy.
            /// </summary>
            /// <returns>The endpoint convention builder.</returns>
            public TBuilder RequireIdempotency()
            {
                builder.Add(endpointBuilder => { endpointBuilder.Metadata.Add(new IdempotencyMetadata()); });

                return builder;
            }

            /// <summary>
            /// Requires idempotency using the specified name policy.
            /// </summary>
            /// <param name="policyName">The name of idempotency policy to use.</param>
            /// <returns>The endpoint convention builder.</returns>
            public TBuilder RequireIdempotency(string policyName)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

                builder.Add(endpointBuilder => { endpointBuilder.Metadata.Add(new IdempotencyMetadata(policyName)); });

                return builder;
            }
        }
    }