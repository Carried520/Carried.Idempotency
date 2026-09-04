using Microsoft.AspNetCore.Builder;

namespace Carried.Idempotency.AspNet;

public static class IdempotencyEndpointConventionBuilderExtensions
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        public TBuilder RequireIdempotency()
        {
            builder.Add(endpointBuilder => { endpointBuilder.Metadata.Add(new IdempotencyMetadata()); });

            return builder;
        }
    }
}