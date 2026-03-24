using IntentOverHttps.AspNetCore;
using IntentOverHttps.AspNetCore.KeyDiscovery;
using IntentOverHttps.AspNetCore.Signing;
using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.DemoWeb.Endpoints;
using IntentOverHttps.DemoWeb.Options;
using IntentOverHttps.DemoWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<DemoIntentOptions>(builder.Configuration.GetSection(DemoIntentOptions.SectionName));
builder.Services.AddIntentOverHttps(options =>
{
	options.Issuer = builder.Configuration[$"{DemoIntentOptions.SectionName}:{nameof(DemoIntentOptions.Issuer)}"] ?? "intent-demo-web";
	options.Version = "1";
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDemoIntentKeyMaterialStore, InMemoryIntentKeyMaterialStore>();
builder.Services.AddSingleton<IKeyResolver>(static serviceProvider => serviceProvider.GetRequiredService<IDemoIntentKeyMaterialStore>());
builder.Services.AddSingleton<IIntentSigner, EcdsaIntentSigner>();
builder.Services.AddSingleton<IIntentKeyMetadataProvider, DemoIntentKeyMetadataProvider>();
builder.Services.AddSingleton<IIntentPublicKeyProvider, DemoIntentPublicKeyProvider>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/pay/demo"));
app.MapIntentDemoEndpoints();
app.MapIntentKeyDiscovery();

app.Run();

public partial class Program;

