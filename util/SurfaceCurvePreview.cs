// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using g3;
using f3;

namespace gs
{
    public class SurfaceCurvePreview : CurvePreview
    {
        public DMeshSO Target;
        int target_timestamp;

        struct SurfaceVertexRef
        {
            public int tid;
            public Vector3dTuple3 offsets;
        }
        List<SurfaceVertexRef> SurfacePoints;
        

        public SurfaceCurvePreview(DMeshSO targetSurf) : base()
        {
            Target = targetSurf;
            SurfacePoints = new List<SurfaceVertexRef>();
            target_timestamp = Target.Timestamp;
        }


        public override void AppendVertex(Vector3d v)
        {
            base.AppendVertex(v);

            // map v to mesh
            v = SceneTransforms.SceneToObjectP(Target, v);

            // TODO encode vertices by normals ??
            DMesh3 Mesh = Target.Mesh;
            DMeshAABBTree3 Spatial = Target.Spatial;
            SurfaceVertexRef r = new SurfaceVertexRef();
            r.tid = Spatial.FindNearestTriangle(v);
            Frame3f f = Mesh.GetTriFrame(r.tid); 
            Index3i tv = Mesh.GetTriangle(r.tid);
            for ( int j = 0; j < 3; ++j ) {
                f.Origin = (Vector3f)Mesh.GetVertex(tv[j]);
                Vector3d dv = f.ToFrameP(v);
                r.offsets[j] = dv;
            }
            SurfacePoints.Add(r);
            if (Curve.VertexCount != SurfacePoints.Count)
                throw new Exception("SurfaceCurvePreview: counts are out of sync!!");
        }


        protected override void update_vertices(FScene s)
        {
            if (Target.Timestamp == target_timestamp)
                return;

            target_timestamp = Target.Timestamp;

            DMesh3 Mesh = Target.Mesh;
            for ( int i = 0; i < VertexCount; ++i ) {
                SurfaceVertexRef r = SurfacePoints[i];
                Vector3d vSum = Vector3d.Zero;
                Frame3f f = Mesh.GetTriFrame(r.tid);
                Index3i tv = Mesh.GetTriangle(r.tid);
                for ( int j = 0; j < 3; ++j ) {
                    f.Origin = (Vector3f)Mesh.GetVertex(tv[j]);
                    Vector3d v = f.FromFrameP(r.offsets[j]);
                    vSum += v;
                }
                vSum /= 3;
                this[i] = SceneTransforms.ObjectToSceneP(Target, vSum);
            }

        }



    }
}
