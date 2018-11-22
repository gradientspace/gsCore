// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;

namespace gs
{
    public class EditObjectGizmoBuilder : ITransformGizmoBuilder
    {
        public bool SupportsMultipleObjects { get { return false; } }

        public ITransformGizmo Build(FScene scene, List<SceneObject> targets)
        {
            if (targets[0] is PrimitiveSO) {
                return new EditPrimitiveGizmo().Create(scene, targets);

            } else if (targets[0] is PolyCurveSO) {
                return new EditCurveGizmo().Create(scene, targets);

            } else
                return null;

        }
    }

}
