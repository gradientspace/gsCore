// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;

namespace gs
{
    public class MeshFaceSelectionCache
    {
        public DMesh3 Mesh;

        public float NormalOffsetDistance = 0.01f;

        public int ChunkSize = 2048;
        public fMaterial ChunkMeshMaterial;
        public fGameObject ChunkMeshParent = null;


        class MeshChunk
        {
            public fMeshGameObject submesh;
            public int current_count;
        }


        struct OrderedTri
        {
            public int tid;
            public float scalar;
        }
        OrderedTri[] tri_ordering;
        int[] tid_to_order_idx;

        class OrderedChunk
        {
            public MeshChunk mesh;
            public Interval1i order_range;
            public Interval1d scalar_range;
        }
        OrderedChunk[] ordered_chunks;

        int current_partial_chunk = -1;

        float current_scalar_thresh;
        public float CurrentScalarThreshold {
            get { return current_scalar_thresh; }
        }




        public MeshFaceSelectionCache(DMesh3 mesh)
        {
            Mesh = mesh;
        }



        public void InitializeGeodesicDistance(Vector3d source, int source_tid)
        {
            DijkstraGraphDistance dist = new DijkstraGraphDistance(Mesh.MaxTriangleID, false,
                (tid) => { return Mesh.IsTriangle(tid); },
                (a, b) => { return (float)Mesh.GetTriCentroid(a).Distance(Mesh.GetTriCentroid(b)); },
                Mesh.TriTrianglesItr);

            dist.AddSeed(source_tid, (float)Mesh.GetTriCentroid(source_tid).Distance(source));

            //Index3i tri = Mesh.GetTriangle(source_tid);
            //for (int j = 0; j < 3; ++j) {
            //    dist.AddSeed(tri[j], (float)Mesh.GetVertex(tri[j]).Distance(source));
            //}

            dist.TrackOrder = true;
            dist.Compute();

            List<int> order = dist.GetOrder();
            tri_ordering = new OrderedTri[order.Count];
            for (int k = 0; k < order.Count; ++k) {
                tri_ordering[k].tid = order[k];
                tri_ordering[k].scalar = dist.GetDistance(order[k]);
            }

            tid_to_order_idx = new int[Mesh.MaxTriangleID];
            for ( int k = 0; k < order.Count; ++k ) {
                tid_to_order_idx[order[k]] = k;
            }


            // rebuild chunks data structures
            update_ordered_chunks();
        }


        public float GetTriScalar(Vector3d pos, int tid)
        {
            int idx = tid_to_order_idx[tid];
            return tri_ordering[idx].scalar;
        }



        public void UpdateFromOrderedScalars(float scalarThresh)
        {
            current_scalar_thresh = scalarThresh;

            int ci = 0;
            while (ci < ordered_chunks.Length) {
                if (ordered_chunks[ci].scalar_range.Contains(scalarThresh))
                    break;
                ci++;
            }

            // hide any extra chunks past end of where we want to show
            if ( ci < current_partial_chunk ) {
                for ( int k = ci+1; k <= current_partial_chunk; ++k ) {
                    hide_chunk(ordered_chunks[k]);
                }
            }

            // for all chunks before partial, update or show
            for ( int k = 0; k < ci; ++k ) {
                if ( ordered_chunks[k].mesh != null && ordered_chunks[k].mesh.current_count == ChunkSize ) {
                    show_chunk(ordered_chunks[k]);
                } else {
                    update_triangles(ordered_chunks[k], scalarThresh);
                }
            }

            // update partial
            update_triangles(ordered_chunks[ci], scalarThresh);
            current_partial_chunk = ci;
        }





        public void ComputeSelection(MeshFaceSelection selection)
        {
            for ( int ci = 0; ci <= current_partial_chunk; ++ci ) {
                OrderedChunk chunk = ordered_chunks[ci];
                if (chunk.mesh == null)
                    continue;
                for ( int k = 0; k < chunk.mesh.current_count; ++k ) {
                    int idx = chunk.order_range.a + k;
                    selection.Select(tri_ordering[idx].tid);
                }
            }
        }



        public void FindTrianglesInScalarInterval(Interval1d interval, List<int> triangles)
        {
            if (tri_ordering == null)
                throw new InvalidOperationException("MeshFaceSelectionCace.FindTrianglesInScalarInterval: ordering is not computed");
            for ( int k = 0; k < tri_ordering.Length; ++k ) {
                double s = tri_ordering[k].scalar;
                if (s > interval.b)
                    break;
                if ( s > interval.a )
                    triangles.Add(tri_ordering[k].tid);
            }
        }



        // update visible triangles for this chunk
        void update_triangles(OrderedChunk chunk, float max_scalar)
        {
            if (chunk.mesh == null) {
                chunk.mesh = new MeshChunk();
            }

            int count = 0;
            foreach (int idx in chunk.order_range) {
                if (tri_ordering[idx].scalar < max_scalar)
                    count++;
            }

            // if we have the same count, we can keep it
            if (chunk.mesh.current_count == count) {
                chunk.mesh.submesh.SetVisible(true);
                return;
            }

            // find subset triangles
            int[] triangles = new int[count];
            for ( int k = 0; k < count; ++k ) {
                int idx = chunk.order_range.a + k;
                triangles[k] = tri_ordering[idx].tid;
            }

            // find submesh
            // [TODO] faster variant of this? Also could be computing these in background...
            DSubmesh3 submesh = new DSubmesh3(Mesh, triangles);
            MeshTransforms.VertexNormalOffset(submesh.SubMesh, NormalOffsetDistance);
            fMesh umesh = UnityUtil.DMeshToUnityMesh(submesh.SubMesh, false);

            // create or update GO
            if (chunk.mesh.submesh == null) {
                chunk.mesh.submesh = new fMeshGameObject(umesh, true, false);
                if (ChunkMeshMaterial != null)
                    chunk.mesh.submesh.SetMaterial(ChunkMeshMaterial);
                if (ChunkMeshParent != null)
                    ChunkMeshParent.AddChild(chunk.mesh.submesh, false);
            } else {
                chunk.mesh.submesh.UpdateMesh(umesh, true, false);
            }

            chunk.mesh.submesh.SetVisible(true);
            chunk.mesh.current_count = count;
        }



        void show_chunk(OrderedChunk chunk)
        {
            if (chunk.mesh != null && chunk.mesh.submesh != null)
                chunk.mesh.submesh.SetVisible(true);
        }
        void hide_chunk(OrderedChunk chunk)
        {
            if (chunk.mesh != null && chunk.mesh.submesh != null)
                chunk.mesh.submesh.SetVisible(false);
        }




        void discard_ordered_chunks()
        {
            if ( ordered_chunks != null ) {
                foreach (var chunk in ordered_chunks) {
                    if (chunk != null && chunk.mesh != null) {
                        discard_chunk(chunk.mesh);
                        chunk.mesh = null;
                    }
                }
            }
            ordered_chunks = null;
        }
        void discard_chunk(MeshChunk m)
        {
            m.submesh.Destroy();
            m.submesh = null;
            m.current_count = 0;
        }



        void update_ordered_chunks()
        {
            if (ordered_chunks != null)
                discard_ordered_chunks();

            int N = tri_ordering.Length / ChunkSize;
            if (N == 0 || tri_ordering.Length % ChunkSize != 0)
                N++;

            ordered_chunks = new OrderedChunk[N];
            ordered_chunks[0] = new OrderedChunk() { order_range = Interval1i.Empty, scalar_range = Interval1d.Empty };
            int ci = 0, cn = 0;
            for ( int k = 0; k < tri_ordering.Length; ++k ) {
                if ( cn == ChunkSize ) {
                    ci++;
                    ordered_chunks[ci] = new OrderedChunk() { order_range = Interval1i.Empty, scalar_range = Interval1d.Empty };
                    cn = 0;
                }
                ordered_chunks[ci].order_range.Contain(k);
                ordered_chunks[ci].scalar_range.Contain(tri_ordering[k].scalar);
                cn++;
            }
            
        }






    }
}
