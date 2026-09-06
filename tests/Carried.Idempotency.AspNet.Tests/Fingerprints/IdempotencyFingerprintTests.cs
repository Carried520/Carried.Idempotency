using System.Text;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Fingerprinting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Carried.Idempotency.AspNet.Tests.Fingerprints;

public sealed class IdempotencyFingerprintCustomizationTests
{
    [Fact]
    public async Task SameContribution_ProducesSameFingerprint()
    {
        DefaultHttpContext firstContext =
            CreateContext();

        DefaultHttpContext secondContext =
            CreateContext();

        var contributor =
            new TestFingerprintContributor(
                "tenant",
                "tenant-1");

        string firstFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                firstContext,
                "/orders",
                [contributor]);

        string secondFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                secondContext,
                "/orders",
                [contributor]);

        Assert.Equal(
            firstFingerprint,
            secondFingerprint);
    }

    [Fact]
    public async Task DifferentContribution_ProducesDifferentFingerprint()
    {
        DefaultHttpContext firstContext =
            CreateContext();

        DefaultHttpContext secondContext =
            CreateContext();

        string firstFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                firstContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "tenant",
                        "tenant-1")
                ]);

        string secondFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                secondContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "tenant",
                        "tenant-2")
                ]);

        Assert.NotEqual(
            firstFingerprint,
            secondFingerprint);
    }

    [Fact]
    public async Task MissingAndEmptyContribution_ProduceDifferentFingerprints()
    {
        DefaultHttpContext missingContext =
            CreateContext();

        DefaultHttpContext emptyContext =
            CreateContext();

        string missingFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                missingContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "tenant",
                        null)
                ]);

        string emptyFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                emptyContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "tenant",
                        string.Empty)
                ]);

        Assert.NotEqual(
            missingFingerprint,
            emptyFingerprint);
    }

    [Fact]
    public async Task ContributorOrder_DoesNotAffectFingerprint()
    {
        DefaultHttpContext firstContext =
            CreateContext();

        DefaultHttpContext secondContext =
            CreateContext();

        string firstFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                firstContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "tenant",
                        "tenant-1"),
                    new TestFingerprintContributor(
                        "region",
                        "eu")
                ]);

        string secondFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                secondContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "region",
                        "eu"),
                    new TestFingerprintContributor(
                        "tenant",
                        "tenant-1")
                ]);

        Assert.Equal(
            firstFingerprint,
            secondFingerprint);
    }

    [Fact]
    public async Task ContributorName_AffectsFingerprint()
    {
        DefaultHttpContext firstContext =
            CreateContext();

        DefaultHttpContext secondContext =
            CreateContext();

        string firstFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                firstContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "tenant",
                        "value")
                ]);

        string secondFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                secondContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "region",
                        "value")
                ]);

        Assert.NotEqual(
            firstFingerprint,
            secondFingerprint);
    }

    [Fact]
    public async Task RegisteredAbsentContributor_AffectsFingerprint()
    {
        DefaultHttpContext withoutContributorContext =
            CreateContext();

        DefaultHttpContext withContributorContext =
            CreateContext();

        string withoutContributorFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                withoutContributorContext,
                "/orders",
                []);

        string withContributorFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                withContributorContext,
                "/orders",
                [
                    new TestFingerprintContributor(
                        "tenant",
                        null)
                ]);

        Assert.NotEqual(
            withoutContributorFingerprint,
            withContributorFingerprint);
    }

    [Fact]
    public async Task HeaderContributor_HeaderValueAffectsFingerprint()
    {
        DefaultHttpContext firstContext =
            CreateContext();

        DefaultHttpContext secondContext =
            CreateContext();

        firstContext.Request.Headers["X-Tenant-Id"] =
            "tenant-1";

        secondContext.Request.Headers["X-Tenant-Id"] =
            "tenant-2";

        var contributor =
            new HeaderFingerprintContributor(
                "X-Tenant-Id");

        string firstFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                firstContext,
                "/orders",
                [contributor]);

        string secondFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                secondContext,
                "/orders",
                [contributor]);

        Assert.NotEqual(
            firstFingerprint,
            secondFingerprint);
    }

    [Fact]
    public async Task HeaderContributor_MissingAndEmptyHeader_ProduceDifferentFingerprints()
    {
        DefaultHttpContext missingContext =
            CreateContext();

        DefaultHttpContext emptyContext =
            CreateContext();

        emptyContext.Request.Headers["X-Tenant-Id"] =
            string.Empty;

        var contributor =
            new HeaderFingerprintContributor(
                "X-Tenant-Id");

        string missingFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                missingContext,
                "/orders",
                [contributor]);

        string emptyFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                emptyContext,
                "/orders",
                [contributor]);

        Assert.NotEqual(
            missingFingerprint,
            emptyFingerprint);
    }

    [Fact]
    public async Task NoContributors_ProducesStableFingerprint()
    {
        DefaultHttpContext firstContext =
            CreateContext();

        DefaultHttpContext secondContext =
            CreateContext();

        string firstFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                firstContext,
                "/orders",
                []);

        string secondFingerprint =
            await RequestFingerprintProvider.CreateAsync(
                secondContext,
                "/orders",
                []);

        Assert.Equal(
            firstFingerprint,
            secondFingerprint);
    }

    [Fact]
    public void AddIdempotencyFingerprintContributor_RegistersContributor()
    {
        var services =
            new ServiceCollection();

        services.AddIdempotencyFingerprintContributor<
            RegisteredTestFingerprintContributor>();

        using ServiceProvider provider =
            services.BuildServiceProvider();

        IIdempotencyFingerprintContributor[] contributors =
            provider
                .GetServices<IIdempotencyFingerprintContributor>()
                .ToArray();

        Assert.Single(
            contributors);

        Assert.IsType<RegisteredTestFingerprintContributor>(
            contributors[0]);
    }

    [Fact]
    public void AddIdempotencyFingerprintContributor_SameTypeRegisteredTwice_RegistersOnce()
    {
        var services =
            new ServiceCollection();

        services.AddIdempotencyFingerprintContributor<
            RegisteredTestFingerprintContributor>();

        services.AddIdempotencyFingerprintContributor<
            RegisteredTestFingerprintContributor>();

        using ServiceProvider provider =
            services.BuildServiceProvider();

        IIdempotencyFingerprintContributor[] contributors =
            provider
                .GetServices<IIdempotencyFingerprintContributor>()
                .ToArray();

        Assert.Single(
            contributors);
    }

    [Fact]
    public void AddIdempotencyFingerprintHeader_RegistersHeaderContributor()
    {
        var services =
            new ServiceCollection();

        services.AddIdempotencyFingerprintHeader(
            "X-Tenant-Id");

        using ServiceProvider provider =
            services.BuildServiceProvider();

        IIdempotencyFingerprintContributor[] contributors =
            provider
                .GetServices<IIdempotencyFingerprintContributor>()
                .ToArray();

        IIdempotencyFingerprintContributor contributor =
            Assert.Single(
                contributors);

        Assert.Equal(
            "header:X-Tenant-Id",
            contributor.Name);

        Assert.IsType<HeaderFingerprintContributor>(
            contributor);
    }

    [Fact]
    public void AddIdempotencyFingerprintHeader_MultipleHeaders_RegistersAllContributors()
    {
        var services =
            new ServiceCollection();

        services.AddIdempotencyFingerprintHeader(
            "X-Tenant-Id");

        services.AddIdempotencyFingerprintHeader(
            "X-Region");

        using ServiceProvider provider =
            services.BuildServiceProvider();

        IIdempotencyFingerprintContributor[] contributors =
            provider
                .GetServices<IIdempotencyFingerprintContributor>()
                .ToArray();

        Assert.Equal(
            2,
            contributors.Length);

        Assert.Contains(
            contributors,
            contributor =>
                contributor.Name == "header:X-Tenant-Id");

        Assert.Contains(
            contributors,
            contributor =>
                contributor.Name == "header:X-Region");
    }

    [Fact]
    public async Task DuplicateFingerprintContributorNames_FailAtStartup()
    {
        HostApplicationBuilder builder =
            Host.CreateApplicationBuilder();

        builder.Services.AddIdempotency(opts => opts.UseInMemory());

        builder.Services.AddIdempotencyFingerprintHeader(
            "X-Tenant-Id");

        builder.Services.AddIdempotencyFingerprintHeader(
            "X-Tenant-Id");

        using IHost host =
            builder.Build();

        var exception =
            await Assert.ThrowsAsync<OptionsValidationException>(
                () => host.StartAsync());

        Assert.Contains(
            "Fingerprint contributor name 'header:X-Tenant-Id' is already configured.",
            exception.Failures);
    }

    [Fact]
    public async Task NullFingerprintContributorName_FailsAtStartup()
    {
        HostApplicationBuilder builder =
            Host.CreateApplicationBuilder();

        builder.Services.AddIdempotency(opts => opts.UseInMemory());

        builder.Services.AddIdempotencyFingerprintContributor<
            NullNameFingerprintContributor>();

        using IHost host =
            builder.Build();

        OptionsValidationException exception =
            await Assert.ThrowsAsync<OptionsValidationException>(
                () => host.StartAsync());

        Assert.Contains(
            "Contributor name cannot be null or whitespace.",
            exception.Failures);
    }

    [Fact]
    public async Task WhitespaceFingerprintContributorName_FailsAtStartup()
    {
        HostApplicationBuilder builder =
            Host.CreateApplicationBuilder();

        builder.Services.AddIdempotency(opts => opts.UseInMemory());

        builder.Services.AddIdempotencyFingerprintContributor<
            WhitespaceNameFingerprintContributor>();

        using IHost host =
            builder.Build();

        var exception =
            await Assert.ThrowsAsync<OptionsValidationException>(
                () => host.StartAsync());

        Assert.Contains(
            "Contributor name cannot be null or whitespace.",
            exception.Failures);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context =
            new DefaultHttpContext();

        context.Request.Method =
            HttpMethods.Post;

        context.Request.ContentType =
            "application/json";

        context.Request.Body =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    """{"value":1}"""));

        return context;
    }

    private sealed class TestFingerprintContributor
        : IIdempotencyFingerprintContributor
    {
        private readonly string? _value;

        public string Name { get; }

        public TestFingerprintContributor(
            string name,
            string? value)
        {
            Name = name;
            _value = value;
        }

        public ValueTask<string?> GetValueAsync(
            HttpContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                _value);
        }
    }

    private sealed class RegisteredTestFingerprintContributor
        : IIdempotencyFingerprintContributor
    {
        public string Name =>
            "registered-test";

        public ValueTask<string?> GetValueAsync(
            HttpContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(
                "value");
        }
    }

    private sealed class NullNameFingerprintContributor
        : IIdempotencyFingerprintContributor
    {
        public string Name =>
            null!;

        public ValueTask<string?> GetValueAsync(
            HttpContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(
                null);
        }
    }

    private sealed class WhitespaceNameFingerprintContributor
        : IIdempotencyFingerprintContributor
    {
        public string Name =>
            "   ";

        public ValueTask<string?> GetValueAsync(
            HttpContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(
                null);
        }
    }
}