namespace Hataori.Monitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new MonitorForm(GetPipeName(args)));
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
