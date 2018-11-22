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
    public class RadialMeasureToolBuilder : IToolBuilder
    {
        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.SingleObject);
        }

        public ITool Build(FScene scene, List<SceneObject> targets)
        {
            return new RadialMeasureTool(scene, targets[0]);
        }
    }


    public class RadialMeasureTool : ITool
    {
        static readonly public string Identifier = "radial_measure";

        FScene scene;

        virtual public string Name
        {
            get { return "RadialMeasure"; }
        }
        virtual public string TypeIdentifier
        {
            get { return Identifier; }
        }

        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors
        {
            get { return behaviors; }
            set { behaviors = value; }
        }

        ParameterSet parameters = new ParameterSet();
        public ParameterSet Parameters { get { return parameters; } }

        public virtual bool AllowSelectionChanges { get { return false; } }



        public float MinRadius = 0.001f;
        public float MaxRadius = 9999.0f;

        float radius = 1.0f;
        virtual public float Radius
        {
            get { return radius; }
            set { Radius = MathUtil.Clamp(value, MinRadius, MaxRadius); }
        }

        // why only MeshSO?
        MeshSO meshTarget;
        ToolIndicatorSet indicators;

        Vector3f measureHitPos;
        //Vector3f measureAxisPos;
        Vector3f displayPosS;
        Vector3f circleCenterS;
        Vector3f maxStart, maxEnd;
        double curDimension;

        virtual public MeshSO Target
        {
            get { return meshTarget; }
        }


        public RadialMeasureTool(FScene scene, SceneObject target)
        {
            this.scene = scene;

            behaviors = new InputBehaviorSet();

            // TODO is this where we should be doing this??
            behaviors.Add(
                new RadialMeasureTool_MouseBehavior(scene.Context) { Priority = 5 });
            //behaviors.Add(
            //    new RevolveTool_SpatialDeviceBehavior(scene.ActiveController) { Priority = 5 });

            // shut off transform gizmo
            scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);

            scene.SelectionChangedEvent += Scene_SelectionChangedEvent;

            this.meshTarget = target as MeshSO;

            var boundsW = meshTarget.GetBoundingBox(CoordSpace.WorldCoords);
            measureHitPos = boundsW.Center;
            update_measurement();


            indicators = new ToolIndicatorSet(this, scene);
            TextLabelIndicator dimensionLabel = new TextLabelIndicator() {
                ScenePositionF = () => { return displayPosS + 0.01f*Vector3f.AxisY; },
                TextHeight = fDimension.Scene(0.05f),  
                DimensionTextF = () => { return " " + (curDimension*100).ToString("F2") + "cm"; }
            };
            indicators.AddIndicator(dimensionLabel);
            CircleIndicator circle = new CircleIndicator() {
                SceneFrameF = () => { return new Frame3f(circleCenterS); },
                RadiusF = () => { return (float)(curDimension * 0.5); },
                ColorF = () => { return new Colorf(Colorf.VideoRed, 0.5f); },
                LineWidth = fDimension.Scene(0.001f)
            };
            indicators.AddIndicator(circle);
            LineIndicator diag = new LineIndicator() {
                SceneStartF = () => { return maxStart; },
                SceneEndF = () => { return maxEnd; },
                ColorF = () => { return Colorf.VideoRed; },
                LineWidth = fDimension.Scene(0.001f)
            };
            indicators.AddIndicator(diag);
            SectionPlaneIndicator section_plane = new SectionPlaneIndicator() {
                SceneFrameF = () => { return new Frame3f(displayPosS); },
                Width = fDimension.Scene(() => { return (float)(curDimension * 1.5); })
            };
            indicators.AddIndicator(section_plane);

        }


        // update snaps
        private void Scene_SelectionChangedEvent(object sender, EventArgs e)
        {
            update_selection(scene.Selected);
        }

        void update_selection(IEnumerable<SceneObject> vSelection)
        {
            List<SceneObject> l = new List<SceneObject>(vSelection);
            SceneObject use = l.Find((x) => { return x is MeshSO; });
            if (use == null)
                return;

            meshTarget = use as MeshSO;
            update_measurement();
        }




        virtual public void PreRender()
        {
            indicators.PreRender();
        }


        virtual public bool HasApply { get { return false; } }
        virtual public bool CanApply { get { return false; } }
        virtual public void Apply() { }


        public virtual void Setup()
        {
        }

        public void Shutdown()
        {
            scene.SelectionChangedEvent -= Scene_SelectionChangedEvent;
            // restore transform gizmo
            scene.Context.TransformManager.PopOverrideGizmoType();

            indicators.Disconnect(true);
        }

        public void UpdateMeasurePoint(Vector3f vPos)
        {
            if ((vPos - measureHitPos).Length > 0.001f) {
                measureHitPos = vPos;
                update_measurement();
            }
        }


        void update_measurement()
        {
            Frame3f localFrame = meshTarget.GetLocalFrame(CoordSpace.ObjectCoords);
            Vector3f localP = localFrame.ToFrameP( scene.ToSceneP(measureHitPos) );

            AxisAlignedBox3f bounds = meshTarget.GetLocalBoundingBox();
            Vector3f dv = localP - bounds.Center;
            dv.y = 0;

            Line3f line = new Line3f(Vector3f.Zero, Vector3f.AxisY);
            Vector3f linePos = line.ClosestPoint(localP);

            Frame3f mFrame = new Frame3f(linePos, Vector3f.AxisY);
            MeshSO.SectionInfo info = meshTarget.MeasureSection(mFrame);
            curDimension = info.maxDiameter;
            Vector3f center = 0.5f * (info.maxDiamPos1 + info.maxDiamPos2);

            //measureAxisPos = scene.ToWorldP(localFrame.FromFrameP(linePos));

            displayPosS = localFrame.FromFrameP(linePos);
            circleCenterS = localFrame.FromFrameP(center);

            maxStart = localFrame.FromFrameP(info.maxDiamPos1);
            maxEnd = localFrame.FromFrameP(info.maxDiamPos2);
        }


    }



    

    class RadialMeasureTool_MouseBehavior : Any2DInputBehavior
    {
        FContext context;

        void upate_hit_pos(RadialMeasureTool tool, Ray3f ray)
        {
            SORayHit soHit;
            if (tool.Target.FindRayIntersection(ray, out soHit))
                tool.UpdateMeasurePoint(soHit.hitPos);
        }

        public RadialMeasureTool_MouseBehavior(FContext s)
        {
            context = s;
        }


        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool == null || !(context.ToolManager.ActiveRightTool is RadialMeasureTool))
                return CaptureRequest.Ignore;
            if ( Pressed(input) ) {
                RadialMeasureTool tool =
                    (context.ToolManager.ActiveRightTool as RadialMeasureTool);
                SORayHit soHit;
                if (tool.Target.FindRayIntersection( WorldRay(input), out soHit))
                    return CaptureRequest.Begin(this);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            RadialMeasureTool tool =
                (context.ToolManager.ActiveRightTool as RadialMeasureTool);
            upate_hit_pos(tool, WorldRay(input) );
            return Capture.Begin(this, CaptureSide.Any);
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            RadialMeasureTool tool =
                (context.ToolManager.ActiveRightTool as RadialMeasureTool);
            if ( Released(input) ) {
                return Capture.End;
            } else {
                upate_hit_pos(tool, WorldRay(input) );
                return Capture.Continue;
            }
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }


        public override bool EnableHover
        {
            get { return false; }
        }
        public override void UpdateHover(InputState input)
        {
        }
        public override void EndHover(InputState input)
        {
        }
    }
}
