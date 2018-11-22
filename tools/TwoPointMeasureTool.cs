// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;

namespace gs
{

    public class TwoPointMeasureToolBuilder : IToolBuilder
    {
        public float SnapThresholdAngle = SceneGraphConfig.DefaultSnapDistVisualDegrees;
        public bool ShowTextLabel = true;
        public DimensionType TextHeightDimensionType = DimensionType.VisualAngle;
        public float TextHeightDimension = 5.0f;
        public bool AllowSelectionChanges = true;

        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return true;
        }

        public ITool Build(FScene scene, List<SceneObject> targets)
        {
            return new TwoPointMeasureTool(scene, targets) {
                SnapThresholdAngle = this.SnapThresholdAngle,
                ShowTextLabel = this.ShowTextLabel,
                TextHeightDimensionType = this.TextHeightDimensionType,
                TextHeightDimension = this.TextHeightDimension,
                AllowSelectionChanges = this.AllowSelectionChanges
            };
        }
    }




    public class TwoPointMeasureTool : ITool
    {
        static readonly public string Identifier = "two_point_measure";

        FScene scene;
        List<SceneObject> SpecificTargets;


        virtual public string Name
        {
            get { return "TwoPointMeasure"; }
        }
        virtual public string TypeIdentifier
        {
            get { return Identifier; }
        }

        public DimensionType TextHeightDimensionType = DimensionType.VisualAngle;
        public float TextHeightDimension = 5.0f;


        bool show_text_label;
        public bool ShowTextLabel {
            get { return show_text_label; }
            set { show_text_label = value; }
        }


        float snapThresholdAngle;
        public float SnapThresholdAngle
        {
            get { return snapThresholdAngle; }
            set { snapThresholdAngle = MathUtil.Clamp(value, 0, 90); }
        }


        public bool Initialized {
            get { return point_initialized[0] && point_initialized[1]; }
        }

        public double CurrentDistance {
            get {
                if (point_initialized[0] && point_initialized[1])
                    return snappedPointsS[0].Distance(snappedPointsS[1]);
                return 0;
            }
        }

        public Vector3d CenterPosScene {
            get { return Initialized ? centerPos.ScenePoint : Vector3d.Zero; }
        }
        public Vector3d CenterPosWorld {
            get { return Initialized ? centerPos.WorldPoint : Vector3d.Zero; }
        }

        public Vector3d StartPosScene {
            get { return snappedPointsS[0]; }
        }
        public Vector3d EndPosScene {
            get { return snappedPointsS[1]; }
        }



        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors
        {
            get { return behaviors; }
            set { behaviors = value; }
        }

        ParameterSet parameters = new ParameterSet();
        public ParameterSet Parameters { get { return parameters; } }

        bool allow_selection_changes;
        public virtual bool AllowSelectionChanges {
            get { return allow_selection_changes; }
            set { allow_selection_changes = value; }
        }



        Vector3d[] setPointsS;
        Vector3d[] snappedPointsS;
        bool[] point_initialized;

        ToolIndicatorSet indicators;
        TextLabelIndicator dimension;

        fPosition centerPos;

        public TwoPointMeasureTool(FScene scene, List<SceneObject> targets)
        {
            this.scene = scene;
            SpecificTargets = (targets == null) ? null : new List<SceneObject>(targets);

            setPointsS = new Vector3d[2];
            setPointsS[0] = setPointsS[1] = Vector3d.Zero;
            snappedPointsS = new Vector3d[2];
            snappedPointsS[0] = snappedPointsS[1] = Vector3d.Zero;
            point_initialized = new bool[2] { false, false };

            behaviors = new InputBehaviorSet();
            if (FPlatform.IsUsingVR()) {
                behaviors.Add( new TwoPointMeasureTool_SpatialDeviceBehavior(scene.Context, this) { Priority = 5 });
            }
            if (FPlatform.IsTouchDevice()) {
                behaviors.Add( new TwoPointMeasureTool_TouchBehavior(scene.Context, this) { Priority = 5 });
            }
            behaviors.Add(
                new TwoPointMeasureTool_MouseBehavior(scene.Context, this) { Priority = 5 });

            // shut off transform gizmo
            scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);
            scene.SelectionChangedEvent += Scene_SelectionChangedEvent;
        }



        public virtual void Setup()
        {
            Func<Vector3f> centerPosF = () => { return (Vector3f)(snappedPointsS[0] + snappedPointsS[1]) * 0.5f; };
            centerPos = fPosition.Scene(centerPosF);

            fDimension textHeight = null;
            if (TextHeightDimensionType == DimensionType.VisualAngle) {
                textHeight = fDimension.VisualAngle(centerPos, TextHeightDimension);
            } else if (TextHeightDimensionType == DimensionType.SceneUnits) {
                textHeight = fDimension.Scene(() => { return TextHeightDimension; });
            } else {
                textHeight = fDimension.World(() => { return TextHeightDimension; });
            }

            Func<Vector3f> labelPosF = () => {
                Vector3f p = centerPos.ScenePoint;
                float h = textHeight.SceneValuef;
                p += 0.5f * h * scene.ToSceneN(Vector3f.AxisY);
                return p;
            };

            indicators = new ToolIndicatorSet(this, scene);
            LineIndicator diag = new LineIndicator() {
                VisibleF = () => { return Initialized; },
                SceneStartF = () => { return (Vector3f)snappedPointsS[0]; },
                SceneEndF = () => { return (Vector3f)snappedPointsS[1]; },
                ColorF = () => { return Colorf.VideoRed; },
                LineWidth = fDimension.VisualAngle(centerPos, 0.5f),
            };
            indicators.AddIndicator(diag);

            dimension = new TextLabelIndicator() {
                VisibleF = () => { return Initialized && ShowTextLabel; },
                ScenePositionF = labelPosF,
                TextHeight = textHeight,
                DimensionTextF = () => { return string.Format("{0:F4}", snappedPointsS[0].Distance(snappedPointsS[1])); }
            };
            indicators.AddIndicator(dimension);
        }



        virtual public void PreRender()
        {
            indicators.PreRender();
        }


        public void Shutdown()
        {
            scene.SelectionChangedEvent -= Scene_SelectionChangedEvent;
            // restore transform gizmo
            scene.Context.TransformManager.PopOverrideGizmoType();

            indicators.Disconnect(true);
        }


        public void UpdateMeasurePoint(Vector3d point, CoordSpace eSpace, int nPointNum, bool isFixedPoint = true)
        {
            if (eSpace == CoordSpace.ObjectCoords)
                throw new NotSupportedException("TwoPointMeasureTool.UpdateMeasurePoint");

            Vector3d pointS = (eSpace == CoordSpace.SceneCoords) ? point : scene.ToSceneP(point);

            int i = MathUtil.Clamp(nPointNum, 0, 1);
            setPointsS[i] = pointS;
            snappedPointsS[i] = setPointsS[i];
            point_initialized[i] = true;

            if (isFixedPoint)
                return;

            // snap
            List<SceneObject> targets = (SpecificTargets != null) ? SpecificTargets : new List<SceneObject>(scene.SceneObjects);

            float fAvgSnapDist = 0;
            int avgcount = 0;
            foreach (var so in targets) {
                fAvgSnapDist += VRUtil.GetVRRadiusForVisualAngle(so.GetLocalFrame(CoordSpace.WorldCoords).Origin,
                    scene.ActiveCamera.GetPosition(), snapThresholdAngle);
                avgcount++;
            }
            if (avgcount == 0)
                return;
            fAvgSnapDist /= avgcount;

            SORayHit nearestW;
            Vector3d pointW = (eSpace == CoordSpace.WorldCoords) ? point : scene.ToWorldP(point);
            if (SceneUtil.FindNearestPoint(targets, pointW, fAvgSnapDist, out nearestW, CoordSpace.WorldCoords)) {
                snappedPointsS[i] = scene.ToSceneP(nearestW.hitPos);
            }
        }



        // update snaps
        private void Scene_SelectionChangedEvent(object sender, EventArgs e)
        {
            SpecificTargets = (scene.Selected.Count > 0) ? new List<SceneObject>(scene.Selected) : null;
            if (point_initialized[0])
                UpdateMeasurePoint(setPointsS[0], CoordSpace.SceneCoords, 0, false);
            if (point_initialized[1])
                UpdateMeasurePoint(setPointsS[1], CoordSpace.SceneCoords, 1, false);
        }

        


        virtual public bool HasApply { get { return false; } }
        virtual public bool CanApply { get { return false; } }
        virtual public void Apply() { }


    }





    class TwoPointMeasureTool_SpatialDeviceBehavior : StandardInputBehavior
    {
        FContext context;
        TwoPointMeasureTool tool;

        public TwoPointMeasureTool_SpatialDeviceBehavior(FContext s, TwoPointMeasureTool tool)
        {
            context = s;
            this.tool = tool;
        }

        override public InputDevice SupportedDevices
        {
            get { return InputDevice.AnySpatialDevice; }
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (input.bLeftTriggerPressed ^ input.bRightTriggerPressed) {
                CaptureSide eSide = (input.bLeftTriggerPressed) ? CaptureSide.Left : CaptureSide.Right;
                //ITool tool = context.ToolManager.GetActiveTool((int)eSide);
                //if (tool != null && tool is TwoPointMeasureTool) {
                    return CaptureRequest.Begin(this, eSide);
                //}
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            Ray3f sideRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            Frame3f sideHandF = (eSide == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            sideHandF.Origin += SceneGraphConfig.VRHandTipOffset * sideHandF.Z;

            bool bTouchingStick =
                (eSide == CaptureSide.Left) ? input.bLeftStickTouching : input.bRightStickTouching;

            AnyRayHit rayHit;
            if (bTouchingStick) { 
                if (context.Scene.FindSceneRayIntersection(sideRay, out rayHit, false)) 
                    tool.UpdateMeasurePoint(rayHit.hitPos, CoordSpace.WorldCoords, (eSide == CaptureSide.Left) ? 0 : 1, true);
                return Capture.Begin(this, eSide);
            } else {
                tool.UpdateMeasurePoint(sideHandF.Origin, CoordSpace.WorldCoords, (eSide == CaptureSide.Left) ? 0 : 1, false);
                return Capture.Begin(this, eSide);
            }
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            // [RMS] this is a hack for trigger+shoulder grab gesture...really need some way
            //   to interrupt captures!!
            if ((data.which == CaptureSide.Left && input.bLeftShoulderPressed) ||
                 (data.which == CaptureSide.Right && input.bRightShoulderPressed)) {
                return Capture.End;
            }

            bool bReleased = (data.which == CaptureSide.Left) ? input.bLeftTriggerReleased : input.bRightTriggerReleased;
            if (bReleased) {
                return Capture.End;
            }

            Ray3f sideRay = (data.which == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            Frame3f sideHandF = (data.which == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            sideHandF.Origin += SceneGraphConfig.VRHandTipOffset * sideHandF.Z;
            bool bTouchingStick =
                (data.which == CaptureSide.Left) ? input.bLeftStickTouching : input.bRightStickTouching;


            AnyRayHit rayHit;
            if (bTouchingStick == false ) {
                if ( context.Scene.FindSceneRayIntersection(sideRay, out rayHit, false)) 
                    tool.UpdateMeasurePoint(rayHit.hitPos, CoordSpace.WorldCoords, (data.which == CaptureSide.Left) ? 0 : 1, true);
            } else {
                
                tool.UpdateMeasurePoint(sideHandF.Origin, CoordSpace.WorldCoords, (data.which == CaptureSide.Left) ? 0 : 1, false);
            }
            return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }
    }










    class TwoPointMeasureTool_MouseBehavior : StandardInputBehavior
    {
        FContext context;
        TwoPointMeasureTool tool;

        class capture_data
        {
            public bool is_right = false;
            public bool is_secondary_left = false;
        }

        public TwoPointMeasureTool_MouseBehavior(FContext s, TwoPointMeasureTool tool)
        {
            context = s;
            this.tool = tool;
        }

        override public InputDevice SupportedDevices {
            get { return InputDevice.Mouse; }
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (input.bLeftMouseDown ^ input.bRightMouseDown) {
                return CaptureRequest.Begin(this, CaptureSide.Any);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            capture_data d = new capture_data() {
                is_right = input.bRightMouseDown,
                is_secondary_left = (input.bRightMouseDown == false) && input.bLeftMouseDown && input.bShiftKeyDown
            };
            return Capture.Begin(this, eSide, d );
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            capture_data d = data.custom_data as capture_data;
            if (d.is_right) {
                if (input.bRightMouseReleased)
                    return Capture.End;
            } else {
                if (input.bLeftMouseReleased)
                    return Capture.End;
            }

            int point = (d.is_right || d.is_secondary_left) ? 1 : 0;

            Ray3f ray = input.vMouseWorldRay;
            AnyRayHit rayHit;
            if (context.Scene.FindSceneRayIntersection(ray, out rayHit, false))
                tool.UpdateMeasurePoint(rayHit.hitPos, CoordSpace.WorldCoords, point, true);
            
            return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }
    }






    class TwoPointMeasureTool_TouchBehavior : StandardInputBehavior
    {
        FContext context;
        TwoPointMeasureTool tool;

        public TwoPointMeasureTool_TouchBehavior(FContext s, TwoPointMeasureTool tool)
        {
            context = s;
            this.tool = tool;
        }

        override public InputDevice SupportedDevices {
            get { return InputDevice.TabletFingers; }
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if ( input.nTouchCount > 0) {
                return CaptureRequest.Begin(this, CaptureSide.Any);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            return Capture.Begin(this, eSide);
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            if (input.nTouchCount == 0)
                return Capture.End;

            Ray3f ray = input.vTouchWorldRay;
            AnyRayHit rayHit;
            if (context.Scene.FindSceneRayIntersection(ray, out rayHit, false))
                tool.UpdateMeasurePoint(rayHit.hitPos, CoordSpace.WorldCoords, 0, true);

            if ( input.nTouchCount > 1 ) {
                ray = input.vSecondTouchWorldRay;
                if (context.Scene.FindSceneRayIntersection(ray, out rayHit, false))
                    tool.UpdateMeasurePoint(rayHit.hitPos, CoordSpace.WorldCoords, 1, true);

            }

            return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }
    }





}
