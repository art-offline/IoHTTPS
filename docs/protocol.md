# Intent over HTTPS (IoHTTPS) Protocol

## 1. Short summary

Intent over HTTPS (IoHTTPS) is an application-layer protocol built on top of HTTPS. It adds a verifiable intent layer for sensitive actions by carrying a canonical intent representation and related signature metadata in HTTP headers.

This document describes the protocol shape implemented in this repository's current C# reference implementation. It is not a formal standard and does not attempt to define behavior beyond what the repository currently exposes.

## 2. Purpose of the protocol

The purpose of IoHTTPS is to make a declared action context verifiable at the HTTP application layer.

In the current implementation, a sender constructs a structured intent, serializes it into a deterministic header value, signs that canonical representation, and transmits both the intent and its signature as HTTP headers. A verifier can then validate:

- the integrity of the serialized intent
- which key was used to sign it
- whether the intent targets the expected origin
- whether the intent is currently within its validity window
- whether a nonce has already been seen, when replay protection is configured

IoHTTPS provides evidence that a private key holder signed a specific canonical intent. It does **not** determine whether that intent is commercially reasonable, policy-compliant, lawful, or trustworthy from a business perspective.

## 3. Scope

This repository currently contains four related components:

- `IntentOverHttps.Core`: protocol model, canonical serialization, parsing, validation, and verification primitives
- `IntentOverHttps.AspNetCore`: ASP.NET Core integration for emitting protocol headers and exposing key discovery
- `IntentOverHttps.DemoWeb`: a minimal demonstration service that emits signed protocol headers and publishes public keys
- `IntentOverHttps.Cli`: development tooling for key generation, intent creation, signing, verification, and example output

This document covers the protocol behavior represented by those components. It does not define a broader interoperability framework, cross-language conformance profile, or a complete deployment standard.

## 4. Terminology

The following terms are used throughout this document.

### Intent descriptor
A structured representation of the action context before serialization. In the C# reference implementation this is modeled by `IntentDescriptor`.

### Intent
The canonical serialized representation of the intent descriptor. In the current implementation this value is transported in the `Intent` HTTP header and is the value that is signed.

### Issuer
The protocol identity that creates and signs the intent and publishes the corresponding public keys. In this repository, `issuer` is a protocol field, not a legal trust statement.

### Target origin
The absolute HTTPS origin the intent is meant for. The current implementation normalizes this to the URI authority portion.

### Beneficiary
A named recipient or destination embedded in the intent payload.

### Nonce
A unique value included in the intent to support replay detection when the receiving application provides replay storage.

### Key ID (`kid`)
An identifier for the signing key. It is transported in the `Intent-Key-Id` header and also published through the key discovery endpoint.

### Algorithm
The signature algorithm identifier. The current implementation uses `ES256` in the demo service and CLI examples.

### Key discovery endpoint
A well-known HTTPS endpoint used to publish public keys for verification.

### Canonical field set
The current canonical field set, in order, is:

1. `action`
2. `issuer`
3. `targetOrigin`
4. `beneficiary`
5. `amount`
6. `currency`
7. `issuedAt`
8. `expiresAt`
9. `nonce`

## 5. Goals

The current implementation is designed around the following goals:

- deterministic serialization of intent data
- explicit HTTP transport through named headers
- cryptographic verification of the canonical intent representation
- public key discovery through a predictable endpoint
- a reusable C# reference implementation for signing and verification workflows
- separation between transport security, protocol semantics, and business logic

## 6. Non-goals

IoHTTPS is intentionally limited in scope. In its current form it is **not**:

- a replacement for TLS or HTTPS
- a business authorization, fraud, or policy engine
- proof that a signed action is acceptable or trustworthy in a business sense
- a complete identity or trust framework
- a general-purpose signing scheme for arbitrary HTTP bodies
- a complete replay-prevention system by itself

Replay prevention, key management policy, deployment hardening, and application-specific acceptance rules remain responsibilities of the integrating system.

## 7. High-level architecture

The repository is organized in layers.

### Core layer
`IntentOverHttps.Core` contains the protocol model and core mechanics:

- `IntentDescriptor`
- canonical serialization through `IntentHeaderSerializer`
- parsing and validation through `IntentHeaderParser`
- verification abstractions such as `IIntentSigner`, `IIntentVerifier`, `IKeyResolver`, and `IReplayProtectionStore`
- ECDSA verification logic through `EcdsaIntentVerifier`

### ASP.NET Core integration layer
`IntentOverHttps.AspNetCore` provides integration helpers for ASP.NET Core applications:

- protocol header constants
- service registration
- response signing support
- a configurable key discovery endpoint mapping

The current ASP.NET Core integration focuses on emitting signed response headers and exposing public keys. It does not currently provide a general inbound verification middleware.

### Demo service layer
`IntentOverHttps.DemoWeb` demonstrates an end-to-end example of:

- creating an intent
- signing the canonical header value
- returning the protocol headers in an HTTP response
- publishing public keys from a well-known endpoint

### CLI tooling layer
`IntentOverHttps.Cli` supports development and debugging workflows such as:

- key generation
- intent creation
- signing
- verification
- example output

## 8. Protocol actors

A deployment may contain one or more of the following actors.

### Issuer / signing service
Creates an intent descriptor and signs the canonical representation with a private key.

### HTTP application
Transports the protocol values over HTTPS, typically by returning the five protocol headers as part of an HTTP response.

### Key publisher
Publishes active public keys at the well-known key discovery endpoint.

### Verifier / relying party
Parses the `Intent` header, obtains or resolves the appropriate public key, and validates the signature and associated constraints.

A single application may perform more than one of these roles.

## 9. Protocol flow overview

The current implementation follows this general flow:

1. Construct an `IntentDescriptor` with the required fields.
2. Serialize it deterministically into the `Intent` header value.
3. Sign the canonical intent representation with the active private key.
4. Emit the following headers together:
   - `Intent`
   - `Intent-Signature`
   - `Intent-Key-Id`
   - `Intent-Alg`
   - `Intent-Version`
5. Publish the corresponding public keys through the well-known key discovery endpoint.
6. A verifier parses the intent, resolves the public key, and validates signature, time window, target origin, and optionally replay state.

In the demo application, this behavior is illustrated by a signed response from `GET /pay/demo` and a public key document exposed at `/.well-known/intent-keys`.

## 10. Transport assumptions

IoHTTPS is designed to operate over HTTPS.

HTTPS remains responsible for transport-layer confidentiality, server authentication, and in-transit protection. IoHTTPS adds application-layer semantics on top of HTTPS by making a canonical intent representation verifiable.

IoHTTPS is therefore a layer **on** HTTPS, not a replacement **for** HTTPS.

## 11. Protocol headers overview

The current implementation uses the following HTTP headers.

### `Intent`
Carries the canonical serialized intent payload.

Example shape:

```text
action=pay;issuer=merchant-demo;targetOrigin=https://merchant.example;beneficiary=merchant-123;amount=12.34;currency=EUR;issuedAt=2026-03-24T12:00:00.0000000+00:00;expiresAt=2026-03-24T12:05:00.0000000+00:00;nonce=test-nonce-001
```

The serialization format is deterministic:

- fields are emitted in a fixed order
- field values use `key=value` form separated by `;`
- the characters `\`, `;`, and `=` are escaped in string fields
- timestamps are formatted in round-trip ISO-8601 form
- `targetOrigin` is serialized as origin only

### `Intent-Signature`
Carries the Base64Url-encoded signature over the canonical `Intent` header value.

The current reference implementation signs the canonical string representation, not the HTTP body and not the full HTTP message.

### `Intent-Key-Id`
Identifies the public key that corresponds to the private key used for signing.

### `Intent-Alg`
Identifies the signature algorithm. The current demo and CLI use `ES256`.

### `Intent-Version`
Carries the protocol version string. The current default is `1`.

## 12. Key discovery overview

The current implementation uses a well-known HTTPS endpoint to publish public verification keys.

### Default path

```text
/.well-known/intent-keys
```

In `IntentOverHttps.AspNetCore`, this path is configurable through `IntentProtocolOptions`, but the default remains the well-known path above.

### Response shape
The key discovery response contains:

- `issuer`
- `version`
- `keys`

Each key entry is a JWK-compatible object with the following fields:

- `kid`
- `kty`
- `crv`
- `use`
- `alg`
- `x`
- `y`

In the current C# implementation, the expected values for the demo and CLI examples are typically:

- `kty = EC`
- `crv = P-256`
- `use = sig`
- `alg = ES256`

The ASP.NET Core integration currently sets `Cache-Control: no-store` on the key discovery response.

## 13. Versioning overview

Protocol versioning is represented explicitly.

- `Intent-Version` carries the protocol version in protocol headers
- the key discovery document also includes a `version` field

The current implementation uses version `1` by default.

The repository does not currently define header negotiation, downgrade handling, or a compatibility matrix across protocol versions. Versioning should therefore be understood as an explicit protocol marker in the current implementation, not as a complete negotiation framework.

## 14. Security model summary

The current implementation supports the following security-relevant properties.

### Canonicalization before signing
The signed value is a deterministic canonical serialization of the intent descriptor. This reduces ambiguity about what was signed.

### Signature verification
`EcdsaIntentVerifier` verifies the signature over the canonical intent representation using a resolved public key.

### Issuer-based key resolution
Verification is driven by the `issuer` field together with key resolution logic supplied through `IKeyResolver`.

### Temporal validity
The verifier can reject intents that are expired or not yet valid, with configurable clock skew.

### Target-origin checking
The verifier can optionally enforce that the intent is intended for a specific target origin.

### Replay protection hook
The verifier can optionally use an `IReplayProtectionStore` to detect reuse of a nonce.

The security model has important limits:

- it proves that the corresponding private key holder signed the canonical intent
- it helps detect tampering with the canonical intent fields
- it does **not** prove that the signer is trustworthy in a business sense
- it does **not** guarantee that the signed action is authorized by all required policies or stakeholders
- it does **not** provide replay prevention unless the application supplies replay storage
- it does **not** replace HTTPS transport security

Private key generation, storage, rotation, and operational protection remain application responsibilities.

## 15. Current implementation scope

The current C# reference implementation covers the following areas.

### In `IntentOverHttps.Core`
- immutable intent modeling
- canonical serialization
- parsing and multi-error validation
- verification abstractions
- ECDSA verification support

### In `IntentOverHttps.AspNetCore`
- protocol header constants
- dependency injection helpers
- response header writing through a response signer
- well-known key discovery endpoint mapping

### In `IntentOverHttps.DemoWeb`
- a minimal demo endpoint returning a signed response
- a public key discovery endpoint
- example key publication for local testing

### In `IntentOverHttps.Cli`
- key pair generation
- canonical intent creation
- intent signing
- intent verification using a public key
- generation of example protocol header sets

The repository should therefore be understood as a C# reference implementation of the current protocol shape.

## 16. Future evolution notes

Future work may evolve this repository, but any such evolution should remain consistent with the current protocol principles:

- canonicalization must stay explicit and deterministic
- signed content boundaries must remain clear
- protocol versioning must remain explicit
- security claims should remain limited to what the implementation can actually verify

Possible future work may include additional framework integrations, broader verifier integration patterns, or more detailed interoperability guidance. Those topics are outside the scope of the current implementation and should not be assumed by readers of this document.

