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
    public class SmoothCockpitTracker : ICockpitViewTracker
    {

        public static SmoothCockpitTracker Enable(Cockpit cp)
        {
            SmoothCockpitTracker tracker = new SmoothCockpitTracker();
            cp.CustomTracker = tracker;
            cp.PositionMode = Cockpit.MovementMode.CustomTracking;
            return tracker;
        }

        public bool IsLocked { get; set; }


        //GameObject tracking_debug;
        //GameObject tracking_avg;

        public enum TrackingState
        {
            NotTracking,
            TrackingWarmup,
            Tracking,
            TrackingCooldown
        }
        TrackingState eState = TrackingState.NotTracking;
        public TrackingState State { get { return eState; } }

        Frame3f currentFrame;
        bool bInitialized = false;
        Vector3f vSlowViewDirTrack;
        double stateChangeStartTime;
        double animation_last_time;

        CockpitTrackingWidget indicator;
        bool show_indicator = true;
        public bool ShowIndicator {
            get { return show_indicator; }
            set { show_indicator = value; }
        }


        public SmoothCockpitTracker()
        {
            vSlowViewDirTrack = Vector3f.Zero;
            IsLocked = false;
        }



        public delegate void TrackingStateChange(TrackingState eState);
        public event TrackingStateChange OnTrackingStateChange;
        
        void set_tracking_state(TrackingState e)
        {
            if ( eState != e ) {
                eState = e;
                FUtil.SafeSendAnyEvent(OnTrackingStateChange, e);
            }
        }


        public void UpdateTracking(Cockpit cockpit, fCamera camera)
        {
            fGameObject cockpitGO = cockpit.RootGameObject;

            if (!bInitialized) {
                currentFrame = cockpit.GetLevelViewFrame(CoordSpace.WorldCoords);
                currentFrame.ConstrainedAlignAxis(2, Vector3f.AxisZ, Vector3f.AxisY);
                bInitialized = true;
            }

            Vector3f vCamFW = camera.Forward();
            vCamFW[1] = 0; vCamFW.Normalize();      // I don't think this is strictly necessary but
                                                    // better to be safe for now...
            Vector3f vCamPos = camera.GetPosition();

            //if (tracking_debug == null)
            //    tracking_debug = UnityUtil.CreatePrimitiveGO("tracking_indicator", PrimitiveType.Sphere, MaterialUtil.CreateStandardMaterial(Color.green), false);
            //tracking_debug.transform.position = vCamPos + 15.0f * vCamFW;

            //if (tracking_avg == null) {
            //    tracking_avg = UnityUtil.CreatePrimitiveGO("tracking_indicator", PrimitiveType.Sphere, MaterialUtil.CreateStandardMaterial(Color.blue), false);
            //    tracking_avg.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            //}
            //tracking_avg.transform.position = vCamPos + 10.0f * vSlowViewDirTrack;

            //tracking_debug.SetVisible(false);
            //tracking_avg.SetVisible(false);

            if (vSlowViewDirTrack == Vector3f.Zero)
                vSlowViewDirTrack = vCamFW;

            float slowTrackSpeed = 0.05f;
            vSlowViewDirTrack = VRUtil.AngleLerp(vSlowViewDirTrack, vCamFW, slowTrackSpeed);


            // head position tracking
            if (IsLocked == false) {
                cockpitGO.SetPosition(vCamPos);
            }
            //Vector3 vDelta = (camera.transform.position - RootGameObject.transform.position);
            //if (vDelta.magnitude > 0.2f)
            //    RootGameObject.transform.position = camera.transform.position;
            ////else if ( vDelta.magnitude > 0.05f)
            //else
            //    RootGameObject.transform.position =
            //        (1.0f - TrackingSpeed) * RootGameObject.transform.position +
            //        (TrackingSpeed) * camera.transform.position;


            float RotationSpeed = 200.0f;
            float WarmupTrackingAngleThresh = 55.0f;
            float ImmediateTrackingAngleThresh = 65.0f;
            float StopTrackingAngleThresh = 5.0f;
            float TrackingWarmupDelay = 2.0f;
            float TrackingCooldownDelay = 0.75f;


            //Vector3 vCockpitFW = cockpitGO.transform.forward;
            Vector3f vCockpitFW = currentFrame.Z;

            float fSlowHDeviation = VRUtil.PlaneAngle(vCockpitFW, vSlowViewDirTrack);
            float fActualViewDeviation = VRUtil.PlaneAngle(vCockpitFW, vCamFW);

            bool bDoTrack = false;
            if (eState == TrackingState.NotTracking) {
                //tracking_debug.GetComponent<Renderer>().material = MaterialUtil.CreateStandardMaterial(Color.green);
                if (fSlowHDeviation > WarmupTrackingAngleThresh) {
                    set_tracking_state(TrackingState.TrackingWarmup);
                    stateChangeStartTime = FPlatform.RealTime();
                }

            } else if (eState == TrackingState.TrackingWarmup) {
                //tracking_debug.GetComponent<Renderer>().material = MaterialUtil.CreateStandardMaterial(Color.yellow);
                if (fSlowHDeviation > ImmediateTrackingAngleThresh) {
                    set_tracking_state(TrackingState.Tracking);
                } else if (fSlowHDeviation > WarmupTrackingAngleThresh) {
                    if ((FPlatform.RealTime() - stateChangeStartTime) > TrackingWarmupDelay)
                        set_tracking_state(TrackingState.Tracking);
                } else
                    set_tracking_state(TrackingState.NotTracking);

            } else if (eState == TrackingState.Tracking) {
                bDoTrack = true;
                //tracking_debug.GetComponent<Renderer>().material = MaterialUtil.CreateStandardMaterial(Color.red);
                if (fActualViewDeviation < StopTrackingAngleThresh) {
                    set_tracking_state(TrackingState.TrackingCooldown);
                    stateChangeStartTime = FPlatform.RealTime();
                }
            } else if (eState == TrackingState.TrackingCooldown) {
                bDoTrack = true;
                //tracking_debug.GetComponent<Renderer>().material = MaterialUtil.CreateStandardMaterial(Color.gray);
                if (fActualViewDeviation < StopTrackingAngleThresh) {
                    if ((FPlatform.RealTime() - stateChangeStartTime) > TrackingCooldownDelay) {
                        set_tracking_state(TrackingState.NotTracking);
                        bDoTrack = false;
                    }
                } else {
                    set_tracking_state(TrackingState.Tracking);
                }
            }

            if (IsLocked) {
                bDoTrack = false;
                set_tracking_state(TrackingState.NotTracking);
            }


            if (bDoTrack) {
                float dt = (float)(FPlatform.RealTime() - animation_last_time);
                float fDelta = RotationSpeed * dt;

                Vector3f vCurrent = new Vector3f(vCockpitFW[0], 0, vCockpitFW[2]).Normalized;
                Vector3f vTarget = new Vector3f(vSlowViewDirTrack[0], 0, vSlowViewDirTrack[2]).Normalized;
                //Vector3 vTarget = new Vector3(vCamFW[0], 0, vCamFW[2]).normalized;
                Vector3f c = Vector3f.Cross(vCurrent, vTarget);
                float a = Vector3f.AngleD(vCurrent, vTarget);
                float fSign = (c[1] < 0) ? -1.0f : 1.0f;

                float fRotAngle = Math.Min(a, fDelta) * fSign;

                currentFrame.Rotate(Quaternionf.AxisAngleD(Vector3f.AxisY, fRotAngle));
            }
            cockpitGO.SetRotation(currentFrame.Rotation);
            animation_last_time = FPlatform.RealTime();


            if (indicator == null) {
                indicator = new CockpitTrackingWidget();
                indicator.Create(this, cockpit);
                cockpit.AddUIElement(indicator, false);
            }
            indicator.EnableIndicator = show_indicator;

        }

    }




    class CockpitTrackingWidget : HUDStandardItem
    {
        fGameObject indicator;

        fMaterial matTracking, matFixed, matHover, matLocked, matLockedHover;

        public bool EnableIndicator = true;
        public float IndicatorDistance = 0.7f;

        Cockpit cockpit;
        SmoothCockpitTracker tracker;

        public override fGameObject RootGameObject {
            get { return indicator; }
        }

        // creates a button in the desired geometry shape
        public void Create(SmoothCockpitTracker tracker, Cockpit cockpit)
        {
            this.tracker = tracker;
            this.cockpit = cockpit;

            fGameObject cockpitGO = cockpit.RootGameObject;
            Frame3f cockpitF = cockpitGO.GetWorldFrame();

            matFixed = MaterialUtil.CreateStandardMaterialF(ColorUtil.ForestGreen);
            matTracking = MaterialUtil.CreateStandardMaterialF(ColorUtil.SelectionGold);
            matHover = MaterialUtil.CreateStandardMaterialF(ColorUtil.Lighten(ColorUtil.ForestGreen, 0.1f));
            matLocked = MaterialUtil.CreateStandardMaterialF(ColorUtil.CgRed);
            matLockedHover  = MaterialUtil.CreateStandardMaterialF(ColorUtil.Lighten(ColorUtil.CgRed, 0.1f));
            

            indicator = UnityUtil.CreatePrimitiveGO("tracking_indicator", UnityEngine.PrimitiveType.Capsule, matFixed, true);
            MaterialUtil.DisableShadows(indicator);
            UnityUtil.SetLayerRecursive(indicator, FPlatform.HUDLayer);
            indicator.SetLocalScale(0.025f * Vector3f.One);
            indicator.RotateD(Vector3f.AxisZ, 90.0f);
            Vector3f vDir = (1.0f * cockpitF.Z - 1.0f * cockpitF.Y).Normalized;
            indicator.SetPosition( cockpitF.Origin + IndicatorDistance * vDir );

            AppendExistingGO(indicator);

            //indicator.transform.SetParent(cockpit.RootGameObject.transform, true);
        }


        public override void PreRender()
        {
            if (EnableIndicator
                  && cockpit.PositionMode == Cockpit.MovementMode.CustomTracking
                  && cockpit.CustomTracker == tracker) {
                indicator.Show();
                if (tracker.IsLocked)
                    indicator.SetMaterial(matLocked, true);
                else if (tracker.State != SmoothCockpitTracker.TrackingState.NotTracking)
                    indicator.SetMaterial(matTracking, true);
                else
                    indicator.SetMaterial(matFixed, true);
            } else {
                indicator.Hide();
            }
        }


        bool in_drag = false;
        Frame3f hitFrame;
        Vector3f startHitPos;
        float start_tilt = 0;
        float start_shift = 0;

        float last_click_time = 0.0f;
        bool in_double_click = false;

        override public bool WantsCapture(InputEvent e)
        {
            return (Enabled && HasGO(e.hit.hitGO) && in_drag == false);
        }

        override public bool BeginCapture(InputEvent e)
        {
            if (Enabled && HasGO(e.hit.hitGO) && in_drag == false) {
                if (last_click_time != 0
                      && (FPlatform.RealTime() - last_click_time) < SceneGraphConfig.ActiveDoubleClickDelay) {
                    in_double_click = true;
                } else {
                    hitFrame = cockpit.GetLocalFrame(CoordSpace.WorldCoords);
                    float fRayT = 0.0f;
                    if (RayIntersection.Sphere(e.ray.Origin, e.ray.Direction, hitFrame.Origin, IndicatorDistance, out fRayT) == false)
                        return false;       // should not happen! but could.
                    startHitPos = e.ray.Origin + fRayT * e.ray.Direction;
                    start_tilt = cockpit.TiltAngle;
                    start_shift = cockpit.ShiftAngle;
                    in_drag = true;
                }
                return true;
            }
            return false;
        }

        override public bool UpdateCapture(InputEvent e)
        {
            if ( in_drag ) {
                float fRayT = 0.0f;
                if (RayIntersection.Sphere(e.ray.Origin, e.ray.Direction, hitFrame.Origin, IndicatorDistance, out fRayT) == false)
                    return true;        // should not happen, but I guess it could if user
                                        // had arms out in some crazy position...
                Vector3f v = e.ray.Origin + fRayT * e.ray.Direction;
                Vector3f vToHit = (v - hitFrame.Origin).Normalized;
                Vector3f vToStart = (startHitPos - hitFrame.Origin).Normalized;
                float deltaUp = -VRUtil.PlaneAngleSigned(vToHit, vToStart, hitFrame.X);
                cockpit.TiltAngle = MathUtil.Clamp(start_tilt + deltaUp, -30.0f, 20.0f);
                float deltaLR = -VRUtil.PlaneAngleSigned(vToHit, vToStart, hitFrame.Y);
                cockpit.ShiftAngle = start_shift + deltaLR;
                //if ( tracker.IsLocked == false )
                cockpit.ShiftAngle = MathUtil.Clamp(cockpit.ShiftAngle, -10.0f, 10.0f);
            }

            return true;
        }

        override public bool EndCapture(InputEvent e)
        {
            if (in_double_click) {
                tracker.IsLocked = !tracker.IsLocked;
                last_click_time = 0.0f;
                in_double_click = false;
            } else { 
                in_drag = false;
                last_click_time = FPlatform.RealTime();
            }
            return true;
        }


        override public bool EnableHover
        {
            get { return true; }
        }
        override public void UpdateHover(Ray3f ray, UIRayHit hit)
        {
            if ( Enabled && HasGO(hit.hitGO) ) {
                indicator.SetMaterial((tracker.IsLocked) ? matLockedHover : matHover, true);
            }
        }
        override public void EndHover(Ray3f ray)
        {
            // just sets the right material back
            PreRender();
        }


    }


}
