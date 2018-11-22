// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;

namespace gs
{
    // [TODO] should take optional selection target...
    public class TwoPointFaceSelectionToolBuilder : IToolBuilder
    {
        public float SphereIndicatorSizeScene = 0.25f;


        // Optional! third parameter is reference to Tool
        public Action<DMeshSO, MeshFaceSelection, object> OnApplyF = null;


        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.SingleObject && targets[0] is DMeshSO);
        }

        public virtual ITool Build(FScene scene, List<SceneObject> targets)
        {
            TwoPointFaceSelectionTool tool = new_tool(scene, targets[0] as DMeshSO);
            tool.SourceIndicatorSizeScene = SphereIndicatorSizeScene;
            tool.ExtentIndicatorSizeScene = SphereIndicatorSizeScene;
            tool.OnApplyF = OnApplyF;
            return tool;
        }

        protected virtual TwoPointFaceSelectionTool new_tool(FScene scene, DMeshSO target)
        {
            return new TwoPointFaceSelectionTool(scene, target);
        }
    }





    public class TwoPointFaceSelectionTool : BaseSurfacePointTool
    {
        static readonly public string Identifier = "two_point_face_selection";
        override public string Name { get { return "TwoPointFaceSelectionTool"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        DMeshSO TargetSO;

        ToolIndicatorSet indicators;
        SphereIndicator sourceIndicator;
        SphereIndicator extentIndicator;
        MeshFaceSelectionCache selectionCache;
        fGameObject targetTrackingGO;
        MeshFaceSelection selection;
        bool selection_valid;


        float source_indicator_size = 0.2f;
        public float SourceIndicatorSizeScene {
            get { return source_indicator_size; }
            set { source_indicator_size = MathUtil.Clamp(value, 0.01f, 10000.0f); }
        }

        float extent_indicator_size = 0.2f;
        public float ExtentIndicatorSizeScene {
            get { return extent_indicator_size; }
            set { extent_indicator_size = MathUtil.Clamp(value, 0.01f, 10000.0f); }
        }

        public MeshFaceSelection CurrentSelection {
            get { validate_output();  return selection; }
        }

        public MeshFaceSelectionCache SelectionCache {
            get { return selectionCache; }
        }

        public Frame3f SourcePositionS {
            get { return CurrentSourceHitPosS; }
        }
        public Frame3f ExtentPositionS {
            get { return CurrentExtentHitPosS; }
        }


        /// <summary>
        /// If you set this value, then you can call Apply() on this tool
        /// </summary>
        public Action<DMeshSO, MeshFaceSelection, object> OnApplyF = null;



        Frame3f CurrentSourceHitPosS;
        Frame3f CurrentExtentHitPosS;
        bool have_source;
        bool have_extent;
        bool source_modified;
        bool extent_modified;


        public TwoPointFaceSelectionTool(FScene scene, DMeshSO target) : base(scene)
        {
            TargetSO = target;
            indicators = new ToolIndicatorSet(this, scene);
            have_source = have_extent = false;

            selection_valid = false;

            selectionCache = new MeshFaceSelectionCache(target.Mesh);
            selectionCache.ChunkMeshMaterial = MaterialUtil.CreateStandardMaterialF(Colorf.Gold);

            targetTrackingGO = GameObjectFactory.CreateTrackingGO("track_target", target.RootGameObject);
            scene.TransientObjectsParent.AddChild(targetTrackingGO, false);
            selectionCache.ChunkMeshParent = targetTrackingGO;
        }


        // override this to limit SOs that can be clicked
        override public bool ObjectFilter(SceneObject so)
        {
            return so == TargetSO;
        }


        override public void PreRender()
        {
            indicators.PreRender();
            update_selection();
            base.PreRender();
        }


        override public void Shutdown()
        {
            indicators.Disconnect(true);
            targetTrackingGO.Destroy();
            base.Shutdown();
        }


        /// <summary>
        /// called on click-down
        /// </summary>
        override public void Begin(SceneObject so, Vector2d downPos, Ray3f downRayWorld)
        {
            SORayHit hit;
            if (TargetSO.FindRayIntersection(downRayWorld, out hit) == false)
                return;

            Vector3d scenePos = SceneTransforms.WorldToSceneP(this.Scene, hit.hitPos);

            if (have_source == false) {
                CurrentSourceHitPosS = new Frame3f(scenePos);

                sourceIndicator = new SphereIndicator() {
                    SceneFrameF = () => { return CurrentSourceHitPosS; },
                    Radius = fDimension.Scene(SourceIndicatorSizeScene * 0.5),
                    ColorF = () => { return Colorf.Orange; }
                };
                indicators.AddIndicator(sourceIndicator);

                have_source = true;
                source_modified = true;

            } else if (have_extent == false) {
                CurrentExtentHitPosS = new Frame3f(scenePos);

                extentIndicator = new SphereIndicator() {
                    SceneFrameF = () => { return CurrentExtentHitPosS; },
                    Radius = fDimension.Scene(ExtentIndicatorSizeScene * 0.5),
                    ColorF = () => { return Colorf.CornflowerBlue; }
                };
                indicators.AddIndicator(extentIndicator);

                have_extent = true;
                extent_modified = true;
            }

        }

        /// <summary>
        /// called each frame as cursor moves
        /// </summary>
        override public void Update(Vector2d downPos, Ray3f downRay)
        {
            SORayHit hit;
            if (TargetSO.FindRayIntersection(downRay, out hit)) {
                Vector3d scenePos = SceneTransforms.WorldToSceneP(this.Scene, hit.hitPos);
                CurrentExtentHitPosS = new Frame3f(scenePos);
                extent_modified = true;
            }

        }

        /// <summary>
        /// called after click is released
        /// </summary>
        override public void End()
        {
        }



        override public bool HasApply { get { return OnApplyF != null; } }
        override public bool CanApply { get { return have_source && have_extent; } }
        override public void Apply()
        {
            if (OnApplyF != null)
                OnApplyF(TargetSO, CurrentSelection, this);
        }





        void update_selection()
        {
            if (have_source && have_extent) {
                
                if (source_modified) {
                    Frame3f objFrame = SceneTransforms.SceneToObject(TargetSO, CurrentSourceHitPosS);
                    int tid = TargetSO.Spatial.FindNearestTriangle(objFrame.Origin);
                    selectionCache.InitializeGeodesicDistance(objFrame.Origin, tid);
                    source_modified = false;
                    selection_valid = false;
                }

                if (extent_modified) {
                    Frame3f objFrame = SceneTransforms.SceneToObject(TargetSO, CurrentExtentHitPosS);
                    int tid = TargetSO.Spatial.FindNearestTriangle(objFrame.Origin);
                    float scalar = selectionCache.GetTriScalar(objFrame.Origin, tid);
                    selectionCache.UpdateFromOrderedScalars(scalar);
                    extent_modified = false;
                    selection_valid = false;
                }

            }
        }




        void validate_output()
        {
            if (selection_valid)
                return;

            selection = new MeshFaceSelection(TargetSO.Mesh);
            selectionCache.ComputeSelection(selection);

            selection_valid = true;
        }



    }
}
