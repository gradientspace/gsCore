// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using g3;
using f3;

namespace gs
{
    public class CurveSnapGenerator : StandardSnapGenerator
    {
        public CurveSnapGenerator() : base()
        {
        }

        public CurveSnapGenerator(Material visibleMaterial, Material hiddenMaterial)
            : base(visibleMaterial, hiddenMaterial)
        {
            parent.SetName("CurveSnapGenerator_parent");
        }


        public override bool CanGenerate(SceneObject so)
        {
            return so is PolyCurveSO;
        }

        public override List<ISnapPoint> GeneratePoints(SceneObject so)
        {
            List<ISnapPoint> v = new List<ISnapPoint>();

            if (so is PolyCurveSO) {
                PolyCurveSO curveSO = so as PolyCurveSO;
                DCurve3 curve = curveSO.Curve;

                Frame3f f = Frame3f.Identity;

                Frame3f startFrame = new Frame3f( (Vector3f)curve.Start, -(Vector3f)curve.Tangent(0), 1 );
                v.Add(new SOFrameSnapPoint(curveSO) { frame = f.FromFrame(startFrame) } );
                Frame3f endFrame = new Frame3f((Vector3f)curve.End, (Vector3f)curve.Tangent(curve.VertexCount-1), 1 );
                v.Add(new SOFrameSnapPoint(curveSO) { frame = f.FromFrame(endFrame)  } );
            }

            if (base.EnableGeometry)
                base.build_geometry(so, v);

            return v;
        }


        public override List<ISnapSegment> GenerateSegments(SceneObject so)
        {
            return null;
        }

    }
}
