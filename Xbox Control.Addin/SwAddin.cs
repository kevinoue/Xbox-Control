using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;

[assembly: ComVisible(false)]

namespace XboxControl.Addin
{
    /// <summary>
    /// SolidWorks add-in entry point. SolidWorks creates this COM class at startup (or when the
    /// add-in is ticked in Tools > Add-ins) and calls ConnectToSW on its UI thread.
    /// </summary>
    [ComVisible(true)]
    [Guid(ClassId)]
    [ProgId(ProgIdName)]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public sealed class SwAddin : ISwAddin
    {
        public const string ClassId = "682711DD-EBC4-4666-B8A9-33FCF8AB5C09";
        public const string ProgIdName = "XboxControl.SolidWorksAddin";
        internal const string Title = "Xbox Control";
        internal const string Description = "Move around models with an Xbox controller";

        private ISldWorks? sw;
        private NavigationLoop? loop;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                sw = (ISldWorks)ThisSW;
                sw.SetAddinCallbackInfo2(0, this, Cookie);
                var settings = NavigationSettings.LoadOrCreate();
                loop = new NavigationLoop(sw, settings);
                Log.Info($"Connected to SolidWorks {sw.RevisionNumber()} (add-in {typeof(SwAddin).Assembly.GetName().Version})");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("ConnectToSW", ex);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                loop?.Dispose();
                loop = null;
                if (sw != null) Marshal.ReleaseComObject(sw);
                sw = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Log.Info("Disconnected");
            }
            catch (Exception ex)
            {
                Log.Error("DisconnectFromSW", ex);
            }
            return true;
        }

        // ---- diagnostics, called late-bound from tools/AddinDev ----------------------------

        public string GetStatus() => loop?.Status ?? "not connected";

        /// <summary>Scenarios: combo (default), rotate, pan, zoom. The view is restored afterwards.</summary>
        public string StartSelfTest(string scenario, double seconds, bool batchRedraw) =>
            loop?.StartSelfTest(scenario ?? "combo", seconds, batchRedraw) ?? "not connected";

        public string GetSelfTestResult() => loop?.SelfTestResult ?? "";

        // ---- registration (regasm /codebase, as administrator) -----------------------------
        // tools/register-addin.ps1 writes the same keys per user without administrator rights.

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\SolidWorks\Addins\{{{ClassId}}}"))
                {
                    key!.SetValue(null, 0);
                    key.SetValue("Description", Description);
                    key.SetValue("Title", Title);
                }
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\SolidWorks\AddInsStartup\{{{ClassId}}}"))
                    key!.SetValue(null, 1, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't write the SolidWorks add-in keys: {ex.Message}");
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                Registry.LocalMachine.DeleteSubKey($@"SOFTWARE\SolidWorks\Addins\{{{ClassId}}}", false);
                Registry.CurrentUser.DeleteSubKey($@"Software\SolidWorks\AddInsStartup\{{{ClassId}}}", false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't remove the SolidWorks add-in keys: {ex.Message}");
            }
        }
    }
}
