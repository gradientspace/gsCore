// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using UnityEngine;
using f3;

namespace gs
{

    // [TODO]
    //   - curve-around-curve
    //   - snaps
    //   - support revolving closed curve

    public class RevolveToolBuilder : IToolBuilder
    {
        public bool IsSupported(ToolTargetType type, List<SceneObject> targets) {
            return (type == ToolTargetType.Scene) ||
                (targets.Find((so) => { return so is PolyCurveSO; }) != null);
        }

        public ITool Build(FScene scene, List<SceneObject> targets) {
            return new RevolveTool(scene, targets);
        }
    }




    public class RevolveTool : ITool
    {
        static readonly public string Identifier = "revolve";

        FScene scene;

        // primitives will not be created with a smaller dimension than this
        public float MinDimension = 0.01f;
        public float MaxDimension = 9999.0f;

        virtual public string Name {
            get { return "Revolve"; }
        }
        virtual public string TypeIdentifier {
            get { return Identifier; }
        }


        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }

        ParameterSet parameters = new ParameterSet();
        public ParameterSet Parameters { get { return parameters; } }

        public virtual bool AllowSelectionChanges { get { return true; } }



        SnapSet Snaps;

        public RevolveTool(FScene scene, List<SceneObject> targets)
        {
            this.scene = scene;

            behaviors = new InputBehaviorSet();

            // TODO is this where we should be doing this??
            behaviors.Add(
                new RevolveTool_MouseBehavior(scene.Context) { Priority = 5 });
            behaviors.Add(
                new RevolveTool_SpatialDeviceBehavior(scene.Context) { Priority = 5 });

            // generate snap target set
            Snaps = SnapSet.CreateStandard(scene);
            Snaps.EnableSnapSegments = false;
            Snaps.EnableSnapPoints = false;

            // shut off transform gizmo
            //scene.ActiveController.TransformManager.SetOverrideGizmoType(TransformManager.NoGizmoType);

            scene.SelectionChangedEvent += Scene_SelectionChangedEvent;
            // initialize active set with input selection
            Scene_SelectionChangedEvent(null, null);


            if (targets != null) {
                if (targets.Count == 2 && targets[0] is PolyCurveSO && targets[1] is PolyCurveSO) {
                    PolyCurveSO curveSO = targets[0] as PolyCurveSO;
                    PolyCurveSO axisSO = targets[1] as PolyCurveSO;
                    set_curve(curveSO, true);
                    set_axis(axisSO, true);

                } else {
                    SceneObject foundCurveSO = targets.Find((so) => { return so is PolyCurveSO; });
                    if (foundCurveSO != null)
                        set_curve(foundCurveSO as PolyCurveSO, false);
                    SceneObject otherSO = targets.Find((so) => { return !(so is PolyCurveSO); });
                    if (otherSO != null)
                        set_axis(otherSO, false);
                }
            }

        }


        // update snaps
        private void Scene_SelectionChangedEvent(object sender, EventArgs e)
        {
            // update active snap set based on new selection
            var selected = scene.Selected;
            Snaps.ClearActive();
            foreach (SceneObject so in selected) {
                if (so is PivotSO || Snaps.IgnoreSet.Contains(so))
                    continue;
                Snaps.AddToActive(so);
            }
            Snaps.AddToActive(scene.Find((x) => x is PivotSO));
        }




        virtual public void PreRender()
        {
            if (Snaps != null)
                Snaps.PreRender(scene.ActiveCamera.GetPosition());
            if (preview != null)
                preview.PreRender();
            if (curvePreview != null)
                curvePreview.PreRender();
        }


        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply { get { return preview != null && preview.IsValid; } }
        virtual public void Apply()
        {
            AddCurrent();
        }


        public virtual void Setup()
        {
        }

        public void Shutdown()
        {
            Cancel();

            scene.SelectionChangedEvent -= Scene_SelectionChangedEvent;
            // restore transform gizmo
            //scene.ActiveController.TransformManager.ClearOverrideGizmoType();

            Snaps.Disconnect(true);

            if (curveSO != null) {
                curveSO.PopOverrideMaterial();
                curveSO.OnTransformModified -= CurveSO_OnTransformModified;
            }
            if (axisSource != null && axisSource is SceneObject)
                (axisSource as SceneObject).OnTransformModified -= SourceSO_OnTransformModified;
        }


        void CreateNewPreview(bool bIsCurve)
        {
            preview = null;
            curvePreview = null;

            if (bIsCurve) {
                curvePreview = new CurveRevolvePreview();
                curvePreview.Create(scene.TransparentNewSOMaterial, scene.RootGameObject, -1);
                sceneCurve = new DCurve3();
                curvePreview.RevolveCurve = sceneCurve;
                axisCurve = new DCurve3();
                curvePreview.AxisCurve = axisCurve;
                curvePreview.Visible = false;
            } else {
                preview = new RevolvePreview();
                preview.Create(scene.TransparentNewSOMaterial, scene.RootGameObject, -1);
                sceneCurve = new DCurve3();
                preview.Curve = sceneCurve;
                preview.Axis = scene.SceneFrame;
                preview.Visible = false;
            }

            // need to do this if we switch between revolve-around-object and revolve-around-curve
            if (curveSO != null)
                update_curve_vertices(curveSO);
        }


        RevolvePreview preview;
        CurveRevolvePreview curvePreview;

        PolyCurveSO curveSO;
        DCurve3 sceneCurve;

        Frame3f axisFrame;
        object axisSource;
        PolyCurveSO axisCurveSO;
        DCurve3 axisCurve;

        void update_preview()
        {
            if (preview != null && axisSource != null && curveSO != null)
                preview.Visible = true;
            else if (curvePreview != null && axisSource != null && axisCurveSO != null)
                curvePreview.Visible = true;
        }

        void set_curve(PolyCurveSO useSO, bool bAxisIsCurve)
        {
            if ( (curvePreview == null && bAxisIsCurve) ||
                 (preview == null && bAxisIsCurve == false) )
                CreateNewPreview(bAxisIsCurve);

            if (curveSO != null) {
                curveSO.PopOverrideMaterial();
                curveSO.OnTransformModified -= CurveSO_OnTransformModified;
                curveSO.OnCurveModified -= CurveSO_OnCurveModified;
            }

            curveSO = useSO;
            update_curve_vertices(curveSO);


            curveSO.PushOverrideMaterial(scene.SelectedMaterial);
            curveSO.OnTransformModified += CurveSO_OnTransformModified;
            curveSO.OnCurveModified += CurveSO_OnCurveModified;

            update_preview();
        }


        public void UpdateCurve(Ray3f ray, SORayHit rayHit)
        {
            if (rayHit.hitSO != null && rayHit.hitSO is PolyCurveSO && rayHit.hitSO != curveSO)
                set_curve(rayHit.hitSO as PolyCurveSO, (axisCurveSO != null) );
        }

        public bool HasCurve
        {
            get { return curveSO != null; }
        }

        private void CurveSO_OnTransformModified(SceneObject so)
        {
            if (so == curveSO) {
                update_curve_vertices(curveSO);
                update_preview();
            }
        }
        private void CurveSO_OnCurveModified(PolyCurveSO so)
        {
            if (so == curveSO) {
                update_curve_vertices(curveSO);
                update_preview();
            }
        }


        private void update_curve_vertices(PolyCurveSO curveSO)
        {
            Vector3d[] verticesS = curveSO.Curve.Vertices.ToArray();
            Frame3f curveFrameS = curveSO.GetLocalFrame(CoordSpace.SceneCoords);
            for (int k = 0; k < verticesS.Length; ++k)
                verticesS[k] = curveFrameS.FromFrameP((Vector3f)verticesS[k]);
            sceneCurve.SetVertices(verticesS);
        }





        void set_axis(SceneObject sourceSO, bool bAxisIsCurve)
        {
            if ( (curvePreview == null && bAxisIsCurve) ||
                 (preview == null && bAxisIsCurve == false) )
                CreateNewPreview(bAxisIsCurve);

            if (axisSource != null && axisSource is SceneObject) {
                (axisSource as SceneObject).OnTransformModified -= SourceSO_OnTransformModified;
                if (axisCurveSO != null)
                    axisCurveSO.OnCurveModified -= SourceSO_OnCurveModified;
                axisSource = null;
                axisCurveSO = null;
            }

            axisFrame = sourceSO.GetLocalFrame(CoordSpace.SceneCoords);
            axisSource = sourceSO;
            axisCurveSO = (bAxisIsCurve) ? (sourceSO as PolyCurveSO) : null;

            if (bAxisIsCurve) {
                update_axis_vertices(axisCurveSO);
                curvePreview.OutputFrame = axisFrame;
                axisCurveSO.OnCurveModified += SourceSO_OnCurveModified;
            } else {
                preview.Axis = axisFrame;
            }

            sourceSO.OnTransformModified += SourceSO_OnTransformModified;


            update_preview();
        }


        public void UpdateAxis(Ray3f ray, SORayHit rayHit)
        {
            // [todo: snaps]
            // [todo: cycle axes]

            if ( rayHit.hitSO != null && rayHit.hitSO is SceneObject ) {
                set_axis(rayHit.hitSO, rayHit.hitSO is PolyCurveSO  );
            }
        }


        private void SourceSO_OnTransformModified(SceneObject so)
        {
            if (so == axisSource) {
                axisFrame = so.GetLocalFrame(CoordSpace.SceneCoords);
                if (preview != null) {
                    preview.Axis = axisFrame;
                } else if ( curvePreview != null ) {
                    update_axis_vertices(axisCurveSO);
                    curvePreview.OutputFrame = axisFrame;
                }
                update_preview();
            }
        }
        private void SourceSO_OnCurveModified(PolyCurveSO so)
        {
            if (so == axisSource) {
                if ( curvePreview != null ) {
                    update_axis_vertices(axisCurveSO);
                }
                update_preview();
            }
        }

        private void update_axis_vertices(PolyCurveSO curveSO)
        {
            Vector3d[] verticesS = axisCurveSO.Curve.Vertices.ToArray();
            Frame3f curveFrameS = axisCurveSO.GetLocalFrame(CoordSpace.SceneCoords);
            for (int k = 0; k < verticesS.Length; ++k)
                verticesS[k] = curveFrameS.FromFrameP((Vector3f)verticesS[k]);
            axisCurve.SetVertices(verticesS);
        }

        



        public void AddCurrent()
        {
            if (preview != null) {
                if (preview.IsValid) {
                    DMeshSO meshSO = preview.BuildSO(scene, scene.DefaultSOMaterial);
                    scene.History.PushChange(
                        new AddSOChange() { scene = scene, so = meshSO });
                    scene.History.PushInteractionCheckpoint();
                }
            } else if ( curvePreview != null ) {
                if ( curvePreview.IsValid ) {
                    DMeshSO meshSO = curvePreview.BuildSO(scene, scene.DefaultSOMaterial);
                    scene.History.PushChange(
                        new AddSOChange() { scene = scene, so = meshSO });
                    scene.History.PushInteractionCheckpoint();
                }
            }

        }

        public void Cancel()
        {
            if (preview != null) {
                preview.Destroy();
                preview = null;
            }
            if (curvePreview != null) {
                curvePreview.Destroy();
                curvePreview = null;
            }
        }


    }







    class RevolveTool_SpatialDeviceBehavior : StandardInputBehavior
    {
        FContext context;

        public RevolveTool_SpatialDeviceBehavior(FContext s)
        {
            context = s;
        }

        override public InputDevice SupportedDevices
        {
            get { return InputDevice.AnySpatialDevice; }
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if ( input.bAButtonPressed ^ input.bXButtonPressed ) {
                CaptureSide eSide = (input.bXButtonPressed) ? CaptureSide.Left : CaptureSide.Right;
                ITool tool = context.ToolManager.GetActiveTool((int)eSide);
                if (tool != null && tool is RevolveTool)
                    return CaptureRequest.Begin(this, eSide);
            }

            if (input.bLeftTriggerPressed ^ input.bRightTriggerPressed) {
                CaptureSide eSide = (input.bLeftTriggerPressed) ? CaptureSide.Left : CaptureSide.Right;
                Ray sideRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
                ITool tool = context.ToolManager.GetActiveTool((int)eSide);
                if (tool != null && tool is RevolveTool) {
                    SORayHit rayHit;
                    if (context.Scene.FindSORayIntersection(sideRay, out rayHit))
                        return CaptureRequest.Begin(this, eSide);
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            if (input.bAButtonPressed ^ input.bXButtonPressed) {
                //RevolveTool tool = context.ToolManager.GetActiveTool((int)eSide) as RevolveTool;
                return Capture.Begin(this, eSide, this);

            } else {
                // assume we are here because of triggers

                Ray sideRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
                //RevolveTool tool = context.ToolManager.GetActiveTool((int)eSide) as RevolveTool;

                SORayHit soHit;
                if (context.Scene.FindSORayIntersection(sideRay, out soHit)) {
                    return Capture.Begin(this, eSide, soHit.hitSO);
                }
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            RevolveTool tool = context.ToolManager.GetActiveTool((int)data.which) as RevolveTool;

            // [RMS] this is a hack for trigger+shoulder grab gesture...really need some way
            //   to interrupt captures!!
            if ( (data.which == CaptureSide.Left && input.bLeftShoulderPressed) ||
                 (data.which == CaptureSide.Right && input.bRightShoulderPressed) ) {
                tool.Cancel();
                return Capture.End;
            }

            if ( data.custom_data == this ) {
                // hack to detect button-press capture set
                bool bReleased = (data.which == CaptureSide.Left) ? input.bXButtonReleased : input.bAButtonReleased;
                if ( bReleased ) { 
                    tool.AddCurrent();
                    return Capture.End;
                }

            } else {
                Ray sideRay = (data.which == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
                SceneObject startHitSO = data.custom_data as SceneObject;

                bool bReleased = (data.which == CaptureSide.Left) ? input.bLeftTriggerReleased : input.bRightTriggerReleased;
                if (bReleased) {
                    SORayHit soHit;
                    if (context.Scene.FindSORayIntersection(sideRay, out soHit)) {
                        if (soHit.hitSO == startHitSO) {
                            if (soHit.hitSO is PolyCurveSO) {
                                if (tool.HasCurve)
                                    tool.UpdateAxis(sideRay, soHit);
                                else
                                    tool.UpdateCurve(sideRay, soHit);
                            } else {
                                tool.UpdateAxis(sideRay, soHit);
                            }
                        }
                    }
                    return Capture.End;
                }
            }

            return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            RevolveTool tool = context.ToolManager.GetActiveTool((int)data.which) as RevolveTool;
            tool.Cancel();
            return Capture.End;
        }


        public override bool EnableHover {
            get { return false; }
        }
        public override void UpdateHover(InputState input) {
        }
        public override void EndHover(InputState input) {
        }
    }








    class RevolveTool_MouseBehavior : StandardInputBehavior
    {
        FContext context;

        public RevolveTool_MouseBehavior(FContext s)
        {
            context = s;
        }

        override public InputDevice SupportedDevices {
            get { return InputDevice.Mouse; }
        }

        override public CaptureRequest WantsCapture(InputState input) {
            if ( context.ToolManager.ActiveRightTool == null || ! (context.ToolManager.ActiveRightTool is RevolveTool) )
                return CaptureRequest.Ignore;
            if (input.bLeftMousePressed) {
                SORayHit rayHit;
                if (context.Scene.FindSORayIntersection(input.vMouseWorldRay, out rayHit))
                    return CaptureRequest.Begin(this);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            //RevolveTool tool = (context.ToolManager.ActiveRightTool as RevolveTool);
            SORayHit soHit;
            if (context.Scene.FindSORayIntersection(input.vMouseWorldRay, out soHit)) {
                return Capture.Begin(this, CaptureSide.Any, soHit.hitSO);
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            RevolveTool tool =
                (context.ToolManager.ActiveRightTool as RevolveTool);

            SceneObject startHitSO = data.custom_data as SceneObject;

            if ( input.bLeftMouseReleased ) {
                SORayHit soHit;
                if (context.Scene.FindSORayIntersection(input.vMouseWorldRay, out soHit)) {
                    if (soHit.hitSO == startHitSO) {
                        if (soHit.hitSO is PolyCurveSO)
                            tool.UpdateCurve(input.vMouseWorldRay, soHit);
                        else
                            tool.UpdateAxis(input.vMouseWorldRay, soHit);
                    }
                }
                return Capture.End;
            } else
                return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            RevolveTool tool =
                (context.ToolManager.ActiveRightTool as RevolveTool);
            tool.Cancel();
            return Capture.End;
        }


        public override bool EnableHover {
            get { return false; }
        }
        public override void UpdateHover(InputState input) {
        }
        public override void EndHover(InputState input)
        {
        }
    }


}
