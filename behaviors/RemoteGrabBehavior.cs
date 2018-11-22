// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using g3;
using f3;

namespace gs
{
    public class RemoteGrabBehavior : StandardInputBehavior
    {
        Cockpit cockpit;

        public float RotationSpeed = 1.0f;
        public float TranslationSpeed = 1.0f;

        // [RMS] GrabbedSO and InGrab are invalid if we are using this behavior for
        //   both hands simultaneously, and user grabs two things!!
        //   (maybe register separate versions for left & right??)

        bool in_grab = false;
        public bool InGrab { get { return in_grab; } }

        SceneObject grabbed_so = null;
        public SceneObject GrabbedSO { get { return grabbed_so; } }

        SnapSet Snaps;
        PreRenderHelper SnapHelper;
        public bool EnableSnapping { get; set; }

        public delegate void GrabEventHandler(object sender, SceneObject target);
        public event GrabEventHandler OnBeginGrab;
        public event GrabEventHandler OnEndGrab;


        public RemoteGrabBehavior(Cockpit cockpit)
        {
            this.cockpit = cockpit;

            EnableSnapping = true;

            // build snap target set
            Snaps = SnapSet.CreateStandard(cockpit.Scene);
            SnapHelper = new PreRenderHelper("remote_grab_snap_helper") {
                PreRenderF = () => { Snaps.PreRender(cockpit.ActiveCamera.GetPosition()); }
            };

            cockpit.Scene.SelectionChangedEvent += ActiveScene_SelectionChangedEvent;
            cockpit.Scene.ChangedEvent += ActiveScene_ChangedEvent;
        }

        public override InputDevice SupportedDevices
        {
            get { return InputDevice.AnySpatialDevice; }
        }






        public void snaps_begin_grab(SceneObject so)
        {
            if (EnableSnapping == false)
                return;

            Snaps.IgnoreSet.Add(so);
            Snaps.AddToActive(cockpit.Scene.Selected);
            Snaps.AddToActive( cockpit.Scene.Find((x) => x is PivotSO) );
            cockpit.Scene.AddUIElement(SnapHelper);
        }
        public void snaps_end_grab(SceneObject so)
        {
            if (EnableSnapping == false)
                return;

            Snaps.IgnoreSet.Remove(so);
            Snaps.Disconnect(false);
            cockpit.Scene.RemoveUIElement(SnapHelper, false);
        }


        private void ActiveScene_SelectionChangedEvent(object sender, EventArgs e)
        {
            // if we are in a potential snap-drag, update the active set based on
            // the new selection
            if (Snaps.IgnoreSet.Count > 0) {
                var selected = cockpit.Scene.Selected;
                Snaps.ClearActive();
                foreach (SceneObject so in selected) {
                    if (so is PivotSO || Snaps.IgnoreSet.Contains(so))
                        continue;
                    Snaps.AddToActive(so);
                }
                Snaps.AddToActive(cockpit.Scene.Find((x) => x is PivotSO));
            }
        }

        private void ActiveScene_ChangedEvent(object sender, SceneObject so, SceneChangeType type)
        {
            // [TODO] if we are in a grab, and the object we are grabbing is removed, we need to
            // gracefully abort somehow...
            // Problem is that we do not know at behavior level which object is grabbed...that info
            // is in GrabInfo, which is CaptureData...maybe Behavior can somehow query active CaptureData??
            //
            // (This happens if we do a grab on a multi-select...then we actually selected a TransientGroupSO,
            //  and we start grab, but they immediately discard this GroupSO when we override the gizmo. 
            //  So our CaptureData is holding a ref to a SO that has been deleted!)
            //
            //if ( in_grab && type == SceneChangeType.Removed && so == 
        }





        void begin_transform(SceneObject so)
        {
            cockpit.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);
            in_grab = true;
            grabbed_so = so;
        }


        void push_position_change(CaptureData data)
        {
            GrabInfo gi = data.custom_data as GrabInfo;
            gi.change.parentAfter = gi.grabbedSO.GetLocalFrame(CoordSpace.SceneCoords);
            gi.change.parentScaleAfter = gi.grabbedSO.GetLocalScale();

            cockpit.Scene.History.PushChange(gi.change, true);
            cockpit.Scene.History.PushInteractionCheckpoint();
        }

        Capture end_transform(CaptureData data)
        {
            push_position_change(data);
            cockpit.Context.TransformManager.PopOverrideGizmoType();
            in_grab = false;
            grabbed_so = null;
            return Capture.End;
        }



        class GrabInfo
        {
            public SceneObject grabbedSO;
            public Cockpit cockpit;
            public Frame3f startObjFW;
            public Frame3f startObjRelF;
            public Frame3f startHandF;
            public Vector2f stickDelta;

            public float RotationSpeed = 1.0f;
            public float TranslationSpeed = 1.0f;

            public TransformGizmoChange change;        // [TODO] shouldn't be using gizmo change for this?
            public GrabInfo(Cockpit cockpit, SceneObject so, Frame3f handF)
            {
                this.cockpit = cockpit;
                this.grabbedSO = so;
                this.startHandF = handF;
                this.startObjFW = so.GetLocalFrame(CoordSpace.WorldCoords);
                this.startObjRelF = this.startHandF.ToFrame(this.startObjFW);
                this.stickDelta = Vector2f.Zero;
                change = new TransformGizmoChange() {
                    parentSO = new WeakReference(so),
                    parentBefore = so.GetLocalFrame(CoordSpace.SceneCoords),
                    parentScaleBefore = so.GetLocalScale()
                };
            }

            public Frame3f curHandF;
            public Frame3f curHandTargetF;
            public Frame3f curUseTargetF;

            DynamicSnapSolver snapSolver;


            public void update(Frame3f handF, SnapSet snap)
            {
                curHandF = handF;

                // [RMS] this function updates the position of object based on hand frame
                //  Not clear how this should work, there are lots of options...

                // [1] scaled relative motion of hand inherited by object  (lags ray though)
                //Vector3 dt = startHandF.ToFrameP(handF.Origin);
                //dt *= 10.0f;
                //Frame3 fNew = new Frame3(startObjFW);
                //fNew.Origin += dt;

                // [2] object stays on ray, inherits a bit of xform
                //   - resulting orientation is weird. works well for rotate in-place around ray,
                //     but up/down/left/right tilts are impossible w/o moving object
                Frame3f fNew = handF.FromFrame(this.startObjRelF);
                if (RotationSpeed != 1.0f) {
                    fNew.Rotation = Quaternionf.Slerp(startObjFW.Rotation, fNew.Rotation, RotationSpeed);
                }
                if (TranslationSpeed != 1.0f) {
                    fNew.Origin = Vector3f.Lerp(startObjFW.Origin, fNew.Origin, TranslationSpeed);
                }

                // [3] object stays on ray but no rotation
                //   - weird if you rotate left/right, because distance stays same but it
                //     keeps pointing in same direction
                //   - we have gizmo for this kind of translation.
                //Frame3 fNew = handF.FromFrame(this.startObjRelF);
                //fNew.Rotation = startObjFW.Rotation;

                // [4] object stays in place, rotate by hand rotation
                //   - pretty hard to control, but would be good for approx orienting...
                //   - definitely needs damping!!
                //Frame3 fNew = startObjFW;
                //Quaternion relative = handF.Rotation * Quaternion.Inverse(startHandF.Rotation);
                //fNew.Rotation = relative * fNew.Rotation;

                // apply stick rotation  DOESN"T WORK
                //Quaternion stickY = Quaternion.AngleAxis(stickDelta[1], startHandF.X);
                //fNew.Rotation = fNew.Rotation * stickY;

                // shift in/out along hand-ray by Z
                fNew.Origin += 0.1f * stickDelta[1] * handF.Z * cockpit.Scene.GetSceneScale();

                curHandTargetF = fNew;
                curUseTargetF = new Frame3f(curHandTargetF);

                if (snap != null) {
                    if (snapSolver == null)
                        snapSolver = new DynamicSnapSolver(grabbedSO);
                    snapSolver.SnapOrientation =
                        (cockpit.Context.TransformManager.ActiveFrameType == FrameType.LocalFrame);
                    curUseTargetF = snapSolver.UpdateSnapW(curUseTargetF, snap);
                }

                // update so
                grabbedSO.SetLocalFrame(curUseTargetF, CoordSpace.WorldCoords);
            }


            public void complete()
            {
                if (snapSolver != null)
                    snapSolver.Disconnect();
            }

        }

        public override CaptureRequest WantsCapture(InputState input)
        {
            if ((input.bLeftTriggerDown && input.bLeftShoulderPressed)
                  || (input.bLeftTriggerPressed && input.bLeftShoulderDown)) {
                return CaptureRequest.Begin(this, CaptureSide.Left);
            } else if ((input.bRightTriggerDown && input.bRightShoulderPressed)
                  || (input.bRightTriggerPressed && input.bRightShoulderDown)) {
                return CaptureRequest.Begin(this, CaptureSide.Right);
            } else
                return CaptureRequest.Ignore;
        }

        public override Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            Ray3f useRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            SORayHit rayHit;
            if (cockpit.Scene.FindSORayIntersection(useRay, out rayHit, (so) => { return so.IsTemporary == false; }  )) {
                var tso = rayHit.hitSO;
                if (tso != null) {
                    Frame3f handF = (eSide == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
                    begin_transform(tso);
                    snaps_begin_grab(tso);
                    if ( OnBeginGrab != null )
                        OnBeginGrab(this, tso);
                    return Capture.Begin(this, eSide, 
                        new GrabInfo(cockpit, tso, handF) { RotationSpeed = this.RotationSpeed, TranslationSpeed = this.TranslationSpeed } );
                }
            }
            return Capture.Ignore;
        }


        public override Capture UpdateCapture(InputState input, CaptureData data)
        {
            GrabInfo gi = data.custom_data as GrabInfo;

            bool bFinished = false;
            if (data.which == CaptureSide.Left && (input.bLeftShoulderReleased || input.bLeftTriggerReleased)) {
                bFinished = true;
            } else if (data.which == CaptureSide.Right && (input.bRightShoulderReleased || input.bRightTriggerReleased)) {
                bFinished = true;
            }
            if ( bFinished ) {
                snaps_end_grab(gi.grabbedSO);
                gi.complete();
                Capture result = end_transform(data);
                if ( OnEndGrab != null )
                    OnEndGrab(this, gi.grabbedSO);
                return result;
            }

            Frame3f handF = (data.which == CaptureSide.Left) ? input.LeftHandFrame : input.RightHandFrame;
            gi.stickDelta += (data.which == CaptureSide.Left) ? input.vLeftStickDelta2D : input.vRightStickDelta2D;
            gi.update(handF, EnableSnapping ? Snaps : null );


            // drop-a-copy on X/A button release
            if ( (data.which == CaptureSide.Left && input.bXButtonReleased) ||
                 (data.which == CaptureSide.Right && input.bAButtonReleased ) ) {

                SceneObject copy = gi.grabbedSO.Duplicate();

                // save an undo-point for the current xform, and start a new one. That way we can
                //  step between drop-a-copy stages
                push_position_change(data);
                gi.change = new TransformGizmoChange() {
                    parentSO = new WeakReference(gi.grabbedSO),
                    parentBefore = gi.grabbedSO.GetLocalFrame(CoordSpace.SceneCoords),
                    parentScaleBefore = gi.grabbedSO.GetLocalScale()
                };

                // if we do this afterwards, and don't push an interaction state, then when 
                //   we undo/redo we don't end up sitting on top of a duplicate.
                cockpit.Scene.History.PushChange(
                    new AddSOChange() { scene = cockpit.Scene, so = copy, bKeepWorldPosition = false });

            }


            return Capture.Continue;
        }

        public override Capture ForceEndCapture(InputState input, CaptureData data)
        {
            GrabInfo gi = data.custom_data as GrabInfo;
            snaps_end_grab(gi.grabbedSO);
            gi.complete();

            return end_transform(data);
        }

    }

}
