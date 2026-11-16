using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SwTools;

// SwViewBench: times the SolidWorks view/camera API calls a direct-control version would use.
//
// Usage (SolidWorks open with a part or assembly active, SolidWorks window visible):
//   SwViewBench               view tests only; the view is restored at the end
//   SwViewBench --camera      also tests a floating camera (ADDS a camera to the document;
//                             run it on a copy and close without saving)
//   SwViewBench --frames 240  frames per test (default 180)

namespace SwViewBench;

internal static class Program
{
    private const int CameraTypeFloating = 2; // swCameraType_e.swCameraType_Floating

    private static readonly StringBuilder Report = new();

    [STAThread]
    private static int Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        bool cameraTest = args.Contains("--camera");
        int frames = 180;
        int fi = Array.IndexOf(args, "--frames");
        if (fi >= 0 && fi + 1 < args.Length) frames = Math.Max(30, int.Parse(args[fi + 1]));

        MessageFilter.Register(); // retry instead of failing while SolidWorks is busy

        if (ComHelpers.GetActiveObject("SldWorks.Application") is not ISldWorks swApp)
        {
            Console.WriteLine("SolidWorks isn't running. Open SolidWorks with a part or assembly, then run this again.");
            return 1;
        }
        if (swApp.ActiveDoc is not IModelDoc2 doc || doc.GetType() is not (1 or 2))
        {
            Console.WriteLine("Open a part or assembly in SolidWorks (and make it the active document), then run this again.");
            return 1;
        }

        var view = (IModelView)doc.ActiveView;
        var math = (IMathUtility)swApp.GetMathUtility();
        var box = GetBox(doc);
        double size = Math.Sqrt(Sq(box[3] - box[0]) + Sq(box[4] - box[1]) + Sq(box[5] - box[2]));
        if (size <= 0) size = 1;

        Log($"SwViewBench  {DateTime.Now:yyyy-MM-dd HH:mm}");
        Log($"SolidWorks revision : {swApp.RevisionNumber()}");
        Log($"Document            : {doc.GetTitle()} ({(doc.GetType() == 2 ? "assembly" : "part")})");
        if (doc.GetType() == 2) Log($"Components          : {((IAssemblyDoc)doc).GetComponentCount(false)}");
        Log($"Model size          : {size * 1000:F0} mm (bounding box diagonal)");
        Log($"View                : {view.FrameWidth}x{view.FrameHeight} px, perspective={view.HasPerspective()}");
        Log($"Frames per test     : {frames}");
        Log("");

        var saved = SaveView(view);
        Console.WriteLine("Keep the SolidWorks window visible and watch how smooth each test looks.");
        for (int i = 3; i > 0; i--) { Console.Write($"\rStarting in {i}... "); Thread.Sleep(1000); }
        Console.WriteLine();

        var results = new List<(string Name, List<double> Ms)>();
        try
        {
            results.Add(Run("COM round trip (read Scale2)", 200, i => { _ = view.Scale2; }));

            double step = 2 * Math.PI / frames;
            results.Add(Run("RotateAboutCenter (one full spin)", frames, _ => view.RotateAboutCenter(0, step)));

            double pan = size * 0.25 / (frames / 2.0);
            results.Add(Run("TranslateBy (pan right, then back)", frames,
                i => view.TranslateBy(i < frames / 2 ? pan : -pan, 0)));

            double zoom = Math.Pow(2.0, 1.0 / (frames / 2.0)); // 2x in, then 2x out
            results.Add(Run("ZoomByFactor (2x in, then out)", frames,
                i => view.ZoomByFactor(i < frames / 2 ? zoom : 1 / zoom)));

            RestoreView(view, math, saved);
            results.Add(Run("Orientation3 set, no redraw call", frames,
                i => view.Orientation3 = Rotated(math, saved.Orientation, 2 * Math.PI * (i + 1) / frames)));
            Log("  ^ if the model did NOT move during that test, setting Orientation3 needs a redraw call.");

            RestoreView(view, math, saved);
            results.Add(Run("Orientation3 set + GraphicsRedraw", frames, i =>
            {
                view.Orientation3 = Rotated(math, saved.Orientation, 2 * Math.PI * (i + 1) / frames);
                view.GraphicsRedraw(null);
            }));

            if (cameraTest)
            {
                RestoreView(view, math, saved);
                results.AddRange(CameraTests(doc, view, box, size, frames));
            }
        }
        catch (Exception ex)
        {
            Log($"Test stopped with an error: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { RestoreView(view, math, saved); } catch { /* best effort */ }
        }

        Log("");
        Log($"{"Test",-40} {"calls",6} {"avg ms",8} {"p95 ms",8} {"max ms",8} {"~Hz",6}");
        foreach (var (name, ms) in results)
        {
            if (ms.Count == 0) continue;
            ms.Sort();
            double avg = ms.Average(), max = ms[^1];
            double p95 = ms[Math.Clamp((int)Math.Ceiling(ms.Count * 0.95) - 1, 0, ms.Count - 1)];
            Log($"{name,-40} {ms.Count,6} {avg,8:F2} {p95,8:F2} {max,8:F2} {1000 / Math.Max(avg, 0.001),6:F0}");
        }
        Log("");
        Log("Rule of thumb: under ~8 ms avg = smooth 60 Hz from a separate app; 8-30 ms = usable,");
        Log("an in-SolidWorks add-in would be smoother; over 30 ms = add-in strongly recommended.");

        string outFile = Path.Combine(AppContext.BaseDirectory, "bench-results.txt");
        File.WriteAllText(outFile, Report.ToString());
        Console.WriteLine($"\nSaved to {outFile}");
        return 0;
    }

    private static IEnumerable<(string, List<double>)> CameraTests(IModelDoc2 doc, IModelView view, double[] box, double size, int frames)
    {
        var results = new List<(string, List<double>)>();
        var ext = doc.Extension;
        int before = ext.GetCameraCount();
        Camera cam = ext.InsertCamera();
        cam.Type = CameraTypeFloating;
        view.Camera = cam; // view through the camera
        doc.GraphicsRedraw2();

        // Walk a straight line through the middle of the model at mid height while turning slowly.
        double cy = (box[1] + box[4]) / 2, cz = (box[2] + box[5]) / 2;
        double startX = box[0] - size * 0.25, endX = box[3] + size * 0.25;
        double yaw0 = cam.Yaw;

        results.Add(Run("Camera: move + yaw, then GraphicsRedraw2", frames, i =>
        {
            double t = (i + 1) / (double)frames;
            cam.SetPositionCartesian(startX + (endX - startX) * t, cy, cz);
            cam.Yaw = yaw0 + 0.5 * Math.Sin(t * Math.PI * 2);
            doc.GraphicsRedraw2();
        }));

        results.Add(Run("Camera: IModelView.TurnBy (look around)", frames,
            i => view.TurnBy(0, (i < frames / 2 ? 1 : -1) * Math.PI / frames)));

        try { view.Camera = null!; } catch { /* camera view may already be off */ }
        doc.GraphicsRedraw2();

        string name = $"Camera{before + 1}";
        bool deleted = ext.SelectByID2(name, "CAMERAS", 0, 0, 0, false, 0, null, 0) && ext.DeleteSelection2(0);
        Log("  Camera: did the view travel through the model smoothly? Check the Undo list too:");
        Log("  if every camera move added an undo step, the camera route isn't usable for live control.");
        Log(deleted
            ? $"  Test camera '{name}' was removed."
            : $"  Couldn't remove the test camera (looked for '{name}'). Close the document without saving.");
        return results;
    }

    private static (string, List<double>) Run(string name, int calls, Action<int> body)
    {
        Console.Write($"{name}... ");
        var ms = new List<double>(calls);
        var sw = new Stopwatch();
        for (int i = 0; i < calls; i++)
        {
            sw.Restart();
            body(i);
            ms.Add(sw.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine($"{ms.Average():F2} ms avg");
        return (name, ms);
    }

    // ---- view state helpers ---------------------------------------------------------------

    private sealed record ViewState(double[] Orientation, double[] Translation, double Scale);

    private static ViewState SaveView(IModelView v) => new(
        (double[])v.Orientation3.ArrayData,
        (double[])v.Translation3.ArrayData,
        v.Scale2);

    private static void RestoreView(IModelView v, IMathUtility math, ViewState s)
    {
        // Order matters: setting Scale2 (even to the same value) multiplies Translation3 by the
        // scale (seen on SW 2023 SP5), so the translation has to be set last.
        v.Orientation3 = (MathTransform)math.CreateTransform(s.Orientation);
        v.Scale2 = s.Scale;
        v.Translation3 = (MathVector)math.CreateVector(s.Translation);
        v.GraphicsRedraw(null);
    }

    /// <summary>The saved orientation rotated by an angle about the model's Y axis.</summary>
    private static MathTransform Rotated(IMathUtility math, double[] baseArr, double angle)
    {
        // ArrayData: [0..8] 3x3 rotation (row-major), [9..11] translation, [12] scale, [13..15] unused.
        double c = Math.Cos(angle), s = Math.Sin(angle);
        double[] ry = { c, 0, -s, 0, 1, 0, s, 0, c };
        var arr = (double[])baseArr.Clone();
        for (int r = 0; r < 3; r++)
            for (int col = 0; col < 3; col++)
                arr[r * 3 + col] = ry[r * 3 + 0] * baseArr[0 * 3 + col]
                                 + ry[r * 3 + 1] * baseArr[1 * 3 + col]
                                 + ry[r * 3 + 2] * baseArr[2 * 3 + col];
        return (MathTransform)math.CreateTransform(arr);
    }

    private static double[] GetBox(IModelDoc2 doc)
    {
        try
        {
            object b = doc.GetType() == 2 ? ((IAssemblyDoc)doc).GetBox(0) : ((IPartDoc)doc).GetPartBox(true);
            if (b is double[] { Length: 6 } d) return d;
        }
        catch { /* fall through */ }
        return new double[] { -0.5, -0.5, -0.5, 0.5, 0.5, 0.5 };
    }

    private static double Sq(double x) => x * x;

    private static void Log(string line)
    {
        Console.WriteLine(line);
        Report.AppendLine(line);
    }
}
