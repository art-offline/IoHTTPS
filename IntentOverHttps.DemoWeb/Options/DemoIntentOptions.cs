namespace IntentOverHttps.DemoWeb.Options;

public sealed class DemoIntentOptions
{
    public const string SectionName = "IntentDemo";

    public string Issuer { get; set; } = "intent-demo-web";

    public string KeyId { get; set; } = "demo-es256-key-1";

    public string Action { get; set; } = "pay";

    public string Beneficiary { get; set; } = "merchant-demo";

    public decimal Amount { get; set; } = 12.34m;

    public string Currency { get; set; } = "EUR";

    public int LifetimeSeconds { get; set; } = 300;
}

