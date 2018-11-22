using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    /// <summary>
    /// Allows updating positions/etc of a mesh while accumulating changes into a
    /// ModifyVerticesMeshChange, which can be used to revert this change, add
    /// to History, etc
    /// </summary>
    public class VertexChangeBuilder
    {
        public DMesh3 Mesh;
        public MeshComponents Components = MeshComponents.All;

        ModifyVerticesMeshChange ActiveChange;
        Dictionary<int, int> ModifiedV;         // could use flat buffer for this?

        public VertexChangeBuilder(DMesh3 mesh, MeshComponents components = MeshComponents.All)
        {
            Mesh = mesh;
            Components = components;

            ModifiedV = new Dictionary<int, int>();
            ActiveChange = new ModifyVerticesMeshChange(mesh, components);
        }


        public ModifyVerticesMeshChange ExtractChange()
        {
            var change = ActiveChange;
            Reset();
            return change;
        }


        public void Reset()
        {
            ModifiedV.Clear();
            ActiveChange = new ModifyVerticesMeshChange(Mesh, Components);
        }


        public void SetPosition(int vid, Vector3d newPos)
        {
            int idx = get_index(vid);
            ActiveChange.NewPositions[idx] = newPos;
            Mesh.SetVertex(vid, newPos);
        }

        public void SetNormal(int vid, Vector3f newNormal)
        {
            int idx = get_index(vid);
            ActiveChange.NewNormals[idx] = newNormal;
            Mesh.SetVertexNormal(vid, newNormal);
        }


        public void SaveCurrentNormals()
        {
            if (ActiveChange.NewNormals != null) {
                foreach (var pair in ModifiedV) {
                    Vector3f normal = Mesh.GetVertexNormal(pair.Key);
                    ActiveChange.NewNormals[pair.Value] = normal;
                }
            }
        }


        int get_index(int vid)
        {
            int idx;
            if (ModifiedV.TryGetValue(vid, out idx) == false) {
                idx = ActiveChange.AppendNewVertex(Mesh, vid);
                ModifiedV[vid] = idx;
            }
            return idx;
        }


    }
}
