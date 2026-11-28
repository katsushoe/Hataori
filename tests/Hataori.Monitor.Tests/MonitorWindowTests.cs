using System.Globalization;
using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

public sealed class MonitorWindowTests
{
    [Theory]
    [InlineData("ja-JP")]
    [InlineData("en-US")]
    public void Settings_window_renders_with_saved_values_and_keyboard_actions(string language)
    {
        RunOnSta(() =>
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            using var form = new MonitorSettingsForm(new MonitorSettings { Language = language, RefreshSeconds = 12, AutoUpdate = false },
                new MonitorSettingsStore(Path.Combine(Path.GetTempPath(), "hataori-test-settings.json")), TestMonitorLogs.CreateErrorHandler());
            form.Opacity = 0;
            form.Show();
            form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            Assert.Equal(form.Width, bitmap.Width);
            Assert.NotNull(form.AcceptButton);
            Assert.NotNull(form.CancelButton);
            Assert.Contains(form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<NumericUpDown>(), control => control.Value == 12);
            var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/validation"));
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, $"monitor-settings-{language}.png"));
            form.Hide();
        });
    }

    [Fact]
    public void Settings_save_button_persists_changes_and_cancel_preserves_them()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hataori-settings-tests", Guid.NewGuid().ToString("N"));
        try
        {
            RunOnSta(() =>
            {
                var store = new MonitorSettingsStore(Path.Combine(directory, "settings.json"));
                var errors = TestMonitorLogs.CreateErrorHandler();
                using (var form = new MonitorSettingsForm(new MonitorSettings(), store, errors))
                {
                    form.Opacity = 0;
                    form.Show();
                    form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<NumericUpDown>().Single().Value = 17;
                    ((Button)form.AcceptButton!).PerformClick();
                    Assert.Equal(DialogResult.OK, form.DialogResult);
                }
                Assert.Equal(17, store.Load("ja-JP").RefreshSeconds);
                using (var form = new MonitorSettingsForm(store.Load("ja-JP"), store, errors))
                {
                    form.Opacity = 0;
                    form.Show();
                    form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<NumericUpDown>().Single().Value = 22;
                    ((Button)form.CancelButton!).PerformClick();
                    Assert.Equal(DialogResult.Cancel, form.DialogResult);
                }
                Assert.Equal(17, store.Load("ja-JP").RefreshSeconds);
            });
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void File_logger_writes_to_the_given_directory_and_defaults_to_the_user_log_directory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hataori-logger-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new MonitorFileLogger(directory);
            new MonitorErrorHandler(logger).Report(new InvalidOperationException("test"), null, "guidance", showDialog: false);
            Assert.StartsWith(directory, logger.LogPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("test", File.ReadAllText(logger.LogPath));
            Assert.StartsWith(Path.Combine(MonitorSettingsStore.UserDirectory, "logs"), new MonitorFileLogger().LogPath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(Path.GetTempPath(), TestMonitorLogs.CreateLogger().LogPath, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Installer_does_not_treat_a_development_output_directory_as_an_installation()
    {
        Assert.False(MonitorUpdateInstaller.IsInstalledMonitor(AppContext.BaseDirectory, AppContext.BaseDirectory));
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
