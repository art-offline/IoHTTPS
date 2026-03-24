# IoHTTPS Threat Model

## Overview

This document describes the threat model for the current `Intent over HTTPS` (IoHTTPS) repository implementation.

IoHTTPS is an application-layer protocol built on top of HTTPS. It adds a signed, canonical intent representation to support verifiable protocol checks for sensitive actions.

This threat model is intentionally implementation-aligned and scoped to current repository behavior (`IntentOverHttps.Core`, `IntentOverHttps.AspNetCore`, `IntentOverHttps.DemoWeb`, and `IntentOverHttps.Cli`).

---

## 1. Purpose of the threat model

The purpose of this threat model is to define:

- what IoHTTPS currently protects,
- what assumptions those protections depend on,
- what attacker behaviors are considered,
- what remains outside protocol guarantees.

It is intended for engineering review, implementation planning, and security operations guidance.

A core distinction in this document:

- **IoHTTPS can establish authenticity and integrity of a signed intent message** (under the configured verification model).
- **IoHTTPS cannot establish legitimacy of business behavior** (for example fairness, policy compliance, or ethical intent).

---

## 2. Security objectives

The current implementation aims to provide the following protocol-level security properties:

1. **Message authenticity**: the intent message was signed by a private key corresponding to a resolvable public key.
2. **Message integrity**: tampering with canonical intent fields is detected by signature verification.
3. **Replay resistance**: replay attempts can be detected when replay storage is configured.
4. **Context binding**: intent can be bound to a target origin when expected origin validation is configured.
5. **Temporal validity checks**: intents outside acceptable issued/expiry windows are rejected with configurable clock skew.
6. **Deterministic parsing and canonicalization**: strict parser and canonical serializer reduce ambiguity.

The protocol does not claim to provide full transaction safety, user-intent semantics, or business authorization.

---

## 3. Assets to protect

The following assets are relevant to IoHTTPS security:

- **Signing private keys** used by issuers.
- **Public key distribution data** (e.g., key discovery payloads at `/.well-known/intent-keys`).
- **Canonical intent payload** (`Intent` header value).
- **Detached signature** (`Intent-Signature` header value).
- **Verification configuration** (`IntentVerificationOptions`, including clock skew, expected origin, key resolver).
- **Replay state** (`IReplayProtectionStore` data for `(issuer, nonce, expiresAt)`).
- **Audit signals** derived from `IntentValidationResult` and `IntentErrorCode`.

---

## 4. Protocol actors

Typical actors in scope:

- **Issuer / Signer**: creates canonical intent and signs it.
- **HTTP Service**: transmits protocol headers over HTTPS.
- **Verifier / Relying Service**: parses and verifies intent using Core logic.
- **Key Publisher**: exposes issuer public keys through a well-known endpoint.
- **Operator**: configures key resolution, replay store, and verification policy.

A single deployment may combine multiple actor roles.

---

## 5. Trust assumptions

Current IoHTTPS protections assume:

1. HTTPS is correctly deployed (certificate validation, secure transport, trusted endpoints).
2. Issuer private keys are protected and not leaked.
3. Public keys returned by key resolution are authentic for the expected issuer.
4. System clocks are reasonably synchronized (within configured skew bounds).
5. Replay store semantics are correctly implemented where replay resistance is required.
6. Application code performs required header extraction and signature decoding before Core verification.

If these assumptions are violated, protocol guarantees may degrade or fail.

---

## 6. Attacker capabilities

The threat model assumes an attacker may be able to:

- observe and modify traffic outside trusted HTTPS boundaries,
- replay previously captured valid protocol messages,
- submit malformed `Intent` payloads,
- omit required headers,
- provide incorrect or unavailable key material indirectly,
- attempt origin substitution in payload fields,
- exploit implementation misconfiguration (e.g., missing key resolver, weak replay configuration).

The model does **not** assume the attacker can break modern cryptography (ECDSA/SHA-256) directly.

---

## 7. In-scope threats

The following threats are in scope for IoHTTPS protocol verification:

- message tampering after signing,
- forged intent messages without access to issuer private key,
- replay of previously valid intent messages,
- substitution of `targetOrigin` context,
- malformed or ambiguous payload serialization attempts,
- verification attempts where signing key is unknown or missing.

These threats are analyzed in detail in section 9.

---

## 8. Out-of-scope threats

The following areas are outside protocol-level guarantees:

- business legitimacy of the requested action,
- user-consent semantics beyond signed payload content,
- browser/user interface trustworthiness,
- endpoint compromise and host-level takeover,
- organizational trust, contractual validity, fraud adjudication.

IoHTTPS is a protocol verification layer, not a full trust, governance, or endpoint security framework.

---

## 9. Threat analysis

### 9.1 Message tampering

**Threat**
An attacker modifies one or more intent fields in transit or at rest.

**Mitigation in current implementation**
- Canonical serialization plus detached signature verification in `EcdsaIntentVerifier`.
- Signature is recomputed over canonical UTF-8 bytes of serialized intent.

**Expected result**
- Verification fails with `SignatureInvalid` (assuming key resolver is configured).

**Limitations**
- If a verifier is configured without `IKeyResolver`, signature checks are skipped by current Core behavior.

---

### 9.2 Forged intent without signing key

**Threat**
Attacker creates a new intent and tries to pass it as authentic without possessing issuer private key.

**Mitigation in current implementation**
- Signature verification against issuer-resolved public key.
- Unknown issuer keys produce `KeyNotFound`.

**Expected result**
- Either `SignatureInvalid` or `KeyNotFound`.

**Limitations**
- Key authenticity and resolver correctness are integration responsibilities.

---

### 9.3 Replay attacks

**Threat**
A previously valid signed intent is submitted multiple times.

**Mitigation in current implementation**
- Optional replay detection through `IReplayProtectionStore.TryStoreAsync(issuer, nonce, expiresAt)`.
- Replay check only executes when prior checks succeeded.

**Expected result**
- First valid submission accepted.
- Subsequent submission with same replay identity rejected with `ReplayDetected`.

**Limitations**
- Replay resistance is not automatic; it requires a configured and correct replay store.
- Distributed consistency and storage durability are outside Core.

---

### 9.4 Target origin substitution

**Threat**
Attacker alters the intended target context (e.g., merchant origin) to redirect intent usage.

**Mitigation in current implementation**
- Parser enforces origin syntax constraints.
- Optional `ExpectedTargetOrigin` comparison in verifier.

**Expected result**
- Mismatch results in `InvalidTargetOrigin`.

**Limitations**
- Origin enforcement is optional and must be configured by verifier.

---

### 9.5 Malformed or ambiguous payloads

**Threat**
Attacker submits malformed, ambiguous, or non-canonical payloads to bypass validation.

**Mitigation in current implementation**
- Strict parser behavior:
  - required field checks,
  - unknown field rejection,
  - duplicate field rejection,
  - escaping validation,
  - field-level type and range checks,
  - temporal ordering checks (`expiresAt > issuedAt`).

**Expected result**
- Parsing fails with machine-readable errors (e.g., `MissingField`, `MalformedField`, `UnknownField`, `DuplicateField`, `Invalid*`).

**Limitations**
- Strictness may reject forward extensions unless parser behavior evolves in future versions.

---

### 9.6 Unknown or missing keys

**Threat**
Verifier cannot obtain a valid public key for the claimed issuer.

**Mitigation in current implementation**
- `IKeyResolver` abstraction returns key bytes or null.
- Null resolution yields `KeyNotFound`.

**Expected result**
- Verification fails closed with `KeyNotFound` when resolver is configured.

**Limitations**
- Current Core verification is issuer-based; `Intent-Key-Id` is not used for key selection.
- Key discovery retrieval, cache freshness, and rotation correctness are outside Core.

---

## 10. Threats not solved by the protocol

The following threats are explicitly not solved by IoHTTPS itself.

### Compromised client device
If the device creating or transmitting requests is compromised, attacker-controlled behavior may still produce valid protocol messages.

### Malicious legitimate issuer
If a legitimate issuer intentionally signs harmful or deceptive intents, IoHTTPS still validates authenticity; it does not judge business legitimacy.

### Social engineering outside protocol guarantees
Human deception and out-of-band manipulation remain outside protocol controls.

### Deceptive UI in non-native clients
IoHTTPS does not control browser or embedded client UI presentation, consent dialogs, or anti-phishing UX.

### Server compromise
If signer or verifier infrastructure is compromised, protocol checks cannot guarantee safe behavior.

---

## 11. Residual risks

Even with correct implementation, residual risks remain:

- key compromise risk (private key theft or misuse),
- stale or incorrect key resolution data,
- replay window misconfiguration (too permissive skew or weak store semantics),
- missing optional checks (e.g., absent expected origin, absent replay store),
- operational errors in clock management,
- misuse of successful protocol verification as a substitute for business authorization.

A key residual risk to emphasize:

- **Authentic signed intent does not imply legitimate business behavior.**

---

## 12. Operational recommendations

For production-grade deployments, implementers should:

1. **Enforce HTTPS everywhere** and treat plaintext transport as unsupported.
2. **Always configure a key resolver** when cryptographic verification is required.
3. **Validate header presence** for at least `Intent` and `Intent-Signature` at the application layer.
4. **Configure `ExpectedTargetOrigin`** where origin binding matters.
5. **Enable replay protection** with durable storage for sensitive operations.
6. **Use conservative clock skew** and maintain synchronized system clocks.
7. **Operate robust key lifecycle controls** (generation, storage, rotation, revocation, auditing).
8. **Treat `Intent-Alg`, `Intent-Key-Id`, and `Intent-Version` as policy inputs** at the application layer until Core-level enforcement evolves.
9. **Log structured validation outcomes** (`IntentErrorCode`, issuer, nonce, timestamps) for forensic review.
10. **Perform business authorization after protocol verification**, not instead of it.

---

## 13. Future hardening opportunities

Potential hardening directions for future protocol and implementation evolution:

- explicit Core-level enforcement of `Intent-Alg` consistency,
- optional `kid`-aware key resolution path in verification,
- clearer key-rotation and key-validity policy guidance,
- standardized replay-store behavior guidance for clustered deployments,
- stronger operational profiles (strict-mode verifier presets),
- formalized interoperability profiles for extension fields and forward compatibility.

These opportunities should be treated as future work, not current guarantees.

