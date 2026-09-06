using Carried.Idempotency.AspNet.Errors;
using Carried.Idempotency.AspNet.Extensions;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddIdempotency(options =>
{
    options.UseInMemory();
    options.HeaderName = "Default-Key-Name";
    options.AddPolicy("strict" ,
        policy =>
        {
            policy.MaxKeyLength = 255;
        });
});

builder.Services.AddIdempotencyOpenApi();
builder.Services.AddIdempotencyLogging();
builder.Services.AddIdempotencyMetrics();
builder.Services.AddIdempotencyFingerprintHeader(
        "X-Tenant-Id"); // add customized fingerprint header - it will affect idempotency result


WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseIdempotency();

app.MapPost(
        "/orders",
        () => Results.Created(
            "/orders/123",
            new
            {
                Id = 123
            }))
    .RequireIdempotency();

app.MapPost(
        "/fakeorders",
        () => Results.Created(
            "/orders/123",
            new
            {
                Id = 123
            }))
    .RequireIdempotency();

app.Run();