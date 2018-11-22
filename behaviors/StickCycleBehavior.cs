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
    public class StickCycleBehavior : StandardInputBehavior
    {
        //SceneController context;
        public CaptureSide Side = CaptureSide.Left;
        public int StickAxis = 0;
        public float RepeatTime = 0.5f;
        public Action<int> Cycle = (x) => { };

        public StickCycleBehavior(FContext scene)
        {
            //this.context = scene;
            Priority = 100;
        }

        public override InputDevice SupportedDevices
        {
            get { return InputDevice.AnySpatialDevice; }
        }

        public override CaptureRequest WantsCapture(InputState input)
        {
            if (Side == CaptureSide.Any || Side == CaptureSide.Both)
                throw new NotImplementedException("StickCycleBehavior.WantsCapture: both/any not supported!");

            // RMS HACK to avoid conflict w/ camera
            if (input.RightCaptureActive && input.bRightShoulderDown)
                return CaptureRequest.Ignore;

            if ( (Side == CaptureSide.Left && input.vLeftStickDelta2D[StickAxis] != 0) ||
                 (Side == CaptureSide.Right && input.vRightStickDelta2D[StickAxis] != 0) )
            {
                return CaptureRequest.Begin(this, Side);
            }
            return CaptureRequest.Ignore;
        }


        class internal_data
        {
            public float last_sent_time = -1;
            public int last_direction = 0;
        }


        public override Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            return Capture.Begin(this, eSide, new internal_data() );
        }

        public override Capture UpdateCapture(InputState input, CaptureData data)
        {
            Vector2f vDelta = input.StickDelta2D((int)data.which);
            internal_data d = data.custom_data as internal_data;

            if ( vDelta[0] < -0.75f ) {
                if (d.last_sent_time == -1 || d.last_direction > 0 || FPlatform.RealTime() - d.last_sent_time > RepeatTime) {
                    Cycle(-1);
                    d.last_sent_time = FPlatform.RealTime();
                    d.last_direction = -1;
                }
            } else if (vDelta[0] > 0.75f ) {
                if (d.last_sent_time == -1 || d.last_direction < 0 || FPlatform.RealTime() - d.last_sent_time > RepeatTime) {
                    Cycle(1);
                    d.last_sent_time = FPlatform.RealTime();
                    d.last_direction = 1;
                }
            }

            if ( vDelta[StickAxis] == 0 ) {
                return Capture.End;
            }

            return Capture.Continue;
        }

        public override Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }
    }
}
