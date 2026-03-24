# IoHTTPS Verification

## Overview

This document describes the verification model currently implemented in the `Intent over HTTPS` (IoHTTPS) repository.

It is based on the current C# reference implementation, in particular:

- `IntentOverHttps.Core.Serialization.IntentHeaderParser`
- `IntentOverHttps.Core.Models.IntentDescriptor`
- `IntentOverHttps.Core.Verification.EcdsaIntentVerifier`
- `IntentOverHttps.Core.Verification.IntentVerificationOptions`
- `IntentOverHttps.Core.Validation.IntentValidationResult`
- `IntentOverHttps.Core.Validation.IntentValidationError`

IoHTTPS verification is layered on top of HTTPS. HTTPS remains responsible for transport security. IoHTTPS verification is responsible for checking that a canonical intent payload is structurally valid, cryptographically verifiable, temporally acceptable, targeted at the expected origin when configured, and not replayed when replay protection is configured.

The verification model does **not** determine whether the business meaning of the intent is acceptable, authorized, or trustworthy.

## 1. Purpose of verification

The purpose of IoHTTPS verification is to establish whether a received intent can be accepted as a valid protocol message under the local verifier's rules.

In the current implementation, successful verification means:

- the `Intent` payload can be parsed into a valid `IntentDescriptor`
- the canonical intent representation matches the detached signature
- a public key can be resolved for the intent issuer, when signature verification is enabled
- the intent is currently within its acceptable time window
- the intent targets the expected origin, when origin checking is enabled
- the nonce has not already been consumed, when replay protection is enabled

Verification therefore answers a technical question: *is this intent message valid according to the protocol rules and local verification options?*

It does not answer broader business questions such as whether the action is desirable, contractually valid, fraud-free, or policy-approved.

## 2. Verification prerequisites

A practical IoHTTPS verifier needs the following inputs and dependencies.

### Required transport assumption

IoHTTPS is designed to run over HTTPS. Verification does not replace transport-layer security.

### Required message inputs

At minimum, a verifier needs:

- the raw `Intent` header value
- the raw `Intent-Signature` header value

### Required Core inputs

The Core verifier itself does not consume raw HTTP requests. It consumes:

- a parsed `IntentDescriptor`
- detached signature bytes
- an `IntentVerificationOptions` instance

### Optional but commonly relevant metadata

The following protocol headers are part of the message shape and may be important to an application:

- `Intent-Key-Id`
- `Intent-Alg`
- `Intent-Version`

These values are not currently parsed or enforced by `IntentHeaderParser` or `EcdsaIntentVerifier`. If an application depends on them, it must validate them at the application layer.

### Verification collaborators

`IntentVerificationOptions` may provide:

- `IKeyResolver` for issuer-based public key lookup
- `IReplayProtectionStore` for replay detection
- `TimeProvider` for time evaluation
- `ClockSkew` for tolerance around `issuedAt` and `expiresAt`
- `ExpectedTargetOrigin` for target-origin enforcement

## 3. Expected inputs

From a protocol message perspective, verification usually starts from the following header set:

- `Intent`
- `Intent-Signature`
- optionally `Intent-Key-Id`
- optionally `Intent-Alg`
- optionally `Intent-Version`

From the Core library perspective, the actual inputs are narrower:

- `IntentDescriptor intent`
- `ReadOnlyMemory<byte> signature`
- `IntentVerificationOptions options`

This distinction is important:

- header extraction and presence checks happen outside Core
- Base64Url decoding of `Intent-Signature` happens outside Core
- key-discovery HTTP access happens outside Core
- Core verification starts after parsing and decoding have already happened

## 4. Verification steps in order

The logical verification sequence for the current implementation is:

1. Check required protocol headers are present.
2. Parse the `Intent` header into an `IntentDescriptor`.
3. Decode the detached signature from `Intent-Signature`.
4. Build `IntentVerificationOptions`.
5. Call `EcdsaIntentVerifier.VerifyAsync(...)`.
6. Evaluate the returned `IntentValidationResult`.

Inside `EcdsaIntentVerifier`, the current verification order is:

1. target origin check
2. time validation
3. signature verification
4. replay protection

Replay protection runs last and only after earlier checks have succeeded.

## 5. Header presence checks

Header presence checks are logically part of verification, but they are not currently implemented in `IntentOverHttps.Core` as a raw HTTP-header validator.

A practical verifier should check at least:

- `Intent` is present
- `Intent-Signature` is present

Depending on application policy, it may also require:

- `Intent-Key-Id`
- `Intent-Alg`
- `Intent-Version`

Current Core behavior:

- if `Intent` is missing, null, empty, or whitespace-only, `IntentHeaderParser` reports all intent payload fields as missing
- if `Intent-Signature` is missing or invalid, failure occurs before or during signature processing at the application layer
- `Intent-Key-Id`, `Intent-Alg`, and `Intent-Version` are not currently required by Core verification logic

## 6. Intent parsing checks

The `Intent` header is parsed by `IntentHeaderParser`.

The parser currently performs the following checks.

### Structural checks

- the header must contain parseable `key=value` segments
- segments are separated by `;`
- empty segments are rejected as malformed
- a field name must be present
- a trailing escape character without a following character is rejected

### Required-field checks

The following fields are required:

- `action`
- `issuer`
- `targetOrigin`
- `beneficiary`
- `amount`
- `currency`
- `issuedAt`
- `expiresAt`
- `nonce`

### Unknown and duplicate fields

- unknown fields are rejected
- duplicate fields are rejected

### Field-level validation

The parser validates:

- `action`: required non-empty text
- `issuer`: required non-empty text
- `targetOrigin`: absolute URI, no path except `/`, no query, no fragment
- `beneficiary`: required non-empty text
- `amount`: positive invariant-culture decimal
- `currency`: 3-letter alphabetic ASCII code
- `issuedAt`: round-trip ISO-8601 format (`O`)
- `expiresAt`: round-trip ISO-8601 format (`O`)
- `nonce`: required non-empty text
- `expiresAt > issuedAt`

### Normalization behavior

The parser and model normalize values as follows:

- surrounding whitespace on text fields is trimmed
- `currency` is normalized to uppercase
- timestamps are normalized to UTC
- `targetOrigin` is normalized to origin form

If parsing fails, no `IntentDescriptor` is produced.

## 7. Signature verification checks

Signature verification is performed by `EcdsaIntentVerifier`.

The current implementation:

1. reserializes the `IntentDescriptor` using `IntentHeaderSerializer`
2. UTF-8 encodes the canonical string
3. resolves the issuer's public key when a key resolver is configured
4. imports the public key as SubjectPublicKeyInfo bytes into `ECDsa`
5. verifies the detached signature using ECDSA with SHA-256

If signature verification fails, the verifier returns an error with code:

- `SignatureInvalid`

Important current behavior:

- the canonical serialized intent string is what is verified
- the HTTP body is not part of the signed input
- the full HTTP message is not part of the signed input
- if no `IKeyResolver` is provided, signature verification is skipped entirely in the current implementation

That last point is important for implementers: signature verification is optional only in the sense that the Core verifier can be configured without a key resolver. An application that requires cryptographic verification must provide a resolver.

## 8. Key resolution behavior

Key resolution is driven by `IKeyResolver`.

Current contract:

```csharp
ValueTask<byte[]?> ResolveKeyAsync(string issuer, CancellationToken cancellationToken = default)
```

### Current behavior

- the verifier calls `ResolveKeyAsync(intent.Issuer, ...)`
- key lookup is therefore issuer-based in the current Core implementation
- the expected key material is public key bytes in SubjectPublicKeyInfo form

### Key-not-found behavior

If the resolver returns `null`, verification fails with:

- `KeyNotFound`

### Relationship to key discovery

Public keys may be published through the well-known endpoint:

```text
/.well-known/intent-keys
```

However, HTTP retrieval, caching, selection, and key-to-issuer mapping are outside `IntentOverHttps.Core`. Those concerns belong to the integrating application.

### Important current limitation

The current Core verifier does **not** use `Intent-Key-Id` to select a key. Verification is issuer-based, not `kid`-based.

## 9. Algorithm validation

The current verifier implementation uses ECDSA with SHA-256.

In practical terms:

- the demo and CLI label the algorithm as `ES256`
- `EcdsaIntentVerifier` verifies with `ECDsa.VerifyData(..., HashAlgorithmName.SHA256)`

Current enforcement boundary:

- Core uses a concrete ECDSA/SHA-256 verification implementation
- Core does **not** parse or enforce the `Intent-Alg` header value
- there is no current Core check that compares `Intent-Alg` to the actual verification algorithm

Therefore, algorithm validation should be understood as follows:

- cryptographic verification uses the configured verifier implementation
- protocol metadata validation for `Intent-Alg` is currently application-defined

If an application requires strict `Intent-Alg` checking, that check must currently be added outside Core.

## 10. Time validation

Time validation is performed by `EcdsaIntentVerifier` using `IntentVerificationOptions.TimeProvider` and `IntentVerificationOptions.ClockSkew`.

### `expiresAt`

The verifier rejects an intent when:

```text
intent.ExpiresAt + clockSkew < now
```

If this condition is true, verification reports:

- `Expired`

This means the verifier allows a grace window equal to the configured clock skew after `expiresAt`.

### `issuedAt`

The verifier rejects an intent when:

```text
intent.IssuedAt - clockSkew > now
```

If this condition is true, verification reports:

- `NotYetValid`

This means the verifier tolerates some clock skew before the nominal `issuedAt`.

### Clock skew handling

`IntentVerificationOptions` defaults `ClockSkew` to 5 minutes if not specified.

Additional current behavior:

- negative clock skew is rejected when constructing `IntentVerificationOptions`
- timestamps are already normalized to UTC by parsing and model construction

Time validation is independent of signature verification. The verifier can accumulate both temporal and cryptographic errors in the same result.

## 11. Target origin validation

Target origin validation is optional and driven by `IntentVerificationOptions.ExpectedTargetOrigin`.

If `ExpectedTargetOrigin` is not configured:

- no target-origin verification is performed

If `ExpectedTargetOrigin` is configured:

- the verifier compares `intent.TargetOrigin` with `ExpectedTargetOrigin`
- comparison is performed on scheme and authority only
- comparison is case-insensitive

If the values do not match, verification reports:

- `InvalidTargetOrigin`

The current implementation normalizes both sides to origin form before comparison. Paths, query strings, and fragments are already rejected by parsing and by `IntentVerificationOptions` construction.

## 12. Replay protection expectations

Replay protection is optional and only applies when an `IReplayProtectionStore` is supplied.

Current contract:

```csharp
ValueTask<bool> TryStoreAsync(
    string issuer,
    string nonce,
    DateTimeOffset expiresAt,
    CancellationToken cancellationToken = default)
```

### Current behavior

The verifier attempts replay storage only if:

- replay protection is configured, and
- no earlier verification errors were recorded

This is an explicit design choice in the current implementation. It prevents invalid, forged, or expired intents from poisoning the replay store.

### Replay result

If `TryStoreAsync(...)` returns `false`, verification reports:

- `ReplayDetected`

### Practical expectation for implementers

A replay store should generally treat `(issuer, nonce)` as unique at least until `expiresAt` has passed.

Storage strategy, eviction, persistence, and clustering behavior are outside the scope of Core and must be implemented by the integrating application.

## 13. Validation result model

Both parsing and verification use `IntentValidationResult`.

Current shape:

- `IsValid`: `true` when there are no errors
- `Errors`: read-only list of `IntentValidationError`

Each `IntentValidationError` contains:

- `Code`: `IntentErrorCode`
- `Message`: human-readable explanation
- `Field`: optional field name when applicable

The result model is designed to support:

- machine-readable error handling through error codes
- human-readable diagnostics through messages
- accumulation of multiple errors in a single result

## 14. Error reporting principles

The current implementation follows these principles.

### Stable error categories

Errors are categorized through `IntentErrorCode`, including for example:

- `MissingField`
- `MalformedField`
- `UnknownField`
- `DuplicateField`
- `InvalidTargetOrigin`
- `InvalidAmount`
- `InvalidCurrency`
- `InvalidIssuedAt`
- `InvalidExpiresAt`
- `TemporalRangeInvalid`
- `Expired`
- `NotYetValid`
- `SignatureInvalid`
- `KeyNotFound`
- `ReplayDetected`

### Multi-error accumulation

Both parser and verifier accumulate errors rather than stopping at the first failure where practical.

Examples supported by tests include:

- multiple parse errors from one malformed `Intent`
- simultaneous origin mismatch and expiration errors during verification

### Field attribution where appropriate

When possible, errors include a field name such as:

- `targetOrigin`
- `amount`
- `currency`
- `expiresAt`

### Separation of concerns

Implementers should distinguish clearly between:

- header extraction failures
- parse failures
- signature decoding failures
- cryptographic verification failures
- policy/application failures outside protocol scope

The protocol verifies message validity. It does not classify business acceptability.

## 15. Example verification flow

The following flow matches the current repository model.

1. Read `Intent` and `Intent-Signature` from the HTTPS message.
2. Ensure both are present.
3. Parse the `Intent` value with `IntentHeaderParser`.
4. If parsing fails, return the parser's `IntentValidationResult`.
5. Decode `Intent-Signature` from Base64Url into signature bytes.
6. Obtain or prepare public key resolution logic, possibly backed by `/.well-known/intent-keys`.
7. Build `IntentVerificationOptions` with:
   - `IKeyResolver`
   - optional `ExpectedTargetOrigin`
   - optional `IReplayProtectionStore`
   - optional `ClockSkew`
   - optional `TimeProvider`
8. Call `EcdsaIntentVerifier.VerifyAsync(parsedIntent, signatureBytes, options)`.
9. Inspect `IntentValidationResult.IsValid`.
10. If invalid, return or log the collected protocol errors.
11. If valid, continue with application-specific authorization and business handling.

Illustrative C# shape:

```csharp
var parser = new IntentHeaderParser();
var parseResult = parser.Parse(intentHeaderValue, out var descriptor);
if (!parseResult.IsValid || descriptor is null)
{
    return parseResult;
}

var signatureBytes = Base64UrlDecode(signatureHeaderValue); // application layer

var options = new IntentVerificationOptions(
    keyResolver: keyResolver,
    replayProtectionStore: replayStore,
    expectedTargetOrigin: expectedOrigin,
    clockSkew: TimeSpan.FromMinutes(5));

var verifier = new EcdsaIntentVerifier(new IntentHeaderSerializer());
var verificationResult = await verifier.VerifyAsync(descriptor, signatureBytes, options);
return verificationResult;
```

## 16. Failure examples

The following examples reflect failures already covered by the current implementation and tests.

### Missing required fields

- `Intent` is missing or whitespace-only
- parser reports missing required fields for the payload

### Unknown or duplicate fields

- `Intent` contains an unsupported field
- `Intent` contains the same field more than once

### Malformed escaping

- `Intent` ends with a trailing escape character
- parser reports a malformed field condition

### Invalid target origin syntax

- `targetOrigin` contains a path, query string, fragment, or is not absolute

### Invalid timestamps

- `issuedAt` or `expiresAt` is not in round-trip ISO-8601 format
- `expiresAt` is not later than `issuedAt`

### Expired intent

- current time is after `expiresAt + clockSkew`
- verifier reports `Expired`

### Not-yet-valid intent

- current time is before `issuedAt - clockSkew`
- verifier reports `NotYetValid`

### Origin mismatch

- `ExpectedTargetOrigin` is configured but does not match the parsed `targetOrigin`
- verifier reports `InvalidTargetOrigin`

### Missing key for issuer

- `IKeyResolver` cannot resolve a public key for the intent's issuer
- verifier reports `KeyNotFound`

### Invalid signature

- signature bytes are corrupted
- the intent payload was modified after signing
- verifier reports `SignatureInvalid`

### Replay detected

- the same `(issuer, nonce)` is presented again after a successful prior verification
- replay store rejects storage
- verifier reports `ReplayDetected`

## 17. Operational notes for implementers

### HTTPS remains mandatory

IoHTTPS verification assumes transport over HTTPS. The protocol does not replace TLS, certificate validation, or normal HTTP service security practices.

### Key discovery is a transport concern, not a Core concern

The repository provides a well-known key publication pattern, but Core does not fetch keys itself. Applications must decide how to:

- retrieve keys
- cache keys
- refresh keys
- select keys
- handle key rotation

### Current verification is issuer-based

Because Core currently resolves keys by issuer, implementers should not assume `Intent-Key-Id` is enforced by the verifier today.

### Metadata headers are not fully enforced by Core

`Intent-Key-Id`, `Intent-Alg`, and `Intent-Version` are part of the protocol message shape, but Core does not currently provide full validation logic for them.

If your deployment depends on:

- exact algorithm labeling
- exact version handling
- `kid`-based key selection

those checks must currently be implemented at the application layer.

### Replay protection requires real storage design

A production replay store must consider:

- persistence boundaries
- multi-node coordination
- expiry cleanup
- throughput and latency
- failure handling

Core defines the contract, not the storage strategy.

### Successful verification is not full authorization

A valid IoHTTPS message means the protocol checks passed under the configured rules. It does not mean the action should automatically be executed.

Application-specific checks such as authorization, rate limits, fraud controls, business policy, and domain validation remain outside protocol scope.


