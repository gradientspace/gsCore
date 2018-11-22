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
    /// <summary>
    /// Variant of RemoteGrabBehavior that is meant to have a higher priority and captures
    /// "alternate" hand action. The ideal is that this allows a grab with one hand, then
    /// grab with second that disables snap, so that they can be simulatanously moved
    /// into better snapping positions. 
    /// </summary>
    public class SecondaryGrabBehavior : RemoteGrabBehavior
    {
        RemoteGrabBehavior primaryGrab;

        public SecondaryGrabBehavior(Cockpit cockpit, RemoteGrabBehavior primary) : base(cockpit)
        {
            EnableSnapping = false;
            primaryGrab = primary;
        }


        public override CaptureRequest WantsCapture(InputState input)
        {
            if ((input.bLeftTriggerDown && input.bLeftShoulderPressed)
                  || (input.bLeftTriggerPressed && input.bLeftShoulderDown)) {

                if ( input.RightCaptureActive && primaryGrab.InGrab )
                    return CaptureRequest.Begin(this, CaptureSide.Left);

            } else if ((input.bRightTriggerDown && input.bRightShoulderPressed)
                  || (input.bRightTriggerPressed && input.bRightShoulderDown)) {

                if ( input.LeftCaptureActive && primaryGrab.InGrab )
                    return CaptureRequest.Begin(this, CaptureSide.Right);

            }

            return CaptureRequest.Ignore;
        }


    }
}
