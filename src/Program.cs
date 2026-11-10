using System.Runtime.InteropServices;

namespace SLAL2OStim;

static class Program
{
    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int pid);

    /// <summary>
    ///   SLAL2OStim.exe                                  -> GUI
    ///   SLAL2OStim.exe [pack folder]                    -> GUI with the pack pre-filled (drag &amp; drop)
    ///   SLAL2OStim.exe --cli [pack] [out] [--prefix X] [--no-fnis]
    ///   SLAL2OStim.exe --cli FNIS_x_List.txt ...        -> convert txt only
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        var rest = args.ToList();
        bool cli = rest.Remove("--cli");
        if (cli)
        {
            if (!Console.IsOutputRedirected) AttachConsole(-1);
            return RunCli(rest);
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(rest.FirstOrDefault()));
        return 0;
    }

    static int RunCli(List<string> args)
    {
        var conv = new Converter { Log = Console.WriteLine };
        int i = args.IndexOf("--prefix");
        if (i >= 0 && i + 1 < args.Count)
        {
            conv.Prefix = args[i + 1];
            args.RemoveRange(i, 2);
        }
        if (args.Remove("--no-fnis")) conv.GenBehaviors = false;
        i = args.IndexOf("--fnis");
        if (i >= 0 && i + 1 < args.Count)
        {
            conv.FnisExe = args[i + 1];
            args.RemoveRange(i, 2);
        }
        if (args.Count == 0)
        {
            Console.WriteLine("usage: SLAL2OStim.exe --cli <pack folder> [out folder] [--prefix X] [--no-fnis] [--fnis exe]");
            Console.WriteLine("       SLAL2OStim.exe --cli FNIS_x_List.txt ...");
            return 2;
        }
        if (args[0].EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return args.Select(conv.ConvertTxt).ToList().All(ok => ok) ? 0 : 1;
        var pack = Path.GetFullPath(args[0].TrimEnd('\\', '/'));
        var outDir = args.Count > 1 ? args[1] : pack + "_OStim";
        return conv.Convert(pack, outDir) ? 0 : 1;
    }
}
