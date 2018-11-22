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

    public interface ISnapGeometry
    {
        // to implement stable snapping, we often need ot track a snap target over
        // multiple frames, and know that it is the *same* target. However 
        // this can be complicated by the fact that the target may also be moving
        // (ie in bimanual snapping). So, we require that each active snappable
        // element have a unique identifier
        //   (use the generator below to simplify this)
        int UniqueID { get; }
    }


    // snap points are in scene coordinates
    public interface ISnapPoint : ISnapGeometry
    {
        object Source { get; }
        Frame3f FrameS { get; }      // scene-space frame

        int Priority { get; }

        // world-space camera position
        void PreRender(Vector3f cameraPositionW);

        // world-space ray
        bool FindRayIntersection(Ray3f rayW, out float fHitDist);

        bool IsEnabled();
        void SetEnabled(bool bEnabled);
        void Destroy();
    }


    public interface ISnapSegment : ISnapGeometry
    {
        object Source { get; }
        Vector3f StartPointS { get; }
        Vector3f EndPointS { get; }
        //Frame3f FrameS { get; }      // assumption: Z is aligned with (Start-End)

        Frame3f GetHitFrameS(Vector3f pos);

        // world-space camera position
        void PreRender(Vector3f cameraPositionW);

        // world-space ray
        bool FindRayIntersection(Ray3f rayW, out float fHitDist);
        Vector3f FindSnapPoint(Ray3f rayW);

        bool IsEnabled();
        void SetEnabled(bool bEnabled);
        void Destroy();
    }


    public interface ISnapGenerator
    {
        bool CanGenerate(SceneObject so);
        List<ISnapPoint> GeneratePoints(SceneObject so);
        List<ISnapSegment> GenerateSegments(SceneObject so);
        void Destroy();
    }

    public interface ISnapCompare<T>
    {
        bool IsSame(T b);
    }


    public enum SnapType
    {
        Point, Segment, Line, Plane, Surface
    }


    public class SnapResult : ISnapCompare<SnapResult>
    {
        public Frame3f FrameS;
        public SnapType Type;

        public ISnapPoint Point;
        public ISnapSegment Segment;


        public SnapResult(ISnapPoint pt) { FrameS = pt.FrameS; Type = SnapType.Point; Point = pt; }
        public SnapResult(ISnapSegment seg, Vector3f hitScene) { FrameS = seg.GetHitFrameS(hitScene); Type = SnapType.Segment; Segment = seg; }

        public bool IsSame(SnapResult b) {
            if (b == null)
                return false;
            if (Type == SnapType.Point && b.Type == SnapType.Point)
                return Point.UniqueID == b.Point.UniqueID;
            else if (Type == SnapType.Segment && b.Type == SnapType.Segment)
                return Segment.UniqueID == b.Segment.UniqueID;
            return false;
        }

        public override string ToString()
        {
            if (Type == SnapType.Point)
                return Type.ToString() + "/" + Point.UniqueID;
            else if (Type == SnapType.Segment)
                return Type.ToString() + "/" + Point.UniqueID;
            else
                return Type.ToString();
        }
    }


    public static class SnapGeometryUniqueIDGenerator
    {
        public static int current_id = 1;
        public static object lockable = new object();
        public static int Allocate() {
            lock(lockable) {
                return current_id++;
            }
        }
    }





    public class SnapSet
    {
        FScene scene;

        bool enable_snap_points = true;
        bool enable_snap_segments = false;

        // flags to turn snapping on/off for different elements
        //  !!! Currently does not support changing during snap!
        public bool EnableSnapPoints
        {
            get { return enable_snap_points; }
            set { enable_snap_points = value; }
        }
        public bool EnableSnapSegments
        {
            get { return enable_snap_segments; }
            set { enable_snap_segments = value; }
        }


        // classes that can generate snap targets for scene objects
        public List<ISnapGenerator> Generators { get; set; }

        // we will not consider these objects for snapping
        public List<SceneObject> IgnoreSet { get; set; }

        // caching of snap targets
        struct CachedPoints {
            public int timestamp;
            public List<ISnapPoint> points;
        }
        Dictionary<SceneObject, CachedPoints> PointsCache;
        struct CachedSegments {
            public int timestamp;
            public List<ISnapSegment> segments;
        }
        Dictionary<SceneObject, CachedSegments> SegmentsCache;


        // active sets of scene objects, from which we collect active snap points
        List<SceneObject> ActiveSet;

        // active snap points/segments we are considering
        List<ISnapPoint> Points;
        List<ISnapSegment> Segments;


        public SnapSet(FScene scene)
        {
            this.scene = scene;

            Points = new List<ISnapPoint>();
            Segments = new List<ISnapSegment>();

            Generators = new List<ISnapGenerator>();

            IgnoreSet = new List<SceneObject>();
            ActiveSet = new List<SceneObject>();

            PointsCache = new Dictionary<SceneObject, CachedPoints>();
            SegmentsCache = new Dictionary<SceneObject, CachedSegments>();
        }


        // initializes snap set with default materials and known generators
        static public SnapSet CreateStandard(FScene scene)
        {
            SnapSet s = new SnapSet(scene);
            var genVisibleMaterial = MaterialUtil.CreateTransparentMaterial(ColorUtil.ForestGreen, 0.5f);
            var genHiddenMaterial = MaterialUtil.CreateTransparentMaterial(ColorUtil.ForestGreen, 0.2f);
            s.Generators.Add(
                new PrimitiveSnapGenerator(genVisibleMaterial, genHiddenMaterial));
            s.Generators.Add(
                new CurveSnapGenerator(genVisibleMaterial, genHiddenMaterial));
            return s;
        }


        public void Disconnect(bool bDestroyGenerators = true)
        {
            while (ActiveSet.Count > 0)
                RemoveFromActive(ActiveSet[0]);
            if (bDestroyGenerators) {
                foreach (var g in Generators)
                    g.Destroy();
            }
        }


        public List<ISnapPoint> GetCachedPoints(SceneObject so)
        {
            if (enable_snap_points == false)
                return null;
            if (PointsCache.ContainsKey(so))
                return PointsCache[so].points;
            return null;
        }
        public List<ISnapPoint> FindOrCachePoints(SceneObject so)
        {
            if (enable_snap_points == false)
                return null;
            if (PointsCache.ContainsKey(so)) {
                CachedPoints c = PointsCache[so];
                if (c.timestamp == so.Timestamp)
                    return c.points;

                // un-cache
                if (c.points != null) {
                    foreach (ISnapPoint pt in c.points)
                        pt.Destroy();
                }
                PointsCache.Remove(so);
            }

            List<ISnapPoint> vPoints = null;
            if (so is PivotSO) {
                vPoints = new List<ISnapPoint>() {
                    new PivotSOSnapPoint(so as PivotSO)
                };

            } else if ( so is GroupSO ) {
                foreach (SceneObject childso in (so as GroupSO).GetChildren()) {
                    List<ISnapPoint> pts = FindOrCachePoints(childso);
                    if (vPoints == null)
                        vPoints = pts;
                    else
                        vPoints.AddRange(pts);
                }

            } else {
                foreach (ISnapGenerator gen in Generators) {
                    if (gen.CanGenerate(so)) {
                        List<ISnapPoint> v = gen.GeneratePoints(so);
                        if (v != null && v.Count > 0) {
                            if (vPoints == null)
                                vPoints = v;
                            else
                                vPoints.AddRange(v);
                        }
                    }
                }
            }

            PointsCache[so] = new CachedPoints() { points = vPoints, timestamp = so.Timestamp };
            return vPoints;
        }



        public List<ISnapSegment> GetCachedSegments(SceneObject so)
        {
            if (enable_snap_segments == false)
                return null;
            if (SegmentsCache.ContainsKey(so))
                return SegmentsCache[so].segments;
            return null;
        }
        public List<ISnapSegment> FindOrCacheSegments(SceneObject so)
        {
            if (enable_snap_segments == false)
                return null;
            if (SegmentsCache.ContainsKey(so)) {
                CachedSegments c = SegmentsCache[so];
                if (c.timestamp == so.Timestamp)
                    return c.segments;

                // un-cache
                if (c.segments != null) {
                    foreach (ISnapSegment pt in c.segments)
                        pt.Destroy();
                }
                SegmentsCache.Remove(so);
            }

            List<ISnapSegment> vSegments = null;
                foreach (ISnapGenerator gen in Generators) {
                if (gen.CanGenerate(so)) {
                    List<ISnapSegment> v = gen.GenerateSegments(so);
                    if (v != null && v.Count > 0) {
                        if (vSegments == null)
                            vSegments = v;
                        else
                            vSegments.AddRange(v);
                    }
                }
            }

            SegmentsCache[so] = new CachedSegments() { segments = vSegments, timestamp = so.Timestamp };
            return vSegments;
        }


        void disable_points(SceneObject so) {
            List<ISnapPoint> v = GetCachedPoints(so);
            if (v != null) {
                foreach (var p in v) {
                    Points.Remove(p);
                    p.SetEnabled(false);
                }
            }
        }
        void enable_points(SceneObject so) {
            List<ISnapPoint> v = FindOrCachePoints(so);
            if (v != null) {
                foreach (var p in v) {
                    Points.Add(p);
                    p.SetEnabled(true);
                }
            }
        }
        void disable_segments(SceneObject so) {
            List<ISnapSegment> v = GetCachedSegments(so);
            if (v != null) {
                foreach (var p in v) {
                    Segments.Remove(p);
                    p.SetEnabled(false);
                }
            }
        }
        void enable_segments(SceneObject so) {
            List<ISnapSegment> v = FindOrCacheSegments(so);
            if (v != null) {
                foreach (var p in v) {
                    Segments.Add(p);
                    p.SetEnabled(true);
                }
            }
        }




        public void RemoveFromActive(SceneObject so)
        {
            if (IgnoreSet.Contains(so))
                return;
            if (ActiveSet.Contains(so) == false)
                return;

            if ( enable_snap_points )
                disable_points(so);
            if (enable_snap_segments)
                disable_segments(so);
            ActiveSet.Remove(so);
            so.OnTransformModified += OnActiveTransformModified;
        }


        public void AddToActive(IEnumerable<SceneObject> v)
        {
            foreach (var so in v)
                AddToActive(so);
        }
        public void AddToActive(SceneObject so)
        {
            if (IgnoreSet.Contains(so))
                return;
            if (ActiveSet.Contains(so))
                return;

            if (enable_snap_points)
                enable_points(so);
            if (enable_snap_segments)
                enable_segments(so);
            ActiveSet.Add(so);
            so.OnTransformModified += OnActiveTransformModified;
        }

        public void ClearActive()
        {
            while (ActiveSet.Count > 0)
                RemoveFromActive(ActiveSet[ActiveSet.Count - 1]);
        }


        // ugh if object is transformed we are just going to re-create snap points.
        // would be nice if there was a more efficient way to do this...
        public void OnActiveTransformModified(SceneObject so)
        {
            if (ActiveSet.Contains(so) == false)
                return;

            //if (enable_snap_points)
            //    disable_points(so);
            //if (enable_snap_segments)
            //    disable_segments(so);

            //if (enable_snap_points)
            //    enable_points(so);
            //if (enable_snap_segments)
            //    enable_segments(so);
        }



        public void PreRender(Vector3f cameraPosition)
        {
            foreach (var pt in Points)
                pt.PreRender(cameraPosition);
            foreach (var seg in Segments)
                seg.PreRender(cameraPosition);
        }





        //! returned hitFrame is in Scene coordinates!
        public SnapResult FindHitSnapPoint(Ray3f ray)
        {
            float fNearest = float.PositiveInfinity;
            ISnapPoint hitO = null;
            ISnapSegment hitSeg = null;

            int min_priority = int.MaxValue;
            foreach (var pt in Points) {
                if ( ! pt.IsEnabled() )
                    continue;
                float fHitDist;
                if ( pt.FindRayIntersection(ray, out fHitDist) ) {
                    if (fHitDist < fNearest || (pt.Priority < min_priority && MathUtil.EpsilonEqual(fHitDist, fNearest, 0.0001f))) {
                        fNearest = fHitDist;
                        hitO = pt;
                        min_priority = pt.Priority;
                    }
                }
            }

            foreach ( var seg in Segments ) {
                if ( ! seg.IsEnabled() )
                    continue;
                float fHitDist;
                if ( seg.FindRayIntersection(ray, out fHitDist) && fHitDist < fNearest ) {
                    fNearest = fHitDist;
                    hitSeg = seg;
                    hitO = null;
                }
            }

            if (hitO != null) {
                return new SnapResult(hitO);
            } else if (hitSeg != null) {
                Vector3f snapPosW = hitSeg.FindSnapPoint(ray);
                return new SnapResult(hitSeg, scene.ToSceneP(snapPosW));
            }
            return null;
        }



        //! input frame is in World coordinates!
        public SnapResult FindNearestSnapPointW(Frame3f fWorld, float fMaxRadiusW)
        {
            float fNearestSqr = float.PositiveInfinity;
            ISnapPoint hitO = null;

            Frame3f fScene = scene.ToSceneFrame(fWorld);
            float fRSqr = fMaxRadiusW / scene.GetSceneScale();
            fRSqr *= fRSqr;

            int min_priority = int.MaxValue;
            foreach (var pt in Points) {
                if (pt.IsEnabled() == false)
                    continue;
                float d = (fScene.Origin - pt.FrameS.Origin).LengthSquared;
                if ( d < fRSqr ) {
                    if (d < fNearestSqr || (pt.Priority < min_priority && MathUtil.EpsilonEqual(d, fNearestSqr, 0.0001f))  ) {
                        fNearestSqr = d;
                        hitO = pt;
                        min_priority = pt.Priority;
                    }
                }
            }

            return (hitO != null) ? new SnapResult(hitO) : null;
        }


        //! input frame is in World coordinates!
        public SnapResult FindNearestSnapPointS(Frame3f fScene, float fMaxRadiusS)
        {
            float fNearestSqr = float.PositiveInfinity;
            ISnapPoint hitO = null;

            float fRSqr = fMaxRadiusS * fMaxRadiusS;
            int min_priority = int.MaxValue;
            foreach (var pt in Points) {
                if (pt.IsEnabled() == false)
                    continue;
                float d = (fScene.Origin - pt.FrameS.Origin).LengthSquared;
                if (d < fRSqr ) {
                    if (d < fNearestSqr || (pt.Priority < min_priority && MathUtil.EpsilonEqual(d, fNearestSqr, 0.0001f)) ) {
                        fNearestSqr = d;
                        hitO = pt;
                        min_priority = pt.Priority;
                    }
                }
            }

            return (hitO != null) ? new SnapResult(hitO) : null;
        }


    }












    // default impl of ISnapPoint for Pivot objects
    public class PivotSOSnapPoint : ISnapPoint
    {
        public PivotSO so;

        public object Source { get { return so; } }

        public Frame3f FrameS
        {
            get { return so.GetLocalFrame(CoordSpace.SceneCoords); }
        }
        Frame3f WorldFrame
        {
            get { return so.GetLocalFrame(CoordSpace.WorldCoords); }
        }

        int unique_id;
        public int UniqueID { get { return unique_id; } }

        int priority = 10;
        public int Priority { get { return priority; } }

        public PivotSOSnapPoint(PivotSO pivotSO, int priority = 10)
        {
            so = pivotSO;
            unique_id = SnapGeometryUniqueIDGenerator.Allocate();
            this.priority = priority;
        }


        public bool FindRayIntersection(Ray3f ray, out float fHitDist)
        {
            fHitDist = float.PositiveInfinity;
            SORayHit hit;
            if (so.FindRayIntersection(ray, out hit)) {
                fHitDist = hit.fHitDist;
                return true;
            }
            return false;
        }
        public void PreRender(Vector3f cameraPosition) { } // PivotSO handles
        public bool IsEnabled() { return true; }
        public void SetEnabled(bool bEnabled) { } // ignore

        public void Destroy()
        {
            // nothing necessary as this is a SO
        }
    }



}
