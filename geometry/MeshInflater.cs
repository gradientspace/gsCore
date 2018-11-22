// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using g3;

namespace gs
{
    public class MeshInflater
    {
        public Polygon2d Loop;
        public double TargetEdgeLength = 0;
        public bool ReverseOrientation = false;
        public double Thickness = 0.1f;

        DMesh3 PlanarMesh;
        DMesh3 InflatedMesh;
        public DMesh3 ResultMesh;


        public MeshInflater(Polygon2d input_loop)
        {
            Loop = new Polygon2d(input_loop);
        }


        public bool Compute()
        {
            PlanarMesh = BuildPlanarMesh(false);
            InflatedMesh = ComputeInflation(PlanarMesh);

            DMesh3 remeshed = GenerateRemesh(InflatedMesh);
            Flatten(remeshed);

            ResultMesh = ComputeInflation(remeshed);

            MeshBoundaryLoops loops = new MeshBoundaryLoops(ResultMesh);

            DMesh3 otherSide = new DMesh3(ResultMesh);
            foreach ( int vid in otherSide.VertexIndices() ) {
                Vector3d v = otherSide.GetVertex(vid);
                v.z = -v.z;
                otherSide.SetVertex(vid, v);
            }
            otherSide.ReverseOrientation();

            MeshEditor editor = new MeshEditor(ResultMesh);
            int[] mapVArray;
            editor.AppendMesh(otherSide, out mapVArray);
            IndexMap mapV = new IndexMap(mapVArray);

            foreach ( EdgeLoop loop in loops ) {
                int[] otherLoop = (int[])loop.Vertices.Clone();
                IndexUtil.Apply(otherLoop, mapV);
                editor.StitchLoop(loop.Vertices, otherLoop);
            }


            Remesher remesh = new Remesher(ResultMesh);
            remesh.SetTargetEdgeLength(TargetEdgeLength);
            remesh.SmoothSpeedT = 0.25f;
            for (int k = 0; k < 10; ++k)
                remesh.BasicRemeshPass();
            ResultMesh = new DMesh3(ResultMesh, true);


            LaplacianMeshSmoother smoother = new LaplacianMeshSmoother(ResultMesh);
            foreach (int vid in ResultMesh.VertexIndices())
                smoother.SetConstraint(vid, ResultMesh.GetVertex(vid), 0.5f, false);
            smoother.SolveAndUpdateMesh();


            return true;
        }



        DMesh3 BuildPlanarMesh(bool bPreservePolygon)
        {
            DMesh3 planarMesh = new DMesh3();

            Vector2d center = CurveUtils2.CentroidVtx(Loop.Vertices);
            int center_id = planarMesh.AppendVertex(new Vector3d(center.x, center.y, 0));

            int prev_id = -1;
            int first_id = -1;
            foreach ( Vector2d v in Loop.Vertices ) {
                int next_id = planarMesh.AppendVertex(new Vector3d(v.x, v.y, Thickness));
                if (prev_id > 0) {
                    planarMesh.AppendTriangle(center_id, prev_id, next_id);
                    prev_id = next_id;
                } else {
                    first_id = next_id;
                    prev_id = next_id;
                }
            }
            planarMesh.AppendTriangle(center_id, prev_id, first_id);

            if (ReverseOrientation)
                planarMesh.ReverseOrientation();

            Debug.Assert(planarMesh.CheckValidity());


            double edge_len = (TargetEdgeLength == 0) ? Loop.AverageEdgeLength : TargetEdgeLength;
            Remesher r = new Remesher(planarMesh);
            r.SetTargetEdgeLength(edge_len);
            r.SmoothSpeedT = 1.0f;
            if (bPreservePolygon) {
                MeshConstraintUtil.FixAllBoundaryEdges(r);
            } else {
                MeshConstraintUtil.PreserveBoundaryLoops(r);
            }

            for (int k = 0; k < 20; ++k)
                r.BasicRemeshPass();

            return planarMesh;
        }



        DMesh3 ComputeInflation(DMesh3 planarMesh)
        {
            DMesh3 mesh = new DMesh3(planarMesh);

            DijkstraGraphDistance dist = new DijkstraGraphDistance(
                mesh.MaxVertexID, false,
                (vid) => { return mesh.IsVertex(vid); },
                (a, b) => { return (float)mesh.GetVertex(a).Distance(mesh.GetVertex(b)); },
                mesh.VtxVerticesItr);

            foreach (int vid in MeshIterators.BoundaryVertices(mesh))
                dist.AddSeed(vid, 0);

            dist.Compute();
            float max_dist = dist.MaxDistance;

            float[] distances = new float[mesh.MaxVertexID];
            foreach (int vid in mesh.VertexIndices())
                distances[vid] = dist.GetDistance(vid);


            List<int> local_maxima = new List<int>();
            foreach ( int vid in MeshIterators.InteriorVertices(mesh)) {
                float d = distances[vid];
                bool is_maxima = true;
                foreach ( int nbrid in mesh.VtxVerticesItr(vid)) {
                    if (distances[nbrid] > d)
                        is_maxima = false;
                }
                if ( is_maxima )
                    local_maxima.Add(vid);
            }

            // smooth distances   (really should use cotan here!!)
            float smooth_alpha = 0.1f;
            int smooth_rounds = 5;
            foreach (int ri in Interval1i.Range(smooth_rounds)) {
                foreach ( int vid in mesh.VertexIndices()) {
                    float cur = distances[vid];
                    float centroid = 0;
                    int nbr_count = 0;
                    foreach (int nbrid in mesh.VtxVerticesItr(vid)) {
                        centroid += distances[nbrid];
                        nbr_count++;
                    }
                    centroid /= nbr_count;
                    distances[vid] = (1 - smooth_alpha) * cur + (smooth_alpha) * centroid;
                }
            }

            Vector3d normal = Vector3d.AxisZ;
            foreach ( int vid in mesh.VertexIndices() ) {
                if (dist.IsSeed(vid))
                    continue;
                float h = distances[vid];

                // [RMS] there are different options here...
                h = 2* (float)Math.Sqrt(h);

                float offset = h;
                Vector3d d = mesh.GetVertex(vid);
                mesh.SetVertex(vid, d + (Vector3d)(offset * normal));
            }


            DMesh3 compacted = new DMesh3();
            var compactInfo = compacted.CompactCopy(mesh);
            IndexUtil.Apply(local_maxima, compactInfo.MapV);
            mesh = compacted;
            MeshVertexSelection boundary_sel = new MeshVertexSelection(mesh);
            HashSet<int> boundaryV = new HashSet<int>(MeshIterators.BoundaryVertices(mesh));
            boundary_sel.Select(boundaryV);
            boundary_sel.ExpandToOneRingNeighbours();

            LaplacianMeshSmoother smoother = new LaplacianMeshSmoother(mesh);
            foreach ( int vid in boundary_sel) {
                if (boundaryV.Contains(vid))
                    smoother.SetConstraint(vid, mesh.GetVertex(vid), 100.0f, true);
                else
                    smoother.SetConstraint(vid, mesh.GetVertex(vid), 10.0f, false);
            }
            foreach (int vid in local_maxima)
                smoother.SetConstraint(vid, mesh.GetVertex(vid), 50, false);

            bool ok = smoother.SolveAndUpdateMesh();
            Util.gDevAssert(ok);


            List<int> intVerts = new List<int>(MeshIterators.InteriorVertices(mesh));
            MeshIterativeSmooth smooth = new MeshIterativeSmooth(mesh, intVerts.ToArray(), true);
            smooth.SmoothType = MeshIterativeSmooth.SmoothTypes.Cotan;
            smooth.Rounds = 10;
            smooth.Alpha = 0.1f;
            smooth.Smooth();

            return mesh;
        }



        DMesh3 GenerateRemesh(DMesh3 mesh)
        {
            DMesh3 remeshed = new DMesh3(mesh);

            DMeshAABBTree3 project = new DMeshAABBTree3(mesh);
            project.Build();
            MeshProjectionTarget Target = new MeshProjectionTarget(project.Mesh, project);

            double minlen, maxlen, avglen;
            MeshQueries.EdgeLengthStats(mesh, out minlen, out maxlen, out avglen);
            double edge_len = (TargetEdgeLength == 0) ? Loop.AverageEdgeLength : avglen;

            Remesher r = new Remesher(remeshed);
            r.SetTargetEdgeLength(edge_len);
            r.SetProjectionTarget(Target);
            MeshConstraintUtil.FixAllBoundaryEdges(r);

            for (int k = 0; k < 20; ++k)
                r.BasicRemeshPass();

            return remeshed;
        }


        void Flatten(DMesh3 meshIn)
        {
            foreach ( int vid in meshIn.VertexIndices() ) {
                Vector3d v = meshIn.GetVertex(vid);
                v.z = Thickness;
                meshIn.SetVertex(vid, v);
            }
        }


    }
}
