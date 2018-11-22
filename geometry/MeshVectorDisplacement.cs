// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using g3;

namespace gs
{
    public class MeshVectorDisplacement
    {
        public DMesh3 BaseMesh;
        public ISpatial BaseSpatial;
        public IMesh DisplaceMesh;


        /// <summary>
        /// Method that displacement is computed relative to base mesh.
        /// This value cannot be changed after calling Precompute()
        /// </summary>
        public enum EncodingType
        {
            SingleVectorBarycentric,    // displacement a vector in triangle frame at barycentric coords
            NearestTriangleNormals      // displacement is avg of displacements at three normals
        }
        public EncodingType Encoding = EncodingType.SingleVectorBarycentric;


        struct BaryDisplace
        {
            public int tID;            // id of triangle in base mesh
            public float a, b, c;      // barycentric coords in base triangle
            public Vector3f dv;         // displacement vector in frame of base triangle
        }
        BaryDisplace[] BaryFaceDisplacements;


        struct TriVtxNormalsDisplace
        {
            public int tID;
            public Vector3f dv0;
            public Vector3f dv1;
            public Vector3f dv2;
        }
        TriVtxNormalsDisplace[] TriVtxNormalDisplacements;
        Frame3f[] VtxFrames;



        public MeshVectorDisplacement(DMesh3 baseMesh, ISpatial baseSpatial, IMesh displaceMesh)
        {
            BaseMesh = baseMesh;
            BaseSpatial = baseSpatial;
            DisplaceMesh = displaceMesh;
        }

        public void Precompute()
        {
            switch ( Encoding ) {
                case EncodingType.SingleVectorBarycentric:
                    Precompute_SingleVectorBarycentric();
                    break;
                case EncodingType.NearestTriangleNormals:
                    Precompute_NearestTriangleNormals();
                    break;
            }
        }




        public void GetCurrentPositions(Vector3d[] NewPositions)
        {
            if (NewPositions.Length < DisplaceMesh.MaxVertexID)
                throw new Exception("MeshVectorDisplacment.Update: position buffer is too small!");

            switch ( Encoding ) {
                case EncodingType.SingleVectorBarycentric:
                    GetCurrentPositions_SingleVectorBarycentric(NewPositions);
                    break;
                case EncodingType.NearestTriangleNormals:
                    GetCurrentPositions_NearestTriangleNormals(NewPositions);
                    break;
            }
        }




        void update_vertex_frames()
        {
            if ( VtxFrames == null )
                VtxFrames = new Frame3f[BaseMesh.MaxVertexID];
            gParallel.ForEach<int>(BaseMesh.VertexIndices(), (vid) => {
                VtxFrames[vid] = BaseMesh.GetVertexFrame(vid);
            });
        }



        public void Precompute_NearestTriangleNormals()
        {
            int N = DisplaceMesh.MaxTriangleID;
            TriVtxNormalDisplacements = new TriVtxNormalsDisplace[N];

            update_vertex_frames();

            //foreach ( int vid in DisplaceMesh.VertexIndices() ) {
            gParallel.ForEach<int>(DisplaceMesh.VertexIndices(), (vid) => {
                Vector3f pos = (Vector3f)DisplaceMesh.GetVertex(vid);
                int tid = BaseSpatial.FindNearestTriangle(pos);
                Index3i tri = BaseMesh.GetTriangle(tid);

                Vector3f dv0 = VtxFrames[tri.a].ToFrameP(pos);
                Vector3f dv1 = VtxFrames[tri.b].ToFrameP(pos);
                Vector3f dv2 = VtxFrames[tri.c].ToFrameP(pos);

                TriVtxNormalDisplacements[vid] = new TriVtxNormalsDisplace() {
                    tID = tid, dv0 = dv0, dv1 = dv1, dv2 = dv2 
                };
            });
        }


        public void GetCurrentPositions_NearestTriangleNormals(Vector3d[] NewPositions)
        {
            update_vertex_frames();

            gParallel.ForEach<int>(DisplaceMesh.VertexIndices(), (vid) => {
                Index3i tri = BaseMesh.GetTriangle(TriVtxNormalDisplacements[vid].tID);

                Vector3f pos = VtxFrames[tri.a].FromFrameP(TriVtxNormalDisplacements[vid].dv0);
                pos += VtxFrames[tri.b].FromFrameP(TriVtxNormalDisplacements[vid].dv1);
                pos += VtxFrames[tri.c].FromFrameP(TriVtxNormalDisplacements[vid].dv2);

                pos *= (1.0f / 3.0f);

                NewPositions[vid] = pos;
            });
        }





        public void Precompute_SingleVectorBarycentric()
        {
            int N = DisplaceMesh.MaxTriangleID;
            BaryFaceDisplacements = new BaryDisplace[N];

            //foreach ( int vid in DisplaceMesh.VertexIndices() ) {
            gParallel.ForEach<int>(DisplaceMesh.VertexIndices(), (vid) => {
                Vector3d pos = DisplaceMesh.GetVertex(vid);
                int tid = BaseSpatial.FindNearestTriangle(pos);
                DistPoint3Triangle3 dist = MeshQueries.TriangleDistance(BaseMesh, tid, pos);
                Vector3f dv = (Vector3f)(pos - dist.TriangleClosest);
                Frame3f triFrame = BaseMesh.GetTriFrame(tid);
                Vector3f relVec = triFrame.ToFrameV(dv);


                BaryFaceDisplacements[vid] = new BaryDisplace() {
                    tID = tid,
                    a = (float)dist.TriangleBaryCoords.x,
                    b = (float)dist.TriangleBaryCoords.y,
                    c = (float)dist.TriangleBaryCoords.z,
                    dv = relVec
                };
            });
        }


        public void GetCurrentPositions_SingleVectorBarycentric(Vector3d[] NewPositions)
        {
            gParallel.ForEach<int>(DisplaceMesh.VertexIndices(), (vid) => {
                Frame3f triFrame = BaseMesh.GetTriFrame(BaryFaceDisplacements[vid].tID);
                Vector3f offsetV = triFrame.FromFrameV(BaryFaceDisplacements[vid].dv);
                Vector3d triPt = BaseMesh.GetTriBaryPoint(BaryFaceDisplacements[vid].tID,
                    BaryFaceDisplacements[vid].a, BaryFaceDisplacements[vid].b, BaryFaceDisplacements[vid].c);
                NewPositions[vid] = triPt + offsetV;
                //NewPositions[vid] = triPt;
            });
        }


    }
}
