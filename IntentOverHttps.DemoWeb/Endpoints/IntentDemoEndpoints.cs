using IntentOverHttps.AspNetCore;
using IntentOverHttps.Core.Models;
using IntentOverHttps.DemoWeb.Contracts;
using IntentOverHttps.DemoWeb.Options;
using Microsoft.Extensions.Options;

namespace IntentOverHttps.DemoWeb.Endpoints;

public static class IntentDemoEndpoints
{
    public static IEndpointRouteBuilder MapIntentDemoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/pay/demo", HandlePayDemoAsync)
            .WithName("GetPayDemo")
            .WithTags("Intent Demo")
            .Produces<PayDemoResponse>();

        return endpoints;
    }

    private static async Task HandlePayDemoAsync(
        HttpContext httpContext,
        IOptions<DemoIntentOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(settings.LifetimeSeconds);
        var descriptor = new IntentDescriptor(
            action: settings.Action,
            issuer: settings.Issuer,
            targetOrigin: GetRequestOrigin(httpContext.Request),
            beneficiary: settings.Beneficiary,
            amount: settings.Amount,
            currency: settings.Currency,
            issuedAt: now,
            expiresAt: expiresAt,
            nonce: Guid.NewGuid().ToString("N"));

        var intentKeysUrl = $"{GetRequestOrigin(httpContext.Request).AbsoluteUri}.well-known/intent-keys";
        var response = new PayDemoResponse(
            Message: "Demo payment intent generated successfully.",
            Issuer: descriptor.Issuer,
            Action: descriptor.Action,
            Beneficiary: descriptor.Beneficiary,
            Amount: descriptor.Amount,
            Currency: descriptor.Currency,
            Nonce: descriptor.Nonce,
            IssuedAt: descriptor.IssuedAt,
            ExpiresAt: descriptor.ExpiresAt,
            IntentKeysUrl: intentKeysUrl);

        await httpContext.WriteIntentSignedJsonAsync(descriptor, response, cancellationToken: cancellationToken);
    }

    private static Uri GetRequestOrigin(HttpRequest request)
    {
        var host = request.Host.HasValue
            ? request.Host.Value
            : throw new InvalidOperationException("Request host is required to build the target origin.");

        return new Uri($"{request.Scheme}://{host}", UriKind.Absolute);
    }
}

