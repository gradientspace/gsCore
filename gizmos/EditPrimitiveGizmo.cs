// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using UnityEngine;
using g3;
using f3;

namespace gs
{

    public class EditPrimitiveGizmoBuilder : ITransformGizmoBuilder
    {
        public bool SupportsMultipleObjects { get { return false; } }

        public ITransformGizmo Build(FScene scene, List<SceneObject> targets)
        {
            var g = new EditPrimitiveGizmo();
            g.Create(scene, targets);
            return g;
        }
    }


    // widgets we use in this gizmo need to implement this interface
    public interface IParameterEditWidget
    {
        string ParameterName { get; }
    }



    // custom ITransformWrapper for editing primitives, which don't actually involve
    //  any transforming...
    public class EditPrimitiveWrapper : ITransformWrapper
    {
        protected SceneObject target;
        virtual public SceneObject Target { get { return target; } }

        virtual public fGameObject RootGameObject {
            get { return Target.RootGameObject; }
        }

        public EditPrimitiveWrapper(SceneObject target)
        {
            this.target = target;
        }

        PrimitiveSOParamChange<float> paramChange;

        public virtual void BeginTransformation() {
            (target as PrimitiveSO).Parameters.SetValue("defer_rebuild", true);
        }

        public void StartParameterChange(string sParamName)
        {
            paramChange = new PrimitiveSOParamChange<float>();
            paramChange.so = (target as PrimitiveSO);
            paramChange.paramName = sParamName;
            paramChange.before = paramChange.so.Parameters.GetValue<float>(sParamName);
        }

        public virtual bool DoneTransformation(bool bEmitChanges) {
            (target as PrimitiveSO).Parameters.SetValue("defer_rebuild", false);

            bool bModified = false;
            paramChange.after = paramChange.so.Parameters.GetValue<float>(paramChange.paramName);
            if (paramChange.after != paramChange.before) {
                bModified = true;
                if (bEmitChanges)
                    target.GetScene().History.PushChange(paramChange, true);
            }
            paramChange = null;
            return bModified;
        }


        virtual public Frame3f GetLocalFrame(CoordSpace eSpace) {
            return target.GetLocalFrame(eSpace);
        }
        virtual public void SetLocalFrame(Frame3f newFrame, CoordSpace eSpace) {
            target.SetLocalFrame(newFrame, eSpace);
        }
        virtual public bool SupportsScaling {
            get { return target.SupportsScaling; }
        }
        virtual public Vector3f GetLocalScale() {
            return target.GetLocalScale();
        }
        virtual public void SetLocalScale(Vector3f scale) {
            target.SetLocalScale(scale);
        }

    }




    public class EditPrimitiveGizmo : BaseTransformGizmo
    {
        Material stdMaterial, stdHoverMaterial;

        List<Action> WidgetParameterUpdates;

        public EditPrimitiveGizmo() : base()
        {
            WidgetParameterUpdates = new List<Action>();
        }


        override public void Disconnect()
        {
            base.Disconnect();
            foreach ( var target in Targets ) {
                if (target is PrimitiveSO)
                    (target as PrimitiveSO).Parameters.OnParameterModified -= on_parameter_modified;
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



        void add_axis_widget(PrimitiveSO primitive, Vector3 frameAxisV, float deltaMult, string paramName, Func<Vector3> localPositionF)
        {
            GameObject go = AppendMeshGO(paramName,
                UnityUtil.GetPrimitiveMesh(PrimitiveType.Cube), stdMaterial, gizmo);
            go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Frame3f sourceFrame = new Frame3f(localPositionF());
            UnityUtil.SetGameObjectFrame(go, sourceFrame, CoordSpace.ObjectCoords);

            WidgetParameterUpdates.Add(() => {
                UnityUtil.SetGameObjectFrame(go, new Frame3f(localPositionF()), CoordSpace.ObjectCoords);
            });

            Widgets[go] = new AxisParameterEditWidget(this, primitive, this.parentScene) {
                RootGameObject = go, StandardMaterial = stdMaterial, HoverMaterial = stdHoverMaterial,
                AxisParamName = paramName, AxisVectorInFrame = frameAxisV, 
                DeltaScalingF = () => { return deltaMult / parentScene.GetSceneScale(); }
            };
        }


        override protected void BuildGizmo()
        {
            gizmo.SetName("EditPrimitiveGizmo");

            float fAlpha = 0.5f;
            stdMaterial = MaterialUtil.CreateTransparentMaterial(Color.yellow, fAlpha);
            stdHoverMaterial = MaterialUtil.CreateStandardMaterial(Color.yellow);

            // [TODO] this should iterate through targets... ??

            Debug.Assert(this.targets.Count == 1);

            // object origin
            foreach ( var so in Targets ) {
                if ((so is PrimitiveSO) == false)
                    continue;
                PrimitiveSO pso = so as PrimitiveSO;

                pso.Parameters.OnParameterModified += on_parameter_modified;

                float fVertMult = 1.0f;
                float fHorzMult = 1.0f;
                if ( pso.Center == CenterModes.Base ) {
                    fHorzMult = 2.0f;
                } 

                if ( pso is CylinderSO ) {
                    add_axis_widget(pso, Vector3.up, fVertMult, "scaled_height",
                        () => { return pso.Parameters.GetValueFloat("scaled_height") * 0.5f * Vector3.up; });
                    add_axis_widget(pso, Vector3.right, 1.0f, "scaled_radius",
                        () => { return pso.Parameters.GetValueFloat("scaled_radius") * Vector3.right; });

                } else if ( pso is SphereSO ) {
                    add_axis_widget(pso, Vector3.up, 1.0f, "scaled_diameter",
                        () => { return pso.Parameters.GetValueFloat("scaled_diameter") * 0.5f * Vector3.up; });

                } else if (pso is BoxSO) {
                    add_axis_widget(pso, Vector3.up, fVertMult, "scaled_height",
                        () => { return pso.Parameters.GetValueFloat("scaled_height") * 0.5f * Vector3.up; });
                    add_axis_widget(pso, Vector3.right, fHorzMult, "scaled_width",
                        () => { return pso.Parameters.GetValueFloat("scaled_width") * 0.5f * Vector3.right; });
                    add_axis_widget(pso, Vector3.forward, fHorzMult, "scaled_depth",
                        () => { return pso.Parameters.GetValueFloat("scaled_depth") * 0.5f * Vector3.forward; });
                }
            }

            gizmo.Hide();
        }


        override protected void InitializeTargetWrapper()
        {
            Debug.Assert(this.targets.Count == 1);
            targetWrapper = new EditPrimitiveWrapper(this.targets[0]);
        }


        void on_parameter_modified(ParameterSet pSet, string sParamName)
        {
            foreach (var f in WidgetParameterUpdates)
                f();
        }


        override protected void OnBeginCapture(Ray3f worldRay, Standard3DWidget w) {
            (targetWrapper as EditPrimitiveWrapper).StartParameterChange(
                (w as IParameterEditWidget).ParameterName);
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
