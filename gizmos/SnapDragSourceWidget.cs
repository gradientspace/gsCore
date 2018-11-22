// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using UnityEngine;
using g3;
using f3;

namespace gs
{
    //
    // 
    // 
    public class SnapDragSourceWidget : Standard3DWidget
    {
        ITransformGizmo parent;
        FScene scene;

        public Frame3f SourceFrameL;             // frame of widget in local coords of gizmo

        SnapSet Targets;

        // you should set this on/around BeginCapture
        public List<SceneObject> TargetObjects;

        public SnapDragSourceWidget(ITransformGizmo parent, FScene scene, SnapSet targets)
        {
            this.parent = parent;
            this.scene = scene;
            this.Targets = targets;
        }

        Frame3f originalTargetS;
        SnapStateMachine<SnapResult> snapState;

        public override void Disconnect()
        {
            RootGameObject.Destroy();
        }

        public override bool BeginCapture(ITransformable target, Ray3f worldRay, UIRayHit hit)
        {
            originalTargetS = target.GetLocalFrame(CoordSpace.SceneCoords);

            snapState = new SnapStateMachine<SnapResult>();

            return true;
        }


        public override bool UpdateCapture(ITransformable target, Ray3f worldRay)
        {
            // find hit
            SnapResult snap = Targets.FindHitSnapPoint(worldRay);
            snapState.UpdateState(snap);

            if (snapState.IsSnapped) {
                SnapResult useSnap = snapState.ActiveSnapTarget;
                Frame3f hitFrameS = Frame3f.Identity;
                if (useSnap != null)
                    hitFrameS = new Frame3f(useSnap.FrameS);

                Frame3f targetF = originalTargetS; // target.GetLocalFrame(CoordSpace.WorldCoords);
                Frame3f pivotF = hitFrameS;
                targetF.Origin = pivotF.Origin;

                if (parent.CurrentFrameMode == FrameType.WorldFrame)
                    targetF.Rotation = Quaternion.identity;
                else
                    targetF.Rotation = pivotF.Rotation;

                Vector3f deltaInT = targetF.FromFrameV(SourceFrameL.Origin);
                targetF.Origin -= deltaInT;   // why is this minus?

                target.SetLocalFrame(targetF, CoordSpace.SceneCoords);

            } else {

                Func<SceneObject, bool> filter = null;
                if (TargetObjects != null && TargetObjects.Count > 0)
                    filter = (x) => { return TargetObjects.Contains(x) == false; };

                AnyRayHit hit;
                if ( scene.FindSceneRayIntersection(worldRay, out hit, true, filter ) ) {
                    Vector3f hitPosS = scene.ToSceneP(hit.hitPos);
                    Vector3f hitNormS = scene.ToSceneN(hit.hitNormal);

                    Frame3f targetF = originalTargetS;
                    targetF.Origin = hitPosS;
                    targetF.AlignAxis(1, hitNormS);

                    if (parent.CurrentFrameMode == FrameType.WorldFrame)
                        targetF.Rotation = Quaternion.identity;

                    Vector3f deltaInT = targetF.FromFrameV(SourceFrameL.Origin);
                    targetF.Origin -= deltaInT;   // why is this minus?
                    target.SetLocalFrame(targetF, CoordSpace.SceneCoords);

                } else
                    target.SetLocalFrame(originalTargetS, CoordSpace.SceneCoords);
            }

            return true;
        }

        public override bool EndCapture(ITransformable target)
        {
            return true;
        }
    }
}

