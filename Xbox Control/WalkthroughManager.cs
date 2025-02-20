// WalkthroughManager.cs

using System;
using System.Runtime.InteropServices;      // For DllImport
using SolidWorks.Interop.sldworks;         // SolidWorks API namespace
using InputSimulatorStandard;              // For InputSimulator
using InputSimulatorStandard.Native;       // For VirtualKeyCode

namespace Xbox_Control
{
    public class WalkthroughManager
    {
        private readonly SldWorks swApp;                  // SolidWorks application object
        private readonly IInputSimulator inputSimulator;  // Input simulator object

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public WalkthroughManager(SldWorks app)
        {
            swApp = app ?? throw new ArgumentNullException(nameof(app), "SolidWorks application instance cannot be null.");

            // Ensure there is an active document in SolidWorks
            ModelDoc2? modelDoc = swApp.ActiveDoc as ModelDoc2;
            if (modelDoc == null)
            {
                throw new InvalidOperationException("No active document found in SolidWorks.");
            }

            // Instantiate the InputSimulator
            inputSimulator = new InputSimulatorStandard.InputSimulator();
        }

        private void BringSolidWorksToFront()
        {
            int windowHandle = swApp.IFrameObject().GetHWnd();
            if (windowHandle == 0)
            {
                throw new InvalidOperationException("Unable to get SolidWorks window handle.");
            }

            IntPtr swMainWindowHandle = new IntPtr(windowHandle);
            SetForegroundWindow(swMainWindowHandle);
        }

        // Movement Methods

        public void StartMovingForward()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.VK_W);
        }

        public void StopMovingForward()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_W);
        }

        public void StartMovingBackward()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.VK_S);
        }

        public void StopMovingBackward()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_S);
        }

        public void StartMovingLeft()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.VK_A);
        }

        public void StopMovingLeft()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_A);
        }

        public void StartMovingRight()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.VK_D);
        }

        public void StopMovingRight()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_D);
        }

        // Turning Methods

        public void StartTurningLeft()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.SHIFT);
            inputSimulator.Keyboard.KeyDown(VirtualKeyCode.VK_A);
        }

        public void StopTurningLeft()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_A);
            inputSimulator.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
        }

        public void StartTurningRight()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.SHIFT);
            inputSimulator.Keyboard.KeyDown(VirtualKeyCode.VK_D);
        }

        public void StopTurningRight()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_D);
            inputSimulator.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
        }

        // Looking Methods

        public void StartLookingUp()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.MENU); // Alt key
            inputSimulator.Keyboard.KeyDown(VirtualKeyCode.UP);
        }

        public void StopLookingUp()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.UP);
            inputSimulator.Keyboard.KeyUp(VirtualKeyCode.MENU);
        }

        public void StartLookingDown()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.MENU);
            inputSimulator.Keyboard.KeyDown(VirtualKeyCode.DOWN);
        }

        public void StopLookingDown()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.DOWN);
            inputSimulator.Keyboard.KeyUp(VirtualKeyCode.MENU);
        }

        // Elevation Methods

        public void StartMovingUp()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.SHIFT);
            inputSimulator.Keyboard.KeyDown(VirtualKeyCode.UP);
        }

        public void StopMovingUp()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.UP);
            inputSimulator.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
        }

        public void StartMovingDown()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.SHIFT);
            inputSimulator.Keyboard.KeyDown(VirtualKeyCode.DOWN);
        }

        public void StopMovingDown()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.DOWN);
            inputSimulator.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
        }

        // Zoom Methods

        public void StartZoomIn()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.VK_Z);
        }

        public void StopZoomIn()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_Z);
        }

        public void StartZoomOut()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyDown(VirtualKeyCode.SHIFT);
            inputSimulator.Keyboard.KeyDown(VirtualKeyCode.VK_Z);
        }

        public void StopZoomOut()
        {
            inputSimulator.Keyboard!.KeyUp(VirtualKeyCode.VK_Z);
            inputSimulator.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
        }

        // Methods to simulate pressing the '=' and '-' keys

        public void PressEqualKey()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyPress(VirtualKeyCode.OEM_PLUS);
        }

        public void PressMinusKey()
        {
            BringSolidWorksToFront();
            inputSimulator.Keyboard!.KeyPress(VirtualKeyCode.OEM_MINUS);
        }
    }
}
