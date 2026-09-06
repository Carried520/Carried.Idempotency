using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Fingerprinting;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;

namespace Carried.Idempotency.AspNet.Tests.Validation;

public sealed class IdempotencyAspNetOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var options =
            new IdempotencyAspNetOptions();

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Succeeded);
    }

    [Fact]
    public void Validate_BlankHeaderName_Fails()
    {
        var options =
            new IdempotencyAspNetOptions
            {
                HeaderName = " "
            };

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Failed);

        Assert.Contains(
            "HeaderName is required.",
            result.Failures);
    }

    [Fact]
    public void Validate_InvalidHeaderName_Fails()
    {
        var options =
            new IdempotencyAspNetOptions
            {
                HeaderName = "bad header"
            };

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Failed);

        Assert.Contains(
            "HeaderName 'bad header' is not a valid HTTP header name.",
            result.Failures);
    }

    [Fact]
    public void Validate_ValidCustomHeaderName_Succeeds()
    {
        var options =
            new IdempotencyAspNetOptions
            {
                HeaderName = "X-Request-Key"
            };

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Succeeded);
    }

    [Fact]
    public void Validate_DefaultPolicyWithZeroMaxKeyLength_Fails()
    {
        var options =
            new IdempotencyAspNetOptions();

        options.DefaultPolicy.MaxKeyLength = 0;

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Failed);

        Assert.Contains(
            "DefaultPolicy.MaxKeyLength must be greater than zero.",
            result.Failures);
    }

    [Fact]
    public void Validate_DefaultPolicyWithNegativeMaxRetainedResponseBodySize_Fails()
    {
        var options =
            new IdempotencyAspNetOptions();

        options.DefaultPolicy.MaxRetainedResponseBodySize = -1;

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Failed);

        Assert.Contains(
            "DefaultPolicy.MaxRetainedResponseBodySize cannot be negative.",
            result.Failures);
    }

    [Fact]
    public void Validate_DefaultPolicyWithZeroMaxRetainedResponseBodySize_Succeeds()
    {
        var options =
            new IdempotencyAspNetOptions();

        options.DefaultPolicy.MaxRetainedResponseBodySize = 0;

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidNamedPolicy_FailsWithPolicyPath()
    {
        var options =
            new IdempotencyAspNetOptions();

        options.AddPolicy(
            "strict",
            policy =>
            {
                policy.MaxKeyLength = 0;
            });

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Failed);

        Assert.Contains(
            "Policies['strict'].MaxKeyLength must be greater than zero.",
            result.Failures);
    }

    [Fact]
    public void Validate_MultipleInvalidValues_ReturnsAllFailures()
    {
        var options =
            new IdempotencyAspNetOptions
            {
                HeaderName = "bad header"
            };

        options.DefaultPolicy.MaxKeyLength = 0;
        options.DefaultPolicy.MaxRetainedResponseBodySize = -1;

        options.AddPolicy(
            "strict",
            policy =>
            {
                policy.MaxKeyLength = 0;
            });

        var validator =
            CreateValidator();

        var result =
            validator.Validate(
                null,
                options);

        Assert.True(
            result.Failed);

        Assert.Contains(
            "HeaderName 'bad header' is not a valid HTTP header name.",
            result.Failures);

        Assert.Contains(
            "DefaultPolicy.MaxKeyLength must be greater than zero.",
            result.Failures);

        Assert.Contains(
            "DefaultPolicy.MaxRetainedResponseBodySize cannot be negative.",
            result.Failures);

        Assert.Contains(
            "Policies['strict'].MaxKeyLength must be greater than zero.",
            result.Failures);

        Assert.Equal(
            4,
            result.Failures.Count());
    }
    
    [Fact]
public void Validate_DefaultPolicyWithBlankReplayHeader_Fails()
{
    var options =
        new IdempotencyAspNetOptions();

    options.DefaultPolicy.ReplayHeaders.Add(
        " ");

    var validator =
        CreateValidator();

    var result =
        validator.Validate(
            null,
            options);

    Assert.True(
        result.Failed);

    Assert.Contains(
        "DefaultPolicy.ReplayHeaders cannot contain empty or whitespace header names.",
        result.Failures);
}

[Fact]
public void Validate_DefaultPolicyWithInvalidReplayHeader_Fails()
{
    var options =
        new IdempotencyAspNetOptions();

    options.DefaultPolicy.ReplayHeaders.Add(
        "bad header");

    var validator =
        CreateValidator();

    var result =
        validator.Validate(
            null,
            options);

    Assert.True(
        result.Failed);

    Assert.Contains(
        "DefaultPolicy.ReplayHeaders contains invalid HTTP header name 'bad header'.",
        result.Failures);
}

[Fact]
public void Validate_DefaultPolicyWithCustomValidReplayHeader_Succeeds()
{
    var options =
        new IdempotencyAspNetOptions();

    options.DefaultPolicy.ReplayHeaders.Add(
        "X-Custom-Header");

    var validator =
        CreateValidator();

    var result =
        validator.Validate(
            null,
            options);

    Assert.True(
        result.Succeeded);
}

[Fact]
public void Validate_NamedPolicyWithInvalidReplayHeader_FailsWithPolicyPath()
{
    var options =
        new IdempotencyAspNetOptions();

    options.AddPolicy(
        "strict",
        policy =>
        {
            policy.ReplayHeaders.Add(
                "bad header");
        });

    var validator =
        CreateValidator();

    var result =
        validator.Validate(
            null,
            options);

    Assert.True(
        result.Failed);

    Assert.Contains(
        "Policies['strict'].ReplayHeaders contains invalid HTTP header name 'bad header'.",
        result.Failures);
}


[Fact]
public async Task StartAsync_InvalidHeaderName_Fails()
{
    WebApplicationBuilder builder =
        WebApplication.CreateBuilder();

    builder.WebHost.UseTestServer();

    builder.Services.AddIdempotency(
        configureAspNetOptions: options =>
        {
            options.HeaderName = "bad header";
        });

    await using WebApplication app =
        builder.Build();

    await Assert.ThrowsAsync<OptionsValidationException>(
        () => app.StartAsync());
}

[Fact]
public async Task StartAsync_InvalidDefaultPolicy_Fails()
{
    WebApplicationBuilder builder =
        WebApplication.CreateBuilder();

    builder.WebHost.UseTestServer();

    builder.Services.AddIdempotency(
        configureAspNetOptions: options =>
        {
            options.DefaultPolicy.MaxKeyLength = 0;
        });

    await using WebApplication app =
        builder.Build();

    await Assert.ThrowsAsync<OptionsValidationException>(
        () => app.StartAsync());
}

[Fact]
public async Task StartAsync_ValidOptions_Succeeds()
{
    WebApplicationBuilder builder =
        WebApplication.CreateBuilder();

    builder.WebHost.UseTestServer();

    builder.Services.AddIdempotency(
        configureAspNetOptions: options =>
        {
            options.HeaderName =
                "X-Idempotency-Key";
        });

    await using WebApplication app =
        builder.Build();

    await app.StartAsync();
}

    private static IdempotencyAspNetOptionsValidator CreateValidator()
    {
        return new IdempotencyAspNetOptionsValidator(
            Array.Empty<IIdempotencyFingerprintContributor>());
    }
}