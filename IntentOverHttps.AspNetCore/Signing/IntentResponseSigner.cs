using IntentOverHttps.AspNetCore.Internal;
using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IntentOverHttps.AspNetCore.Signing;

/// <summary>
/// Default <see cref="IIntentResponseSigner"/> implementation.
/// Serializes the intent, signs it, and writes all five protocol headers.
/// </summary>
internal sealed class IntentResponseSigner : IIntentResponseSigner
{
    private readonly IIntentSigner _signer;
    private readonly IIntentKeyMetadataProvider _keyMetadataProvider;
    private readonly IntentHeaderSerializer _serializer;
    private readonly IOptions<IntentProtocolOptions> _options;

    public IntentResponseSigner(
        IIntentSigner signer,
        IIntentKeyMetadataProvider keyMetadataProvider,
        IntentHeaderSerializer serializer,
        IOptions<IntentProtocolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(keyMetadataProvider);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(options);

        _signer = signer;
        _keyMetadataProvider = keyMetadataProvider;
        _serializer = serializer;
        _options = options;
    }

    /// <inheritdoc />
    public async ValueTask WriteHeadersAsync(
        HttpResponse response,
        IntentDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(descriptor);

        var canonical = _serializer.Serialize(descriptor);
        var signatureBytes = await _signer.SignAsync(descriptor, cancellationToken);
        var keyMetadata = await _keyMetadataProvider.GetCurrentKeyMetadataAsync(cancellationToken);

        response.Headers[IntentHeaderNames.Intent] = canonical;
        response.Headers[IntentHeaderNames.Signature] = Base64Url.Encode(signatureBytes);
        response.Headers[IntentHeaderNames.KeyId] = keyMetadata.KeyId;
        response.Headers[IntentHeaderNames.Algorithm] = keyMetadata.Algorithm;
        response.Headers[IntentHeaderNames.Version] = _options.Value.Version;
    }
}

