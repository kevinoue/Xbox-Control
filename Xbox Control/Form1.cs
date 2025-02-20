// Form1.cs

using System;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;     // SolidWorks API namespace
using System.Runtime.InteropServices;  // For COMException
using SharpDX.XInput;                  // For Xbox controller input
using Microsoft.VisualBasic;           // For Interaction.GetObject
using SharpDX;                         // For SharpDXException

namespace Xbox_Control
{
    public partial class Form1 : Form
    {
        private readonly SldWorks? swApp;
        private WalkthroughManager? walkthroughManager;
        private Controller? controller;
        private System.Windows.Forms.Timer? controllerTimer;
        private System.Windows.Forms.Timer? controllerRetryTimer;

        private const float DEADZONE = 0.4f;

        // Movement State Variables
        private bool isMovingForward = false;
        private bool isMovingBackward = false;
        private bool isMovingRight = false;
        private bool isMovingLeft = false;
        private bool isLookingUp = false;
        private bool isLookingDown = false;
        private bool isTurningLeft = false;
        private bool isTurningRight = false;
        private bool isMovingUp = false;
        private bool isMovingDown = false;

        // Bumper and DPad State Variables
        private bool isZoomingIn = false;
        private bool isZoomingOut = false;
        private bool wasDPadUpPressed = false;
        private bool wasDPadDownPressed = false;

        public Form1()
        {
            InitializeComponent();

            try
            {
                swApp = InitializeSolidWorks();

                if (swApp == null)
                {
                    MessageBox.Show("Failed to initialize SolidWorks.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }

                // Try to initialize the controller
                TryInitializeController();

                if (controller == null || !controller.IsConnected)
                {
                    // Display a label indicating waiting for controller
                    Label lblWaiting = new Label()
                    {
                        Text = "Waiting for Xbox controller to connect...",
                        AutoSize = true,
                        Location = new System.Drawing.Point(10, 10)
                    };
                    this.Controls.Add(lblWaiting);

                    // Start a timer to retry controller initialization
                    controllerRetryTimer = new System.Windows.Forms.Timer
                    {
                        Interval = 2000 // Retry every 2 seconds
                    };
                    controllerRetryTimer.Tick += (s, e) =>
                    {
                        TryInitializeController();

                        if (controller != null && controller.IsConnected)
                        {
                            controllerRetryTimer.Stop();
                            controllerRetryTimer.Dispose();
                            controllerRetryTimer = null;

                            // Remove the waiting label
                            this.Controls.Remove(lblWaiting);

                            // Initialize the walkthrough manager and start timers
                            InitializeWalkthroughManagerAndTimers();
                        }
                    };
                    controllerRetryTimer.Start();
                }
                else
                {
                    // Controller is connected; initialize walkthrough manager and timers
                    InitializeWalkthroughManagerAndTimers();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }
        }

        private SldWorks? InitializeSolidWorks()
        {
            SldWorks? swAppLocal = null;

            try
            {
                // Attempt to get the running instance of SolidWorks
                swAppLocal = (SldWorks?)Interaction.GetObject(null, "SldWorks.Application");
            }
            catch (COMException)
            {
                // COMException caught; will attempt to create a new instance below
            }

            if (swAppLocal == null)
            {
                // If still null, try to create a new instance
                Type? swType = Type.GetTypeFromProgID("SldWorks.Application");
                if (swType == null)
                {
                    throw new InvalidOperationException("Unable to get SolidWorks type.");
                }

                swAppLocal = (SldWorks?)Activator.CreateInstance(swType);
                if (swAppLocal == null)
                {
                    throw new InvalidOperationException("Unable to create SolidWorks instance.");
                }
                swAppLocal.Visible = true;
            }

            return swAppLocal;
        }

        private void TryInitializeController()
        {
            try
            {
                controller = new Controller(UserIndex.One);

                if (!controller.IsConnected)
                {
                    controller = null;
                }
            }
            catch (SharpDXException)
            {
                // Controller not connected
                controller = null;
            }
        }

        private void InitializeWalkthroughManagerAndTimers()
        {
            if (swApp == null)
            {
                throw new InvalidOperationException("SolidWorks application is not initialized.");
            }

            walkthroughManager = new WalkthroughManager(swApp);
            controllerTimer = InitializeControllerTimer();
        }

        private System.Windows.Forms.Timer InitializeControllerTimer()
        {
            var timer = new System.Windows.Forms.Timer
            {
                Interval = 50 // Adjust the interval as needed
            };
            timer.Tick += ControllerTimer_Tick;
            timer.Start();

            return timer;
        }

        private void ControllerTimer_Tick(object? sender, EventArgs? e)
        {
            UpdateControllerState();
        }

        private void UpdateControllerState()
        {
            if (walkthroughManager == null || controller == null) return;

            var state = controller.GetState();

            float leftThumbX = NormalizeThumbValue(state.Gamepad.LeftThumbX);
            float leftThumbY = NormalizeThumbValue(state.Gamepad.LeftThumbY);
            float rightThumbX = NormalizeThumbValue(state.Gamepad.RightThumbX);
            float rightThumbY = NormalizeThumbValue(state.Gamepad.RightThumbY);

            float leftTrigger = state.Gamepad.LeftTrigger / 255.0f;
            float rightTrigger = state.Gamepad.RightTrigger / 255.0f;

            ProcessMovement(leftThumbX, leftThumbY);
            ProcessRotation(rightThumbX, rightThumbY);
            ProcessElevation(leftTrigger, rightTrigger);
            ProcessBumpersAndDPad(state.Gamepad.Buttons);
        }

        private void ProcessBumpersAndDPad(GamepadButtonFlags buttons)
        {
            // Left Bumper (LB) - Zoom Out (Shift+Z)
            bool isLeftBumperPressed = buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
            if (isLeftBumperPressed && !isZoomingOut)
            {
                isZoomingOut = true;
                walkthroughManager!.StartZoomOut();
            }
            else if (!isLeftBumperPressed && isZoomingOut)
            {
                isZoomingOut = false;
                walkthroughManager!.StopZoomOut();
            }

            // Right Bumper (RB) - Zoom In (Z)
            bool isRightBumperPressed = buttons.HasFlag(GamepadButtonFlags.RightShoulder);
            if (isRightBumperPressed && !isZoomingIn)
            {
                isZoomingIn = true;
                walkthroughManager!.StartZoomIn();
            }
            else if (!isRightBumperPressed && isZoomingIn)
            {
                isZoomingIn = false;
                walkthroughManager!.StopZoomIn();
            }

            // DPad Up - '=' key (one press per push)
            bool isDPadUpPressed = buttons.HasFlag(GamepadButtonFlags.DPadUp);
            if (isDPadUpPressed && !wasDPadUpPressed)
            {
                walkthroughManager!.PressEqualKey();
            }
            wasDPadUpPressed = isDPadUpPressed;

            // DPad Down - '-' key (one press per push)
            bool isDPadDownPressed = buttons.HasFlag(GamepadButtonFlags.DPadDown);
            if (isDPadDownPressed && !wasDPadDownPressed)
            {
                walkthroughManager!.PressMinusKey();
            }
            wasDPadDownPressed = isDPadDownPressed;
        }

        private float NormalizeThumbValue(short value)
        {
            const float MAX_THUMB_VALUE = 32767.0f;
            return value / MAX_THUMB_VALUE;
        }

        private void ProcessMovement(float leftThumbX, float leftThumbY)
        {
            // Movement Forward and Backward
            if (leftThumbY > DEADZONE)
            {
                if (!isMovingForward)
                {
                    isMovingForward = true;
                    walkthroughManager!.StartMovingForward();
                }
            }
            else if (isMovingForward)
            {
                isMovingForward = false;
                walkthroughManager!.StopMovingForward();
            }

            if (leftThumbY < -DEADZONE)
            {
                if (!isMovingBackward)
                {
                    isMovingBackward = true;
                    walkthroughManager!.StartMovingBackward();
                }
            }
            else if (isMovingBackward)
            {
                isMovingBackward = false;
                walkthroughManager!.StopMovingBackward();
            }

            // Movement Right and Left
            if (leftThumbX > DEADZONE)
            {
                if (!isMovingRight)
                {
                    isMovingRight = true;
                    walkthroughManager!.StartMovingRight();
                }
            }
            else if (isMovingRight)
            {
                isMovingRight = false;
                walkthroughManager!.StopMovingRight();
            }

            if (leftThumbX < -DEADZONE)
            {
                if (!isMovingLeft)
                {
                    isMovingLeft = true;
                    walkthroughManager!.StartMovingLeft();
                }
            }
            else if (isMovingLeft)
            {
                isMovingLeft = false;
                walkthroughManager!.StopMovingLeft();
            }
        }

        private void ProcessRotation(float rightThumbX, float rightThumbY)
        {
            // Turning Right and Left
            if (rightThumbX > DEADZONE)
            {
                if (!isTurningRight)
                {
                    isTurningRight = true;
                    walkthroughManager!.StartTurningRight();
                }
            }
            else if (isTurningRight)
            {
                isTurningRight = false;
                walkthroughManager!.StopTurningRight();
            }

            if (rightThumbX < -DEADZONE)
            {
                if (!isTurningLeft)
                {
                    isTurningLeft = true;
                    walkthroughManager!.StartTurningLeft();
                }
            }
            else if (isTurningLeft)
            {
                isTurningLeft = false;
                walkthroughManager!.StopTurningLeft();
            }

            // Looking Up and Down
            if (rightThumbY > DEADZONE)
            {
                if (!isLookingUp)
                {
                    isLookingUp = true;
                    walkthroughManager!.StartLookingUp();
                }
            }
            else if (isLookingUp)
            {
                isLookingUp = false;
                walkthroughManager!.StopLookingUp();
            }

            if (rightThumbY < -DEADZONE)
            {
                if (!isLookingDown)
                {
                    isLookingDown = true;
                    walkthroughManager!.StartLookingDown();
                }
            }
            else if (isLookingDown)
            {
                isLookingDown = false;
                walkthroughManager!.StopLookingDown();
            }
        }

        private void ProcessElevation(float leftTrigger, float rightTrigger)
        {
            // Moving Up and Down
            if (rightTrigger > DEADZONE)
            {
                if (!isMovingUp)
                {
                    isMovingUp = true;
                    walkthroughManager!.StartMovingUp();
                }
            }
            else if (isMovingUp)
            {
                isMovingUp = false;
                walkthroughManager!.StopMovingUp();
            }

            if (leftTrigger > DEADZONE)
            {
                if (!isMovingDown)
                {
                    isMovingDown = true;
                    walkthroughManager!.StartMovingDown();
                }
            }
            else if (isMovingDown)
            {
                isMovingDown = false;
                walkthroughManager!.StopMovingDown();
            }
        }

        private void BtnQuit_Click(object? sender, EventArgs? e)
        {
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // Dispose of resources
            controllerTimer?.Stop();
            controllerTimer?.Dispose();

            controllerRetryTimer?.Stop();
            controllerRetryTimer?.Dispose();

            if (swApp != null)
            {
                Marshal.ReleaseComObject(swApp);
            }
        }
    }
}
