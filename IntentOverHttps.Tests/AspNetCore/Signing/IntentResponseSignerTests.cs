using IntentOverHttps.AspNetCore;
using IntentOverHttps.AspNetCore.Signing;
using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.Tests.AspNetCore.Fakes;
using IntentOverHttps.Tests.Core.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IntentOverHttps.Tests.AspNetCore.Signing;

/// <summary>
/// Tests for <see cref="IIntentResponseSigner"/> registered via
/// <see cref="IntentServiceCollectionExtensions.AddIntentOverHttps"/>.
/// Verifies that all protocol headers are written correctly.
/// </summary>
public sealed class IntentResponseSignerTests
{
    private readonly IntentHeaderSerializer _serializer = new();

    private IIntentResponseSigner CreateSigner(
        FakeIntentSigner? signer = null,
        FakeIntentKeyMetadataProvider? keyMetadata = null,
        Action<IntentProtocolOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddIntentOverHttps(opts =>
        {
            opts.Version = "1";
            configureOptions?.Invoke(opts);
        });
        services.AddSingleton<IIntentSigner>(signer ?? new FakeIntentSigner());
        services.AddSingleton<IIntentKeyMetadataProvider>(keyMetadata ?? new FakeIntentKeyMetadataProvider());

        return services.BuildServiceProvider().GetRequiredService<IIntentResponseSigner>();
    }

    // ── Header presence ───────────────────────────────────────────────────────

    [Fact]
    public async Task WriteHeadersAsync_ShouldWriteAllFiveProtocolHeaders()
    {
        // Arrange
        var signer = CreateSigner();
        var descriptor = IntentDescriptorFactory.CreateValid();
        var context = new DefaultHttpContext();

        // Act
        await signer.WriteHeadersAsync(context.Response, descriptor);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Intent));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Signature));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.KeyId));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Algorithm));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Version));
    }

    // ── Intent header ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteHeadersAsync_IntentHeader_ShouldMatchSerializedDescriptor()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid();
        var expected = _serializer.Serialize(descriptor);
        var signer = CreateSigner();
        var context = new DefaultHttpContext();

        // Act
        await signer.WriteHeadersAsync(context.Response, descriptor);

        // Assert
        Assert.Equal(expected, context.Response.Headers[IntentHeaderNames.Intent].ToString());
    }

    // ── Signature header ──────────────────────────────────────────────────────

    [Fact]
    public async Task WriteHeadersAsync_SignatureHeader_ShouldBeBase64UrlEncodedSignatureBytes()
    {
        // Arrange
        // base64url of [0x01, 0x02, 0x03, 0xFB] = "AQID-w" (no padding, '-' for '+', '_' for '/')
        var signatureBytes = new byte[] { 0x01, 0x02, 0x03, 0xFB };
        var fakeSigner = new FakeIntentSigner(signatureBytes);
        var signer = CreateSigner(signer: fakeSigner);
        var descriptor = IntentDescriptorFactory.CreateValid();
        var context = new DefaultHttpContext();

        // Act
        await signer.WriteHeadersAsync(context.Response, descriptor);

        // Assert
        var sigHeader = context.Response.Headers[IntentHeaderNames.Signature].ToString();
        Assert.Equal("AQID-w", sigHeader);
        Assert.DoesNotContain("=", sigHeader);  // no padding
        Assert.DoesNotContain("+", sigHeader);  // URL-safe
        Assert.DoesNotContain("/", sigHeader);  // URL-safe
    }

    // ── Key metadata headers ──────────────────────────────────────────────────

    [Fact]
    public async Task WriteHeadersAsync_KeyIdAndAlgorithmHeaders_ShouldMatchKeyMetadataProvider()
    {
        // Arrange
        var fakeMetadata = new FakeIntentKeyMetadataProvider(keyId: "prod-key-v3", algorithm: "ES256");
        var signer = CreateSigner(keyMetadata: fakeMetadata);
        var descriptor = IntentDescriptorFactory.CreateValid();
        var context = new DefaultHttpContext();

        // Act
        await signer.WriteHeadersAsync(context.Response, descriptor);

        // Assert
        Assert.Equal("prod-key-v3", context.Response.Headers[IntentHeaderNames.KeyId].ToString());
        Assert.Equal("ES256", context.Response.Headers[IntentHeaderNames.Algorithm].ToString());
    }

    // ── Version header ────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteHeadersAsync_VersionHeader_ShouldReflectConfiguredVersion()
    {
        // Arrange
        var signer = CreateSigner(configureOptions: opts => opts.Version = "2");
        var descriptor = IntentDescriptorFactory.CreateValid();
        var context = new DefaultHttpContext();

        // Act
        await signer.WriteHeadersAsync(context.Response, descriptor);

        // Assert
        Assert.Equal("2", context.Response.Headers[IntentHeaderNames.Version].ToString());
    }

    // ── Null guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteHeadersAsync_ShouldThrowArgumentNullException_WhenDescriptorIsNull()
    {
        var signer = CreateSigner();
        var context = new DefaultHttpContext();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => signer.WriteHeadersAsync(context.Response, null!).AsTask());
    }

    [Fact]
    public async Task WriteHeadersAsync_ShouldThrowArgumentNullException_WhenResponseIsNull()
    {
        var signer = CreateSigner();
        var descriptor = IntentDescriptorFactory.CreateValid();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => signer.WriteHeadersAsync(null!, descriptor).AsTask());
    }
}

