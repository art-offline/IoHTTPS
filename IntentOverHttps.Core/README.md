# IntentOverHttps.Core

Protocol core library with no ASP.NET dependency. It models, serializes, parses, and validates canonical IoHTTPS intent payloads.

## Main types

- `Models/IntentDescriptor`: immutable intent model
- `Serialization/IntentHeaderSerializer`: deterministic canonical serialization
- `Serialization/IntentHeaderParser`: parsing with multi-error validation
- `Validation/IntentValidationResult`: validation result model
- `Verification/IntentVerificationOptions`: verification options with `TimeProvider`
- `Abstractions/*`: contracts for signing, verification, key resolution, and replay protection

## Canonical format

Field order:

1. `action`
2. `issuer`
3. `targetOrigin`
4. `beneficiary`
5. `amount`
6. `currency`
7. `issuedAt`
8. `expiresAt`
9. `nonce`

Format: `key=value` pairs separated by `;`, with deterministic escaping of `\`, `;`, and `=`.

## Minimal usage

```csharp
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;

var descriptor = new IntentDescriptor(
    action: "authorize",
    issuer: "wallet-service",
    targetOrigin: new Uri("https://merchant.example"),
    beneficiary: "merchant-42",
    amount: 12.34m,
    currency: "EUR",
    issuedAt: DateTimeOffset.UtcNow,
    expiresAt: DateTimeOffset.UtcNow.AddMinutes(5),
    nonce: Guid.NewGuid().ToString("N"));

var serializer = new IntentHeaderSerializer();
var header = serializer.Serialize(descriptor);

var parser = new IntentHeaderParser();
var validation = parser.Parse(header, out var parsed);
```

