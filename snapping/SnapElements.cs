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
    public class SOFrameSnapPoint : ISnapPoint
    {
        public SceneObject so;
        public Frame3f frame;

        public GameObject primGO;
        public bool IsSurface = true;

        int priority = 999;
        public int Priority { get { return priority; } }


        public object Source { get { return so; } }
        public Frame3f FrameS
        {
            get {
                return SceneTransforms.ObjectToScene(so, frame);
            }
        }

        int unique_id;
        public int UniqueID { get { return unique_id; } }

        public SOFrameSnapPoint(SceneObject tso, int priority = 999)
        {
            this.so = tso;
            this.priority = priority;
            unique_id = SnapGeometryUniqueIDGenerator.Allocate();
        }

        public bool FindRayIntersection(Ray3f ray, out float fHitDist)
        {
            fHitDist = float.PositiveInfinity;
            GameObjectRayHit hit;
            if (UnityUtil.FindGORayIntersection(ray, primGO, out hit)) {
                fHitDist = hit.fHitDist;
                return true;
            }
            return false;
        }

        public void Build(Frame3f worldFrame, GameObject parent, Material mat, int nLayer = -1)
        {
            primGO = UnityUtil.CreatePrimitiveGO("generated_point", PrimitiveType.Sphere, mat, true);
            UnityUtil.SetGameObjectFrame(primGO, worldFrame, CoordSpace.WorldCoords);
            MaterialUtil.DisableShadows(primGO);
            UnityUtil.AddChild(parent, primGO, true);
            if (nLayer > 0)
                primGO.SetLayer(nLayer);
        }

        public void PreRender(Vector3f cameraPosition)
        {
            // since we are supporting case where so is moving (ie bimanual grab), we need to
            // update position here. And SetGameObjectFrame() does not support SceneCoords.
            // So map scene to world
            Frame3f FrameW = SceneTransforms.TransformTo(FrameS, so, CoordSpace.SceneCoords, CoordSpace.WorldCoords);
            UnityUtil.SetGameObjectFrame(primGO, FrameW, CoordSpace.WorldCoords);

            float fScaling = VRUtil.GetVRRadiusForVisualAngle(
                primGO.transform.position, cameraPosition,
                SceneGraphConfig.DefaultPivotVisualDegrees * 0.98f);
            // [RMS] not sure this makes sense...eg what if we have multiple parents? they could
            //   have different scalings, no? ParentScale seems to be inherited from scene scaling,
            //   somehow, but it is unclear...
            float fParentScale = primGO.transform.parent.localScale[0];
            float fSceneScale = so.GetScene().GetSceneScale();
            fScaling = fScaling / fParentScale / fSceneScale;
            primGO.transform.localScale = new Vector3f(fScaling, fScaling, fScaling);
        }

        public void SetEnabled(bool bEnabled)
        {
            primGO.SetVisible(bEnabled);
        }
        public bool IsEnabled()
        {
            return primGO.IsVisible();
        }

        public void Destroy()
        {
            primGO.transform.parent = null;
            primGO.Destroy();
        }

    }






    public class StandardSnapSegment : ISnapSegment
    {
        public SceneObject so;

        // these define segment (frame Z axis is segment direction)
        public Frame3f center;
        public float extent;

        protected float fLineWidthW;
        protected Segment3f segmentW;

        protected GameObject lineGO;

        public object Source { get { return so; } }

        public Vector3f StartPointS { get { return center.Origin + extent * center.Z; } }
        public Vector3f EndPointS { get { return center.Origin - extent * center.Z; } }
        public Frame3f FrameS { get { return center; } }

        int unique_id;
        public int UniqueID { get { return unique_id; } }

        public StandardSnapSegment(SceneObject tso)
        {
            this.so = tso;
            unique_id = SnapGeometryUniqueIDGenerator.Allocate();
            segmentW = new Segment3f(Vector3f.Zero, Vector3f.AxisX);
        }


        public Frame3f GetHitFrameS(Vector3f pos) {
            return new Frame3f(pos, center.Rotation);
        }


        public bool FindRayIntersection(Ray3f ray, out float fHitDist)
        {
            fHitDist = float.PositiveInfinity;
            float fHitThreshSqr = 2.0f * fLineWidthW;
            fHitThreshSqr *= fHitThreshSqr;

            DistRay3Segment3 dist = new DistRay3Segment3(ray, segmentW);
            float fDistWSqr = (float)dist.GetSquared();
            if (fDistWSqr < fHitThreshSqr) {
                fHitDist = (float)dist.RayParameter;
                return true;
            }
            return false;
        }

        public Vector3f FindSnapPoint(Ray3f ray)
        {
            DistRay3Segment3 dist = new DistRay3Segment3(ray, segmentW);
            dist.GetSquared();
            return (Vector3f)dist.SegmentClosest;
        }

        public void Build(Frame3f centerW, float extentW, GameObject parent, Material mat, int nLayer = -1)
        {
            lineGO = new GameObject("snap_line");
            LineRenderer ren = lineGO.AddComponent<LineRenderer>();
            ren.startWidth = ren.endWidth = 0.05f;
            ren.material = mat;
            ren.useWorldSpace = false;
            ren.positionCount = 2;
            ren.SetPosition(0, centerW.Origin - extentW * centerW.Z);
            ren.SetPosition(1, centerW.Origin + extentW * centerW.Z);

            MaterialUtil.DisableShadows(lineGO);
            UnityUtil.AddChild(parent, lineGO, false);
            if (nLayer > 0)
                lineGO.SetLayer(nLayer);
        }

        public void PreRender(Vector3f cameraPosition)
        {
            FScene scene = so.GetScene();

            // update world-space segment that we need for ray-intersections
            Frame3f frameW = scene.ToWorldFrame(center);
            segmentW.Center = frameW.Origin;
            segmentW.Direction = frameW.Z;
            segmentW.Extent = scene.ToWorldDimension(extent);

            //lineGO.GetComponent<LineRenderer>().SetPosition(0, segmentW.P0);

            fLineWidthW = VRUtil.GetVRRadiusForVisualAngle(segmentW.Center, cameraPosition,
                SceneGraphConfig.DefaultSnapCurveVisualDegrees);
            // [RMS] not sure this makes sense...eg what if we have multiple parents? they could
            //   have different scalings, no? ParentScale seems to be inherited from scene scaling,
            //   somehow, but it is unclear...
            float fParentScale = lineGO.transform.parent.localScale[0];
            float fSceneScale = scene.GetSceneScale();
            fLineWidthW = fLineWidthW / fParentScale / fSceneScale;

            LineRenderer ren = lineGO.GetComponent<LineRenderer>();
            ren.startWidth = ren.endWidth = fLineWidthW;
        }

        public void SetEnabled(bool bEnabled)
        {
            lineGO.SetVisible(bEnabled);
        }
        public bool IsEnabled()
        {
            return lineGO.IsVisible();
        }

        public void Destroy()
        {
            lineGO.transform.parent = null;
            lineGO.Destroy();
        }

    }

}
