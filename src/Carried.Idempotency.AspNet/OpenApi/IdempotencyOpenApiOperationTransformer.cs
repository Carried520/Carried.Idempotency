using Carried.Idempotency.AspNet.Metadata;
using Carried.Idempotency.AspNet.Options;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Carried.Idempotency.AspNet.OpenApi;

internal sealed class IdempotencyOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    private readonly IdempotencyAspNetOptions _options;

    public IdempotencyOpenApiOperationTransformer(IOptions<IdempotencyAspNetOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        bool requiresIdempotency =
            context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotencyMetadata>().Any();

        if (!requiresIdempotency)
            return Task.CompletedTask;

        operation.Parameters ??= new List<IOpenApiParameter>();

        bool alreadyExists = operation.Parameters.Any(parameter =>
            parameter.In == ParameterLocation.Header && string.Equals(
                parameter.Name,
                _options.HeaderName,
                StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
            return Task.CompletedTask;

        operation.Parameters.Add(
            new OpenApiParameter
            {
                Name = _options.HeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Unique key used to make retries of the same operation idempotent.",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String
                }
            });

        return Task.CompletedTask;
    }
}