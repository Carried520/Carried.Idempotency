using Microsoft.AspNetCore.Builder;

namespace Carried.Idempotency.AspNet.Extensions;

/// <summary>
/// Provides extension methods for adding idempotency middleware to an ASP.NET Core application.
/// </summary>
public static class IdempotencyApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the idempotency middleware to the application's request pipeline.
        /// </summary>
        /// <remarks>
        /// The middleware must run after routing so that endpoint idempotency metadata
        /// is available.
        /// </remarks>
        /// <returns>The application builder.</returns>
        public IApplicationBuilder UseIdempotency()
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<IdempotencyMiddleware>();
        }
    }
}