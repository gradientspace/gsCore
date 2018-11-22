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
    public abstract class StandardSnapGenerator : ISnapGenerator
    {
        protected Material visibleMaterial, hiddenMaterial;
        protected GameObject parent;

        protected bool EnableGeometry { get { return parent != null; } }

        protected StandardSnapGenerator()
        {
            // this constructor is just for re-using this class to generate
            // snap points
        }

        protected StandardSnapGenerator(Material visibleMaterial, Material hiddenMaterial)
        {
            this.visibleMaterial = visibleMaterial;
            if (this.visibleMaterial == null)
                this.visibleMaterial = MaterialUtil.CreateTransparentMaterial(ColorUtil.PivotYellow, 0.5f);
            this.hiddenMaterial = hiddenMaterial;
            if (this.hiddenMaterial == null)
                this.hiddenMaterial = MaterialUtil.CreateTransparentMaterial(ColorUtil.PivotYellow, 0.3f);
            parent = new GameObject("StandardSnapGenerator_parent");
        }


        public void Destroy()
        {
            if (parent != null)
                parent.Destroy();
        }

        abstract public bool CanGenerate(SceneObject so);
        abstract public List<ISnapPoint> GeneratePoints(SceneObject so);
        abstract public List<ISnapSegment> GenerateSegments(SceneObject so);



        protected void build_geometry(SceneObject so, List<ISnapPoint> points) 
        {
            int nNoClipLayer = FPlatform.WidgetOverlayLayer;

            // we re-use this class to generate snap points for non-SnapSet use, and in
            // those cases we don't want to build gameobjects/etc
            if (EnableGeometry) {
                foreach (var p in points) {
                    SOFrameSnapPoint pg = p as SOFrameSnapPoint;
                    Frame3f worldFrame = so.GetScene().ToWorldFrame(pg.FrameS);

                    // only add always-hidden points to overlay layer (other points are z-clipped)
                    //pg.Build(worldFrame, parent, (pg.IsSurface) ? visibleMaterial : hiddenMaterial);
                    //if (pg.IsSurface == false)
                    //    pg.primGO.SetLayer(nNoClipLayer);

                    // always add all points to overlay layer
                    pg.Build(worldFrame, parent, (pg.IsSurface) ? visibleMaterial : hiddenMaterial, nNoClipLayer);
                    //pg.primGO.SetLayer(nNoClipLayer);
                }

                // have to attach our root object to Scene, otherwise it won't move
                UnityUtil.AddChild(so.GetScene().RootGameObject, parent, true);
            }
        }




        protected void build_geometry(SceneObject so, List<ISnapSegment> segments)
        {
            int nNoClipLayer = FPlatform.WidgetOverlayLayer;

            // we re-use this class to generate snap points for non-SnapSet use, and in
            // those cases we don't want to build gameobjects/etc
            if (EnableGeometry) {
                foreach (var p in segments) {
                    StandardSnapSegment pg = p as StandardSnapSegment;

                    Frame3f centerW = so.GetScene().ToWorldFrame(pg.center);
                    float extentW = so.GetScene().ToWorldDimension(pg.extent);

                    // only add always-hidden points to overlay layer (other points are z-clipped)
                    //pg.Build(worldFrame, parent, (pg.IsSurface) ? visibleMaterial : hiddenMaterial);
                    //if (pg.IsSurface == false)
                    //    pg.primGO.SetLayer(nNoClipLayer);

                    // always add all points to overlay layer
                    pg.Build(centerW, extentW, parent, visibleMaterial, nNoClipLayer);
                }

                // have to attach our root object to Scene, otherwise it won't move
                UnityUtil.AddChild(so.GetScene().RootGameObject, parent, true);
            }
        }

    }
}
