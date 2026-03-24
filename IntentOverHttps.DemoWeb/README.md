# IntentOverHttps.DemoWeb

Minimal ASP.NET Core demo service showing how to emit IoHTTPS protocol headers and publish public keys.

The project intentionally uses `IntentOverHttps.AspNetCore` for protocol integration, while keeping demo-specific key material and payload values local.

## Endpoints

- `GET /pay/demo`: builds a demo `IntentDescriptor`, signs it through the shared integration layer, and returns a JSON payload.
- `GET /.well-known/intent-keys`: publishes active public keys in a JWK-like shape (`issuer`, `version`, `keys`).

## Protocol headers

- `Intent`
- `Intent-Signature`
- `Intent-Key-Id`
- `Intent-Alg`
- `Intent-Version`

## Run locally

```powershell
dotnet run --project "E:\DEVELOPPEMENT\C#\IntentOverHttps\IntentOverHttps.DemoWeb\IntentOverHttps.DemoWeb.csproj"
```

