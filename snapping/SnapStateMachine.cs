// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;

namespace gs
{
    public enum SnapState
    {
        NoSnap,
        PendingSnapChange,
        Snapped
    }


    //
    // This class provides a snapping experience where there is a slight delay
    //  between changing potential snap targets. This damps out the jumpy behavior
    //  that occurs with immediate per-frame snap updates.
    //
    //   usage:
    //      1) construct SnapStateMachine on begin-drag
    //      2) each frame, find snap_hit (or null)
    //      3) call UpdateState(snap_hit or null)
    //      4) if .IsSnapped, then snap to .ActiveSnapTarget
    //
    public class SnapStateMachine<T> where T : class, ISnapCompare<T>
    {
        // delay in seconds between snap transitions
        public float SnapDelay = 0.2f;

        // if this returns true, you should be snapping to the active target
        public bool IsSnapped { get { return (ActiveSnapTarget != null); } }

        // current snap target. If this is non-null, you should be snapping to it.
        public T ActiveSnapTarget;
        public object ActiveSnapData;

        // internals
        SnapState eState;
        SnapState State { get { return eState; } }
        T PendingSnapTarget;
        object PendingSnapData;
        double startPending;


        public SnapStateMachine()
        {
            eState = SnapState.NoSnap;
            startPending = 0;
        }


        public bool UpdateState(T snapHit, object userData = null) 
        {
            bool bStateChanged = false;

            // update state machine
            if (snapHit != null) {

                if (eState == SnapState.NoSnap) {
                    // have no snap, transition to pending snap

                    eState = SnapState.PendingSnapChange;
                    PendingSnapTarget = snapHit;
                    PendingSnapData = userData;
                    startPending = FPlatform.RealTime();
                    bStateChanged = true;

                } else if (eState == SnapState.Snapped) {

                    if ( snapHit.IsSame(ActiveSnapTarget) == false ) {
                        // snapped but got new target, transition to pending-change state
                        eState = SnapState.PendingSnapChange;
                        PendingSnapTarget = snapHit;
                        PendingSnapData = userData;
                        startPending = FPlatform.RealTime();
                        bStateChanged = true;

                    } else {
                        // same "snap point" but incoming hit might have new data, so switch to it
                        ActiveSnapTarget = snapHit;
                        ActiveSnapData = userData;
                        bStateChanged = true;
                    }

                } else if (eState == SnapState.PendingSnapChange) {

                    // ? we could have a state here where if we see current active, we switch back
                    //   to snappend state? this might stabilize in noisy conditions...

                    if ( snapHit.IsSame(PendingSnapTarget) == false ) {
                        // in pending but got new pending target, stay in pending, reset timer
                        PendingSnapTarget = snapHit;
                        PendingSnapData = userData;
                        startPending = FPlatform.RealTime();

                    } else if (FPlatform.RealTime() - startPending > SnapDelay) {
                        // delay elapsed, transition to snapped with pending target set as active
                        eState = SnapState.Snapped;
                        ActiveSnapTarget = PendingSnapTarget;
                        PendingSnapTarget = null;
                        ActiveSnapData = PendingSnapData;
                        PendingSnapData = null;
                        bStateChanged = true;
                    }
                }

            } else {        // no-hit branch

                if (eState == SnapState.Snapped) {
                    // currently snapped but got a null target, transition to pending-nosnap state
                    eState = SnapState.PendingSnapChange;
                    PendingSnapTarget = null;
                    PendingSnapData = null;
                    startPending = FPlatform.RealTime();
                    bStateChanged = true;

                } else if (eState == SnapState.PendingSnapChange) {

                    if (PendingSnapTarget != null) {
                        // in pending but got a no-hit, transition to pending-nosnap
                        PendingSnapTarget = null;
                        PendingSnapData = null;
                        startPending = FPlatform.RealTime();

                    } else if (FPlatform.RealTime() - startPending > SnapDelay) {
                        // in pending and timer elapsed, transition to no-snap state
                        eState = SnapState.NoSnap;
                        ActiveSnapTarget = null;
                        ActiveSnapData = null;
                        bStateChanged = true;
                    }
                }
            }

            return bStateChanged;
        }
    }
}
