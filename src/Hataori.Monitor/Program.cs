using Hataori.Application;
using Hataori.Application.Localization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Hataori.Monitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
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
        try
        {
            if (args.Length == 2 && args[0] == "--apply-update")
            {
                RunUpdateHelper(args[1], errors);
                return;
            }
            var layout = InstallationLayout.Resolve(AppContext.BaseDirectory);
            var defaultLanguage = DisplayLanguage.ApplyFromConfiguration(Path.GetFullPath(Environment.GetEnvironmentVariable("HATAORI_CONFIG_PATH") ?? layout.ConfigurationPath));
            var pipeName = GetPipeName(args);
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(layout.RootPath.ToUpperInvariant() + "|" + pipeName + "|" + Environment.UserName)));
            using var mutex = new Mutex(false, @"Local\Hataori.Monitor." + identity);
            using var restore = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Hataori.Monitor.Restore." + identity);
            bool ownsMutex;
            try { ownsMutex = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex)
            {
                restore.Set();
                return;
            }
            try
            {
                var store = new MonitorSettingsStore(Path.Combine(MonitorSettingsStore.UserDirectory, "settings.json"));
                MonitorSettings settings;
                try { settings = store.Load(defaultLanguage); }
                catch (Exception exception)
                {
                    settings = new MonitorSettings { Language = defaultLanguage, AutoUpdate = false };
                    errors.Report(exception, null, DisplayLanguage.Text("Monitor設定を読み込めませんでした。今回は自動更新を無効にして起動します。設定画面で保存し直してください。", "Could not read Monitor settings. Automatic updates are disabled for this launch. Save your settings again."), showDialog: true);
                }
                using var resource = typeof(Program).Assembly.GetManifestResourceStream("Hataori.Monitor.monitor-update.json")
                    ?? throw new InvalidDataException("Missing update configuration.");
                using var document = JsonDocument.Parse(resource);
                var endpoint = new Uri(document.RootElement.GetProperty("endpoint").GetString()!);
                var services = new ServiceCollection();
                services.AddHttpClient("updates", client => client.Timeout = TimeSpan.FromMinutes(2))
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
                using var provider = services.BuildServiceProvider();
                using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("updates");
                using var form = new MonitorForm(pipeName, new MonitorControlClient(), errors);
                form.ConfigureRuntime(settings, store, new MonitorUpdateClient(http, endpoint), args.Contains("--skip-update", StringComparer.OrdinalIgnoreCase));
                using var restoreTimer = new System.Windows.Forms.Timer { Interval = 400 };
                restoreTimer.Tick += (_, _) => { if (restore.WaitOne(0)) form.RestoreFromTray(); };
                restoreTimer.Start();
                System.Windows.Forms.Application.Run(form);
            }
            finally { mutex.ReleaseMutex(); }
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            errors.Report(exception, null, DisplayLanguage.Text("Monitorを起動できませんでした。ログを確認してください。", "Could not start Monitor. Check the log."), showDialog: true);
        }
    }

    private static void RunUpdateHelper(string jobPath, MonitorErrorHandler errors)
    {
        using var dispatcher = new Control();
        dispatcher.CreateControl();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        var started = false;
        using var context = new ApplicationContext();
        EventHandler? handler = null;
        handler = async (_, _) =>
        {
            if (started) return;
            started = true;
            System.Windows.Forms.Application.Idle -= handler;
            Environment.ExitCode = await MonitorUpdateInstaller.ApplyAsync(jobPath, errors).ConfigureAwait(true);
            context.ExitThread();
        };
        System.Windows.Forms.Application.Idle += handler;
        System.Windows.Forms.Application.Run(context);
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
