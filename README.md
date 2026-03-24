# Intent over HTTPS (IoHTTPS)

Intent over HTTPS (IoHTTPS) is an application-layer protocol built on top of HTTPS.
It adds a signed, canonical intent layer to help services verify message authenticity,
integrity, temporal validity, and context binding for sensitive actions.

This repository is an early-stage C# reference implementation.

## Repository layout

- `IntentOverHttps.Core`: protocol model, canonical serialization/parser, verification primitives
- `IntentOverHttps.AspNetCore`: ASP.NET Core integration for header emission and key discovery endpoint mapping
- `IntentOverHttps.DemoWeb`: minimal demo service using the shared ASP.NET Core integration
- `IntentOverHttps.Cli`: local tooling for key generation, intent creation, signing, and verification
- `IntentOverHttps.Tests`: unit and integration tests
- `docs/`: protocol and security documentation

## Protocol headers

- `Intent`
- `Intent-Signature`
- `Intent-Key-Id`
- `Intent-Alg`
- `Intent-Version`

## Key discovery

Default well-known endpoint:

```text
/.well-known/intent-keys
```

Current response shape:

- `issuer`
- `version`
- `keys` (entries contain `kid`, `kty`, `crv`, `use`, `alg`, `x`, `y`)

## Build and test

```powershell
dotnet build "E:\DEVELOPPEMENT\C#\IntentOverHttps\IntentOverHttps.sln"
dotnet test "E:\DEVELOPPEMENT\C#\IntentOverHttps\IntentOverHttps.Tests\IntentOverHttps.Tests.csproj"
```

## Documentation

- `docs/protocol.md`
- `docs/message-format.md`
- `docs/verification.md`
- `docs/threat-model.md`

## Scope notes

IoHTTPS verifies signed protocol intent. It does not replace HTTPS and does not determine
whether a signed business action is legitimate or acceptable.

