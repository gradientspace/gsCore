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

    public class PositionConstrainedGizmoBuilder : ITransformGizmoBuilder
    {
        public bool SupportsMultipleObjects { get { return false; } }

        public Vector3f WidgetScale = Vector3f.One;

        public virtual ITransformGizmo Build(FScene scene, List<SceneObject> targets)
        {
            PositionConstrainedGizmo gizmo = gizmo_factory();
            gizmo.WidgetScale = WidgetScale;
            gizmo.Create(scene, targets);
            return gizmo;
        }

        protected virtual PositionConstrainedGizmo gizmo_factory()
        {
            return new PositionConstrainedGizmo();
        }
    }



    public class PositionConstrainedGizmo : BaseTransformGizmo
    {
        Material srcMaterial, srcHoverMaterial;
        List<Action> WidgetParameterUpdates;

        public Func<Ray3f, Vector3d> ScenePositionF = null;
        public Vector3f WidgetScale = Vector3f.One;

        fMeshGameObject centerGO;

        public PositionConstrainedGizmo() : base()
        {
            WidgetParameterUpdates = new List<Action>();
        }

        override public void Disconnect()
        {
            base.Disconnect();
            foreach (var target in Targets) {
                target.OnTransformModified -= on_transform_modified;
            }
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
                v.Key.SetLocalScale(fScaling * WidgetScale);
            }
        }


        virtual protected void make_materials()
        {
            float fAlpha = 0.5f;
            srcMaterial = MaterialUtil.CreateTransparentMaterial(ColorUtil.CgRed, fAlpha);
            srcHoverMaterial = MaterialUtil.CreateStandardMaterial(ColorUtil.CgRed);
        }


        override protected void BuildGizmo()
        {
            gizmo.SetName("PositionConstrainedGizmo");

            make_materials();

            centerGO = AppendMeshGO("object_origin",
                UnityUtil.GetPrimitiveMesh(PrimitiveType.Sphere), srcMaterial, gizmo);
            centerGO.SetLocalScale(WidgetScale);

            Widgets[centerGO] = new PositionConstrainedPointWidget(this, this.parentScene) {
                RootGameObject = centerGO, StandardMaterial = srcMaterial, HoverMaterial = srcHoverMaterial
            };

            gizmo.Hide();
        }

        override protected void OnBeginCapture(Ray3f worldRay, Standard3DWidget w)
        {
            foreach (var v in Widgets) {
                if (v.Value is PositionConstrainedPointWidget) {
                    PositionConstrainedPointWidget widget = v.Value as PositionConstrainedPointWidget;
                    widget.SourceSO = Targets[0];
                    widget.ScenePositionF = this.ScenePositionF;
                }
            }
        }


        //SceneObject curActiveSO = null;

        override protected void OnUpdateCapture(Ray3f worldRay, Standard3DWidget w)
        {
        }

        protected override void OnEndCapture(Ray3f worldRay, Standard3DWidget w)
        {
            //clear TargetObjects in widgets?
        }


        void on_transform_modified(SceneObject so)
        {
            foreach (var f in WidgetParameterUpdates)
                f();
        }


        //
        // ITransformGizmo impl
        //
        override public bool SupportsFrameMode { get { return false; } }

    }
}
