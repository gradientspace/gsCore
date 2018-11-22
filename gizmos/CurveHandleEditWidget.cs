// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using g3;
using f3;

namespace gs
{
    public class CurveHandleEditWidget : Standard3DWidget
    {

        //ITransformGizmo parent;
        //Scene scene;
        PolyCurveSO curve;

        public int VertexIndex;
        public const int LastIndex = 9999999;

        Frame3f targetW;

        public CurveHandleEditWidget(ITransformGizmo parent, PolyCurveSO curve, FScene scene)
        {
            //this.parent = parent;
            this.curve = curve;
            //this.scene = scene;
            VertexIndex = 0;
        }

        ArcLengthSoftTranslation deformer;

        public override void Disconnect()
        {
            RootGameObject.Destroy();
        }

        public override bool BeginCapture(ITransformable target, Ray3f worldRay, UIRayHit hit)
        {
            if ( deformer == null ) {
                deformer = new ArcLengthSoftTranslation() {
                    Curve = curve.Curve, ArcRadius = 3.0f
                };
            }

            DCurve3 c = curve.Curve;
            int vi = (VertexIndex == LastIndex) ? c.VertexCount - 1 : 0;
            deformer.Handle = c.GetVertex(vi);
            deformer.UpdateROI(vi);
            deformer.BeginDeformation();

            targetW = target.GetLocalFrame(CoordSpace.WorldCoords);

            return true;
        }

        public override bool UpdateCapture(ITransformable target, Ray3f worldRay)
        {
            Frame3f plane = new Frame3f(Vector3.zero, Vector3.forward);
            Vector3 vHit = plane.RayPlaneIntersection(worldRay.Origin, worldRay.Direction, 2);

            vHit = targetW.ToFrameP(vHit);
            deformer.UpdateDeformation(vHit);

            return true;
        }

        public override bool EndCapture(ITransformable target)
        {
            deformer.EndDeformation();
            return true;
        }
    }
}
