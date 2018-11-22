// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using g3;
using f3;

namespace gs
{
    /// <summary>
    /// Preview of a surface-of-revolution of a curve around another curve.
    /// Call BuildSO() to create DMeshSO.
    /// </summary>
    public class CurveRevolvePreview
    {

        DCurve3 revolveCurve;
        public DCurve3 RevolveCurve
        {
            get { return revolveCurve; }
            set { revolveCurve = value; bUpdatePending = true; }
        }
        public bool CurveModified
        {
            get { return bUpdatePending; }
            set { if ( value == true ) bUpdatePending = true; }
        }
        int curve_timestamp = -1;

        DCurve3 axisCurve;
        public DCurve3 AxisCurve
        {
            get { return axisCurve; }
            set { axisCurve = value;  bUpdatePending = true; }
        }
        int axis_timestamp = -1;

        // output object will have this local frame
        public Frame3f OutputFrame = Frame3f.Identity;


        fMeshGameObject meshObject;
        bool bUpdatePending;

        public void Create(SOMaterial useMaterial, fGameObject parent, int nLayer = -1)
        {
            if ( revolveCurve == null )
                revolveCurve = new DCurve3();
            if (axisCurve == null)
                axisCurve = new DCurve3();

            meshObject = GameObjectFactory.CreateMeshGO("curve_revolve_preview");
            meshObject.SetMaterial(MaterialUtil.ToMaterialf(useMaterial), true);
            if (nLayer != -1)
                meshObject.SetLayer(nLayer);

            bUpdatePending = true;

            meshObject.SetParent(parent, false);
            meshObject.SetLocalScale(1.001f * Vector3f.One);
        }

        public void PreRender()
        {
            update_geometry();
        }


        public bool Visible
        {
            get { return meshObject.IsVisible(); }
            set { meshObject.SetVisible(value); }
        }

        public bool IsValid {
            get {
                return meshObject != null && meshObject.GetSharedMesh().triangles.Length > 0;
            }
        }

        public DMeshSO BuildSO(FScene scene, SOMaterial material)
        {
            DMesh3 revolveMesh = UnityUtil.UnityMeshToDMesh(meshObject.GetSharedMesh(), false);

            // move axis frame to center of bbox of mesh, measured in axis frame
            Frame3f useF = OutputFrame;
            AxisAlignedBox3f boundsInFrame = (AxisAlignedBox3f)BoundsUtil.BoundsInFrame(revolveMesh.Vertices(), useF);
            useF.Origin = useF.FromFrameP(boundsInFrame.Center);

            // transform mesh into this frame
            MeshTransforms.ToFrame(revolveMesh, useF);

            // create new so
            DMeshSO meshSO = new DMeshSO();
            meshSO.Create(revolveMesh, material);
            meshSO.SetLocalFrame(useF, CoordSpace.ObjectCoords);

            return meshSO;
        }


        public void Destroy()
        {
            meshObject.Destroy();
        }


        void update_geometry()
        {
            if (bUpdatePending == false && curve_timestamp == revolveCurve.Timestamp && axis_timestamp == axisCurve.Timestamp)
                return;
            if (revolveCurve.VertexCount < 2)
                return;

            // generate mesh tube
            Curve3Curve3RevolveGenerator meshGen = new Curve3Curve3RevolveGenerator();
            meshGen.Curve = revolveCurve.Vertices.ToArray();
            meshGen.Axis = axisCurve.Vertices.ToArray();;
            meshGen.Generate();
            meshObject.SetSharedMesh(meshGen.MakeUnityMesh(true));

            bUpdatePending = false;
            curve_timestamp = revolveCurve.Timestamp;
            axis_timestamp = axisCurve.Timestamp;
        }



    }
}
