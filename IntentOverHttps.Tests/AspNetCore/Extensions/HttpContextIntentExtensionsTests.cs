using System.Text.Json;
using IntentOverHttps.AspNetCore;
using IntentOverHttps.AspNetCore.Signing;
using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.Tests.AspNetCore.Fakes;
using IntentOverHttps.Tests.Core.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IntentOverHttps.Tests.AspNetCore.Extensions;

/// <summary>
/// Tests for <see cref="HttpContextIntentExtensions.WriteIntentSignedJsonAsync{T}"/>.
/// Verifies that both the IoHTTPS headers and the JSON body are written to the response.
/// </summary>
public sealed class HttpContextIntentExtensionsTests
{
    private readonly IntentHeaderSerializer _serializer = new();

    private HttpContext CreateContext(
        FakeIntentSigner? signer = null,
        FakeIntentKeyMetadataProvider? keyMetadata = null)
    {
        var services = new ServiceCollection();
        services.AddIntentOverHttps(opts => opts.Version = "1");
        services.AddSingleton<IIntentSigner>(signer ?? new FakeIntentSigner());
        services.AddSingleton<IIntentKeyMetadataProvider>(keyMetadata ?? new FakeIntentKeyMetadataProvider());

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    // ── Header emission ───────────────────────────────────────────────────────

    [Fact]
    public async Task WriteIntentSignedJsonAsync_ShouldWriteAllFiveProtocolHeaders()
    {
        // Arrange
        var context = CreateContext();
        var descriptor = IntentDescriptorFactory.CreateValid();

        // Act
        await context.WriteIntentSignedJsonAsync(descriptor, new { ok = true });

        // Assert
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Intent));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Signature));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.KeyId));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Algorithm));
        Assert.True(context.Response.Headers.ContainsKey(IntentHeaderNames.Version));
    }

    [Fact]
    public async Task WriteIntentSignedJsonAsync_IntentHeader_ShouldContainSerializedDescriptor()
    {
        // Arrange
        var context = CreateContext();
        var descriptor = IntentDescriptorFactory.CreateValid();
        var expected = _serializer.Serialize(descriptor);

        // Act
        await context.WriteIntentSignedJsonAsync(descriptor, new { ok = true });

        // Assert
        Assert.Equal(expected, context.Response.Headers[IntentHeaderNames.Intent].ToString());
    }

    // ── Body emission ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteIntentSignedJsonAsync_ShouldWriteJsonBodyToResponse()
    {
        // Arrange
        var context = CreateContext();
        var descriptor = IntentDescriptorFactory.CreateValid();
        var body = new { message = "hello", amount = 42 };

        // Act
        await context.WriteIntentSignedJsonAsync(descriptor, body);

        // Assert body content
        context.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("hello", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(42, json.RootElement.GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task WriteIntentSignedJsonAsync_ShouldSetJsonContentType()
    {
        // Arrange
        var context = CreateContext();
        var descriptor = IntentDescriptorFactory.CreateValid();

        // Act
        await context.WriteIntentSignedJsonAsync(descriptor, new { ok = true });

        // Assert — content-type must start with application/json
        Assert.StartsWith("application/json", context.Response.ContentType);
    }

    // ── Null guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteIntentSignedJsonAsync_ShouldThrow_WhenDescriptorIsNull()
    {
        var context = CreateContext();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => context.WriteIntentSignedJsonAsync<object>(null!, new { }));
    }

    [Fact]
    public async Task WriteIntentSignedJsonAsync_ShouldThrow_WhenContextIsNull()
    {
        HttpContext? nullContext = null;
        var descriptor = IntentDescriptorFactory.CreateValid();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => nullContext!.WriteIntentSignedJsonAsync(descriptor, new { }));
    }
}

