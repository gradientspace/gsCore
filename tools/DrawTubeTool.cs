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
    public class DrawTubeToolBuilder : IToolBuilder
    {
        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.Scene);
        }

        public ITool Build(FScene scene, List<SceneObject> targets)
        {
            return new DrawTubeTool(scene);
        }
    }




    public class DrawTubeTool : ITool
    {
        static readonly public string Identifier = "draw_tube";

        FScene scene;

        // primitives will not be created with a smaller dimension than this
        public float MinRadius = 0.1f;
        public float MaxRadius = 9999.0f;


        float radius = 0.25f;
        public float Radius
        {
            get { return radius; }
            set { radius = MathUtil.Clamp(value, MinRadius, MaxRadius); update_polygon(); }
        }

        int nSlices = 8;

        virtual public string Name {
            get { return "DrawTube"; }
        }
        virtual public string TypeIdentifier {
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

        public virtual bool AllowSelectionChanges { get { return true; } }


        Frame3f lastPreviewPos;
        ToolIndicatorSet indicators;
        fMaterial brushSphereMat;


        public DrawTubeTool(FScene scene)
        {
            this.scene = scene;

            behaviors = new InputBehaviorSet();

            // TODO is this where we should be doing this??
            behaviors.Add(
                new DrawTubeTool_MouseBehavior(scene.Context) { Priority = 5 });
            behaviors.Add(
                new DrawTubeTool_SpatialDeviceBehavior(this, scene.Context) { Priority = 5 });

            // restore radius
            if (SavedSettings.Restore("DrawTubeTool_radius") != null)
                radius = (float)SavedSettings.Restore("DrawTubeTool_radius");


            indicators = new ToolIndicatorSet(this, scene);
            BrushCursorSphere brushSphere = new BrushCursorSphere() {
                PositionF = () => { return lastPreviewPos.Origin; },
                Radius = fDimension.Scene( () => { return Radius; } )
            };
            brushSphereMat = MaterialUtil.CreateTransparentMaterialF(Colorf.CornflowerBlue, 0.2f);
            brushSphere.material = brushSphereMat;
            indicators.AddIndicator(brushSphere);
        }

        virtual public void PreRender()
        {
            if (preview != null)
                preview.PreRender();

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
            indicators.Disconnect(true);
        }


        void update_polygon() {
            if (preview != null)
                preview.Polygon = Polygon2d.MakeCircle(Radius, nSlices);
        }

        void CreateNewTube()
        {
            preview = new MeshTubePreview() {
                Polygon = Polygon2d.MakeCircle(Radius, nSlices)
            };
            preview.Create(scene.NewSOMaterial, scene.RootGameObject);

            smoother = new InPlaceIterativeCurveSmooth() {
                Curve = preview.Curve,
                Alpha = 0.2f
            };
        }


        MeshTubePreview preview;
        InPlaceIterativeCurveSmooth smoother;
        Frame3f planarF;



        // wow this is tricky. Want to do smoothed appending but as-you-draw, not at the end.
        // The main problem is the last vertex. We can wait until next vertex is far enough
        // (fDistThresh) but then drawing is very "poppy". So, we append the last point as
        // a "temp" vertex, which we then re-use when we actually append. 
        //
        // However, just allowing any temp vertex is not idea, as with spatial control your
        // hand will wiggle, and then the tube end will do ugly things. So, if the temp
        // vertex isn't lying close to the tangent at the actual curve-end, we project
        // it onto the tangent, and use that. Unless it has negative T, then we ignore it. whew!
        //
        Vector3f last_append;
        bool appended_last_update;
        bool have_temp_append;
        public void smooth_append(DCurve3 curve, Vector3f newPos, float fDistThresh)
        {
            // empty curve, always append
            if ( curve.VertexCount == 0 ) {
                curve.AppendVertex(newPos);
                last_append = newPos;
                appended_last_update = true;
                have_temp_append = false;
                return;
            } else if ( curve.VertexCount == 1 ) {
                curve.AppendVertex(newPos);
                last_append = newPos;
                appended_last_update = true;
                have_temp_append = true;
                return;
            } else if ( curve.VertexCount <= 3 ) {
                curve[curve.VertexCount - 1] = newPos;
            }

            double d = (newPos - last_append).Length;
            if (d < fDistThresh) {
                // have not gone far enough for a real new vertex!

                Vector3f usePos = new Vector3f(newPos);
                bool bValid = false;

                // do we have enough vertices to do a good job?
                if ( curve.VertexCount > 3) {
                    int nLast = (have_temp_append) ? curve.VertexCount - 2 : curve.VertexCount - 1;
                    Vector3d tan = curve.Tangent(nLast);
                    double fDot = tan.Dot( (usePos - curve[nLast]).Normalized );
                    if (fDot > 0.9f) {      // cos(25) ~= 0.9f
                        // new vtx is aligned with tangent of last "real" vertex
                        bValid = true;
                    } else { 
                        // not aligned, try projection onto tangent
                        Line3d l = new Line3d(curve[nLast], tan);
                        double t = l.Project(newPos);
                        if (t > 0) {
                            // projection of new vtx is 'ahead' so we can use it
                            usePos = (Vector3f)l.PointAt(t);
                            bValid = true;
                        }
                    } 
                }

                if (bValid) {
                    if (appended_last_update) {
                        curve.AppendVertex(usePos);
                        have_temp_append = true;
                    } else if (have_temp_append) {
                        curve[curve.VertexCount - 1] = usePos;
                    }
                }

                appended_last_update = false;

            } else {
                // ok we drew far enough, add this position

                if (have_temp_append) {
                    // re-use temp vertex
                    curve[curve.VertexCount - 1] = newPos;
                    have_temp_append = false;
                } else {
                    curve.AppendVertex(newPos);
                }
                last_append = newPos;
                appended_last_update = true;

                // do smoothing pass
                smoother.End = curve.VertexCount - 1;
                smoother.Start = MathUtil.Clamp(smoother.End - 5, 0, smoother.End);
                smoother.UpdateDeformation(2);
            }
        }


        float dist_thresh(float fTubeRadius, float fSceneScale)
        {
            // when you zoom in you can draw smoother curves, but not too much smoother...
            return MathUtil.Lerp(fTubeRadius, fTubeRadius * fSceneScale, 0.35f);
            //return fTubeRadius;
        }


        public void BeginDraw_Ray(AnyRayHit rayHit)
        {
            CreateNewTube();

            planarF = UnityUtil.GetGameObjectFrame(scene.ActiveCamera.GameObject(), CoordSpace.WorldCoords);
            planarF.Origin = rayHit.hitPos;

            float fScale = scene.GetSceneScale();
            smooth_append(preview.Curve,
                scene.ToSceneP(rayHit.hitPos), dist_thresh(Radius,fScale) );
        }


        public void UpdateDraw_Ray(Ray3f ray)
        {
            float fScale = scene.GetSceneScale();
            Vector3f vHit = planarF.RayPlaneIntersection(ray.Origin, ray.Direction, 2);
            smooth_append(preview.Curve, scene.ToSceneP(vHit), dist_thresh(Radius, fScale));
        }

        Frame3f startHandF;
        Vector3f handHitVec;

        public void BeginDraw_Spatial(AnyRayHit rayHit, Frame3f handFrame)
        {
            BeginDraw_Ray(rayHit);
            startHandF = handFrame;
            handHitVec = startHandF.ToFrameP(rayHit.hitPos);
        }


        public void BeginDraw_Spatial_Direct(Frame3f handFrame)
        {
            CreateNewTube();
            float fScale = scene.GetSceneScale();
            Vector3f tipPos = handFrame.Origin + SceneGraphConfig.HandTipOffset * handFrame.Z;
            smooth_append(preview.Curve, scene.ToSceneP(tipPos), dist_thresh(Radius, fScale));
            startHandF = handFrame;
            handHitVec = startHandF.ToFrameP(tipPos);
        }


        public void UpdateDraw_Spatial(Ray3f ray, Frame3f handFrame)
        {
            float fScale = scene.GetSceneScale();
            Vector3f newHitPos = handFrame.FromFrameP(handHitVec);
            smooth_append(preview.Curve, scene.ToSceneP(newHitPos), dist_thresh(Radius, fScale));
        }


        public void UpdateSizePreview(Frame3f vPos)
        {
            lastPreviewPos = vPos;
        }


        public void EndDraw()
        {
            if (preview.Curve.ArcLength > 2*Radius) { 
                // store undo/redo record for new primitive
                PolyTubeSO tubeSO = preview.BuildSO(scene.DefaultSOMaterial, 1.0f);
                scene.History.PushChange(
                    new AddSOChange() { scene = scene, so = tubeSO, bKeepWorldPosition = false });
                scene.History.PushInteractionCheckpoint();
            }

            preview.Destroy();
            preview = null;

            SavedSettings.Save("DrawTubeTool_radius", radius);
        }

        public void CancelDraw()
        {
            if (preview != null) {
                preview.Destroy();
                preview = null;
            }
        }


    }







    class DrawTubeTool_SpatialDeviceBehavior : StandardInputBehavior
    {
        FContext context;
        DrawTubeTool tool;

        public DrawTubeTool_SpatialDeviceBehavior(DrawTubeTool tool, FContext s)
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
                ITool tool = context.ToolManager.GetActiveTool((int)eSide);
                if (tool != null && tool is DrawTubeTool) {
                    return CaptureRequest.Begin(this, eSide);
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            Ray3f sideRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            Frame3f sideHandF = (eSide == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            DrawTubeTool tool = context.ToolManager.GetActiveTool((int)eSide) as DrawTubeTool;

            bool bTouchingStick =
                (eSide == CaptureSide.Left) ? input.bLeftStickTouching : input.bRightStickTouching;

            AnyRayHit rayHit;
            if (bTouchingStick == false && context.Scene.FindSceneRayIntersection(sideRay, out rayHit)) {
                tool.BeginDraw_Spatial(rayHit, sideHandF);
                return Capture.Begin(this, eSide);
            } else {
                tool.BeginDraw_Spatial_Direct(sideHandF);
                return Capture.Begin(this, eSide);
            }
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            DrawTubeTool tool = context.ToolManager.GetActiveTool((int)data.which) as DrawTubeTool;

            // [RMS] this is a hack for trigger+shoulder grab gesture...really need some way
            //   to interrupt captures!!
            if ((data.which == CaptureSide.Left && input.bLeftShoulderPressed) ||
                 (data.which == CaptureSide.Right && input.bRightShoulderPressed)) {
                tool.CancelDraw();
                return Capture.End;
            }

            Vector2f vStick = (data.which == CaptureSide.Left) ? input.vLeftStickDelta2D : input.vRightStickDelta2D;
            if ( vStick[1] != 0 ) {
                tool.Radius = tool.Radius + vStick[1] * 0.01f;
            }

            Ray3f sideRay = (data.which == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            Frame3f sideHandF = (data.which == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            tool.UpdateDraw_Spatial(sideRay, sideHandF);

            bool bReleased = (data.which == CaptureSide.Left) ? input.bLeftTriggerReleased : input.bRightTriggerReleased;
            if (bReleased) {
                tool.EndDraw();
                return Capture.End;
            } else
                return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            DrawTubeTool tool = context.ToolManager.GetActiveTool((int)data.which) as DrawTubeTool;
            tool.CancelDraw();
            return Capture.End;
        }




        public override bool EnableHover
        {
            get { return true; }
        }
        public override void UpdateHover(InputState input)
        {
            ToolSide eSide = context.ToolManager.FindSide(tool);
            Frame3f sideHandF = (eSide == ToolSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            sideHandF.Origin += SceneGraphConfig.VRHandTipOffset * sideHandF.Z;
            //update_last_hit(tool, input.vMouseWorldRay);
            tool.UpdateSizePreview(sideHandF);

            Vector2f vStick = (eSide == ToolSide.Left) ? input.vLeftStickDelta2D : input.vRightStickDelta2D;
            if (vStick[1] != 0) {
                tool.Radius = tool.Radius + vStick[1] * 0.01f;
            }
        }
        public override void EndHover(InputState input)
        {
        }

    }








    class DrawTubeTool_MouseBehavior : StandardInputBehavior
    {
        FContext context;

        public DrawTubeTool_MouseBehavior(FContext s)
        {
            context = s;
        }

        override public InputDevice SupportedDevices
        {
            get { return InputDevice.Mouse; }
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool == null || !(context.ToolManager.ActiveRightTool is DrawTubeTool))
                return CaptureRequest.Ignore;
            if (input.bLeftMousePressed) {
                AnyRayHit rayHit;
                if (context.Scene.FindSceneRayIntersection(input.vMouseWorldRay, out rayHit))
                    return CaptureRequest.Begin(this);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            DrawTubeTool tool =
                (context.ToolManager.ActiveRightTool as DrawTubeTool);
            AnyRayHit rayHit;
            if (context.Scene.FindSceneRayIntersection(input.vMouseWorldRay, out rayHit)) {
                tool.BeginDraw_Ray(rayHit);
                return Capture.Begin(this);
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            DrawTubeTool tool =
                (context.ToolManager.ActiveRightTool as DrawTubeTool);

            tool.UpdateDraw_Ray(input.vMouseWorldRay);

            if (input.bLeftMouseReleased) {
                tool.EndDraw();
                return Capture.End;
            } else
                return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            DrawTubeTool tool =
                (context.ToolManager.ActiveRightTool as DrawTubeTool);
            tool.CancelDraw();
            return Capture.End;
        }
    }
}
