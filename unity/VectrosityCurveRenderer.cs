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
 
    public static class __VectrosityConfig
    {
        public static bool IsConfigured = false;

        public static void Configure()
        {
            if (IsConfigured == false) {

                // If we don't do things in 3D, then the lines exist in a 
                // "VectorCanvas" go, and it will be drawn on top of everything
                // in the scene (??)
                VectorManager.useDraw3D = true;

                IsConfigured = true;
            }
        }
    }





    public class VectrosityCurveRenderer : CurveRendererImplementation
    {
        VectorLine vectorLine;
        float width;
        Colorf color;

        public virtual void initialize(fGameObject go, Colorf color)
        {
            // ugly...
            __VectrosityConfig.Configure();

            if (go.GetComponent<MeshFilter>() == null)
                go.AddComponent<MeshFilter>();
            if (go.GetComponent<MeshRenderer>() == null)
                go.AddComponent<MeshRenderer>();

            List<Vector3> vertices = new List<Vector3>() { Vector3f.Zero, Vector3f.Zero };
            vectorLine = new VectorLine(go.GetName() + "_vline", vertices, 1.0f, LineType.Continuous);
            VectorManager.ObjectSetup(go, vectorLine, Visibility.Dynamic, Brightness.None);
            vectorLine.SetColor(color);
        }

        public virtual void initialize(fGameObject go, fMaterial material, bool bSharedMaterial)
        {
            initialize(go, material.color);
        }

        public virtual void update_curve(Vector3f[] Vertices)
        {
            if ( Vertices == null ) {
                vectorLine.points3 = new List<Vector3>();
                return;
            }
            if ( vectorLine.points3.Count == Vertices.Length ) {
                for (int i = 0; i < Vertices.Length; ++i)
                    vectorLine.points3[i] = Vertices[i];
            } else {
                //vectorLine.points3 = new List<Vector3>(Vertices.Length);
                vectorLine.points3.Clear();
                for (int i = 0; i < Vertices.Length; ++i)
                    vectorLine.points3.Add(Vertices[i]);
            }
            vectorLine.SetWidth(width);
            vectorLine.SetColor(color);
        }
        public virtual void update_num_points(int N)
        {
            if ( vectorLine.points3.Count != N )
                vectorLine.points3 = new List<Vector3>(N);
        }
        public virtual void update_position(int i, Vector3f v)
        {
            vectorLine.points3[i] = v;
            vectorLine.SetColor(color, i);
            vectorLine.SetWidth(width, i);
        }
        public virtual void update_width(float width)
        {
            this.width = width;
            vectorLine.SetWidth(width);
        }
        public virtual void update_width(float startWidth, float endWidth)
        {
            throw new NotImplementedException("VectrosityCurveRenderer.update_width: how to do start/end width?");
            //vectorLine.SetWidths()
        }

        public virtual void update_color(Colorf color)
        {
            this.color = color;
            vectorLine.SetColor(color);
        }

        public virtual void set_corner_quality(int n)
        {
            if (n == 0) {
                vectorLine.joins = Joins.None;
            } else {
                vectorLine.joins = Joins.Fill;
            }
        }


        public virtual bool is_pixel_width() {
            return true;
        }

    }


    public class VectrosityCurveRendererFactory : CurveRendererFactory
    {
        public CurveRendererImplementation Build(LineWidthType widthType)
        {
            if (widthType == LineWidthType.World)
                return new UnityCurveRenderer();
            else
                return new VectrosityCurveRenderer();
        }
    }


}
