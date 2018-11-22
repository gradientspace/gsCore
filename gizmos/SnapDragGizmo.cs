// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using g3;
using f3;

namespace gs
{

    public class SnapDragGizmoBuilder : ITransformGizmoBuilder
    {
        public bool SupportsMultipleObjects { get { return false; } }

        public ITransformGizmo Build(FScene scene, List<SceneObject> targets)
        {
            var g = new SnapDragGizmo();
            g.Create(scene, targets);
            return g;
        }
    }



    public class SnapDragGizmo : BaseTransformGizmo
    {
        Material srcMaterial, srcHoverMaterial;

        SnapSet Snaps;

        public SnapDragGizmo() : base()
        {
        }

        override public void Disconnect()
        {
            base.Disconnect();
            Snaps.Disconnect(true);
        }


        // called on per-frame Update()
        override public void PreRender()
        {
            gizmo.Show();

            foreach (var v in Widgets) {
                float fScaling = VRUtil.GetVRRadiusForVisualAngle(
                   v.Key.GetPosition(),
                   parentScene.ActiveCamera.GetPosition(),
                   SceneGraphConfig.DefaultPivotVisualDegrees);
                fScaling /= parentScene.GetSceneScale();
                v.Key.SetLocalScale(new Vector3f(fScaling, fScaling, fScaling));
            }

            Snaps.PreRender(parentScene.ActiveCamera.GetPosition());
        }


        void add_snap_source(Vector3 vPosition, string name, SnapSet targets)
        {
            fMeshGameObject go = AppendMeshGO(name,
                UnityUtil.GetPrimitiveMesh(PrimitiveType.Sphere), srcMaterial, gizmo);
            go.SetLocalScale(0.5f * Vector3f.One);
            Frame3f sourceFrame = new Frame3f(vPosition);
            UnityUtil.SetGameObjectFrame(go, sourceFrame, CoordSpace.ObjectCoords);
            Widgets[go] = new SnapDragSourceWidget(this, this.parentScene, targets) {
                RootGameObject = go, StandardMaterial = srcMaterial, HoverMaterial = srcHoverMaterial,
                SourceFrameL = sourceFrame
            };
        }

        override protected void BuildGizmo()
        {
            gizmo.SetName("SnapDragGizmo");

            float fAlpha = 0.5f;
            srcMaterial = MaterialUtil.CreateTransparentMaterial(ColorUtil.SelectionGold, fAlpha);
            srcHoverMaterial = MaterialUtil.CreateStandardMaterial(ColorUtil.SelectionGold);

            // generate snap target set
            Snaps = SnapSet.CreateStandard(Scene);
            Snaps.IgnoreSet.AddRange(this.targets);
            Snaps.PreRender(parentScene.ActiveCamera.GetPosition());

            // [TODO] this should iterate through targets...

            Debug.Assert(this.targets.Count == 1);
            // [TODO] should maybe be using GetBoundingBox now??
            Bounds b = this.targets[0].GetLocalBoundingBox();
            float h = b.extents[1];

            // object origin
            add_snap_source(Vector3.zero, "obj_origin", Snaps);
            add_snap_source(Vector3.zero + h * Vector3.up, "obj_top", Snaps);
            add_snap_source(Vector3.zero - h * Vector3.up, "obj_base", Snaps);

            gizmo.Hide();
        }

        override protected void OnBeginCapture(Ray3f worldRay, Standard3DWidget w)
        {
            foreach ( var v in Widgets ) {
                if (v.Value is SnapDragSourceWidget)
                    (v.Value as SnapDragSourceWidget).TargetObjects = 
                        new List<SceneObject>(Targets.Cast<SceneObject>());
            }
        }


        SceneObject curActiveSO = null;

        override protected void OnUpdateCapture(Ray3f worldRay, Standard3DWidget w)
        {
            SceneObject hitSO = null;
            SORayHit hit;
            IEnumerable<SceneObject> targetSOs = this.targets;
            if ( parentScene.FindSORayIntersection(worldRay, out hit, (x) => ((x is PivotSO) == false) && (targetSOs.Contains(x) == false) ) ) {
                hitSO = hit.hitSO;
            }
            if (hitSO != null && hitSO != curActiveSO) {
                Snaps.RemoveFromActive(curActiveSO);
                Snaps.AddToActive(hitSO);
                curActiveSO = hitSO;
            }
        }

        protected override void OnEndCapture(Ray3f worldRay, Standard3DWidget w)
        {
            //clear TargetObjects in widgets?
        }



        //
        // ITransformGizmo impl
        //

        FrameType eCurrentFrameMode;
        override public FrameType CurrentFrameMode
        {
            get { return eCurrentFrameMode; }
            set {
                eCurrentFrameMode = value;
            }
        }
        override public bool SupportsFrameMode { get { return true; } }

    }
}
