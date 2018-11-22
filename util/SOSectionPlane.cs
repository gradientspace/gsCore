using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using f3;

namespace gs
{
    /// <summary>
    /// Computes section-plane curves of a DMeshSO.
    /// Call UpdateSection()
    /// 
    /// 
    /// [TODO] 
    ///    - compute in separate thread? or on a copy? (mapping is hard though)
    /// </summary>
    public class SOSectionPlane
    {
        public DMeshSO SO;

        /// <summary>
        /// What space should output curves be in? default is scene space.
        /// </summary>
        public CoordSpace OutputSpace = CoordSpace.SceneCoords;


        public SOSectionPlane(SceneObject so)
        {
            if (so is DMeshSO == false)
                throw new Exception("SOSectionPlane: only DMeshSO currently supported");

            SO = so as DMeshSO;
        }


        Frame3f frameL;
        DGraph3 graph;
        DGraph3Util.Curves localCurves;

        public void UpdateSection(Frame3f frame, CoordSpace space)
        {
            if (space != CoordSpace.ObjectCoords)
                frameL = SceneTransforms.TransformTo(frame, SO, space, CoordSpace.ObjectCoords);
            else
                frameL = frame;
            update();
        }


        void update()
        {
            MeshIsoCurves iso = new MeshIsoCurves(SO.Mesh, (v) => {
                return (v - frameL.Origin).Dot(frameL.Z);
            });
            iso.Compute();
            graph = iso.Graph;
            localCurves = DGraph3Util.ExtractCurves(graph);

            // ugh need xform seq for to/from world...
            if ( OutputSpace == CoordSpace.WorldCoords ) { 
                foreach (DCurve3 c in localCurves.Loops) {
                    for (int i = 0; i < c.VertexCount; ++i)
                        c[i] = SceneTransforms.TransformTo((Vector3f)c[i], SO, CoordSpace.ObjectCoords, OutputSpace);
                }
                foreach (DCurve3 c in localCurves.Paths) {
                    for (int i = 0; i < c.VertexCount; ++i)
                        c[i] = SceneTransforms.TransformTo((Vector3f)c[i], SO, CoordSpace.ObjectCoords, OutputSpace);
                }
            } else if (OutputSpace == CoordSpace.SceneCoords) {
                TransformSequence xform = SceneTransforms.ObjectToSceneXForm(SO);
                foreach (DCurve3 c in localCurves.Loops) {
                    for (int i = 0; i < c.VertexCount; ++i)
                        c[i] = xform.TransformP(c[i]);
                }
                foreach (DCurve3 c in localCurves.Paths) {
                    for (int i = 0; i < c.VertexCount; ++i)
                        c[i] = xform.TransformP(c[i]);
                }
            }
        }



        /// <summary>
        /// available after call to UpdateSection()
        /// </summary>
        public List<DCurve3> GetSectionCurves()
        {
            var curves = new List<DCurve3>();
            if (localCurves.Loops != null)
                curves.AddRange(localCurves.Loops);
            if (localCurves.Paths != null)
                curves.AddRange(localCurves.Paths);
            return curves;
        }


        public List<Polygon2d> GetPolygons()
        {
            Frame3f f = frameL;
            if (OutputSpace != CoordSpace.ObjectCoords)
                f = SceneTransforms.TransformTo(f, SO, CoordSpace.ObjectCoords, OutputSpace);

            List<Polygon2d> polygons = new List<Polygon2d>();
            foreach (DCurve3 c in localCurves.Loops) {
                Polygon2d poly = new Polygon2d();
                for (int i = 0; i < c.VertexCount; ++i) {
                    Vector2f uv = f.ToPlaneUV((Vector3f)c[i], 2);
                    poly.AppendVertex(uv);
                }
                polygons.Add(poly);
            }
            return polygons;
        }


        
        public List<GeneralPolygon2d> GetSolids()
        {
            Frame3f f = frameL;
            if (OutputSpace != CoordSpace.ObjectCoords)
                f = SceneTransforms.TransformTo(f, SO, CoordSpace.ObjectCoords, OutputSpace);

            PlanarComplex complex = new PlanarComplex();
            foreach (DCurve3 c in localCurves.Loops) {
                Polygon2d poly = new Polygon2d();
                for (int i = 0; i < c.VertexCount; ++i) {
                    Vector2f uv = f.ToPlaneUV((Vector3f)c[i], 2);
                    poly.AppendVertex(uv);
                }
                complex.Add(poly);
            }
            PlanarComplex.FindSolidsOptions options = PlanarComplex.FindSolidsOptions.SortPolygons;
            var info = complex.FindSolidRegions(options);
            return info.Polygons;
        }




        /// <summary>
        /// available after call to UpdateSection()
        /// </summary>
        public DMesh3 GetSectionMesh(double simplifyTol = 0.01)
        {
            DMesh3 mesh = new DMesh3();
            if (localCurves.Loops == null)
                return mesh;

            List<GeneralPolygon2d> solids = GetSolids();
            foreach ( GeneralPolygon2d poly in solids ) {
                poly.Simplify(simplifyTol, simplifyTol / 10, true);
                TriangulatedPolygonGenerator gen = new TriangulatedPolygonGenerator() {
                    Polygon = poly
                };
                DMesh3 polyMesh = gen.Generate().MakeDMesh();
                MeshTransforms.PerVertexTransform(polyMesh, (uv) => {
                    return frameL.FromPlaneUV((Vector2f)uv.xy, 2);
                });
                MeshEditor.Append(mesh, polyMesh);
            }

            if (OutputSpace != CoordSpace.ObjectCoords ) {
                MeshTransforms.PerVertexTransform(mesh, (v) => {
                    return SceneTransforms.TransformTo((Vector3f)v, SO, CoordSpace.ObjectCoords, OutputSpace);
                });
            }

            return mesh;
        }


    }
}
