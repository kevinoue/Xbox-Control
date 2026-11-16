using System;
using System.Diagnostics;
using System.IO;
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
        internal const string SettingsAppName = "XboxControlSettings.exe";

        private const string SettingsMenu = "Xbox Control Settings...@&Tools";
        private static readonly int[] MenuDocTypes = { 0, 1, 2, 3 }; // none, part, assembly, drawing

        private ISldWorks? sw;
        private int cookie;
        private NavigationLoop? loop;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                sw = (ISldWorks)ThisSW;
                cookie = Cookie;
                sw.SetAddinCallbackInfo2(0, this, Cookie);
                loop = new NavigationLoop(sw, NavigationSettings.DefaultPath, OpenSettings);
                foreach (int docType in MenuDocTypes)
                    sw.AddMenuItem5(docType, cookie, SettingsMenu, -1, nameof(OpenSettings), "",
                        "Map the Xbox controller's buttons and sticks", "");
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
                if (sw != null)
                {
                    foreach (int docType in MenuDocTypes) sw.RemoveMenu(docType, SettingsMenu, "");
                    Marshal.ReleaseComObject(sw);
                }
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

        /// <summary>Tools menu callback (and the "Open these settings" button action).</summary>
        public void OpenSettings()
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(SwAddin).Assembly.Location)!;
                string exe = Path.Combine(dir, SettingsAppName);
                if (File.Exists(exe))
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                else
                    sw?.SendMsgToUser2($"The Xbox Control Settings app wasn't found:\n{exe}" +
                        $"\n\nYou can also edit {NavigationSettings.DefaultPath} by hand.",
                        1 /* warning */, 2 /* OK */);
            }
            catch (Exception ex)
            {
                Log.Error("OpenSettings", ex);
            }
        }

        // ---- diagnostics, called late-bound from tools/AddinDev ----------------------------

        public string GetStatus() => loop?.Status ?? "not connected";

        /// <summary>Scenarios: combo (default), rotate, pan, zoom. The view is restored afterwards.</summary>
        public string StartSelfTest(string scenario, double seconds, bool batchRedraw) =>
            loop?.StartSelfTest(scenario ?? "combo", seconds, batchRedraw) ?? "not connected";

        public string GetSelfTestResult() => loop?.SelfTestResult ?? "";

        /// <summary>Press and release controller buttons, e.g. "Y" or "A,DPadUp".</summary>
        public string PressButtons(string names) => loop?.PressButtons(names) ?? "not connected";

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
