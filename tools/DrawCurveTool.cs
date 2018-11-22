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
    public class DrawCurveToolBuilder : IToolBuilder
    {
        public float DefaultWidth = 0.25f;

        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.Scene);
        }

        public ITool Build(FScene scene, List<SceneObject> targets)
        {
            DrawCurveTool tool = new DrawCurveTool(scene);
            tool.Width = DefaultWidth;
            tool.MinWidth = Math.Min(tool.MinWidth, DefaultWidth * 0.1f);
            return tool;
        }
    }




    public class DrawCurveTool : ITool
    {
        static readonly public string Identifier = "draw_curve";

        FScene scene;

        virtual public string Name
        {
            get { return "DrawCurve"; }
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

        public virtual bool AllowSelectionChanges { get { return true; } }



        public float MinWidth = 0.001f;
        public float MaxWidth = 9999.0f;

        float width = 0.25f;
        virtual public float Width
        {
            get { return width; }
            set { width = MathUtil.Clamp(value, MinWidth, MaxWidth); }
        }




        public DrawCurveTool(FScene scene)
        {
            this.scene = scene;

            behaviors = new InputBehaviorSet();

            // TODO is this where we should be doing this??
            behaviors.Add(
                new DrawCurveTool_2DBehavior(scene.Context) { Priority = 5 });
            behaviors.Add(
                new DrawCurveTool_SpatialDeviceBehavior(scene.Context) { Priority = 5 });

            // restore radius
            if (SavedSettings.Restore("DrawCurveTool_width") != null)
                width = (float)SavedSettings.Restore("DrawCurveTool_width");
        }

        virtual public void PreRender()
        {
            if (preview != null)
                preview.PreRender(scene);
        }

        virtual public bool HasApply { get { return false; } }
        virtual public bool CanApply { get { return false; } }
        virtual public void Apply() { }


        public virtual void Setup()
        {
        }
        public virtual void Shutdown()
        {
        }


        void CreateNewCurve()
        {
            preview = new CurvePreview();
            preview.Create(scene.NewSOMaterial, scene.RootGameObject);

            smoother = new InPlaceIterativeCurveSmooth() {
                Curve = preview.Curve,
                Alpha = 0.2f
            };
        }


        CurvePreview preview;
        InPlaceIterativeCurveSmooth smoother;
        Frame3f planarF;



        // wow this is tricky. Want to do smoothed appending but as-you-draw, not at the end.
        // The main problem is the last vertex. We can wait until next vertex is far enough
        // (fDistThresh) but then drawing is very "poppy". So, we append the last point as
        // a "temp" vertex, which we then re-use when we actually append. 
        //
        // However, just allowing any temp vertex is not idea, as with spatial control your
        // hand will wiggle, and then the Curve end will do ugly things. So, if the temp
        // vertex isn't lying close to the tangent at the actual curve-end, we project
        // it onto the tangent, and use that. Unless it has negative T, then we ignore it. whew!
        //
        Vector3f last_append;
        bool appended_last_update;
        bool have_temp_append;
        public void smooth_append(CurvePreview preview, Vector3f newPos, float fDistThresh)
        {
            // empty curve, always append
            if (preview.VertexCount == 0) {
                preview.AppendVertex(newPos);
                last_append = newPos;
                appended_last_update = true;
                have_temp_append = false;
                return;
            }

            double d = (newPos - last_append).Length;
            if (d < fDistThresh) {
                // have not gone far enough for a real new vertex!

                Vector3f usePos = new Vector3f(newPos);
                bool bValid = false;

                // do we have enough vertices to do a good job?
                if (preview.VertexCount > 3) {
                    int nLast = (have_temp_append) ? preview.VertexCount - 2 : preview.VertexCount - 1;
                    Vector3d tan = preview.Tangent(nLast);
                    double fDot = tan.Dot((usePos - preview[nLast]).Normalized);
                    if (fDot > 0.9f) {      // cos(25) ~= 0.9f
                        // new vtx is aligned with tangent of last "real" vertex
                        bValid = true;
                    } else {
                        // not aligned, try projection onto tangent
                        Line3d l = new Line3d(preview[nLast], tan);
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
                        preview.AppendVertex(usePos);
                        have_temp_append = true;
                    } else if (have_temp_append) {
                        preview[preview.VertexCount - 1] = usePos;
                    }
                }

                appended_last_update = false;

            } else {
                // ok we drew far enough, add this position

                if (have_temp_append) {
                    // re-use temp vertex
                    preview[preview.VertexCount - 1] = newPos;
                    have_temp_append = false;
                } else {
                    preview.AppendVertex(newPos);
                }
                last_append = newPos;
                appended_last_update = true;

                // do smoothing pass
                smoother.End = preview.VertexCount - 1;
                smoother.Start = MathUtil.Clamp(smoother.End - 5, 0, smoother.End);
                smoother.UpdateDeformation(2);
            }
        }


        float dist_thresh(float fCurveRadius, float fSceneScale)
        {
            // when you zoom in you can draw smoother curves, but not too much smoother...
            return MathUtil.Lerp(fCurveRadius, fCurveRadius * fSceneScale, 0.35f);
            //return fCurveRadius;
        }


        public void BeginDraw_Ray(AnyRayHit rayHit)
        {
            CreateNewCurve();

            planarF = UnityUtil.GetGameObjectFrame(scene.ActiveCamera.GameObject(), CoordSpace.WorldCoords);
            planarF.Origin = rayHit.hitPos;

            float fScale = scene.GetSceneScale();
            smooth_append(preview,
                scene.SceneFrame.ToFrameP(rayHit.hitPos) / fScale, dist_thresh(Width, fScale));
        }


        public void UpdateDraw_Ray(Ray3f ray)
        {
            float fScale = scene.GetSceneScale();
            Vector3f vHit = planarF.RayPlaneIntersection(ray.Origin, ray.Direction, 2);
            smooth_append(preview, scene.SceneFrame.ToFrameP(vHit) / fScale, dist_thresh(Width, fScale));
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
            CreateNewCurve();
            float fScale = scene.GetSceneScale();
            Vector3f tipPos = handFrame.Origin + SceneGraphConfig.HandTipOffset * handFrame.Z;
            smooth_append(preview,
                scene.SceneFrame.ToFrameP(tipPos) / fScale, dist_thresh(Width, fScale));
            startHandF = handFrame;
            handHitVec = startHandF.ToFrameP(tipPos);
        }


        public void UpdateDraw_Spatial(Ray3f ray, Frame3f handFrame)
        {
            float fScale = scene.GetSceneScale();
            Vector3f newHitPos = handFrame.FromFrameP(handHitVec);
            smooth_append(preview, scene.SceneFrame.ToFrameP(newHitPos) / fScale, dist_thresh(Width, fScale));
        }


        public void EndDraw()
        {
            if (preview.Curve.ArcLength > 2 * Width) {
                // store undo/redo record for new primitive
                PolyCurveSO CurveSO = preview.BuildSO(scene.DefaultCurveSOMaterial, 1.0f);
                scene.History.PushChange(
                    new AddSOChange() { scene = scene, so = CurveSO, bKeepWorldPosition = false });
                scene.History.PushInteractionCheckpoint();
            }

            preview.Destroy();
            preview = null;

            SavedSettings.Save("DrawCurveTool_width", width);
        }

        public void CancelDraw()
        {
            if (preview != null) {
                preview.Destroy();
                preview = null;
            }
        }


    }







    class DrawCurveTool_SpatialDeviceBehavior : StandardInputBehavior
    {
        FContext context;

        public DrawCurveTool_SpatialDeviceBehavior(FContext s)
        {
            context = s;
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
                if (tool != null && tool is DrawCurveTool) {
                    return CaptureRequest.Begin(this, eSide);
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            Ray3f sideRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            Frame3f sideHandF = (eSide == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            DrawCurveTool tool = context.ToolManager.GetActiveTool((int)eSide) as DrawCurveTool;

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
            DrawCurveTool tool = context.ToolManager.GetActiveTool((int)data.which) as DrawCurveTool;

            // [RMS] this is a hack for trigger+shoulder grab gesture...really need some way
            //   to interrupt captures!!
            if ((data.which == CaptureSide.Left && input.bLeftShoulderPressed) ||
                 (data.which == CaptureSide.Right && input.bRightShoulderPressed)) {
                tool.CancelDraw();
                return Capture.End;
            }

            Vector2f vStick = (data.which == CaptureSide.Left) ? input.vLeftStickDelta2D : input.vRightStickDelta2D;
            if (vStick[1] != 0) {
                tool.Width = tool.Width + vStick[1] * 0.01f;
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
            DrawCurveTool tool = context.ToolManager.GetActiveTool((int)data.which) as DrawCurveTool;
            tool.CancelDraw();
            return Capture.End;
        }
    }








    class DrawCurveTool_2DBehavior : Any2DInputBehavior
    {
        FContext context;

        public DrawCurveTool_2DBehavior(FContext s)
        {
            context = s;
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool == null || !(context.ToolManager.ActiveRightTool is DrawCurveTool))
                return CaptureRequest.Ignore;
            if ( Pressed(input) ) {
                AnyRayHit rayHit;
                if (context.Scene.FindSceneRayIntersection( WorldRay(input), out rayHit))
                    return CaptureRequest.Begin(this);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            DrawCurveTool tool =
                (context.ToolManager.ActiveRightTool as DrawCurveTool);
            AnyRayHit rayHit;
            if (context.Scene.FindSceneRayIntersection( WorldRay(input), out rayHit)) {
                tool.BeginDraw_Ray(rayHit);
                return Capture.Begin(this);
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            DrawCurveTool tool =
                (context.ToolManager.ActiveRightTool as DrawCurveTool);

            tool.UpdateDraw_Ray( WorldRay(input) );

            if ( Released(input) ) {
                tool.EndDraw();
                return Capture.End;
            } else
                return Capture.Continue;
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            DrawCurveTool tool =
                (context.ToolManager.ActiveRightTool as DrawCurveTool);
            tool.CancelDraw();
            return Capture.End;
        }
    }
}
