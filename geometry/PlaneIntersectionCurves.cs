// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    public class PlaneIntersectionCurves
    {
        DMesh3 Mesh;

        Frame3f Plane;
        int nPlaneAxis;

        public double NormalOffset = 0.0;

        /*
         * Outputs
         */

        public DCurve3[] Loops;

        public PlaneIntersectionCurves(DMesh3 mesh, Frame3f plane, int nPlaneAxis)
        {
            Mesh = mesh;
            Plane = plane;
            this.nPlaneAxis = nPlaneAxis;
        }


        public bool Compute()
        {
            DMesh3 copy = new DMesh3(Mesh);

            //Frame3f PlaneO = SceneTransforms.SceneToObject(TargetSO, PlaneS);
            Vector3f PlaneNormal = Plane.GetAxis(nPlaneAxis);

            MeshPlaneCut cut = new MeshPlaneCut(copy, Plane.Origin, PlaneNormal);
            cut.Cut();

            Loops = new DCurve3[cut.CutLoops.Count];
            for (int li = 0; li < cut.CutLoops.Count; ++li) {

                EdgeLoop edgeloop = cut.CutLoops[li];
                DCurve3 loop = MeshUtil.ExtractLoopV(copy, edgeloop.Vertices);

                // [TODO] collapse degenerate points...

                if (NormalOffset > 0) {
                    for (int i = 0; i < loop.VertexCount; ++i) {
                        Vector3f n = Vector3f.Zero;
                        if ( copy.HasVertexNormals ) {
                            n = (Vector3f)copy.GetVertexNormal(edgeloop.Vertices[i]);
                        } else {
                            n = (Vector3f)MeshNormals.QuickCompute(Mesh, edgeloop.Vertices[i]);
                        }

                        n -= n.Dot(PlaneNormal) * PlaneNormal;
                        n.Normalize();
                        loop[i] += NormalOffset * (Vector3d)n;
                    }
                }

                Loops[li] = loop;
            }

            return Loops.Length > 0;
        }

    }
}
