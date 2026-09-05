using Carried.Idempotency.AspNet.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Carried.Idempotency.AspNet.OpenApi;

public class IdempotencyOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        bool requiresIdempotency = context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotencyMetadata>().Any();

        if (!requiresIdempotency)
            return Task.CompletedTask;

        operation.Parameters ??= new List<IOpenApiParameter>();

        bool alreadyExists = operation.Parameters.Any(parameter =>
            parameter.In == ParameterLocation.Header && string.Equals(parameter.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
            return Task.CompletedTask;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
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