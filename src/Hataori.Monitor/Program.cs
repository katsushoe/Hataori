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
            var configuration = Path.GetFullPath(Environment.GetEnvironmentVariable("HATAORI_CONFIG_PATH") ?? layout.ConfigurationPath);
            var defaultLanguage = DisplayLanguage.ApplyFromConfiguration(configuration);
            var pipeName = GetPipeName(args, configuration);
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.TrimEndingDirectorySeparator(layout.RootPath).ToUpperInvariant() + "|" + Environment.UserName)));
            using var mutex = new Mutex(false, @"Local\Hataori.Monitor." + identity);
            using var restore = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Hataori.Monitor.Restore." + identity);
            using var stop = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Hataori.Monitor.Stop." + identity);
            if (args.Contains("--stop", StringComparer.Ordinal))
            {
                if (!TakeMutex(mutex, TimeSpan.Zero))
                {
                    stop.Set();
                    if (!TakeMutex(mutex, TimeSpan.FromSeconds(35)))
                        throw new TimeoutException("Hataori did not shut down in time.");
                }
                mutex.ReleaseMutex();
                return;
            }
            var logon = args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
            if (!TakeMutex(mutex, TimeSpan.Zero))
            {
                if (!logon) restore.Set();
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
                using var server = new UserServerProcess();
                var ownsServer = !args.Contains("--connect-only", StringComparer.Ordinal) &&
                    MonitorUpdateInstaller.IsInstalledMonitor(AppContext.BaseDirectory, layout.RootPath);
                if (ownsServer) server.Start(layout);
                using var form = new MonitorForm(pipeName, new MonitorControlClient(), errors);
                form.ConfigureHost(ownsServer ? server : null, logon);
                form.ConfigureRuntime(settings, store, new MonitorUpdateClient(http, endpoint), args.Contains("--skip-update", StringComparer.OrdinalIgnoreCase));
                using var restoreTimer = new System.Windows.Forms.Timer { Interval = 400 };
                restoreTimer.Tick += (_, _) =>
                {
                    if (stop.WaitOne(0)) form.RequestExit();
                    else if (restore.WaitOne(0)) form.RestoreFromTray();
                };
                restoreTimer.Start();
                System.Windows.Forms.Application.Run(form);
            }
            finally { mutex.ReleaseMutex(); }
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            errors.Report(exception, null, DisplayLanguage.Text("Monitorの処理に失敗しました。ログを確認してください。", "Monitor operation failed. Check the log."), showDialog: !args.Contains("--stop", StringComparer.Ordinal));
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

    private static bool TakeMutex(Mutex mutex, TimeSpan timeout)
    {
        try { return mutex.WaitOne(timeout); }
        catch (AbandonedMutexException) { return true; }
    }

    private static string GetPipeName(IReadOnlyList<string> args, string configuration)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], "--pipe", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        var environment = Environment.GetEnvironmentVariable("HATAORI_CONTROL_PIPE_NAME")
            ?? Environment.GetEnvironmentVariable("HATAORI_SERVER__CONTROLPIPENAME");
        if (!string.IsNullOrWhiteSpace(environment)) return environment;
        if (File.Exists(configuration))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configuration));
            if (document.RootElement.TryGetProperty("server", out var server) &&
                server.TryGetProperty("controlPipeName", out var name) && name.GetString() is { Length: > 0 } configured)
                return configured;
        }
        return "hataori-control";
    }
}
