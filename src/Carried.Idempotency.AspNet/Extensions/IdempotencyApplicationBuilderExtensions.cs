using Microsoft.AspNetCore.Builder;

namespace Carried.Idempotency.AspNet.Extensions;

public static class IdempotencyApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseIdempotency()
        {
            ArgumentNullException.ThrowIfNull(app);
            
            return app.UseMiddleware<IdempotencyMiddleware>();
        }
    }
}