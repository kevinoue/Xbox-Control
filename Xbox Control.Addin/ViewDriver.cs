using SolidWorks.Interop.sldworks;

namespace XboxControl.Addin
{
    /// <summary>Applies view motion and view commands to the active SolidWorks model view.</summary>
    internal sealed class ViewDriver
    {
        private const int DocPart = 1, DocAssembly = 2;

        // swStandardViews_e
        private const int ViewFront = 1, ViewLeft = 3, ViewRight = 4, ViewTop = 5, ViewIsometric = 7;

        /// <summary>Apply all axes with graphics updates off, then redraw once.</summary>
        public bool BatchRedraw { get; set; } = true;

        public void Apply(IModelDoc2 doc, IModelView view, ViewMotion m)
        {
            bool is3D = Is3D(doc);                       // drawings only pan and zoom
            bool rotate = is3D && m.HasRotation;
            bool roll = is3D && m.HasRoll;
            if (!rotate && !roll && !m.HasPan && !m.HasZoom) return;

            if (BatchRedraw) view.EnableGraphicsUpdate = false;
            try
            {
                if (rotate) view.RotateAboutCenter(m.Pitch, m.Yaw);
                if (roll) view.RollBy(m.Roll);
                if (m.HasPan)
                {
                    double u = UnitsPerViewHeight(view);
                    view.TranslateBy(m.PanX * u, m.PanY * u);
                }
                if (m.HasZoom) view.ZoomByFactor(m.Zoom);
            }
            finally
            {
                if (BatchRedraw) view.EnableGraphicsUpdate = true;
            }
            if (BatchRedraw) view.GraphicsRedraw(null);
        }

        /// <summary>Runs a button command. Returns a status message, or null.</summary>
        public string? Run(IModelDoc2 doc, IModelView view, ViewCommand command)
        {
            bool is3D = Is3D(doc);
            switch (command)
            {
                case ViewCommand.ZoomToFit:
                    doc.ViewZoomtofit2();
                    return "Zoom to fit";
                case ViewCommand.ZoomToSelection:
                    if (((ISelectionMgr)(object)doc.SelectionManager).GetSelectedObjectCount2(-1) == 0)
                        return "Select something first, then press B to zoom to it";
                    doc.ViewZoomToSelection();
                    return "Zoom to selection";
                case ViewCommand.Isometric when is3D: return NamedView(doc, "*Isometric", ViewIsometric, "Isometric view");
                case ViewCommand.Front when is3D: return NamedView(doc, "*Front", ViewFront, "Front view");
                case ViewCommand.Top when is3D: return NamedView(doc, "*Top", ViewTop, "Top view");
                case ViewCommand.Left when is3D: return NamedView(doc, "*Left", ViewLeft, "Left view");
                case ViewCommand.Right when is3D: return NamedView(doc, "*Right", ViewRight, "Right view");
                case ViewCommand.TogglePerspective when is3D:
                    bool on = !view.HasPerspective();
                    if (on) view.AddPerspective(); else view.RemovePerspective();
                    view.GraphicsRedraw(null);
                    return on ? "Perspective on" : "Perspective off";
                case ViewCommand.Isometric:
                case ViewCommand.Front:
                case ViewCommand.Top:
                case ViewCommand.Left:
                case ViewCommand.Right:
                case ViewCommand.TogglePerspective:
                    return "Not available in drawings";
                default:
                    return null;
            }
        }

        private static string NamedView(IModelDoc2 doc, string name, int id, string message)
        {
            doc.ShowNamedView2(name, id);
            return message;
        }

        private static bool Is3D(IModelDoc2 doc)
        {
            int type = doc.GetType();
            return type == DocPart || type == DocAssembly;
        }

        /// <summary>
        /// TranslateBy moves a fixed on-screen distance whatever the zoom: one unit is
        /// (Transform scale / Scale2) pixels. Measured on SW 2023 SP5.
        /// </summary>
        private static double UnitsPerViewHeight(IModelView view)
        {
            var xform = (double[])(object)view.Transform.ArrayData;
            double pxPerUnit = xform[12] / view.Scale2;
            return pxPerUnit > 0 && view.FrameHeight > 0 ? view.FrameHeight / pxPerUnit : 0.3;
        }
    }

    /// <summary>Saved view, restored in the order SolidWorks needs.</summary>
    internal sealed class ViewState
    {
        private readonly double[] orientation;
        private readonly double[] translation;
        private readonly double scale;

        private ViewState(double[] orientation, double[] translation, double scale)
        {
            this.orientation = orientation;
            this.translation = translation;
            this.scale = scale;
        }

        public static ViewState Capture(IModelView v) => new(
            (double[])(object)v.Orientation3.ArrayData, (double[])(object)v.Translation3.ArrayData, v.Scale2);

        public void Restore(IModelView v, IMathUtility math)
        {
            // Setting Scale2 (even to the same value) multiplies Translation3 by the scale,
            // so the translation must be set last (SW 2023 SP5).
            v.Orientation3 = (MathTransform)(object)math.CreateTransform(orientation);
            v.Scale2 = scale;
            v.Translation3 = (MathVector)(object)math.CreateVector(translation);
            v.GraphicsRedraw(null);
        }
    }
}
