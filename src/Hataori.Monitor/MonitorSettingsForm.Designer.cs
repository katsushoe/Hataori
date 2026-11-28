#nullable enable

namespace Hataori.Monitor;

partial class MonitorSettingsForm
{
    private System.ComponentModel.IContainer? components;
    private TableLayoutPanel layout = null!;
    private CheckBox closeToTray = null!;
    private CheckBox startMinimized = null!;
    private CheckBox autoUpdate = null!;
    private Label intervalLabel = null!;
    private NumericUpDown refreshSeconds = null!;
    private Label languageLabel = null!;
    private ComboBox language = null!;
    private Label pathLabel = null!;
    private FlowLayoutPanel buttons = null!;
    private Button save = null!;
    private Button cancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new TableLayoutPanel();
        closeToTray = new CheckBox();
        startMinimized = new CheckBox();
        autoUpdate = new CheckBox();
        intervalLabel = new Label();
        refreshSeconds = new NumericUpDown();
        languageLabel = new Label();
        language = new ComboBox();
        pathLabel = new Label();
        buttons = new FlowLayoutPanel();
        save = new Button();
        cancel = new Button();
        SuspendLayout();
        layout.Dock = DockStyle.Fill;
        layout.Padding = new Padding(16);
        layout.ColumnCount = 2;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        layout.RowCount = 7;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        closeToTray.AutoSize = true;
        closeToTray.TabIndex = 0;
        startMinimized.AutoSize = true;
        startMinimized.TabIndex = 1;
        autoUpdate.AutoSize = true;
        autoUpdate.TabIndex = 2;
        intervalLabel.AutoSize = true;
        intervalLabel.Anchor = AnchorStyles.Left;
        refreshSeconds.Minimum = 1;
        refreshSeconds.Maximum = 300;
        refreshSeconds.TabIndex = 3;
        languageLabel.AutoSize = true;
        languageLabel.Anchor = AnchorStyles.Left;
        language.DropDownStyle = ComboBoxStyle.DropDownList;
        language.Dock = DockStyle.Top;
        language.TabIndex = 4;
        pathLabel.Dock = DockStyle.Fill;
        pathLabel.AutoEllipsis = true;
        buttons.Dock = DockStyle.Fill;
        buttons.FlowDirection = FlowDirection.RightToLeft;
        save.AutoSize = true;
        save.TabIndex = 5;
        save.Click += SaveClick;
        cancel.AutoSize = true;
        cancel.TabIndex = 6;
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(closeToTray, 0, 0);
        layout.SetColumnSpan(closeToTray, 2);
        layout.Controls.Add(startMinimized, 0, 1);
        layout.SetColumnSpan(startMinimized, 2);
        layout.Controls.Add(autoUpdate, 0, 2);
        layout.SetColumnSpan(autoUpdate, 2);
        layout.Controls.Add(intervalLabel, 0, 3);
        layout.Controls.Add(refreshSeconds, 1, 3);
        layout.Controls.Add(languageLabel, 0, 4);
        layout.Controls.Add(language, 1, 4);
        layout.Controls.Add(pathLabel, 0, 5);
        layout.SetColumnSpan(pathLabel, 2);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 380);
        MinimumSize = new Size(640, 380);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = save;
        CancelButton = cancel;
        ResumeLayout(false);
    }
}
