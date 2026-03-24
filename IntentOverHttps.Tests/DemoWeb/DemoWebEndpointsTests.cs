using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.DemoWeb.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntentOverHttps.Tests.DemoWeb;

public sealed class DemoWebEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IntentHeaderParser _parser = new();

    public DemoWebEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    [Fact]
    public async Task PayDemo_ShouldReturnSignedIntentHeadersAndBody()
    {
        using var response = await _client.GetAsync("/pay/demo");

        response.EnsureSuccessStatusCode();

        var intentHeader = AssertSingleHeaderValue(response, "Intent");
        var signatureHeader = AssertSingleHeaderValue(response, "Intent-Signature");
        var keyIdHeader = AssertSingleHeaderValue(response, "Intent-Key-Id");
        var algorithmHeader = AssertSingleHeaderValue(response, "Intent-Alg");
        var versionHeader = AssertSingleHeaderValue(response, "Intent-Version");

        Assert.Equal("ES256", algorithmHeader);
        Assert.Equal("1", versionHeader);

        var parseResult = _parser.Parse(intentHeader, out var descriptor);

        Assert.True(parseResult.IsValid);
        Assert.NotNull(descriptor);
        Assert.Equal("pay", descriptor.Action);
        Assert.Equal(new Uri("http://localhost"), descriptor.TargetOrigin);

        var body = await response.Content.ReadFromJsonAsync<PayDemoResponse>();

        Assert.NotNull(body);
        Assert.Equal(descriptor.Issuer, body.Issuer);
        Assert.Equal(descriptor.Action, body.Action);
        Assert.Equal(descriptor.Beneficiary, body.Beneficiary);
        Assert.Equal(descriptor.Amount, body.Amount);
        Assert.Equal(descriptor.Currency, body.Currency);
        Assert.Equal(descriptor.Nonce, body.Nonce);
        Assert.Equal(descriptor.IssuedAt, body.IssuedAt);
        Assert.Equal(descriptor.ExpiresAt, body.ExpiresAt);
        Assert.Equal("http://localhost/.well-known/intent-keys", body.IntentKeysUrl);

        using var keysResponse = await _client.GetAsync("/.well-known/intent-keys");
        keysResponse.EnsureSuccessStatusCode();

        using var keysDocument = await JsonDocument.ParseAsync(await keysResponse.Content.ReadAsStreamAsync());
        var keysElement = keysDocument.RootElement.GetProperty("keys");
        var matchingKey = keysElement.EnumerateArray().Single(key => key.GetProperty("kid").GetString() == keyIdHeader);

        var signature = DecodeBase64Url(signatureHeader);
        var payload = Encoding.UTF8.GetBytes(intentHeader);
        var x = DecodeBase64Url(matchingKey.GetProperty("x").GetString()!);
        var y = DecodeBase64Url(matchingKey.GetProperty("y").GetString()!);

        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = x,
                Y = y
            }
        });

        Assert.True(verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public async Task IntentKeys_ShouldReturnPublishedPublicKeyMetadata()
    {
        using var response = await _client.GetAsync("/.well-known/intent-keys");

        response.EnsureSuccessStatusCode();
        Assert.Equal("no-store", AssertSingleHeaderValue(response, "Cache-Control"));

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var keys = root.GetProperty("keys").EnumerateArray().ToArray();
        var issuer = root.GetProperty("issuer").GetString();

        Assert.Equal("1", root.GetProperty("version").GetString());
        Assert.Single(keys);
        Assert.False(string.IsNullOrWhiteSpace(issuer));

        var x = keys[0].GetProperty("x").GetString();
        var y = keys[0].GetProperty("y").GetString();

        Assert.Equal("EC", keys[0].GetProperty("kty").GetString());
        Assert.Equal("P-256", keys[0].GetProperty("crv").GetString());
        Assert.Equal("sig", keys[0].GetProperty("use").GetString());
        Assert.Equal("ES256", keys[0].GetProperty("alg").GetString());
        Assert.False(string.IsNullOrWhiteSpace(x));
        Assert.False(string.IsNullOrWhiteSpace(y));
    }

    private static string AssertSingleHeaderValue(HttpResponseMessage response, string headerName)
    {
        Assert.True(response.Headers.TryGetValues(headerName, out var values));
        return Assert.Single(values);
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var paddingLength = (4 - normalized.Length % 4) % 4;
        normalized = normalized.PadRight(normalized.Length + paddingLength, '=');
        return Convert.FromBase64String(normalized);
    }
}

