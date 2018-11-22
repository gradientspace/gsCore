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
    /// Set of geometry objects for boundary curves of a mesh.
    /// Initialize with a MeshBoundaryLoops.
    /// Necessary tube objects will be dynamically generated.
    /// 
    /// TODO:
    ///    - implement mode that uses fGraphGameObject and tube shader
    ///    - generate per-curve spatial DS on-demand or in background thread?
    ///      only need if user clicks within curve bbox...
    ///    - use OBB instead of AABB for bounds
    ///    - custom tube mesh generator optimized for this use-case, generates unity mesh
    /// 
    /// </summary>
    public class BoundaryCurveSet
    {
        struct Curve
        {
            public int id;
            public bool visible;

            public DCurve3 curve;
            public AxisAlignedBox3d bounds;
            public DCurve3BoxTree spatial;

            public fMeshGameObject tubeMeshGO;
        }

        int slices = 6;
        public int Slices {
            get { return slices; }
            set { if (slices != value) set_slices(value); }
        }

        double radius = 1.0;
        public double Radius {
            get { return radius; }
            set { if (! MathUtil.EpsilonEqual(radius,value,MathUtil.ZeroTolerancef)) set_radius(value); }
        }


        fGameObject parentGO;
        fMaterial curveMaterial;
        Curve[] CurveSet;
        bool geometry_valid = false;


        public BoundaryCurveSet(fGameObject parentGO, fMaterial curveMaterial)
        {
            this.parentGO = parentGO;
            this.curveMaterial = curveMaterial;
        }


        public void Initialize(MeshBoundaryLoops loops)
        {
            if (CurveSet != null)
                Clear();
            CurveSet = new Curve[loops.Count];

            for (int i = 0; i < loops.Count; ++i ) { 
                Curve c = new Curve();
                c.id = i;
                c.curve = loops[i].ToCurve().ResampleSharpTurns();
                c.bounds = c.curve.GetBoundingBox();
                c.spatial = new DCurve3BoxTree(c.curve);
                c.visible = true;
                CurveSet[i] = c;
            }
            invalidate_geometry();
        }



        /// <summary>
        /// try to find intersection of ray with curves
        /// </summary>
        public int FindRayIntersection(Ray3d ray, double useRadius = -1)
        {
            double r = (useRadius > 0) ? useRadius : Radius;

            double nearest_t = double.MaxValue;
            int hit_i = -1;
            for (int i = 0; i < CurveSet.Length; ++i) {
                if (CurveSet[i].visible == false)
                    continue;
                if (IntrRay3AxisAlignedBox3.Intersects(ref ray, ref CurveSet[i].bounds, r) == false)
                    continue;

                int hitSeg; double ray_t;
                if ( CurveSet[i].spatial.FindClosestRayIntersction(ray, r, out hitSeg, out ray_t) ) {
                    if (ray_t < nearest_t) {
                        nearest_t = ray_t;
                        hit_i = i;
                    }
                }

                // brute-force version
                //double ray_t;
                //if ( CurveUtils.FindClosestRayIntersection(CurveSet[i].curve, r, ray, out ray_t )) {
                //    if ( ray_t < nearest_t ) {
                //        nearest_t = ray_t;
                //        hit_i = i;
                //    }
                //}
            }
            return hit_i;
        }


        public void SetLayer(int layer)
        {
            foreach (Curve c in CurveSet)
                c.tubeMeshGO.SetLayer(layer, true);
        }


        public void SetVisibility(int loopi, bool bVisible)
        {
            CurveSet[loopi].visible = bVisible;
            if (CurveSet[loopi].tubeMeshGO != null) {
                CurveSet[loopi].tubeMeshGO.SetVisible(bVisible);
            }
        }

        public void PreRender()
        {
            if (geometry_valid)
                return;

            validate_tube_meshes();
            geometry_valid = true;
        }


        public void Clear()
        {
            foreach (Curve c in CurveSet) {
                if ( c.tubeMeshGO != null ) {
                    c.tubeMeshGO.Destroy();
                }
            }
            CurveSet = null;
        }


        void set_radius(double r)
        {
            radius = r;
            invalidate_geometry();
        }

        void set_slices(int n)
        {
            slices = n;
            invalidate_geometry();
        }


        void invalidate_geometry()
        {
            geometry_valid = false;
        }


        void validate_tube_meshes()
        {
            Polygon2d circle = Polygon2d.MakeCircle(Radius, Slices);
            for (int i = 0; i < CurveSet.Length; ++i ) {
                TubeGenerator gen = new TubeGenerator(CurveSet[i].curve, circle) { NoSharedVertices = false };
                gen.Generate();
                CurveSet[i].tubeMeshGO = GameObjectFactory.CreateMeshGO("tube_" + i.ToString(),
                    gen.MakeUnityMesh(), false, true);
                CurveSet[i].tubeMeshGO.SetMaterial(curveMaterial, true);
                parentGO.AddChild(CurveSet[i].tubeMeshGO, false);
            }
        }


    }
}
