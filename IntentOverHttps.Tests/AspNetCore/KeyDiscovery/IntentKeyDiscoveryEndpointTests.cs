using System.Text.Json;
using IntentOverHttps.AspNetCore;
using IntentOverHttps.AspNetCore.KeyDiscovery;
using IntentOverHttps.Tests.AspNetCore.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IntentOverHttps.Tests.AspNetCore.KeyDiscovery;

/// <summary>
/// Integration tests for <c>MapIntentKeyDiscovery()</c>.
/// Uses a real in-process <see cref="TestServer"/> to exercise the full
/// ASP.NET Core pipeline including routing, JSON serialization and headers.
/// </summary>
public sealed class IntentKeyDiscoveryEndpointTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private static readonly IReadOnlyList<IntentPublicKey> TestKeys =
    [
        new IntentPublicKey("key-1", "EC", "P-256", "sig", "ES256", "QUJD", "REVG")
    ];

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddIntentOverHttps(opts =>
        {
            opts.Issuer = "test-issuer";
            opts.Version = "1";
        });
        builder.Services.AddSingleton<IIntentPublicKeyProvider>(new FakeIntentPublicKeyProvider(TestKeys));

        _app = builder.Build();
        _app.MapIntentKeyDiscovery();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    // ── HTTP status ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WellKnownEndpoint_ShouldReturn200Ok()
    {
        using var response = await _client.GetAsync("/.well-known/intent-keys");
        response.EnsureSuccessStatusCode();
    }

    // ── Cache-Control ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WellKnownEndpoint_ShouldSetCacheControlNoStore()
    {
        using var response = await _client.GetAsync("/.well-known/intent-keys");

        Assert.True(response.Headers.TryGetValues("Cache-Control", out var values));
        Assert.Contains("no-store", values);
    }

    // ── Response shape ────────────────────────────────────────────────────────

    [Fact]
    public async Task WellKnownEndpoint_ShouldReturnIssuerAndVersion()
    {
        using var response = await _client.GetAsync("/.well-known/intent-keys");
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal("test-issuer", doc.RootElement.GetProperty("issuer").GetString());
        Assert.Equal("1", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task WellKnownEndpoint_ShouldReturnPublishedKeys()
    {
        using var response = await _client.GetAsync("/.well-known/intent-keys");
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        var keys = doc.RootElement.GetProperty("keys").EnumerateArray().ToArray();
        Assert.Single(keys);

        var key = keys[0];
        Assert.Equal("key-1", key.GetProperty("kid").GetString());
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("P-256", key.GetProperty("crv").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
        Assert.Equal("QUJD", key.GetProperty("x").GetString());
        Assert.Equal("REVG", key.GetProperty("y").GetString());
    }

    // ── Configurable path ─────────────────────────────────────────────────────

    [Fact]
    public async Task WellKnownEndpoint_ShouldReturn404_ForUnregisteredPath()
    {
        using var response = await _client.GetAsync("/not-intent-keys");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WellKnownEndpoint_ShouldRespectCustomPath_WhenConfigured()
    {
        // Build a separate app with a custom well-known path
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddIntentOverHttps(opts =>
        {
            opts.Issuer = "issuer-b";
            opts.Version = "1";
            opts.WellKnownPath = "/custom/intent-keys";
        });
        builder.Services.AddSingleton<IIntentPublicKeyProvider>(new FakeIntentPublicKeyProvider());

        await using var customApp = builder.Build();
        customApp.MapIntentKeyDiscovery();
        await customApp.StartAsync();
        using var customClient = customApp.GetTestClient();

        using var response = await customClient.GetAsync("/custom/intent-keys");
        response.EnsureSuccessStatusCode();

        using var notFound = await customClient.GetAsync("/.well-known/intent-keys");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, notFound.StatusCode);
    }
}

