#nullable enable

namespace Hataori.Monitor;

partial class MonitorForm
{
    private System.ComponentModel.IContainer? components;
    private TableLayoutPanel rootLayout = null!;
    private FlowLayoutPanel toolbar = null!;
    private Button refreshButton = null!;
    private Label connectionStatusLabel = null!;
    private TabControl tabs = null!;
    private TabPage tasksPage = null!;
    private TabPage agentsPage = null!;
    private TabPage sessionsPage = null!;
    private TabPage statusPage = null!;
    private DataGridView taskGrid = null!;
    private DataGridView agentGrid = null!;
    private DataGridView sessionGrid = null!;
    private TableLayoutPanel statusLayout = null!;
    private Label serverValueLabel = null!;
    private Label itogurumaValueLabel = null!;
    private Label mcpValueLabel = null!;
    private Label sqliteValueLabel = null!;
    private Label queueValueLabel = null!;
    private System.Windows.Forms.Timer refreshTimer = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rootLayout = new TableLayoutPanel();
        toolbar = new FlowLayoutPanel();
        refreshButton = new Button();
        connectionStatusLabel = new Label();
        tabs = new TabControl();
        tasksPage = new TabPage();
        agentsPage = new TabPage();
        sessionsPage = new TabPage();
        statusPage = new TabPage();
        taskGrid = CreateGrid();
        agentGrid = CreateGrid();
        sessionGrid = CreateGrid();
        statusLayout = new TableLayoutPanel();
        serverValueLabel = new Label();
        itogurumaValueLabel = new Label();
        mcpValueLabel = new Label();
        sqliteValueLabel = new Label();
        queueValueLabel = new Label();
        refreshTimer = new System.Windows.Forms.Timer(components);
        SuspendLayout();
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.RowCount = 2;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        toolbar.Dock = DockStyle.Fill;
        toolbar.FlowDirection = FlowDirection.LeftToRight;
        toolbar.Padding = new Padding(6);
        refreshButton.AutoSize = true;
        refreshButton.Text = "更新";
        refreshButton.Click += RefreshButtonClick;
        connectionStatusLabel.AutoSize = true;
        connectionStatusLabel.Margin = new Padding(12, 8, 3, 3);
        connectionStatusLabel.Text = "未接続";
        toolbar.Controls.Add(refreshButton);
        toolbar.Controls.Add(connectionStatusLabel);
        tabs.Dock = DockStyle.Fill;
        tasksPage.Text = "Task";
        agentsPage.Text = "Agent";
        sessionsPage.Text = "Conversation / Session";
        statusPage.Text = "状態";
        tasksPage.Controls.Add(taskGrid);
        agentsPage.Controls.Add(agentGrid);
        sessionsPage.Controls.Add(sessionGrid);
        tabs.TabPages.AddRange([tasksPage, agentsPage, sessionsPage, statusPage]);
        statusLayout.ColumnCount = 2;
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusLayout.Dock = DockStyle.Fill;
        statusLayout.Padding = new Padding(16);
        AddStatusRow("Hataori Server", serverValueLabel, 0);
        AddStatusRow("Itoguruma", itogurumaValueLabel, 1);
        AddStatusRow("MCP", mcpValueLabel, 2);
        AddStatusRow("SQLite", sqliteValueLabel, 3);
        AddStatusRow("Queue件数", queueValueLabel, 4);
        statusPage.Controls.Add(statusLayout);
        rootLayout.Controls.Add(toolbar, 0, 0);
        rootLayout.Controls.Add(tabs, 0, 1);
        Controls.Add(rootLayout);
        MinimumSize = new Size(800, 500);
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Hataori Monitor";
        refreshTimer.Interval = 3000;
        refreshTimer.Tick += RefreshTimerTick;
        ResumeLayout(false);
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView { AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };
    }

    private void AddStatusRow(string name, Label value, int row)
    {
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        statusLayout.Controls.Add(new Label { AutoSize = true, Text = name }, 0, row);
        value.AutoSize = true;
        value.Text = "-";
        statusLayout.Controls.Add(value, 1, row);
    }
}
