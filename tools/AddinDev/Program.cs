using System.Globalization;
using System.Reflection;
using SolidWorks.Interop.sldworks;
using SwTools;

// AddinDev load <dll>            load the add-in into the running SolidWorks
// AddinDev unload <dll>          unload it
// AddinDev status                the add-in's status text
// AddinDev selftest [combo|rotate|pan|zoom] [seconds] [--nobatch]
//                                synthetic stick input through the add-in's own loop, inside
//                                SolidWorks; the view is restored afterwards

const string AddinClsid = "{682711DD-EBC4-4666-B8A9-33FCF8AB5C09}";

Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
MessageFilter.Register();
if (ComHelpers.GetActiveObject("SldWorks.Application") is not ISldWorks sw)
{
    Console.WriteLine("SolidWorks isn't running.");
    return 1;
}

string cmd = args.Length > 0 ? args[0] : "status";
switch (cmd)
{
    case "load":
    case "unload":
    {
        string path = Path.GetFullPath(args[1]);
        int rc = cmd == "load" ? sw.LoadAddIn(path) : sw.UnloadAddIn(path);
        // LoadAddIn: 0 = loaded, 2 = already loaded, anything else = failed (swLoadAddinError_e)
        Console.WriteLine($"{cmd} {path} -> {rc}");
        return rc is 0 or 2 ? 0 : 1;
    }
    case "status":
        Console.WriteLine(Call(Addin(), "GetStatus"));
        return 0;
    case "selftest":
    {
        string scenario = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "combo";
        double seconds = args.Length > 2 && double.TryParse(args[2], out var s) ? s : 6;
        bool batch = !args.Contains("--nobatch");
        var addin = Addin();
        Console.WriteLine($"Self-test {scenario}, {seconds} s, batch={batch}: {Call(addin, "StartSelfTest", scenario, seconds, batch)}");
        var until = DateTime.UtcNow.AddSeconds(seconds + 20);
        while (DateTime.UtcNow < until)
        {
            Thread.Sleep(250);
            if (Call(addin, "GetSelfTestResult") is string r && r.Length > 0)
            {
                Console.Write(r);
                return 0;
            }
        }
        Console.WriteLine("Timed out waiting for the result.");
        return 1;
    }
    default:
        Console.WriteLine("Commands: load <dll> | unload <dll> | status | selftest [scenario] [seconds] [--nobatch]");
        return 1;
}

object Addin() => sw.GetAddInObject(AddinClsid) ?? throw new InvalidOperationException("The add-in isn't loaded.");

static object? Call(object target, string method, params object[] args) =>
    target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);
