using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;

namespace gs
{
    public class CurveDrawingUtil
    {


        public static void ProjectToTarget_Scene(IList<Vector3d> vertices, FScene scene, IProjectionTarget target)
        {
            int N = vertices.Count;
            for (int i = 0; i < N; ++i) {
                Vector3f v = (Vector3f)vertices[i];
                Vector3f vW = SceneTransforms.SceneToWorldP(scene, v);
                vW = (Vector3f)target.Project(vW);
                vertices[i] = SceneTransforms.WorldToSceneP(scene, vW);
            }
        }




        public static Action<List<Vector3d>> MakeLoopOnSurfaceProcessorF(FScene scene, IProjectionTarget surface, 
            Func<float> SampleRateF, Func<bool> ClosedF, 
            float fSmoothAlpha = 0.2f, int nSmoothIter = 15 )
        {
            return (vertices) => {
                CurveResampler resampler = new CurveResampler();
                IWrappedCurve3d temp_curve = new IWrappedCurve3d() { VertexList = vertices, Closed = ClosedF() };
                float rate = SampleRateF();
                List<Vector3d> result = resampler.SplitCollapseResample(temp_curve, rate, rate * 0.6);
                if (result != null && result.Count > 3) {
                    vertices.Clear();
                    vertices.AddRange(result);
                }
                CurveUtils.InPlaceSmooth(vertices, fSmoothAlpha, nSmoothIter, ClosedF());
                gs.CurveDrawingUtil.ProjectToTarget_Scene(vertices, scene, surface);
            };
        }



    }
}
