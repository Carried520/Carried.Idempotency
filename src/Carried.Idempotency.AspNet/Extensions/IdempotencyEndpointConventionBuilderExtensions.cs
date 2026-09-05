using Carried.Idempotency.AspNet.Metadata;
using Microsoft.AspNetCore.Builder;

namespace Carried.Idempotency.AspNet.Extensions;

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