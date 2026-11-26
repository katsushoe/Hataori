namespace Hataori.Monitor;

internal interface IMonitorDialogs
{
    bool ConfirmCancel(IWin32Window owner, string message, string title);

    void ShowCancellationFailure(IWin32Window owner, string message, string title);

    bool ConfirmServerRestart(IWin32Window owner, string message, string title);
}

internal sealed class WinFormsMonitorDialogs : IMonitorDialogs
{
    public bool ConfirmCancel(IWin32Window owner, string message, string title) => MessageBox.Show(
        owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    public void ShowCancellationFailure(IWin32Window owner, string message, string title) =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public bool ConfirmServerRestart(IWin32Window owner, string message, string title) => MessageBox.Show(
        owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}
