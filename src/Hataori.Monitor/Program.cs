using Hataori.Application;
using Hataori.Application.Localization;

namespace Hataori.Monitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var layout = InstallationLayout.Resolve(AppContext.BaseDirectory);
        DisplayLanguage.ApplyFromConfiguration(Path.GetFullPath(Environment.GetEnvironmentVariable("HATAORI_CONFIG_PATH") ?? layout.ConfigurationPath));
        var logger = new MonitorFileLogger();
        var errors = new MonitorErrorHandler(logger);
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, eventArgs) => errors.Report(eventArgs.Exception, null, DisplayLanguage.Text("予期しないエラーが発生しました。Monitorを再起動し、解消しない場合はログを管理者へ共有してください。", "An unexpected error occurred. Restart Monitor and share the log with an administrator if the problem continues."), showDialog: true);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => errors.Report(eventArgs.ExceptionObject as Exception ?? new InvalidOperationException("An unknown fatal error occurred."), null, DisplayLanguage.Text("Monitorを継続できないエラーが発生しました。ログを確認してMonitorを再起動してください。", "A fatal error prevents Monitor from continuing. Check the log and restart Monitor."), showDialog: true);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            errors.Report(eventArgs.Exception, null, DisplayLanguage.Text("バックグラウンド処理でエラーが発生しました。ログを確認してください。", "A background operation failed. Check the log for details."), showDialog: true);
            eventArgs.SetObserved();
        };
        System.Windows.Forms.Application.Run(new MonitorForm(GetPipeName(args), new MonitorControlClient(), errors));
    }

    private static string GetPipeName(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], "--pipe", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return Environment.GetEnvironmentVariable("HATAORI_CONTROL_PIPE_NAME") ?? "hataori-control";
    }
}
