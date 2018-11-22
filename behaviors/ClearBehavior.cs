// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;

namespace gs
{
    public class ClearBehavior : StandardInputBehavior
    {
        FContext context;

        public ClearBehavior(FContext scene)
        {
            this.context = scene;
            Priority = 10;
        }

        public override InputDevice SupportedDevices
        {
            get { return InputDevice.AnySpatialDevice; }
        }

        public override CaptureRequest WantsCapture(InputState input)
        {
            if ( input.bLeftStickPressed ^ input.bRightStickPressed ) {
                CaptureSide eSide = (input.bLeftStickPressed) ? CaptureSide.Left : CaptureSide.Right;
                return CaptureRequest.Begin(this, eSide);
            }
            return CaptureRequest.Ignore;
        }


        public override Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            return Capture.Begin(this, eSide);
        }

        public override Capture UpdateCapture(InputState input, CaptureData data)
        {
            if ( (data.which == CaptureSide.Left && input.bLeftStickReleased) ||
                 (data.which == CaptureSide.Right && input.bRightStickReleased) ) {

                int nSide = (data.which == CaptureSide.Left) ? 0 : 1;
                if (context.ToolManager.HasActiveTool(nSide)) {

                    context.ToolManager.DeactivateTool(nSide);

                } else if (
                    ((data.which == CaptureSide.Left && input.RightCaptureActive == false) ||
                     (data.which == CaptureSide.Right&& input.LeftCaptureActive == false) )
                       && context.Scene.Selected.Count > 0 ) { 
                    
                    context.Scene.ClearSelection();
                }

                return Capture.End;
            } else {
                return Capture.Continue;
            }

        }

        public override Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }
    }

}
