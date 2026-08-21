using Hataori.Cli;

var layout = Hataori.Application.InstallationLayout.Resolve(AppContext.BaseDirectory);
Hataori.Application.Localization.DisplayLanguage.ApplyFromConfiguration(
    Path.GetFullPath(Environment.GetEnvironmentVariable("HATAORI_CONFIG_PATH") ?? layout.ConfigurationPath));

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
return await CliApplication.RunAsync(args, Console.In, Console.Out, Console.Error, cancellation.Token);
