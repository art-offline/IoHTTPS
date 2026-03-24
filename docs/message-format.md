# IoHTTPS Message Format

## Overview

This document describes the message format currently implemented in the `Intent over HTTPS` (IoHTTPS) repository.

It focuses on the wire-visible header format used by the C# implementation today:

- a canonical serialized `Intent` header value
- a detached signature carried in `Intent-Signature`
- additional metadata headers for key identification, algorithm labeling, and protocol versioning

This document is intentionally practical and implementation-oriented. Where behavior is enforced by the current codebase, it is described as such. Where behavior is not fully enforced by the current implementation, it is marked as implementation-defined.

## 1. Purpose of the message format

The IoHTTPS message format exists to transport a verifiable intent alongside an HTTPS exchange.

In the current implementation:

- the structured intent payload is represented as an `IntentDescriptor`
- that descriptor is serialized into a deterministic string form
- the serialized string is signed
- the serialized intent and detached signature are transmitted as HTTP headers

The message format is designed to make the signed intent explicit, reproducible, and parseable by other components in the same protocol implementation.

This format provides a verifiable representation of what was signed. It does **not** determine whether the business meaning of that intent is acceptable, authorized, or trustworthy.

## 2. Header inventory

The current implementation uses the following headers:

| Header | Purpose |
|---|---|
| `Intent` | Canonical serialized intent payload |
| `Intent-Signature` | Detached signature over the canonical `Intent` value |
| `Intent-Key-Id` | Identifier of the signing key |
| `Intent-Alg` | Signature algorithm identifier |
| `Intent-Version` | Protocol version string |

All five names are defined as constants in the C# implementation.

## 3. Description of each header

### `Intent`

`Intent` carries the canonical serialized form of the intent payload.

This is the primary signed value in the current protocol shape. The serializer emits the payload as a semicolon-separated sequence of `key=value` pairs in a fixed field order.

Example:

```text
action=pay;issuer=merchant-demo;targetOrigin=https://merchant.example;beneficiary=merchant-123;amount=12.34;currency=EUR;issuedAt=2026-03-24T12:00:00.0000000+00:00;expiresAt=2026-03-24T12:05:00.0000000+00:00;nonce=test-nonce-001
```

### `Intent-Signature`

`Intent-Signature` carries a detached signature over the canonical `Intent` header value.

In the current ASP.NET Core integration, the signature bytes are Base64Url-encoded before being written to the header. The verifier expects the signature bytes separately from the `Intent` payload.

The signature is detached: it is not embedded inside the `Intent` value.

### `Intent-Key-Id`

`Intent-Key-Id` identifies the signing key used to produce the detached signature.

In the current implementation, this value is emitted by the application layer. Core parsing and verification logic do not currently parse or enforce this header directly.

### `Intent-Alg`

`Intent-Alg` identifies the signing algorithm used by the sender.

In the current demo and CLI flows, this value is typically `ES256`.

This header is currently emitted metadata. Core parsing and verification logic do not currently parse or enforce this header directly.

### `Intent-Version`

`Intent-Version` carries the protocol version string.

In the current implementation, the default version value is `1`.

This header is emitted by higher-level integration code. Core parsing of the `Intent` value does not depend on it.

## 4. Canonical intent serialization rules

The current canonical serializer in `IntentOverHttps.Core` emits the `Intent` value using the following rules:

1. The payload is serialized as a flat string.
2. Each field is serialized as `name=value`.
3. Fields are separated using `;`.
4. Fields are emitted in a fixed order.
5. The serializer escapes `\`, `;`, and `=` inside string field values.
6. `amount` is formatted with invariant culture.
7. `issuedAt` and `expiresAt` are formatted using the round-trip `O` format in UTC.
8. `targetOrigin` is serialized as origin only (`scheme://host[:port]`).

The current canonical form is therefore a protocol string format, not JSON.

## 5. Field order and determinism requirements

The current serializer emits fields in this exact order:

1. `action`
2. `issuer`
3. `targetOrigin`
4. `beneficiary`
5. `amount`
6. `currency`
7. `issuedAt`
8. `expiresAt`
9. `nonce`

This order is part of the canonical signed representation.

Determinism matters for signing. Two implementations that intend to interoperate with this repository must serialize the same logical intent into the same canonical string before signing.

Important distinction:

- **Serialization** is order-sensitive and canonical.
- **Parsing** in the current implementation does not require fields to appear in canonical order.

The parser reads named fields by key, rejects duplicates, and validates the resulting set. Canonical order is therefore required for deterministic emission and signing, not for successful parsing of an already-formed incoming header.

## 6. Escaping or encoding rules currently used

### String field escaping inside `Intent`

The serializer escapes the following characters inside string field values:

- backslash: `\`
- semicolon: `;`
- equals sign: `=`

Escaping is performed by prefixing the character with a backslash.

Examples:

| Raw value | Serialized form |
|---|---|
| `merchant=123` | `merchant\=123` |
| `settlement;desk` | `settlement\;desk` |
| `A\B` | `A\\B` |

The parser reverses this escaping when parsing the `Intent` value.

A trailing backslash with no escaped character after it is treated as malformed input.

### `amount` encoding

`amount` is formatted using invariant culture with the pattern:

```text
0.###############################
```

This means:

- integer values are emitted without a decimal point
- decimal values preserve significant fractional digits
- culture-specific separators are not used

Examples:

| Decimal value | Serialized form |
|---|---|
| `12m` | `12` |
| `12.34m` | `12.34` |
| `87.5m` | `87.5` |

### Timestamp encoding

`issuedAt` and `expiresAt` are serialized in UTC using the round-trip `O` format.

Example:

```text
2026-03-24T12:05:00.0000000+00:00
```

### Signature encoding

The detached signature in `Intent-Signature` is currently represented as Base64Url-encoded signature bytes.

The exact low-level ECDSA signature byte layout is implementation-defined from the perspective of this document. The message format only requires that `Intent-Signature` carries the detached signature bytes in Base64Url form as produced and consumed by the current C# implementation.

## 7. Required fields in the intent payload

The current parser requires all of the following fields to be present in the `Intent` payload:

- `action`
- `issuer`
- `targetOrigin`
- `beneficiary`
- `amount`
- `currency`
- `issuedAt`
- `expiresAt`
- `nonce`

If the `Intent` header is null, empty, or whitespace-only, the parser reports all nine fields as missing.

Unknown fields are rejected.

Duplicate fields are rejected.

## 8. Field definitions

This section describes the current field behavior as enforced by `IntentDescriptor` construction and `IntentHeaderParser` validation.

### `action`

- required
- text field
- must not be null, empty, or whitespace-only
- leading and trailing whitespace is trimmed during normalization

No additional protocol-level vocabulary is currently enforced by Core.

### `issuer`

- required
- text field
- must not be null, empty, or whitespace-only
- leading and trailing whitespace is trimmed during normalization

The current implementation uses `issuer` as the protocol identity associated with key resolution and verification.

### `targetOrigin`

- required
- must be a valid absolute URI
- must not include a query string
- must not include a fragment
- must not include a path other than `/`
- is normalized to URI authority form

Examples of accepted forms:

```text
https://merchant.example
https://merchant.example/
https://merchant.example:8443
```

Examples of rejected forms:

```text
/relative/path
https://merchant.example/pay
https://merchant.example?x=1
https://merchant.example#fragment
```

The serializer emits the normalized origin only.

### `beneficiary`

- required
- text field
- must not be null, empty, or whitespace-only
- leading and trailing whitespace is trimmed during normalization
- may contain characters that require escaping in canonical serialization

### `amount`

- required
- invariant-culture decimal number
- must parse as a decimal using invariant culture
- must be greater than zero

Examples of valid forms:

```text
1
1.25
87.5
```

Examples of invalid forms:

```text
0
-1
-0.01
1,25
```

### `currency`

- required
- must be exactly 3 alphabetic ASCII characters
- is normalized to uppercase in the current implementation

Examples of valid forms:

```text
EUR
usd
Usd
```

Examples of resulting normalized values:

```text
EUR
USD
USD
```

Examples of invalid forms:

```text
EU
EURO
1EU
E R
```

### `issuedAt`

- required
- must use round-trip ISO-8601 format (`O`)
- is normalized to UTC in the current implementation

### `expiresAt`

- required
- must use round-trip ISO-8601 format (`O`)
- is normalized to UTC in the current implementation
- must be strictly later than `issuedAt`

If `expiresAt` is equal to or earlier than `issuedAt`, parsing fails.

### `nonce`

- required
- text field
- must not be null, empty, or whitespace-only
- leading and trailing whitespace is trimmed during normalization

The current verifier can optionally use `nonce` together with an `IReplayProtectionStore` to detect replay.

## 9. Signature representation overview

The current protocol uses a detached signature model.

In practical terms:

- `Intent` contains the canonical payload
- `Intent-Signature` contains the detached signature over that canonical payload
- `Intent-Key-Id` identifies the signing key
- `Intent-Alg` labels the algorithm

The signed value is the canonical serialized `Intent` string, not the HTTP body and not the full HTTP message.

The current C# implementation uses ECDSA verification support in Core and emits Base64Url-encoded signature bytes at the integration layer.

## 10. Algorithm identifier handling

`Intent-Alg` is currently treated as message metadata.

The current demo and CLI examples use:

```text
ES256
```

The Core verifier currently verifies signatures using its configured implementation and key material. It does not currently parse or enforce `Intent-Alg` as part of `IntentHeaderParser`.

For that reason:

- senders should emit the algorithm identifier they actually use
- receivers may log, inspect, or route on `Intent-Alg` at the application layer
- exact enforcement semantics for `Intent-Alg` are currently implementation-defined outside Core parsing

## 11. Version header semantics

`Intent-Version` carries the protocol version string.

The current implementation uses version `1` by default.

The version header is currently emitted by higher-level code such as the ASP.NET Core integration and CLI examples. Core intent parsing does not depend on it.

No version negotiation or compatibility policy is currently defined in the repository. Version handling beyond carrying and exposing the version string is therefore implementation-defined.

## 12. Example valid header set

The following is a representative valid header set for the current implementation:

```http
Intent: action=pay;issuer=merchant-demo;targetOrigin=https://merchant.example;beneficiary=merchant-123;amount=12.34;currency=EUR;issuedAt=2026-03-24T12:00:00.0000000+00:00;expiresAt=2026-03-24T12:05:00.0000000+00:00;nonce=test-nonce-001
Intent-Signature: MEUCIQDX-example-signature-value-would-be-base64url-encoded
Intent-Key-Id: demo-es256-key-1
Intent-Alg: ES256
Intent-Version: 1
```

Notes:

- the `Intent` value matches the canonical field order
- the `Intent-Signature` value above is illustrative in shape only
- the signature must be the real detached signature bytes encoded as Base64Url for an actual valid message

## 13. Example malformed cases

The following examples reflect the kinds of failures the current parser and verifier already distinguish.

### Missing required fields

```http
Intent: action=pay
```

Expected result: parsing fails and missing required fields are reported.

### Unknown field

```http
Intent: action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=1.25;currency=EUR;issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1;extra=value
```

Expected result: parsing fails with an unknown-field error for `extra`.

### Duplicate field

```http
Intent: action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=1.25;currency=EUR;issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1;issuer=issuer-b
```

Expected result: parsing fails with a duplicate-field error for `issuer`.

### Invalid `targetOrigin`

```http
Intent: action=pay;issuer=issuer-a;targetOrigin=https://merchant.example/shop;beneficiary=shop;amount=1.25;currency=EUR;issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1
```

Expected result: parsing fails because `targetOrigin` contains a path.

### Invalid `amount`

```http
Intent: action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=0;currency=EUR;issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1
```

Expected result: parsing fails because `amount` must be greater than zero.

### Invalid `currency`

```http
Intent: action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=1.25;currency=EURO;issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1
```

Expected result: parsing fails because `currency` is not a 3-letter alphabetic code.

### Invalid temporal range

```http
Intent: action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=1.25;currency=EUR;issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:00:00.0000000+00:00;nonce=n-1
```

Expected result: parsing fails because `expiresAt` must be later than `issuedAt`.

### Incomplete escape sequence

```http
Intent: action=pay;issuer=wallet;targetOrigin=https://merchant.example;beneficiary=b;amount=1;currency=EUR;issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1\
```

Expected result: parsing fails because the header ends with an incomplete escape sequence.

## 14. Parsing and validation expectations

The current parser and verifier have the following observable behavior.

### Parser behavior

`IntentHeaderParser` currently:

- accepts a serialized `Intent` string and parses fields by name
- accumulates multiple errors rather than stopping at the first failure
- reports missing required fields
- rejects unknown fields
- rejects duplicate fields
- validates field-level syntax and constraints
- normalizes parsed values where applicable

If parsing fails, no `IntentDescriptor` is produced.

### Validation behavior

Current validation covers at least:

- required-field presence
- malformed field segments
- malformed or incomplete escaping
- absolute `targetOrigin` with no path, query, or fragment
- positive invariant-culture `amount`
- 3-letter alphabetic `currency`
- round-trip timestamp format for `issuedAt` and `expiresAt`
- `expiresAt > issuedAt`

### Verification behavior

After successful parsing, verification logic can additionally validate:

- signature correctness
- expected target origin match
- expiration and not-yet-valid checks with clock skew
- replay detection when a replay store is configured
- issuer-based key resolution

These verification steps are separate from `Intent` string parsing itself.

## 15. Notes on forward compatibility

The current parser is strict.

In particular:

- unknown fields are rejected
- duplicate fields are rejected
- the required field set is fixed in the current implementation

This means the current message format is not forward-compatible with arbitrary additional intent fields unless parsing behavior changes in a future version.

Practical implications for developers:

- do not add extra fields to the canonical `Intent` payload if you expect compatibility with the current parser
- do not change field order when generating canonical signed values
- do not assume `Intent-Alg` or `Intent-Version` are enforced by Core parsing today
- treat version-specific extensions as application-level or future work unless the repository explicitly adds support for them

Future versions may evolve the format, but the current repository behavior should be treated as the source of truth for existing interoperable messages.

