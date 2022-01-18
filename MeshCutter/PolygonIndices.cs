using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeshCutting {

    public class PolygonIndices {

        // =========== Properties =========== //
        public List<int> inds;
        public Vector3   assignedCenter;
        public Vector3[] fromCenters;
        public Vector3   dottedVect;
        public float[]   dotValues;
        public bool[]    isComparedAt;

        public int VertsCount {
            get => inds.Count;
        }

        // =========== Constructors =========== //
        public PolygonIndices (List<int> inds) {

            this.inds  = new List<int>(inds);

            dotValues  = new float[inds.Count];
            fromCenters = new Vector3[inds.Count];
            isComparedAt   = new bool[inds.Count];

            for (int i = 0 ; i < isComparedAt.Length ; i++) {
                isComparedAt[i] = false;
            }

            assignedCenter = Vector3.zero;
            dottedVect     = Vector3.zero;
        }

        // =========== Methods ===========//
        public void CalculateFromCenters (Vector3 center, List<Vector3> verts) {

            assignedCenter = center;

            for (int i = 0 ; i < inds.Count ; i++)
                fromCenters[i] = verts[ inds[i] ] - center;

        }

        public void CalculateDotValues (Vector3 dottedVect, List<Vector3> verts) {

            this.dottedVect = dottedVect;

            for (int i = 0 ; i < inds.Count ; i++)
                dotValues[i] = Vector3.Dot(verts[ inds[i] ], dottedVect);

        }

        public bool isConnectedWith (List<Vector3> otherPolyVerts, List<Vector3> verts) {

            for (int i = 0 ; i < inds.Count ; i++) {

                int ii = (i + 1) % inds.Count;

                for (int j = 0 ; j < otherPolyVerts.Count ; j++) {

                    int jj = (j + 1) % otherPolyVerts.Count;

                    if (verts[ inds[i] ] == otherPolyVerts[jj] && verts[ inds[ii] ] == otherPolyVerts[j])
                        return true;

                }
            }

            return false;
        }

    }

}
