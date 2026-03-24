using IntentOverHttps.Cli.Parsing;
using IntentOverHttps.Core.Models;

namespace IntentOverHttps.Cli.Commands;

internal static class IntentCommandSupport
{
    public const string IntentOptionsHelp = """
Common intent options:
  --action <value>                Required. Example: pay
  --issuer <value>                Required. Example: merchant-demo
  --target-origin <uri>           Required absolute origin. Example: https://merchant.example
  --beneficiary <value>           Required. Example: merchant-123
  --amount <decimal>              Required. Example: 12.34
  --currency <iso3>               Required. Example: EUR
  --issued-at <timestamp>         Optional ISO-8601 timestamp. Defaults to current UTC time.
  --expires-at <timestamp>        Optional ISO-8601 timestamp. Defaults to issued-at + lifetime-seconds.
  --lifetime-seconds <seconds>    Optional when expires-at is omitted. Default: 300
  --nonce <value>                 Optional. Defaults to a generated GUID without dashes.
""";

    public static IntentDescriptor CreateIntentDescriptor(CommandArguments arguments, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var provider = timeProvider ?? TimeProvider.System;
            var issuedAt = arguments.GetOptionalDateTimeOffset("issued-at") ?? provider.GetUtcNow();
            var expiresAt = arguments.GetOptionalDateTimeOffset("expires-at");
            var lifetimeSeconds = arguments.GetOptionalInt32("lifetime-seconds") ?? 300;

            if (!expiresAt.HasValue && lifetimeSeconds <= 0)
            {
                throw new CommandUsageException("Option '--lifetime-seconds' must be greater than zero.");
            }

            return new IntentDescriptor(
                action: arguments.GetRequired("action"),
                issuer: arguments.GetRequired("issuer"),
                targetOrigin: arguments.GetOptionalAbsoluteUri("target-origin")
                    ?? throw new CommandUsageException("Missing required option '--target-origin'."),
                beneficiary: arguments.GetRequired("beneficiary"),
                amount: arguments.GetRequiredDecimal("amount"),
                currency: arguments.GetRequired("currency"),
                issuedAt: issuedAt,
                expiresAt: expiresAt ?? issuedAt.AddSeconds(lifetimeSeconds),
                nonce: arguments.GetOptional("nonce") ?? Guid.NewGuid().ToString("N"));
        }
        catch (ArgumentException ex)
        {
            throw new CommandUsageException(ex.Message);
        }
    }
}

internal static class IntentHeaderNames
{
    public const string Intent = "Intent";
    public const string Signature = "Intent-Signature";
    public const string KeyId = "Intent-Key-Id";
    public const string Algorithm = "Intent-Alg";
    public const string Version = "Intent-Version";
}

