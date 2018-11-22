// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;

namespace gs
{
    public class MultiPointToolBuilder : IToolBuilder
    {
        public float DefaultIndicatorSizeScene = 0.25f;
        public IndicatorFactory IndicatorBuilder = null;
        public Func<Ray3d, Vector3d, double, int, LinearIntersection> CustomHitTestF = null;

        public virtual bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.SingleObject);
        }

        public virtual ITool Build(FScene scene, List<SceneObject> targets)
        {
            MultiPointTool tool = new_tool(scene, targets[0]);
            configure_tool(tool);
            return tool;
        }

        protected virtual void configure_tool(MultiPointTool tool)
        {
            tool.DefaultIndicatorSize = DefaultIndicatorSizeScene;
            if (IndicatorBuilder != null)
                tool.IndicatorBuilder = IndicatorBuilder;
            if (CustomHitTestF != null)
                tool.PointHitTestF = CustomHitTestF;
        }

        protected virtual MultiPointTool new_tool(FScene scene, SceneObject target)
        {
            return new MultiPointTool(scene, target);
        }
    }




    /// <summary>
    /// MultiPointTool provides a generic framework for building a tool with 1 or more
    /// click-draggable points as input handles. The points by default are connected to
    /// the input object, but they can also be constrained to an arbitrary ray-intersection target.
    /// 
    /// Currently this tool is not really meant to be used directly, you should subclass
    /// to implement specific functionality. However you can technically instantiate and
    /// configure it, and read out the point positions yourself.
    /// 
    /// Use AppendSurfacePoint() and AppendTargetPoint() to add new target points.
    /// Set/GetPointPosition can be used to move/query points.
    /// SetPointColor can be used to set point color and also the rendering layer.
    /// 
    /// Subclasses can override OnPointAdded/OnPointUpdated/etc, to do their stuff.
    /// 
    /// The points are implemented as SphereIndictors, by default. You can override
    /// their construction by setting a new IndicatorBuilder. Alternately you
    /// can parse through the indicator set (via Indicators) to do after-the-fact customization.
    /// The Indicator.RootGameObject name will be set to the name string passed to AppendSurfacePoint()
    /// 
    /// </summary>
    public class MultiPointTool : ITool
    {
        virtual public string Name { get { return "MultiPointTool"; } }
        virtual public string TypeIdentifier { get { return "multi_point_tool"; } }


        protected FScene Scene;
        public SceneObject TargetSO;

        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }

        ParameterSet parameters = new ParameterSet();
        public ParameterSet Parameters { get { return parameters; } }

        public ToolIndicatorSet Indicators { get; set; }
        public IndicatorFactory IndicatorBuilder { get; set; }


        /// <summary>
        /// Ray-intersection for a ControlPoint. Arguments are [Ray,Center,Radius,PointID].
        /// Inputs and output are all in **SCENE COORDINATES**
        /// </summary>
        public Func<Ray3d, Vector3d, double, int, LinearIntersection> PointHitTestF;



        protected class ControlPoint
        {
            public int id;
            public string name;
            public float sizeS;
            public Colorf color;
            public int layer;
            public SphereIndicator indicator;
            public Frame3f currentFrameS;
            public bool initialized;
        }

        protected class SurfacePoint : ControlPoint
        {
        }

        protected class TargetPoint : ControlPoint
        {
            public IIntersectionTarget RayTarget;
        }


        protected Dictionary<int, ControlPoint> GizmoPoints;
        int point_id_counter = 0;

        protected int active_surface_point = -1;


        float default_indicator_size = 0.2f;
        public float DefaultIndicatorSize {
            get { return default_indicator_size; }
            set { default_indicator_size = MathUtil.Clamp(value, 0.01f, 10000.0f); }
        }


        public MultiPointTool(FScene scene, SceneObject target)
        {
            this.Scene = scene;
            TargetSO = target;

            // do this here ??
            behaviors = new InputBehaviorSet();
            behaviors.Add(
                new MultiPointTool_2DBehavior(scene.Context, this) { Priority = 5 });
            if (FPlatform.IsUsingVR()) {
                behaviors.Add(
                    new MultiPointTool_SpatialBehavior(scene.Context, this) { Priority = 5 });
            }

            Indicators = new ToolIndicatorSet(this, scene);
            IndicatorBuilder = new StandardIndicatorFactory();
            GizmoPoints = new Dictionary<int, ControlPoint>();
            PointHitTestF = PointIntersectionTest;
        }


        // these are callbacks you can use to implement your tool


        protected virtual void OnPointAdded(ControlPoint pt)
        {
            // do something
        }
        protected virtual void OnPointInitialized(ControlPoint pt)
        {
            // do something
        }
        protected virtual void OnPointUpdated(ControlPoint pt, Frame3f prevFrameS, bool isFirst)
        {
            // do something
        }
        protected virtual void OnBeginMovePoint(ControlPoint pt)
        {
            // do something
        }
        protected virtual void OnEndPointMove(ControlPoint pt)
        {
            // do something
        }


        /// <summary>
        /// Add point that lives on TargetSO surface
        /// TODO: generalize to multiple target SOs??
        /// </summary>
        public virtual int AppendSurfacePoint(string name, Colorf color, float sizeScene = -1)
        {
            SurfacePoint pt = new SurfacePoint();
            pt.id = point_id_counter++;
            pt.name = name;
            pt.color = color;
            pt.layer = -1;
            pt.sizeS = (sizeScene > 0) ? sizeScene : default_indicator_size;
            pt.initialized = false;
            GizmoPoints[pt.id] = pt;
            OnPointAdded(pt);
            return pt.id;
        }


        /// <summary>
        /// Add point that lives on arbitrary surface
        /// rayTarget Must compute ray-intersections in *scene space*
        /// </summary>
        public virtual int AppendTargetPoint(string name, Colorf color, IIntersectionTarget sceneRayTarget, float sizeScene = -1)
        {
            TargetPoint pt = new TargetPoint();
            pt.id = point_id_counter++;
            pt.name = name;
            pt.color = color;
            pt.layer = -1;
            pt.sizeS = (sizeScene > 0) ? sizeScene : default_indicator_size;
            pt.initialized = false;
            pt.RayTarget = sceneRayTarget;
            GizmoPoints[pt.id] = pt;
            OnPointAdded(pt);
            return pt.id;
        }



        public void SetPointPosition(int id, Frame3f frameS, CoordSpace space)
        {
            ControlPoint pt;
            if (GizmoPoints.TryGetValue(id, out pt) == false)
                throw new Exception("MultiSurfacePointTool.SetPointPosition: point with id " + id + " does not exist!");
            bool isFirst = (pt.initialized == false);
            Frame3f prevFrameS = pt.currentFrameS;

            if (SetPointPosition_Internal(id, frameS, space) == false)
                return;

            OnPointUpdated(pt, prevFrameS, isFirst);
        }

        // same as above but does not post OnPointUpdated
        protected bool SetPointPosition_Internal(int id, Frame3f frameS, CoordSpace space)
        {
            if (space == CoordSpace.WorldCoords)
                frameS = SceneTransforms.WorldToScene(this.Scene, frameS);
            else if (space == CoordSpace.ObjectCoords)
                frameS = SceneTransforms.ObjectToScene(TargetSO, frameS);
            
            ControlPoint pt;
            if (GizmoPoints.TryGetValue(id, out pt) == false)
                throw new Exception("MultiSurfacePointTool.SetPointPosition_Internal: point with id " + id + " does not exist!");

            // ignore tiny movements
            if (pt.currentFrameS.Origin.EpsilonEqual(frameS.Origin, 0.0001f))
                return false;
            pt.currentFrameS = frameS;

            if (pt.initialized == false)
                initialize_point(pt);

            return true;
        }


        public Frame3f GetPointPosition(int id, CoordSpace space = CoordSpace.SceneCoords)
        {
            ControlPoint pt;
            if (GizmoPoints.TryGetValue(id, out pt) == false)
                throw new Exception("MultiSurfacePointTool.SetPointPosition: point with id " + id + " does not exist!");

            if (space == CoordSpace.WorldCoords)
                return SceneTransforms.SceneToWorld(this.Scene, pt.currentFrameS);
            else if (space == CoordSpace.ObjectCoords)
                return SceneTransforms.SceneToObject(TargetSO, pt.currentFrameS);
            else
                return pt.currentFrameS;
        }


        public void SetPointColor(int id, Colorf color, int nLayer = -1) 
        {
            ControlPoint pt;
            if (GizmoPoints.TryGetValue(id, out pt) == false)
                throw new Exception("MultiSurfacePointTool.SetPointMaterial: point with id " + id + " does not exist!");
            pt.color = color;
            if (nLayer != -1) {
                pt.layer = nLayer;
                if (pt.indicator != null)
                    Indicators.SetLayer(pt.indicator, nLayer);
            }
        }


        public bool IsPointInitialized(int id)
        {
            ControlPoint pt;
            if (GizmoPoints.TryGetValue(id, out pt) == false)
                throw new Exception("MultiSurfacePointTool.SetPointMaterial: point with id " + id + " does not exist!");
            return pt.initialized;
        }


        public virtual bool AllowSelectionChanges { get { return false; } }


        virtual public bool HasApply { get { return false; } }
        virtual public bool CanApply { get { return false; } }
        virtual public void Apply() { }


        // override this to limit SOs that can be clicked
        virtual public bool ObjectFilter(SceneObject so)
        {
            return so == TargetSO;
        }


        public virtual void PreRender()
        {
            Indicators.PreRender();
        }

        public virtual void Setup()
        {
        }

        public virtual void Shutdown()
        {
            Indicators.Disconnect(true);
            GizmoPoints.Clear();
        }



        public int FindNearestHitPoint(Ray3f worldRay)
        {
            ControlPoint hitPt;
            float hitT = find_target_hit(worldRay, out hitPt);
            if (hitT < double.MaxValue)
                return hitPt.id;
            return -1;
        }


        /// <summary>
        /// called on click-down
        /// </summary>
        virtual public void Begin(int hitPointID, Ray3f downRayWorld)
        {
            active_surface_point = hitPointID;


            //if (active_surface_point != -1)
            //    SetPointPosition(active_surface_point, new Frame3f(scenePos), CoordSpace.SceneCoords);
        }

        /// <summary>
        /// called each frame as cursor moves
        /// </summary>
        virtual public void Update(Ray3f downRay)
        {
            if (active_surface_point == -1)
                return;
            ControlPoint pt;
            if (GizmoPoints.TryGetValue(active_surface_point, out pt) == false) {
                active_surface_point = -1;
                return;
            }

            if (pt is SurfacePoint) {
                SORayHit hit;
                if (TargetSO.FindRayIntersection(downRay, out hit)) {
                    Frame3f hitFrameW = new Frame3f(hit.hitPos, hit.hitNormal);
                    Frame3f hitFrameS = this.Scene.ToSceneFrame(hitFrameW);
                    SetPointPosition(active_surface_point, hitFrameS, CoordSpace.SceneCoords);
                }

            } else if (pt is TargetPoint ) {
                TargetPoint targetPt = pt as TargetPoint;
                Vector3d hitS, hitNormal;
                Ray3d sceneRay = (Ray3d)Scene.ToSceneRay(downRay);
                if ( targetPt.RayTarget.RayIntersect(sceneRay, out hitS, out hitNormal) ) {
                    Frame3f hitFrameS = new Frame3f(hitS, hitNormal);
                    SetPointPosition(active_surface_point, hitFrameS, CoordSpace.SceneCoords);
                }
            }
        }

        /// <summary>
        /// called after click is released
        /// </summary>
        virtual public void End()
        {
            if (active_surface_point == -1)
                return;
            ControlPoint pt;
            if (GizmoPoints.TryGetValue(active_surface_point, out pt) != false) {
                OnEndPointMove(pt);
            }
            active_surface_point = -1;
        }




        protected virtual void initialize_point(ControlPoint pt)
        {
            SphereIndicator indicator = IndicatorBuilder.MakeSphereIndicator(
                pt.id, pt.name,
                fDimension.Scene(pt.sizeS / 2),
                () => { return pt.currentFrameS; },
                () => { return pt.color; },
                () => { return true; }
            );
            Indicators.AddIndicator(indicator);
            indicator.RootGameObject.SetName(pt.name);

            pt.indicator = indicator;
            if (pt.layer != -1)
                Indicators.SetLayer(indicator, pt.layer);
            pt.initialized = true;
            OnPointInitialized(pt);
        }



        protected virtual LinearIntersection PointIntersectionTest(Ray3d rayS, Vector3d positionS, double radiusS, int pointID)
        {
            return IntersectionUtil.RaySphere(ref rayS.Origin, ref rayS.Direction, ref positionS, radiusS);
        }



        protected virtual float find_target_hit(Ray3f worldRay, out ControlPoint hitPoint)
        {
            hitPoint = null;
            double near_t = double.MaxValue;

            Ray3d rayS = (Ray3d)Scene.ToSceneRay(worldRay);
            foreach ( ControlPoint point in GizmoPoints.Values ) {
                if (point.initialized == false)
                    continue;

                // USE INDICATOR HIT-TEST ??

                Vector3d centerS = (Vector3d)point.currentFrameS.Origin;
                float r = point.sizeS;
                LinearIntersection isect = PointIntersectionTest(rayS, centerS, r, point.id);
                if (isect.intersects && isect.numIntersections == 2 && isect.parameter.a < near_t ) {
                    near_t = isect.parameter.a;
                    hitPoint = point;
                }
            }
            return (float)near_t;
        }


    }








    class MultiPointTool_SpatialBehavior : StandardInputBehavior
    {
        FContext context;
        MultiPointTool tool;

        public MultiPointTool_SpatialBehavior(FContext s, MultiPointTool tool)
        {
            context = s;
            this.tool = tool;
        }

        override public InputDevice SupportedDevices {
            get { return InputDevice.AnySpatialDevice; }
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (input.bLeftTriggerPressed ^ input.bRightTriggerPressed) {
                CaptureSide eSide = (input.bLeftTriggerPressed) ? CaptureSide.Left : CaptureSide.Right;
                if (context.ToolManager.GetActiveTool((int)eSide) == tool) {
                    Ray3f worldRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
                    int hit_pt_id = tool.FindNearestHitPoint(worldRay);
                    if (hit_pt_id >= 0) {
                        return CaptureRequest.Begin(this, eSide);
                    }
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            Ray3f worldRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            int hit_pt_id = tool.FindNearestHitPoint(worldRay);
            if (hit_pt_id >= 0) {
                tool.Begin(hit_pt_id, worldRay);
                return Capture.Begin(this, eSide);
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            bool bReleased = (data.which == CaptureSide.Left) ? input.bLeftTriggerReleased : input.bRightTriggerReleased;
            if (bReleased) {
                tool.End();
                return Capture.End;
            } else {
                Ray3f worldRay = (data.which == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
                tool.Update(worldRay);
                return Capture.Continue;
            }
        }


        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            tool.End();
            return Capture.End;
        }
    }





    
    class MultiPointTool_2DBehavior : Any2DInputBehavior
    {
        FContext context;
        MultiPointTool tool;

        public MultiPointTool_2DBehavior(FContext s, MultiPointTool tool)
        {
            context = s;
            this.tool = tool;
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool != tool)
                return CaptureRequest.Ignore;
            if (Pressed(input)) {
                int hit_pt_id = tool.FindNearestHitPoint(WorldRay(input));
                if ( hit_pt_id >= 0 ) {
                    return CaptureRequest.Begin(this);
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            int hit_pt_id = tool.FindNearestHitPoint(WorldRay(input));
            if ( hit_pt_id >= 0 ) {
                tool.Begin(hit_pt_id, WorldRay(input));
                return Capture.Begin(this);
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            if (Released(input)) {
                tool.End();
                return Capture.End;
            } else {
                tool.Update(WorldRay(input));
                return Capture.Continue;
            }
        }


        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            tool.End();
            return Capture.End;
        }
    }


}
