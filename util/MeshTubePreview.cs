// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using g3;
using f3;

namespace gs
{
    // this class lets you create a primitive and position/size it interactively.
    // supports center modes, handles negative width/height/depth by shifting origin, etc
    // use BuildSO() to convert to a PrimitiveSO at same position

    public class MeshTubePreview
    {

        DCurve3 curve;
        public DCurve3 Curve
        {
            get { return curve; }
            set { curve = value; bUpdatePending = true; }
        }
        public bool CurveModified
        {
            get { return bUpdatePending; }
            set { if ( value == true ) bUpdatePending = true; }
        }
        int curve_timestamp = -1;

        Polygon2d polygon;
        public Polygon2d Polygon
        {
            get { return polygon; }
            set { polygon = value; bUpdatePending = true; }
        }


        fMeshGameObject meshObject;
        bool bUpdatePending;

        public void Create(SOMaterial useMaterial, fGameObject parent)
        {
            if ( curve == null )
                curve = new DCurve3();

            meshObject = GameObjectFactory.CreateMeshGO("mesh_tube_preview");
            //meshObject.SetMesh(new fMesh())
            meshObject.SetMaterial(MaterialUtil.ToMaterialf(useMaterial));
            bUpdatePending = true;

            parent.AddChild(meshObject, false);
        }

        public void PreRender()
        {
            update_geometry();
        }


        public PolyTubeSO BuildSO(SOMaterial material, float scale = 1.0f)
        {
            Vector3d vCenter = curve.GetBoundingBox().Center;
            DCurve3 shifted = new DCurve3(curve);
            for (int i = 0; i < shifted.VertexCount; ++i)
                shifted[i] -= vCenter;
            Frame3f shiftedFrame = new Frame3f((Vector3f)vCenter, Quaternionf.Identity);

            PolyTubeSO so = new PolyTubeSO() {
                Curve = shifted,
                Polygon = polygon
            };
            so.Create(material);
            so.SetLocalFrame(shiftedFrame, CoordSpace.WorldCoords);

            return so;
        }


        public void Destroy()
        {
            meshObject.SetParent(null);
            meshObject.Destroy();
        }


        void update_geometry()
        {
            if (bUpdatePending == false && curve_timestamp == curve.Timestamp)
                return;
            if (curve.VertexCount < 2)
                return;

            // generate mesh tube
            TubeGenerator meshGen = new TubeGenerator() {
                Vertices = new List<Vector3d>(curve.Vertices), Capped = true,
                Polygon = polygon
                //, Frame = new Frame3f(Vector3f.Zero, Vector3f.AxisY)
            };
            meshGen.Generate();
            meshObject.SetMesh(meshGen.MakeUnityMesh(false));

            bUpdatePending = false;
            curve_timestamp = curve.Timestamp;
        }



    }
}
