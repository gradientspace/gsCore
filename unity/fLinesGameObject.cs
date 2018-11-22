// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

using UnityEngine;
using Vectrosity;
using f3;

namespace gs
{





    // set of separate line segments, as one GO, using Vectrosity
    public class fLinesGameObject : fGameObject
    {
        float width = 0.05f;
        Colorf color = Colorf.Black;

        Vector3f[] Vertices;
        bool bVertsValid;

        VectorLine vectorLine;

        public fLinesGameObject(GameObject baseGO, string name = "line")
            : base(baseGO, FGOFlags.EnablePreRender)
        {
            baseGO.SetName(name);

            // ugly...
            __VectrosityConfig.Configure();

            if (baseGO.GetComponent<MeshFilter>() == null)
                baseGO.AddComponent<MeshFilter>();
            if (baseGO.GetComponent<MeshRenderer>() == null)
                baseGO.AddComponent<MeshRenderer>();

            List<Vector3> vertices = new List<Vector3>() { Vector3.zero, Vector3.one };
            vectorLine = new VectorLine(name, vertices, width, LineType.Discrete);
            VectorManager.ObjectSetup(baseGO, vectorLine, Visibility.Dynamic, Brightness.None);
        }

        public void SetLineWidth(float fWidth) {
            update(fWidth, color);
        }
        public float GetLineWidth() { return width; }

        public override void SetColor(Colorf newColor) {
            update(width, newColor);
        }
        public Colorf GetColor() { return color; }


        public override void SetVisible(bool bVisible)
        {
            base.SetVisible(bVisible);
            vectorLine.active = bVisible;
        }

        public override void SetLayer(int layer, bool bSetOnChildren = false)
        {
            base.SetLayer(layer, bSetOnChildren);
            vectorLine.layer = layer;
        }

        protected void update(float newWidth, Colorf newColor)
        {
            if (width != newWidth) {
                vectorLine.SetWidth(newWidth);
                width = newWidth;
            }
            if (color != newColor) {
                vectorLine.SetColor(newColor);
                color = newColor;
            }
        }


        public void SetVertices(List<Vector3f> vertices, bool bImmedateUpdate = false) {
            Vertices = vertices.ToArray();
            bVertsValid = false;
            if (bImmedateUpdate)
                PreRender();
        }


        public override void PreRender()
        {
            if (bVertsValid)
                return;

            if ( vectorLine.points3.Count == Vertices.Length ) {
                for (int i = 0; i < Vertices.Length; ++i)
                    vectorLine.points3[i] = Vertices[i];
            } else {
                vectorLine.points3 = new List<Vector3>(Vertices.Length);
                for (int i = 0; i < Vertices.Length; ++i)
                    vectorLine.points3.Add(Vertices[i]);
            }

            vectorLine.SetWidth(width);
            vectorLine.SetColor(color);

            bVertsValid = true;
        }
    }




    

}
