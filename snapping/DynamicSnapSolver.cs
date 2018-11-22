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
    public class DynamicSnapSolver
    {
        SceneObject targetSO;

        List<Frame3f> snapFramesL;
        SOIndicatorSet snapIndicators;

        public float SnapThreshVisualAngleDeg = SceneGraphConfig.DefaultSnapDistVisualDegrees;
        SnapStateMachine<SnapResult> snapState;

        Frame3f snapFrameS;
        public Frame3f SnapFrameS {
            get { return snapFrameS; }
        }

        public bool SnapOrientation = true;

        public DynamicSnapSolver(SceneObject target)
        {
            snapFramesL = new List<Frame3f>();
            targetSO = target;

            Frame3f targetSceneF = targetSO.GetLocalFrame(CoordSpace.SceneCoords);
            generate_points(targetSO, targetSceneF);

            snapIndicators = new SOIndicatorSet(targetSO);
            foreach ( Frame3f f in snapFramesL ) {
                snapIndicators.AddSphereL(f, SceneGraphConfig.DefaultPivotVisualDegrees * 0.75f, target.GetScene().PivotMaterial );
            }

            snapState = new SnapStateMachine<SnapResult>();
        }

        public void Disconnect()
        {
            snapIndicators.Disconnect(true);
        }



        // returns snapped world-frame, or input frame if no snap
        public Frame3f UpdateSnapW(Frame3f fSourceFrameW, SnapSet Snaps)
        {
            FScene scene = targetSO.GetScene();
            float fSnapRadiusW = VRUtil.GetVRRadiusForVisualAngle(fSourceFrameW.Origin,
                scene.ActiveCamera.GetPosition(), SnapThreshVisualAngleDeg);
            float fSnapRadiusS = fSnapRadiusW / scene.GetSceneScale();

            // fSourceFrameW in Scene coordinates
            Frame3f fSourceS = scene.ToSceneFrame(fSourceFrameW);

            SnapResult best_snap = null;
            float fNearest = float.MaxValue;
            Frame3f fBestSourceL = Frame3f.Identity;

            // snapframes are stored in local coords relative to object
            foreach ( Frame3f fPointFrameL in snapFramesL ) {
                // convert local-coord snap frame into scene coords
                Frame3f fPointFrameS = fSourceS.FromFrame(fPointFrameL);

                SnapResult snap = Snaps.FindNearestSnapPointS(fPointFrameS, fSnapRadiusS);
                if (snap != null) {
                    float d = ((Vector3f)snap.FrameS.Origin - fPointFrameS.Origin).Length;
                    if (d < fNearest) {
                        fNearest = d;
                        fBestSourceL = fPointFrameL;
                        best_snap = snap;
                    }
                }
            }

            snapState.UpdateState(best_snap, fBestSourceL);
            if ( snapState.IsSnapped ) {
                SnapResult useSnap = snapState.ActiveSnapTarget;
                Frame3f useSourceL = (Frame3f)snapState.ActiveSnapData;

                if (SnapOrientation) {
                    // compute min-rotation frame, then align origins
                    Frame3f fAlignedSourceS =
                        Frame3f.SolveMinRotation(fSourceS, useSnap.FrameS);
                    Frame3f fPointFrameS = fAlignedSourceS.FromFrame(useSourceL);
                    Vector3f deltaS = (Vector3f)useSnap.FrameS.Origin - fPointFrameS.Origin;
                    snapFrameS = fAlignedSourceS.Translated(deltaS);


                    //// this is tricky...we have an object-space frame useSourceL, which
                    ////  we want to snap to a scene-space frame usePoint.FrameS. So we need
                    ////  to shift origin of that frame by -useSourceL_in_FrameS!
                    //snapFrameS = usePoint.FrameS.Translated(
                    //    -usePoint.FrameS.FromFrameV(useSourceL.Origin));
                } else {
                    // translation-only snap - find shift in scene space, apply to input source frame
                    Frame3f fPointFrameS = fSourceS.FromFrame(useSourceL);
                    Vector3f deltaS = (Vector3f)useSnap.FrameS.Origin - fPointFrameS.Origin;
                    snapFrameS = fSourceS.Translated(deltaS);
                }

                // now convert to world frame for return
                return scene.ToWorldFrame(snapFrameS);

            }

            return fSourceFrameW;
        }






        void generate_points(SceneObject tso, Frame3f targetSceneF)
        {
            if (tso is PrimitiveSO)
                generate_points(tso as PrimitiveSO, targetSceneF);
            else if (tso is PolyCurveSO)
                generate_points(tso as PolyCurveSO, targetSceneF);
            else if (tso is PivotSO)
                generate_points(tso as PivotSO, targetSceneF);
            else if (tso is GroupSO)
                generate_points(tso as GroupSO, targetSceneF);
        }

        void generate_points(PrimitiveSO primSO, Frame3f targetSceneF)
        {
            PrimitiveSnapGenerator snapGen = new PrimitiveSnapGenerator();
            List<ISnapPoint> points = snapGen.GeneratePoints(primSO);
            foreach ( ISnapPoint pt in points ) {
                Frame3f objF = targetSceneF.ToFrame(pt.FrameS);
                snapFramesL.Add(objF);
            }
        }


        void generate_points(PolyCurveSO curveSO, Frame3f targetSceneF)
        {
            CurveSnapGenerator snapGen = new CurveSnapGenerator();
            List<ISnapPoint> points = snapGen.GeneratePoints(curveSO);
            foreach ( ISnapPoint pt in points ) {
                Frame3f objF = targetSceneF.ToFrame(pt.FrameS);
                snapFramesL.Add(objF);
            }
        }


        void generate_points(PivotSO pivotSO, Frame3f targetSceneF)
        {
            PivotSOSnapPoint pt = new PivotSOSnapPoint(pivotSO);
            Frame3f objF = targetSceneF.ToFrame(pt.FrameS);
            snapFramesL.Add(objF);
        }


        void generate_points(GroupSO so, Frame3f targetSceneF)
        {
            foreach ( var childso in so.GetChildren() ) {
                generate_points(childso, targetSceneF);
            }
        }


    }
}
