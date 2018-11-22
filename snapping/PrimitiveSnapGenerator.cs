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

    public class PrimitiveSnapGenerator : StandardSnapGenerator
    {
        public PrimitiveSnapGenerator() : base()
        {
        }

        public PrimitiveSnapGenerator(Material visibleMaterial, Material hiddenMaterial)
            : base(visibleMaterial, hiddenMaterial)
        {
            parent.SetName("PrimitiveSnapGenerator_parent");
        }

        override public bool CanGenerate(SceneObject so)
        {
            return (so is PrimitiveSO);
        }

        override public List<ISnapPoint> GeneratePoints(SceneObject so)
        {
            List<ISnapPoint> v = new List<ISnapPoint>();

            if (so is CylinderSO) {
                CylinderSO cyl = so as CylinderSO;
                Frame3f f = Frame3f.Identity;
                v.Add(new SOFrameSnapPoint(cyl) { IsSurface = false, frame = f });
                v.Add(new SOFrameSnapPoint(cyl) { frame = f.Translated(cyl.Height * 0.5f * f.Y) });
                v.Add(new SOFrameSnapPoint(cyl) { frame = f.Translated(-cyl.Height * 0.5f * f.Y) });

                // face-centers
                float fR = cyl.Radius;
                v.Add(new SOFrameSnapPoint(cyl) { frame = f.Translated(fR * f.X).Rotated(-90.0f, 2) });
                v.Add(new SOFrameSnapPoint(cyl) { frame = f.Translated(-fR * f.X).Rotated(90.0f, 2) });
                v.Add(new SOFrameSnapPoint(cyl) { frame = f.Translated(fR * f.Z).Rotated(90.0f, 0) });
                v.Add(new SOFrameSnapPoint(cyl) { frame = f.Translated(-fR * f.Z).Rotated(-90.0f, 0) });

            } else if (so is BoxSO) {
                BoxSO box = so as BoxSO;
                Frame3f f = Frame3f.Identity;

                // object center
                v.Add(new SOFrameSnapPoint(box) { IsSurface = false, frame = f });

                // face centers
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated( box.Height* 0.5f * f.Y ) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(-box.Height* 0.5f * f.Y).Rotated(180.0f,0) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(box.Width * 0.5f * f.X).Rotated(-90.0f, 2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(-box.Width * 0.5f * f.X).Rotated(90.0f, 2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(box.Depth * 0.5f * f.Z).Rotated(90.0f, 0) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(-box.Depth* 0.5f * f.Z).Rotated(-90.0f, 0) });

                // corners
                Vector3f extAxis0 = 0.5f * box.Width * f.X;
                Vector3f extAxis1 = 0.5f * box.Height * f.Y;
                Vector3f extAxis2 = 0.5f * box.Depth * f.Z;
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(-extAxis0 - extAxis1 - extAxis2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(extAxis0 - extAxis1 - extAxis2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(extAxis0 + extAxis1 - extAxis2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(- extAxis0 + extAxis1 - extAxis2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(- extAxis0 - extAxis1 + extAxis2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(extAxis0 - extAxis1 + extAxis2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(extAxis0 + extAxis1 + extAxis2) });
                v.Add(new SOFrameSnapPoint(box) { frame = f.Translated(- extAxis0 + extAxis1 + extAxis2) });

            } else if (so is SphereSO) {
                SphereSO sphere = so as SphereSO;
                Frame3f f = Frame3f.Identity;
                v.Add(new SOFrameSnapPoint(sphere) { frame = f });

                // sphere face-centers
                float fR = sphere.Radius;
                v.Add(new SOFrameSnapPoint(sphere) { frame = f.Translated(fR * f.Y) });
                v.Add(new SOFrameSnapPoint(sphere) { frame = f.Translated(-fR * f.Y).Rotated(180.0f, 0) });
                v.Add(new SOFrameSnapPoint(sphere) { frame = f.Translated(fR * f.X).Rotated(-90.0f, 2) });
                v.Add(new SOFrameSnapPoint(sphere) { frame = f.Translated(-fR * f.X).Rotated(90.0f, 2) });
                v.Add(new SOFrameSnapPoint(sphere) { frame = f.Translated(fR * f.Z).Rotated(90.0f, 0) });
                v.Add(new SOFrameSnapPoint(sphere) { frame = f.Translated(-fR * f.Z).Rotated(-90.0f, 0) });
            }

            if (base.EnableGeometry)
                base.build_geometry(so, v);

            return v;
        }




        override public List<ISnapSegment> GenerateSegments(SceneObject so)
        {
            List<ISnapSegment> v = new List<ISnapSegment>();

            if (so is BoxSO) {
                BoxSO box = so as BoxSO;
                Frame3f f = box.GetLocalFrame(CoordSpace.SceneCoords);

                // corners
                float ext0 = 0.5f * box.ScaledWidth, ext1 = 0.5f * box.ScaledHeight, ext2 = 0.5f * box.ScaledDepth;
                Vector3f extAxis0 = ext0 * f.X, extAxis1 = ext1 * f.Y, extAxis2 = ext2 * f.Z;

                v.Add(new StandardSnapSegment(box) {
                    center = f.Translated(-extAxis0 - extAxis1), extent = ext2 });
                v.Add(new StandardSnapSegment(box) {
                    center = f.Translated(extAxis0 - extAxis1), extent = ext2 });
                v.Add(new StandardSnapSegment(box) {
                    center = f.Translated(extAxis0 + extAxis1), extent = ext2 });
                v.Add(new StandardSnapSegment(box) {
                    center = f.Translated(-extAxis0 + extAxis1), extent = ext2 });

                Frame3f fX = f.Rotated(90.0f, 1);
                v.Add(new StandardSnapSegment(box) {
                    center = fX.Translated(-extAxis1 - extAxis2), extent = ext0 });
                v.Add(new StandardSnapSegment(box) {
                    center = fX.Translated(extAxis1 - extAxis2), extent = ext0 });
                v.Add(new StandardSnapSegment(box) {
                    center = fX.Translated(extAxis1 + extAxis2), extent = ext0 });
                v.Add(new StandardSnapSegment(box) {
                    center = fX.Translated(-extAxis1 + extAxis2), extent = ext0 });

                Frame3f fY = f.Rotated(90.0f, 0);
                v.Add(new StandardSnapSegment(box) {
                    center = fY.Translated(-extAxis0 - extAxis2), extent = ext1 });
                v.Add(new StandardSnapSegment(box) {
                    center = fY.Translated(extAxis0 - extAxis2), extent = ext1 });
                v.Add(new StandardSnapSegment(box) {
                    center = fY.Translated(extAxis0 + extAxis2), extent = ext1 });
                v.Add(new StandardSnapSegment(box) {
                    center = fY.Translated(-extAxis0 + extAxis2), extent = ext1 });

            }

            if (base.EnableGeometry)
                base.build_geometry(so, v);

            return v;
        }



    }
}
