using System.Globalization;
using System.Reflection;
using SolidWorks.Interop.sldworks;
using SwTools;

// AddinDev load <dll>            load the add-in into the running SolidWorks
// AddinDev unload <dll>          unload it
// AddinDev status                the add-in's status text
// AddinDev press <buttons>       press and release controller buttons (e.g. Y, "A,DPadUp")
// AddinDev saveview / restoreview <file>   save and restore the active view
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
    case "press":
        Console.WriteLine($"press {args[1]}: {Call(Addin(), "PressButtons", args[1])}");
        Thread.Sleep(1500); // let the add-in run it (and any view animation finish)
        Console.WriteLine(Call(Addin(), "GetStatus"));
        return 0;
    case "saveview":
    case "restoreview":
    {
        var view = (IModelView)((IModelDoc2)sw.ActiveDoc).ActiveView;
        var math = (IMathUtility)sw.GetMathUtility();
        string file = args[1];
        if (cmd == "saveview")
        {
            var parts = ((double[])view.Orientation3.ArrayData)
                .Concat((double[])view.Translation3.ArrayData)
                .Append(view.Scale2).Append(view.HasPerspective() ? 1 : 0);
            File.WriteAllText(file, string.Join(" ", parts.Select(d => d.ToString("R"))));
        }
        else
        {
            var d = File.ReadAllText(file).Split(' ').Select(double.Parse).ToArray();
            int n = d.Length; // orientation (16) + translation (3) + scale + perspective
            if ((d[n - 1] == 1) != view.HasPerspective())
            {
                if (d[n - 1] == 1) view.AddPerspective(); else view.RemovePerspective();
            }
            // Scale2 multiplies Translation3, so translation goes last.
            view.Orientation3 = (MathTransform)math.CreateTransform(d[..(n - 5)]);
            view.Scale2 = d[n - 2];
            view.Translation3 = (MathVector)math.CreateVector(d[(n - 5)..(n - 2)]);
            view.GraphicsRedraw(null);
        }
        Console.WriteLine($"{cmd} {file}");
        return 0;
    }
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
