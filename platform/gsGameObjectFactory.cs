// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using UnityEngine;
using g3;

namespace gs
{
    public static partial class GameObjectFactoryExt
    {

        public static fLinesGameObject CreateLinesGO(string sName, List<Vector3f> vLineVerts, Colorf color, float fLineWidth)
        {
            GameObject go = new GameObject(sName);
            fLinesGameObject fgo = new fLinesGameObject(go, sName);
            fgo.SetColor(color);
            fgo.SetLineWidth(fLineWidth);
            fgo.SetVertices(vLineVerts, true);
            return fgo;
        }


    }
}
