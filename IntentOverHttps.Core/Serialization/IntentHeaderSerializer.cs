using System.Globalization;
using System.Text;
using IntentOverHttps.Core.Models;

namespace IntentOverHttps.Core.Serialization;

public sealed class IntentHeaderSerializer
{
    public string Serialize(IntentDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return string.Join(
            ';',
            CreateField("action", descriptor.Action),
            CreateField("issuer", descriptor.Issuer),
            CreateField("targetOrigin", descriptor.TargetOrigin.GetLeftPart(UriPartial.Authority)),
            CreateField("beneficiary", descriptor.Beneficiary),
            CreateField("amount", FormatAmount(descriptor.Amount)),
            CreateField("currency", descriptor.Currency),
            CreateField("issuedAt", descriptor.IssuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            CreateField("expiresAt", descriptor.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            CreateField("nonce", descriptor.Nonce));
    }

    private static string CreateField(string name, string value) => $"{name}={Escape(value)}";

    private static string FormatAmount(decimal amount) => amount.ToString("0.###############################", CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '\\' or ';' or '=')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}

