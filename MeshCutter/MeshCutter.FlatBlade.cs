using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomMathMethods;
using CustomToolFunctions;

namespace MeshCutting {

    public partial class MeshCutter {

        static Plane _blade;


        public static GameObject CutFlat (GameObject goVictim, GameObject goPlane, Material capMat = null) {

            Mesh plane = goPlane.GetComponent<MeshFilter>().mesh;
            Vector3 planePos = goPlane.transform.TransformPoint(plane.vertices[0]);
            Vector3 planeDir = goPlane.transform.TransformDirection(plane.normals[0]);

            if (capMat == null)
                return CutFlat(goVictim, planePos, planeDir, null);

            else
                return CutFlat(goVictim, planePos, planeDir, capMat);
        }

        public static GameObject CutFlat (GameObject goVictim, Vector3 bladePos, Vector3 bladeDir, Material capMat) {

            // Setup Victim-Mesh and Blade (at Victim-Mesh's Local Transform)
            _mesh = goVictim.GetComponent<MeshFilter>().mesh;
            _blade = new Plane( goVictim.transform.InverseTransformDirection(bladeDir), goVictim.transform.InverseTransformPoint(bladePos) );

            // Initialize Caches
            _cuttedMarker[0].Clear();
            _cuttedMarker[1].Clear();
            _newVertsCache.Clear();
            _newNormsChahe.Clear();

            var meshVertices = _mesh.vertices;
            var meshUV = _mesh.uv;
            var meshNormals = _mesh.normals;
            var meshTangents = _mesh.tangents;

            // Go Through All SubMeshes
            for (int subI = 0; subI < _mesh.subMeshCount; subI++) {

                var tris = _mesh.GetTriangles(subI);

                for (int i = 0; i < tris.Length; i+=3) {

                    for (int indOfTri = 0; indOfTri < 3; indOfTri++) {

                        int ind = tris[i + indOfTri];

                        _triCache.vertices[indOfTri] = meshVertices[ ind ];
                        _triCache.uvs[indOfTri]      = meshUV[ ind ];
                        _triCache.normals[indOfTri]  = meshNormals[ ind ];
                        _triCache.tangents[indOfTri] = meshTangents[ ind ];

                        _isPosSideCache[indOfTri] = _blade.GetSide( meshVertices[ ind ] );
                    }

                    // Check Which Triangle Not Be Cutted
                    if (_isPosSideCache[0] == _isPosSideCache[1] && _isPosSideCache[1] == _isPosSideCache[2]) {

                        if (_isPosSideCache[0])
                            _cuttedMarker[0].AddTriangle(_triCache, subI);

                        else
                            _cuttedMarker[1].AddTriangle(_triCache, subI);

                    }
                    else {
                        CutTriangleFlat(ref _triCache, subI);
                    }
                }
            }


            if (_newVertsCache.Count > 0) {

                Material[] mats = GetMaterialsWithCap(goVictim, capMat);

                CapFlatCuttedFace();


                GameObject anotherPart = new GameObject(goVictim.name + "-part1", typeof(MeshFilter), typeof(MeshRenderer));
                anotherPart.transform.localPosition = goVictim.transform.localPosition;
                anotherPart.transform.localRotation = goVictim.transform.localRotation;
                anotherPart.transform.localScale    = goVictim.transform.localScale;
                anotherPart.GetComponent<MeshFilter>().mesh = _cuttedMarker[1].GetMesh();
                anotherPart.GetComponent<MeshRenderer>().materials = mats;

                goVictim.name = goVictim.name + "-part0";
                goVictim.GetComponent<MeshFilter>().mesh = _cuttedMarker[0].GetMesh();
                goVictim.GetComponent<MeshRenderer>().materials = mats;

                return anotherPart;

            }
            else
                return null;

        }


        // Cutting Caching
        static MeshMarker.Triangle _newTrisCache = new MeshMarker.Triangle(new Vector3[3], new Vector2[3], new Vector3[3], new Vector4[3]);
        static MeshMarker.Triangle _intersectionsCache = new MeshMarker.Triangle(new Vector3[2], new Vector2[2], new Vector3[2], new Vector4[2]);

        static void CutTriangleFlat (ref MeshMarker.Triangle tri, int subMesh) {

            // ==  _isPosSideCache[3] Are Already Set Before   ==

            int[] sideCount = {0, 0};
            int[] sideSeq = new int[3];
            int smlSide = -1, othSide = -1;       // Smaller Side, Other Side

            /// Step.1 - Find the Smaller Side and Set to _newTrisCache Arranged Clockwise ///
            // 1.1 - Find the Smaller Side
            for (int i = 0; i < 3; i++) {

                int side = -1;

                if (_isPosSideCache[i])  side = 0;
                else                     side = 1;

                sideCount[side]++;
                sideSeq[i] = side;
            }

            if (sideCount[0] == 1)  smlSide = 0;
            else                    smlSide = 1;
            othSide = (smlSide + 1) % 2;

            int triSmlInd = MyTools.GetIndexByValue(sideSeq, smlSide);  // Triangle Smaller Side Index

            // 1.2 - Set to _newTrisCache (Make It Clockwise)
            for (int i = 0; i < 3; i++) {

                _newTrisCache.vertices[i] = tri.vertices[ (triSmlInd + i) % 3 ];
                _newTrisCache.uvs[i]      = tri.uvs     [ (triSmlInd + i) % 3 ];
                _newTrisCache.normals[i]  = tri.normals [ (triSmlInd + i) % 3 ];
                _newTrisCache.tangents[i] = tri.tangents[ (triSmlInd + i) % 3 ];
            }

            /// Step.2 - Find Intersections ///
            float dist = 0f;
            float ratio = 0f;
            Vector3 intersectedEdge = Vector3.zero;

            for (int d = 0; d < 2; d++) {
                intersectedEdge = _newTrisCache.vertices[d + 1] - _newTrisCache.vertices[0];
                _blade.Raycast( new Ray(_newTrisCache.vertices[0], intersectedEdge.normalized), out dist );
                ratio = dist / intersectedEdge.magnitude;

                _intersectionsCache.vertices[d] = Vector3.Lerp(_newTrisCache.vertices[0], _newTrisCache.vertices[d + 1], ratio);
                _intersectionsCache.uvs[d]      = Vector2.Lerp(_newTrisCache.uvs[0],      _newTrisCache.uvs[d + 1],      ratio);
                _intersectionsCache.normals[d]  = Vector3.Lerp(_newTrisCache.normals[0],  _newTrisCache.normals[d + 1],  ratio);
                _intersectionsCache.tangents[d] = Vector4.Lerp(_newTrisCache.tangents[0], _newTrisCache.tangents[d + 1], ratio);
            }

            if (_intersectionsCache.vertices[0] != _intersectionsCache.vertices[1]) {

                _newVertsCache.Add(_intersectionsCache.vertices[0]);
                _newVertsCache.Add(_intersectionsCache.vertices[1]);

                _newNormsChahe.Add(_intersectionsCache.normals[0]);
                _newNormsChahe.Add(_intersectionsCache.normals[1]);
            }

            /// Step.3 - Add Triangles (1 at Smaller Side, 2 at Bigger Side) ///
            // 3.1 - The Triangle at the Smaller Side
            _triCache.vertices[0] = _newTrisCache.vertices[0];
            _triCache.vertices[1] = _intersectionsCache.vertices[0];
            _triCache.vertices[2] = _intersectionsCache.vertices[1];

            _triCache.uvs[0] = _newTrisCache.uvs[0];
            _triCache.uvs[1] = _intersectionsCache.uvs[0];
            _triCache.uvs[2] = _intersectionsCache.uvs[1];

            _triCache.normals[0] = _newTrisCache.normals[0];
            _triCache.normals[1] = _intersectionsCache.normals[0];
            _triCache.normals[2] = _intersectionsCache.normals[1];

            _triCache.tangents[0] = _newTrisCache.tangents[0];
            _triCache.tangents[1] = _intersectionsCache.tangents[0];
            _triCache.tangents[2] = _intersectionsCache.tangents[1];

            _cuttedMarker[smlSide].AddTriangle(_triCache, subMesh);

            // 3.2 - The Right Triangle at the Bigger Side
            _triCache.vertices[0] = _newTrisCache.vertices[1];
            _triCache.vertices[1] = _intersectionsCache.vertices[1];
            _triCache.vertices[2] = _intersectionsCache.vertices[0];

            _triCache.uvs[0] = _newTrisCache.uvs[1];
            _triCache.uvs[1] = _intersectionsCache.uvs[1];
            _triCache.uvs[2] = _intersectionsCache.uvs[0];

            _triCache.normals[0] = _newTrisCache.normals[1];
            _triCache.normals[1] = _intersectionsCache.normals[1];
            _triCache.normals[2] = _intersectionsCache.normals[0];

            _triCache.tangents[0] = _newTrisCache.tangents[1];
            _triCache.tangents[1] = _intersectionsCache.tangents[1];
            _triCache.tangents[2] = _intersectionsCache.tangents[0];

            _cuttedMarker[othSide].AddTriangle(_triCache, subMesh);

            // 3.3 - The Left Triangle ar the Bigger Side
            _triCache.vertices[0] = _newTrisCache.vertices[1];
            _triCache.vertices[1] = _newTrisCache.vertices[2];
            _triCache.vertices[2] = _intersectionsCache.vertices[1];

            _triCache.uvs[0] = _newTrisCache.uvs[1];
            _triCache.uvs[1] = _newTrisCache.uvs[2];
            _triCache.uvs[2] = _intersectionsCache.uvs[1];

            _triCache.normals[0] = _newTrisCache.normals[1];
            _triCache.normals[1] = _newTrisCache.normals[2];
            _triCache.normals[2] = _intersectionsCache.normals[1];

            _triCache.tangents[0] = _newTrisCache.tangents[1];
            _triCache.tangents[1] = _newTrisCache.tangents[2];
            _triCache.tangents[2] = _intersectionsCache.tangents[1];

            _cuttedMarker[othSide].AddTriangle(_triCache, subMesh);

        }


        // Capping Cutted Face
        static List<int>            _newVertsUsedIndsCache = new List<int>();
        static List<int>            _capPolygonIndsCache   = new List<int>();

        static void CapFlatCuttedFace () {

            _newVertsUsedIndsCache.Clear();
            _polysCache.Clear();

            // One Polygon per Loop
            for (int i = 0; i < _newVertsCache.Count; i += 2) {

                // Step.1 - Find and Split the Polygon
                if ( !_newVertsUsedIndsCache.Contains(i) ) {

                    // 1.1 - Find out the Polugon
                    _capPolygonIndsCache.Clear();

                    // Must Arranged Clockwise (_blade.normal point at your face)
                    Vector3 faceDir = Vector3.Cross(_newNormsChahe[i], _newVertsCache[i + 1] - _newVertsCache[i]);

                    if (Vector3.Dot(faceDir, _blade.normal) > 0) {

                        _capPolygonIndsCache.Add(i);
                        _capPolygonIndsCache.Add(i + 1);
                    }
                    else {

                        _capPolygonIndsCache.Add(i + 1);
                        _capPolygonIndsCache.Add(i);
                    }

                    _newVertsUsedIndsCache.Add(i);
                    _newVertsUsedIndsCache.Add(i + 1);

                    Vector3[] edgePoint = new Vector3[2] {
                        _newVertsCache[ _capPolygonIndsCache[0] ],
                        _newVertsCache[ _capPolygonIndsCache[1] ]
                    };

                    // Stretch both sides
                    for (bool isDone = false; !isDone; ) {

                        isDone = true;

                        for (int j = 0; j < _newVertsCache.Count; j += 2) {

                            if ( !_newVertsUsedIndsCache.Contains(j) ) {

                                Vector3[] nextVert = new Vector3[2] {
                                    _newVertsCache[j],
                                    _newVertsCache[j + 1]
                                };

                                bool[,] cond = new bool[2,2];
                                cond[0,0] = edgePoint[0] == nextVert[0];
                                cond[0,1] = edgePoint[0] == nextVert[1];
                                cond[1,0] = edgePoint[1] == nextVert[0];
                                cond[1,1] = edgePoint[1] == nextVert[1];

                                if ( cond[0,0] || cond[0,1] || cond[1,0] || cond[1,1] ) {

                                    _newVertsUsedIndsCache.Add(j);
                                    _newVertsUsedIndsCache.Add(j + 1);

                                    if (cond[0,0]) {

                                        _capPolygonIndsCache.Insert(0, j + 1);
                                        edgePoint[0] = _newVertsCache[j + 1];
                                    }
                                    else if (cond[0,1]) {

                                        _capPolygonIndsCache.Insert(0, j);
                                        edgePoint[0] = _newVertsCache[j];
                                    }
                                    else if (cond[1,0]) {

                                        _capPolygonIndsCache.Add(j + 1);
                                        edgePoint[1] = _newVertsCache[j + 1];
                                    }
                                    else if (cond[1,1]) {

                                        _capPolygonIndsCache.Add(j);
                                        edgePoint[1] = _newVertsCache[j];
                                    }

                                    isDone = false;
                                }
                            }
                        }

                    }

                    // Check if the Link is Closed
                    if (_newVertsCache[ _capPolygonIndsCache[0] ] == _newVertsCache[ _capPolygonIndsCache[ _capPolygonIndsCache.Count - 1 ] ])
                        _capPolygonIndsCache.RemoveAt(0);

                    // 1.2 - Pending to Split the Polygon
                    _polysCache.Add( new PolygonIndices(_capPolygonIndsCache) );
                }
            }

            TriangulatePolygons(_polysCache, _blade.normal, 2, _capMatSub);
        }


    }   // End of the (partical) Class
}
