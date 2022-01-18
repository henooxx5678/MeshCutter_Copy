using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeshCutting {

    public class MeshMarker {

        /// =========== Properties =========== ///
        List<Vector3>   _vertices   = new List<Vector3>();
        List<Vector2>   _uvs        = new List<Vector2>();
        List<Vector3>   _normals    = new List<Vector3>();
        List<Vector4>   _tangents   = new List<Vector4>();
        List<List<int>> _subMeshTriInds = new List<List<int>>();

        public int VertsCount {
            get {
                return _vertices.Count;
            }
        }

        /// =========== Methods =========== ///
        public void Clear () {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _tangents.Clear();
            _subMeshTriInds.Clear();
        }

        public void AddTriangle (Triangle tri, int subMesh) {
            AddTriangle(tri.vertices, tri.uvs, tri.normals, tri.tangents, subMesh);
        }
        public void AddTriangle (Vector3[] verts, Vector2[] uvs, Vector3[] normals, int subMesh = 0) {
            AddTriangle(verts, uvs, normals, null, subMesh);
        }
        public void AddTriangle (Vector3[] verts, Vector2[] uvs, Vector3[] normals, Vector4[] tangs, int subMesh = 0) {

            int vertsCountBefore = _vertices.Count;

            // Add _subMeshTriInds Amount to Current Sub-Mesh Amount
            for (int i = _subMeshTriInds.Count; i < subMesh + 1; i++) {
                _subMeshTriInds.Add( new List<int>() );
            }

            for (int i = 0; i < 3; i++) {
                _vertices.Add(   verts[i] );
                _uvs.Add     (     uvs[i] );
                _normals.Add ( normals[i] );

                if (tangs != null) _tangents.Add( tangs[i] );

                _subMeshTriInds[subMesh].Add(vertsCountBefore + i);
            }

        }

        // public void RemoveDoubles () {
        //
        //     int doubleCount = 0;
        //
        //     Vector3 vert   = Vector3.zero;
        //     Vector2 uv     = Vector2.zero;
        //     Vector3 normal = Vector3.zero;
        //
        //     for (int i = 0; i < VertsCount; i++) {
        //
        //         vert   = _vertices[i];
        //         uv     = _uvs[i];
        //         normal = _normals[i];
        //
        //         // Look Backwards
        //         for (int j = i - 1; j >= 0; j--) {
        //
        //             if (vert == _vertices[j] && uv == _uvs[j] && normal == _normals[j]) {
        //                 doubleCount++;
        //                 MergeDouble(j, i);
        //                 i--;   // Bucause _vertices lost 1 element
        //                 break;
        //             }
        //         }
        //
        //         Debug.Log("MeshMarker: " + doubleCount + " Doubles Found");
        //
        //     }
        //
        // }

        // void MergeDouble (int first, int duplicated) {
        //
        //     for (int i = 0; i < _subMeshTriInds.Count; i++) {
        //         for (int j = 0; j < _subMeshTriInds[i].Count; j++) {
        //
        //             if (_subMeshTriInds[i][j] > duplicated)
        //                 _subMeshTriInds[i][j]--;  // Because all List will lost 1 element
        //
        //             else if (_subMeshTriInds[i][j] == duplicated)
        //                 _subMeshTriInds[i][j] = first;  // Replace it
        //
        //         }
        //     }
        //
        //     _vertices.RemoveAt(duplicated);
        //     _uvs.RemoveAt(duplicated);
        //     _normals.RemoveAt(duplicated);
        //
        //     if (_tangents.Count > 0)
        //         _tangents.RemoveAt(duplicated);
        //
        // }

        public Mesh GetMesh () {

            Mesh m = new Mesh();

            m.name = "Generated Mesh";
            m.SetVertices(_vertices);
            m.SetUVs(0, _uvs);
            m.SetNormals(_normals);
            m.SetTangents(_tangents);

            m.subMeshCount = _subMeshTriInds.Count;

            for (int i = 0; i < _subMeshTriInds.Count; i++) {
                m.SetTriangles(_subMeshTriInds[i], i);
            }

            return m;
        }

        /// ============ Struct =========== ///
        public struct Triangle {

            public Vector3[] vertices;
            public Vector2[] uvs;
            public Vector3[] normals;
            public Vector4[] tangents;

            public Triangle (Vector3[] verts = null, Vector2[] uvs = null, Vector3[] normals = null, Vector4[] tangs = null) {
                this.vertices = verts;
                this.uvs      = uvs;
                this.normals  = normals;
                this.tangents = tangs;
            }


            public bool IsPointInside (Vector3 p) {

                Plane triPlane = new Plane(vertices[0], vertices[1], vertices[2]);
                if (Vector3.Dot(Vector3.Cross(vertices[1] - vertices[0], p - vertices[0]), triPlane.normal) > 0 &&
                    Vector3.Dot(Vector3.Cross(vertices[2] - vertices[1], p - vertices[1]), triPlane.normal) > 0 &&
                    Vector3.Dot(Vector3.Cross(vertices[0] - vertices[2], p - vertices[2]), triPlane.normal) > 0)

                    return true;

                else
                    return false;

            }

        }

    }

}
