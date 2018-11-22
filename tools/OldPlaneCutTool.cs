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
    // [TODO] should take optional selection target...
    public class OldPlaneCutToolBuilder : IToolBuilder
    {
        public bool GenerateFillSurface = false;

        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            //return (type == ToolTargetType.SingleObject && targets[0].IsSurface);
            return (type == ToolTargetType.SingleObject && targets[0] is DMeshSO);
        }

        public ITool Build(FScene scene, List<SceneObject> targets)
        {
            OldPlaneCutTool tool = new OldPlaneCutTool(scene, targets[0]);
            tool.GenerateFillSurface = GenerateFillSurface;
            return tool;
        }
    }



    public class OldPlaneCutTool : ITool
    {
        static readonly public string Identifier = "plane_cut";

        virtual public string Name { get { return "PlaneCut"; } }
        virtual public string TypeIdentifier { get { return Identifier; } }

        FScene scene;

        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors
        {
            get { return behaviors; }
            set { behaviors = value; }
        }

        ParameterSet parameters = new ParameterSet();
        public ParameterSet Parameters { get { return parameters; } }

        public virtual bool AllowSelectionChanges { get { return true; } }


        /// <summary>
        /// Closed loop or open curve
        /// </summary>
        virtual public bool GenerateFillSurface
        {
            get { return generate_fill; }
            set { generate_fill = value; }
        }
        bool generate_fill = false;


        public DMeshSO Target
        {
            get { return target; }
        }
        DMeshSO target;


        public Frame3f CutPlane {
            get { return cut_plane; }
            set { cut_plane = value; }
        }
        Frame3f cut_plane;

        ToolIndicatorSet indicators;


        public OldPlaneCutTool(FScene scene, SceneObject target)
        {
            this.scene = scene;
            this.target = target as DMeshSO;

            double plane_dim = 1.5*this.target.Mesh.CachedBounds.DiagonalLength;

            behaviors = new InputBehaviorSet();

            // TODO is this where we should be doing this??
            behaviors.Add(
                new PlaneCutTool_SpatialBehavior(this, scene.Context) { Priority = 5 });

            // shut off transform gizmo
            scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);


            indicators = new ToolIndicatorSet(this, scene);
            SectionPlaneIndicator section_plane = new SectionPlaneIndicator() {
                SceneFrameF = () => { return cut_plane; },
                Width = fDimension.Scene(() => { return (float)(plane_dim); })
            };
            indicators.AddIndicator(section_plane);

        }

        public virtual void Setup()
        {
        }
        public void Shutdown()
        {
            scene.Context.TransformManager.PopOverrideGizmoType();
            indicators.Disconnect(true);
        }


        virtual public void PreRender()
        {
            indicators.PreRender();
        }

        virtual public bool HasApply { get { return false; } }
        virtual public bool CanApply { get { return false; } }
        virtual public void Apply() { }




        virtual public void UpdatePlane(Frame3f frameW)
        {
            cut_plane = scene.ToSceneFrame(frameW);
        }



        virtual public void CutMesh()
        {
            Frame3f frameL = SceneTransforms.SceneToObject(target, cut_plane);

            Action<DMesh3> editF = (mesh) => {
                MeshPlaneCut cut = new MeshPlaneCut(mesh, frameL.Origin, frameL.Y);
                cut.Cut();

                PlaneProjectionTarget planeTarget = new PlaneProjectionTarget() {
                    Origin = frameL.Origin, Normal = frameL.Y
                };

                if (GenerateFillSurface) {
                    double min, max, avg;
                    MeshQueries.EdgeLengthStats(mesh, out min, out max, out avg);

                    cut.FillHoles();

                    MeshFaceSelection selection = new MeshFaceSelection(mesh);
                    foreach (var tris in cut.LoopFillTriangles)
                        selection.Select(tris);
                    RegionRemesher.QuickRemesh(mesh, selection.ToArray(), 2 * avg, 1.0f, 25, planeTarget);

                    MeshNormals normals = new MeshNormals(mesh);
                    normals.Compute();
                    normals.CopyTo(mesh);
                }
            };

            target.EditAndUpdateMesh(editF, GeometryEditTypes.ArbitraryEdit);
        }


    }






    class PlaneCutTool_SpatialBehavior : StandardInputBehavior
    {
        FContext context;
        OldPlaneCutTool tool;

        public PlaneCutTool_SpatialBehavior(OldPlaneCutTool tool, FContext s)
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
                ITool sideTool = context.ToolManager.GetActiveTool((int)eSide);
                if (sideTool == tool) {
                    return CaptureRequest.Begin(this, eSide);
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            //Ray3f sideRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            Frame3f sideHandF = (eSide == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            sideHandF.Origin += SceneGraphConfig.VRHandTipOffset * sideHandF.Z;
            tool.UpdatePlane(sideHandF);

            return Capture.Begin(this, eSide);
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            // [RMS] this is a hack for trigger+shoulder grab gesture...really need some way to interrupt captures!!
            if ((data.which == CaptureSide.Left && input.bLeftShoulderPressed) ||
                 (data.which == CaptureSide.Right && input.bRightShoulderPressed)) {
                return Capture.End;
            }

            //Ray3f sideRay = (data.which == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            Frame3f sideHandF = (data.which == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            sideHandF.Origin += SceneGraphConfig.VRHandTipOffset * sideHandF.Z;

            bool bReleased = (data.which == CaptureSide.Left) ? input.bLeftTriggerReleased : input.bRightTriggerReleased;
            if (bReleased) {
                tool.CutMesh();
                return Capture.End;
            } else {
                tool.UpdatePlane(sideHandF);
                return Capture.Continue;
            }
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
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
            tool.UpdatePlane(sideHandF);
        }
        public override void EndHover(InputState input)
        {
        }
    }


}
