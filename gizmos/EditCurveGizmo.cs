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
    class EditCurveGizmo : BaseTransformGizmo
    {
        Material stdMaterial, stdHoverMaterial;

        List<Action> WidgetParameterUpdates;


        public EditCurveGizmo()
        {
            WidgetParameterUpdates = new List<Action>();
        }


        override public void Disconnect()
        {
            base.Disconnect();
            foreach (var target in Targets) {
                if (target is PolyCurveSO)
                    (target as PolyCurveSO).OnCurveModified -= on_curve_modified;
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
                v.Key.SetLocalScale(new Vector3f(fScaling, fScaling, fScaling));
            }
        }


        void add_curve_widget(PolyCurveSO curve, bool bFirst, Func<Vector3> localPositionF )
        {
            GameObject go = AppendMeshGO("curve_endpoint",
                UnityUtil.GetPrimitiveMesh(PrimitiveType.Cube), stdMaterial, gizmo);
            go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            Frame3f sourceFrame = new Frame3f(localPositionF());
            UnityUtil.SetGameObjectFrame(go, sourceFrame, CoordSpace.ObjectCoords);
            WidgetParameterUpdates.Add(() => {
                UnityUtil.SetGameObjectFrame(go, new Frame3f(localPositionF()), CoordSpace.ObjectCoords);
            });

            Widgets[go] = new CurveHandleEditWidget(this, curve, this.parentScene) {
                RootGameObject = go, StandardMaterial = stdMaterial, HoverMaterial = stdHoverMaterial,
                VertexIndex = (bFirst) ? 0 : CurveHandleEditWidget.LastIndex
            };
        }



        protected override void BuildGizmo()
        {
            gizmo.SetName("EditPrimitiveGizmo");

            float fAlpha = 0.5f;
            stdMaterial = MaterialUtil.CreateTransparentMaterial(Color.yellow, fAlpha);
            stdHoverMaterial = MaterialUtil.CreateStandardMaterial(Color.yellow);

            // [TODO] this should iterate through targets... ??

            Debug.Assert(this.targets.Count == 1);

            PolyCurveSO target = this.targets[0] as PolyCurveSO;
            target.OnCurveModified += on_curve_modified;

            add_curve_widget(target, true, 
                () => { return (Vector3)target.Curve.Start; } );
            add_curve_widget(target, false,
                () => { return (Vector3)target.Curve.End; } );


            gizmo.Hide();
        }


        void on_curve_modified(PolyCurveSO so)
        {
            foreach (var f in WidgetParameterUpdates)
                f();
        }


    }
}
