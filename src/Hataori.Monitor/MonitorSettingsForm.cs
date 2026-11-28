using Hataori.Application.Localization;

namespace Hataori.Monitor;

/// <summary>Monitorの動作設定を編集します。</summary>
internal partial class MonitorSettingsForm : Form
{
    private readonly MonitorSettingsStore _store;
    private readonly MonitorErrorHandler _errors;

    public MonitorSettingsForm(MonitorSettings settings, MonitorSettingsStore store, MonitorErrorHandler errors)
    {
        _store = store;
        _errors = errors;
        Settings = settings;
        InitializeComponent();
        Text = DisplayLanguage.Text("Hataori Monitor 設定", "Hataori Monitor Settings");
        closeToTray.Text = DisplayLanguage.Text("閉じるボタンでタスクトレイへ格納", "Close button hides to the system tray");
        startMinimized.Text = DisplayLanguage.Text("最小化して起動", "Start minimized to the system tray");
        autoUpdate.Text = DisplayLanguage.Text("起動時に自動更新", "Update automatically at startup");
        intervalLabel.Text = DisplayLanguage.Text("表示の更新間隔（秒）", "Refresh interval (seconds)");
        languageLabel.Text = DisplayLanguage.Text("表示言語", "Display language");
        language.Items.AddRange(["日本語", "English"]);
        language.SelectedIndex = settings.Language == DisplayLanguage.Japanese ? 0 : 1;
        closeToTray.Checked = settings.CloseToTray;
        startMinimized.Checked = settings.StartMinimized;
        autoUpdate.Checked = settings.AutoUpdate;
        refreshSeconds.Value = settings.RefreshSeconds;
        save.Text = DisplayLanguage.Text("保存", "Save");
        cancel.Text = DisplayLanguage.Text("キャンセル", "Cancel");
        pathLabel.Text = DisplayLanguage.Text("設定保存先: ", "Settings file: ") + store.FilePath;
    }

    public MonitorSettings Settings { get; private set; }

    private void SaveClick(object? sender, EventArgs e)
    {
        try
        {
            var settings = new MonitorSettings
            {
                CloseToTray = closeToTray.Checked,
                StartMinimized = startMinimized.Checked,
                AutoUpdate = autoUpdate.Checked,
                RefreshSeconds = (int)refreshSeconds.Value,
                Language = language.SelectedIndex == 0 ? DisplayLanguage.Japanese : DisplayLanguage.English,
            };
            _store.Save(settings);
            Settings = settings;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            _errors.Report(exception, this, DisplayLanguage.Text("設定を保存できませんでした。保存先の権限と空き容量を確認してください。", "Could not save settings. Check permissions and free space."), showDialog: true);
        }
    }
}
