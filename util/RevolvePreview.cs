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
    /// Preview of a surface-of-revolution around given axis.
    /// Call BuildSO() to create DMeshSO.
    /// </summary>
    public class RevolvePreview
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

        Frame3f axis;
        public Frame3f Axis
        {
            get { return axis; }
            set { axis = value;  bUpdatePending = true; }
        }


        fMeshGameObject meshObject;
        bool bUpdatePending;

        public void Create(SOMaterial useMaterial, fGameObject parent, int nLayer = -1)
        {
            if ( curve == null )
                curve = new DCurve3();
            axis = Frame3f.Identity;

            meshObject = GameObjectFactory.CreateMeshGO("revolve_preview");
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
            Frame3f useF = axis;
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
            if (bUpdatePending == false && curve_timestamp == curve.Timestamp)
                return;
            if (curve.VertexCount < 2)
                return;

            // generate mesh tube
            Curve3Axis3RevolveGenerator meshGen = new Curve3Axis3RevolveGenerator();
            meshGen.Curve = curve.Vertices.ToArray();
            meshGen.Axis = axis;
            meshGen.Generate();
            meshObject.SetSharedMesh(meshGen.MakeUnityMesh(true));

            bUpdatePending = false;
            curve_timestamp = curve.Timestamp;
        }



    }
}
