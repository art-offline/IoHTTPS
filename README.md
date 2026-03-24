# Intent over HTTPS (IoHTTPS)

**Author:** AK  
**Language:** C# (reference implementation)  
**Status:** Draft v0.1  
**Scope:** Application-layer protocol over HTTPS  

---

## Overview

**Intent over HTTPS (IoHTTPS)** is a lightweight protocol designed to add a **verifiable, signed layer of intent** on top of standard HTTPS communication.

HTTPS ensures:
- confidentiality
- integrity of transport
- server authentication

However, HTTPS does **not** guarantee:
- what action a server is asking the user to perform
- whether that action is consistent with expectations
- whether content is safe or misleading
- protection against phishing or UI manipulation

IoHTTPS addresses this gap by introducing a **cryptographically signed intent descriptor**, transmitted via standard HTTP mechanisms, without modifying the underlying transport stack.

---

## Goals

- Provide **explicit, verifiable intent** for sensitive actions (payments, authentication, etc.)
- Ensure **integrity and authenticity** of declared actions
- Remain **fully compatible with existing HTTPS infrastructure**
- Require **no modification to TLS, TCP, or browsers** for initial adoption
- Be **incrementally deployable**
- Be **simple, auditable, and standardizable**

---

## Non-Goals

- Replace HTTPS or TLS
- Guarantee trustworthiness of issuers (only authenticity)
- Prevent all forms of social engineering
- Require browser modifications for basic usage

---

## Core Concept

IoHTTPS introduces a new principle:

**Sensitive actions must be explicitly declared and cryptographically signed.**

Instead of relying on implicit behavior and UI, servers declare intent in a structured, verifiable way.

---

## Protocol Components

### 1. Intent Header

The `Intent` HTTP header describes the requested action.

Example:

    Intent: action="payment", amount=8.40, currency="EUR", beneficiary="merchant_7821", exp=1774347420, nonce="abc123"

---

### 2. HTTP Message Signature

The intent is signed using HTTP Message Signatures.

Example:

    Signature-Input: sig1=("intent" "@authority" "@path");created=1710000000
    Signature: sig1=:BASE64_SIGNATURE:

This ensures:
- integrity of the intent
- binding to request context
- protection against tampering

---

### 3. Key Discovery

Public keys are exposed via:

    https://example.com/.well-known/intent-keys

Example response:

    {
      "keys": [
        {
          "kid": "key-1",
          "alg": "ed25519",
          "public_key": "BASE64...",
          "created_at": "2026-01-01T00:00:00Z",
          "expires_at": "2027-01-01T00:00:00Z"
        }
      ]
    }

---

### 4. Intent Policy (Optional)

Domains may expose policies:

    https://example.com/.well-known/intent-policy

Used to define:
- allowed actions
- required constraints
- validation expectations

---

## Verification Flow

A verifier performs the following:

1. Ensure HTTPS connection
2. Extract the `Intent` header
3. Parse its structure
4. Retrieve issuer public key
5. Verify signature
6. Validate:
   - expiration
   - nonce uniqueness
   - domain consistency
   - action constraints
7. Accept or reject

---

## Example Flow

    Client → HTTPS GET /pay/123
    Server → Response with Intent + Signature
    Client → Verify signature and constraints
    Client → Display verified action
    Client → Proceed or reject

---

## Security Properties

IoHTTPS provides:

- Intent integrity (no undetected modification)
- Issuer authenticity (via signature)
- Replay protection (nonce + expiration)
- Context binding (domain and path)

---

## Limitations

- Does not prevent malicious but correctly signed issuers
- Does not enforce UI at browser level without native support
- Requires verifier implementation
- Does not protect compromised devices

---

## Reference Implementation (C#)

This repository includes a C# reference implementation:

- Intent creation
- Header encoding
- Signature generation (Ed25519)
- Signature verification
- Key discovery client
- Test vectors

Example usage:

    var intent = new IntentDescriptor
    {
        Action = "payment",
        Amount = 8.40m,
        Currency = "EUR",
        Beneficiary = "merchant_7821",
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
        Nonce = Crypto.GenerateNonce()
    };

    var signedHeaders = IntentSigner.Sign(intent, requestContext, privateKey);

---

## Implementation Strategy

IoHTTPS is designed for gradual adoption:

### Phase 1
- Server-side generation
- Logging and validation

### Phase 2
- Backend/API enforcement
- Proxy or gateway validation

### Phase 3
- Client-side integration
- Browser and OS support

---

## Why This Matters

The web verifies **who you connect to**, but not **what they ask you to do**.

IoHTTPS introduces:

**A verifiable contract of intent between client and server.**

This enables:
- safer payments
- safer authentication flows
- reduced phishing risks
- auditable actions

---

## Design Principles

- Build on existing standards
- Avoid reinventing transport or crypto
- Keep implementation minimal and auditable
- Enable interoperability
- Support progressive adoption

---

## Contributing

Contributions are welcome:
- specification improvements
- security analysis
- interoperability testing
- alternative implementations

---

## Roadmap

- Formal specification v1.0
- Interoperability test suite
- Multi-language implementations
- Security review
- Standardization proposal

---

## License

MIT License

---

## Author

AK  
Passionate developer exploring secure-by-design web protocols.
