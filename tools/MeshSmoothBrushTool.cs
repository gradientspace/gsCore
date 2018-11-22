using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using f3;

namespace gs
{
    public class MeshSmoothBaseBrushToolBuilder : SurfaceBrushToolBuilder
    {
        public Action<UpdateVerticesChange> EmitChangeF = null;

        protected override SurfaceBrushTool new_tool(FScene scene, DMeshSO target)
        {
            return new MeshSmoothBaseBrushTool(scene, target) {
                EmitChangeF = this.EmitChangeF
            };
        }
    }



    public class MeshSmoothBaseBrushTool : SurfaceBrushTool
    {
        static readonly public string SubIdentifier = "mesh_smooth_brush";

        override public string Name {
            get { return "MeshSmoothBaseBrush"; }
        }
        override public string TypeIdentifier {
            get { return SubIdentifier; }
        }

        public override SurfaceBrushType PrimaryBrush {
            set {
                base.PrimaryBrush = value;
            }
        }

        public override SurfaceBrushType SecondaryBrush {
            set {
                base.SecondaryBrush = value;
            }
        }


        public Action<UpdateVerticesChange> EmitChangeF = null;


        public MeshSmoothBaseBrushTool(FScene scene, DMeshSO target) : base(scene, target)
        {
        }


        public override void Setup()
        {
            base.Setup();

            PrimaryBrush = new StandardLaplacianMeshSmoothBrush();
            SecondaryBrush = new StandardLaplacianMeshSmoothBrush();
        }

        VertexChangeBuilder activeChange;

        protected override void begin_stroke(Frame3f vStartFrameL, int nHitTID)
        {
            if ( activeChange == null )
                activeChange = new VertexChangeBuilder(Target.Mesh);

            base.begin_stroke(vStartFrameL, nHitTID);
        }

        protected override void update_stroke(Frame3f vLocalF, int nHitTID)
        {
            base.update_stroke(vLocalF, nHitTID);
            (base.ActiveBrush as MeshSmoothBaseBrush).BakeDisplacements(Target, activeChange);
        }

        protected override void end_stroke()
        {
            base.end_stroke();

            activeChange.SaveCurrentNormals();
            if (EmitChangeF != null) {
                ModifyVerticesMeshChange change = activeChange.ExtractChange();
                UpdateVerticesChange soChange = new UpdateVerticesChange(Target, change);
                EmitChangeF(soChange);
            } else
                activeChange.Reset();

        }

    }




    public abstract class MeshSmoothBaseBrush : SurfaceBrushType
    {
        public double Power = 0.3;

        DijkstraGraphDistance dist;
        DijkstraGraphDistance Dijkstra {
            get {
                if (dist == null) {
                    dist = new DijkstraGraphDistance(Mesh.MaxVertexID, false, Mesh.IsVertex, vertex_dist, Mesh.VtxVerticesItr);
                    dist.TrackOrder = true;
                }
                return dist;
            }
        }


        float vertex_dist(int a, int b)
        {
            return (float)Mesh.GetVertex(a).Distance(Mesh.GetVertex(b));
        }


        public virtual void BakeDisplacements(DMeshSO so, VertexChangeBuilder changeBuilder)
        {
            so.EditAndUpdateMesh((mesh) => {
                foreach (int vid in ModifiedV)
                    changeBuilder.SetPosition(vid, Displacements[vid]);
                gParallel.ForEach(ModifiedV, (vid) => {
                    MeshNormals.QuickCompute(Mesh, vid);
                });
            }, GeometryEditTypes.VertexDeformation);
        }

        DVector<Vector3d> Displacements = new DVector<Vector3d>();
        HashSet<int> ModifiedV = new HashSet<int>();

        public override void Apply(Frame3f vNextPos, int tid)
        {

            if (Mesh.MaxVertexID > Displacements.Length)
                Displacements.resize(Mesh.MaxVertexID);
            ModifiedV.Clear();

            DijkstraGraphDistance dj = Dijkstra;
            dj.Reset();

            Index3i ti = Mesh.GetTriangle(tid);
            Vector3d c = Mesh.GetTriCentroid(tid);
            dj.AddSeed(ti.a, (float)Mesh.GetVertex(ti.a).Distance(c));
            dj.AddSeed(ti.b, (float)Mesh.GetVertex(ti.b).Distance(c));
            dj.AddSeed(ti.c, (float)Mesh.GetVertex(ti.c).Distance(c));
            double compute_Dist = MathUtil.SqrtTwo * Radius;
            dj.ComputeToMaxDistance((float)compute_Dist);

            ApplyCurrentStamp(vNextPos, tid, dj, Displacements, ModifiedV);
        }


        /// <summary>
        /// Subclass implements this
        /// </summary>
        protected abstract void ApplyCurrentStamp(Frame3f vCenter, int tid, DijkstraGraphDistance dj, 
            DVector<Vector3d> Displacements, HashSet<int> ModifiedV );
    }



    /// <summary>
    /// This brush adds a soft radial displacement along the center triangle face normal
    /// </summary>
    public class StandardLaplacianMeshSmoothBrush : MeshSmoothBaseBrush
    {
        protected override void ApplyCurrentStamp(Frame3f vCenter, int tid, DijkstraGraphDistance dj,
            DVector<Vector3d> Displacements, HashSet<int> ModifiedV)
        {
            List<int> order = dj.GetOrder();
            int N = order.Count;
            for (int k = 0; k < N; ++k) {
                int vid = order[k];
                Vector3d v = Mesh.GetVertex(vid);

                double d = v.Distance(vCenter.Origin);
                double t = MathUtil.Clamp(d / Radius, 0.0, 1.0);
                t = MathUtil.WyvillFalloff01(t);

                Vector3d c = Vector3d.Zero;
                Mesh.VtxOneRingCentroid(vid, ref c);

                t *= Power;
                Vector3d s = Vector3d.Lerp(ref v, ref c, t);

                Displacements[vid] = s;
                ModifiedV.Add(vid);
            }
        }
    }



}
