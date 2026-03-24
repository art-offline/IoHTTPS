using IntentOverHttps.Cli;
using IntentOverHttps.Cli.Commands;

var application = new CliApplication(
[
    new GenerateKeyCommand(),
    new CreateIntentCommand(),
    new SignIntentCommand(),
    new VerifyIntentCommand(),
    new ShowExampleCommand()
]);

return await application.RunAsync(args, CancellationToken.None);

