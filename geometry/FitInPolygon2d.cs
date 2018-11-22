// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using g3;

namespace gs
{
    public class FitInPolygon2d
    {
        public GeneralPolygon2d Target;
        public Polygon2d Fit;

        public double ApplyScale = 1.0;

        public bool EnableTranslate = true;

        public bool EnableRotate = true;
        public bool EnableRandomRotations = false;
        public double[] ValidRotationsDeg = new double[] { 0, 10, 20, 30, 45, 60, 90 };

        // only center/axes of this are relevant...
        public Box2d FitBounds;
        public Matrix2d FitRotation;
        public Polygon2d FitPolygon;


        Vector2d InputTranslate = Vector2d.Zero;


        public FitInPolygon2d()
        {
        }



        public bool Solve(int MaxRounds = 1024)
        {
            AxisAlignedBox2d TargetBounds = Target.Bounds;

            Box2d fitBounds = new Box2d(Fit.GetBounds());
            InputTranslate = -fitBounds.Center;
            int N = Fit.VertexCount;

            Polygon2d FitCentered = new Polygon2d(Fit);
            for ( int k = 0; k < N; ++k ) {
                FitCentered[k] = Fit[k] + InputTranslate;
            }
            fitBounds = new Box2d(FitCentered.GetBounds());


            Polygon2d FitXForm = new Polygon2d(FitCentered);

            Random r = new Random(31337);

            int ri = 0;
            bool solved = false;
            while (ri++ < MaxRounds && solved == false) {

                Vector2d center = random_point_in_target(r, TargetBounds.SampleT, Target.Contains);

                Matrix2d rotate = Matrix2d.Identity;
                if ( EnableRotate ) {
                    Util.gDevAssert(EnableRandomRotations == false);
                    double rotAngle = ValidRotationsDeg[r.Next() % ValidRotationsDeg.Length];
                    rotate.SetToRotationDeg(rotAngle);
                }

                for ( int k = 0; k < N; ++k ) {
                    FitXForm[k] = rotate * (ApplyScale * FitCentered[k]) + center;
                }

                if (!Target.Outer.Contains(FitXForm))
                    continue;

                if (Target.Intersects(FitXForm))
                    continue;

                FitBounds = fitBounds;
                FitBounds.RotateAxes(rotate);
                FitBounds.Translate(center);
                FitRotation = rotate;
                FitPolygon = FitXForm;
                solved = true;
            }

            return solved;
        }
        


        public Vector2d ApplyFitTransform(Vector2d pt)
        {
            pt = pt + InputTranslate;
            pt = FitRotation * (ApplyScale * pt) + FitBounds.Center;
            return pt;
        }




        Vector2d random_point_in_target(Random r, Func<double, double, Vector2d> sampleSpaceF, Func<Vector2d,bool> inTargetF)
        {
            while (true) {
                double a = r.NextDouble(), b = r.NextDouble();
                Vector2d p = sampleSpaceF(a, b);
                if (inTargetF(p))
                    return p;
            }
        }


    }


}

