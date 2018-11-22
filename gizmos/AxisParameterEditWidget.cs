// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using UnityEngine;
using g3;
using f3;

namespace gs
{
    //
    // 
    // 
    public class AxisParameterEditWidget : Standard3DWidget, IParameterEditWidget
    {
        public string AxisParamName = "default";
        public float ValidRangeMin = 0.01f;
        public float ValidRangeMax = 1000.0f;
        public Vector3f AxisVectorInFrame = Vector3f.AxisY;  // defines frame-relative axis we will drag along

        // scaling factor applied to delta values. You can use this to:
        //  - remap parameter values (eg if param is "width" but widget is placed at half-width)
        //  - change speed of parameter edit (eg "precision mode")
        //  - compensate for global scene scaling
        public Func<float> DeltaScalingF = () => { return 1.0f; };

        //ITransformGizmo parent;
        FScene scene;
        PrimitiveSO primitive;

        public AxisParameterEditWidget(ITransformGizmo parent, PrimitiveSO primitive, FScene scene)
        {
            //this.parent = parent;
            this.primitive = primitive;
            this.scene = scene;
        }

        float fStartValue;

        // stored frames from target used during click-drag interaction
        Frame3f targetFrameW;     // world-space frame
        Vector3f heightAxisW;     // world axis that height is aligned with

        // computed values during interaction
        Frame3f raycastFrame;        // camera-facing plane containing heightAxisW
        float fHeightStartT;		// start T-value along heightAxisW

        public override bool BeginCapture(ITransformable target, Ray3f worldRay, UIRayHit hit)
        {
            fStartValue = primitive.Parameters.GetValue<float>(AxisParamName);

            // save necessary frame info
            targetFrameW = target.GetLocalFrame(CoordSpace.WorldCoords);
            heightAxisW = targetFrameW.FromFrameV(AxisVectorInFrame);

            // save t-value of closest point on height axis, so we can find delta-t
            Vector3f vWorldHitPos = hit.hitPos;
            fHeightStartT = Distance.ClosestPointOnLineT(
                targetFrameW.Origin, heightAxisW, vWorldHitPos);

            // construct plane we will ray-intersect with in UpdateCapture()
            Vector3f makeUp = Vector3f.Cross(scene.ActiveCamera.Forward(), heightAxisW).Normalized;
            Vector3f vPlaneNormal = Vector3f.Cross(makeUp, heightAxisW).Normalized;
            raycastFrame = new Frame3f(vWorldHitPos, vPlaneNormal);

            return true;
        }

        public override bool UpdateCapture(ITransformable target, Ray3f worldRay)
        {
            // ray-hit with plane that contains translation axis
            Vector3f planeHit = raycastFrame.RayPlaneIntersection(worldRay.Origin, worldRay.Direction, 2);

            // figure out new T-value along axis, then our translation update is delta-t
            float fNewT = Distance.ClosestPointOnLineT(targetFrameW.Origin, heightAxisW, planeHit);
            float fDeltaT = (fNewT - fHeightStartT);
            fDeltaT *= DeltaScalingF();

            float fNewValue = fStartValue + fDeltaT;
            fNewValue = Mathf.Clamp(fNewValue, ValidRangeMin, ValidRangeMax);

            primitive.Parameters.SetValue(AxisParamName, fNewValue);

            return true;
        }

        public override bool EndCapture(ITransformable target)
        {
            return true;
        }

        public override void Disconnect()
        {
            RootGameObject.Destroy();
        }


        // IParameterEditWidget impl
        public string ParameterName {
            get { return AxisParamName; }
        }
    }
}

