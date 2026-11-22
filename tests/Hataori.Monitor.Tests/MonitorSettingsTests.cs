using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

public sealed class MonitorSettingsTests
{
    [Fact]
    public void Missing_settings_use_defaults_and_installer_language()
    {
        var store = new MonitorSettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        var settings = store.Load("en-US");
        Assert.True(settings.CloseToTray);
        Assert.True(settings.AutoUpdate);
        Assert.False(settings.StartMinimized);
        Assert.Equal(3, settings.RefreshSeconds);
        Assert.Equal("en-US", settings.Language);
    }

    [Fact]
    public void Settings_roundtrip_and_invalid_save_preserves_existing_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hataori-settings-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new MonitorSettingsStore(Path.Combine(directory, "settings.json"));
            var settings = new MonitorSettings { CloseToTray = false, StartMinimized = true, AutoUpdate = false, Language = "en-US", RefreshSeconds = 20 };
            store.Save(settings);
            Assert.Equal(settings, store.Load("ja-JP"));
            var original = File.ReadAllText(store.FilePath);
            Assert.Throws<InvalidDataException>(() => store.Save(settings with { RefreshSeconds = 0 }));
            Assert.Equal(original, File.ReadAllText(store.FilePath));
            Assert.Single(Directory.EnumerateFiles(directory));
            Assert.Contains("close_to_tray", original);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"refresh_seconds\":301}")]
    [InlineData("{\"language\":\"de-DE\"}")]
    public void Corrupt_settings_are_reported_and_preserved(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, json);
            Assert.ThrowsAny<Exception>(() => new MonitorSettingsStore(path).Load("ja-JP"));
            Assert.Equal(json, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(CloseReason.UserClosing, true, false, true)]
    [InlineData(CloseReason.UserClosing, false, false, false)]
    [InlineData(CloseReason.UserClosing, true, true, false)]
    [InlineData(CloseReason.WindowsShutDown, true, false, false)]
    [InlineData(CloseReason.TaskManagerClosing, true, false, false)]
    public void Closing_hides_only_for_user_close_when_enabled(CloseReason reason, bool closeToTray, bool exiting, bool expected) =>
        Assert.Equal(expected, MonitorForm.ShouldHideOnClose(reason, closeToTray, exiting));
}
