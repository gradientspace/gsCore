// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using g3;
using f3;

namespace gs
{
    //
    // 
    // 
    public class PositionConstrainedPointWidget : Standard3DWidget
    {
        //ITransformGizmo parent;
        FScene Scene;

        // you should set these on/around BeginCapture
        public SceneObject SourceSO;

        public Func<Ray3f, Vector3d> ScenePositionF = null;

        public PositionConstrainedPointWidget(ITransformGizmo parent, FScene scene)
        {
            //this.parent = parent;
            this.Scene = scene;
        }

        public override void Disconnect()
        {
            RootGameObject.Destroy();
        }

        public override bool BeginCapture(ITransformable target, Ray3f worldRay, UIRayHit hit)
        {
            return true;
        }


        public override bool UpdateCapture(ITransformable target, Ray3f worldRay)
        {
            Ray3f sceneRay = Scene.ToSceneRay(worldRay);
            Vector3f newPos = (Vector3f)ScenePositionF(sceneRay);

            Frame3f f = SourceSO.GetLocalFrame(CoordSpace.SceneCoords);
            if ( f.Origin.EpsilonEqual(newPos, MathUtil.Epsilonf) == false ) { 
                f.Origin = newPos;
                SourceSO.SetLocalFrame(f, CoordSpace.SceneCoords);
            }

            return true;
        }

        public override bool EndCapture(ITransformable target)
        {
            return true;
        }
    }
}

