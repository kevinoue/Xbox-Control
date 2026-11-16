using SolidWorks.Interop.sldworks;

namespace XboxControl.Addin
{
    /// <summary>Applies view motion and view commands to the active SolidWorks model view.</summary>
    internal sealed class ViewDriver
    {
        private const int DocPart = 1, DocAssembly = 2;

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

        /// <summary>Runs a press action on the active document. Returns a status message, or null.</summary>
        public string? Run(IModelDoc2 doc, IModelView view, ButtonAction action)
        {
            switch (action)
            {
                case ButtonAction.ZoomToFit:
                    doc.ViewZoomtofit2();
                    return "Zoom to fit";
                case ButtonAction.ZoomToSelection:
                    if (((ISelectionMgr)(object)doc.SelectionManager).GetSelectedObjectCount2(-1) == 0)
                        return "Select something first, then press again to zoom to it";
                    doc.ViewZoomToSelection();
                    return "Zoom to selection";
                case ButtonAction.TogglePerspective:
                    if (!Is3D(doc)) return "Not available in drawings";
                    bool on = !view.HasPerspective();
                    if (on) view.AddPerspective(); else view.RemovePerspective();
                    view.GraphicsRedraw(null);
                    return on ? "Perspective on" : "Perspective off";
            }

            var (name, id) = StandardView(action);
            if (id == 0) return null;
            if (!Is3D(doc)) return "Not available in drawings";
            doc.ShowNamedView2(name, id);
            return ControlMapping.Label(action);
        }

        /// <summary>Named view and swStandardViews_e id for a view action; id 0 if not a view.</summary>
        private static (string Name, int Id) StandardView(ButtonAction action) => action switch
        {
            ButtonAction.ViewFront => ("*Front", 1),
            ButtonAction.ViewBack => ("*Back", 2),
            ButtonAction.ViewLeft => ("*Left", 3),
            ButtonAction.ViewRight => ("*Right", 4),
            ButtonAction.ViewTop => ("*Top", 5),
            ButtonAction.ViewBottom => ("*Bottom", 6),
            ButtonAction.ViewIsometric => ("*Isometric", 7),
            _ => ("", 0),
        };

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
