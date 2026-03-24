# IntentOverHttps CLI

A small .NET 10 console application for generating, signing, inspecting, and verifying IoHTTPS protocol values during local development.

## Goals

- no external command-line framework
- practical for debugging and documentation
- reuses `IntentOverHttps.Core` for protocol models, parsing, serialization, and verification

## Commands

### `generate-key`

Generate a new ES256 (ECDSA P-256) key pair and print:

- private key as PKCS#8 PEM
- public key as SPKI PEM
- public key as Base64 SPKI
- JWK `x` / `y` coordinates
- a suggested dev-friendly `kid`

```powershell
cd "E:\DEVELOPPEMENT\C#\IntentOverHttps"
dotnet run --project .\IntentOverHttps.Cli -- generate-key
```

### `create-intent`

Create an `IntentDescriptor` from command-line values and print the canonical `Intent` header value.

```powershell
dotnet run --project .\IntentOverHttps.Cli -- create-intent `
  --action pay `
  --issuer merchant-demo `
  --target-origin https://merchant.example `
  --beneficiary merchant-123 `
  --amount 12.34 `
  --currency EUR
```

### `sign-intent`

Create and sign an intent using a private key.

```powershell
dotnet run --project .\IntentOverHttps.Cli -- sign-intent `
  --private-key-file .\dev-private-key.pem `
  --action pay `
  --issuer merchant-demo `
  --target-origin https://merchant.example `
  --beneficiary merchant-123 `
  --amount 12.34 `
  --currency EUR
```

### `verify-intent`

Verify a serialized intent and signature using a public key.

```powershell
dotnet run --project .\IntentOverHttps.Cli -- verify-intent `
  --public-key-file .\dev-public-key.pem `
  --intent "action=pay;issuer=merchant-demo;targetOrigin=https://merchant.example;beneficiary=merchant-123;amount=12.34;currency=EUR;issuedAt=2026-03-24T12:00:00.0000000+00:00;expiresAt=2026-03-24T12:05:00.0000000+00:00;nonce=abc123" `
  --signature "<Base64UrlSignature>"
```

### `show-example`

Print a complete valid example header set plus the matching public key.

```powershell
dotnet run --project .\IntentOverHttps.Cli -- show-example
```

## Accepted key formats

### Private key input

- PKCS#8 PEM
- Base64-encoded PKCS#8 bytes

### Public key input

- PEM public key
- Base64-encoded SubjectPublicKeyInfo bytes

## Notes

- `create-intent` and `sign-intent` default `issuedAt` to the current UTC time.
- `expiresAt` defaults to `issuedAt + 300 seconds` unless you pass `--expires-at`.
- `nonce` defaults to a generated GUID without dashes.
- `verify-intent` returns a non-zero exit code when validation fails.
- Run `dotnet run --project .\IntentOverHttps.Cli -- <command> --help` for per-command help.

