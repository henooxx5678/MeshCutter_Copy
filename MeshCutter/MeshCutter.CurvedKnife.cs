using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomMathMethods;

namespace MeshCutting {

    public partial class MeshCutter {

        // static Transform _debugTrans;

        static CurvedKnife _cKnife;

        public static GameObject CutCurved (GameObject goVictim, GameObject goKnife, Material capMat = null) {

            Mesh      knifeMesh  = goKnife.GetComponent<MeshFilter>().mesh;
            Vector3[] knifeVerts = new Vector3[knifeMesh.vertices.Length];

            // Set knife's vertices to goVictim's local position
            for (int i = 0 ; i < knifeVerts.Length ; i++) {

                Vector3 worldPos = goKnife.transform.TransformPoint(knifeMesh.vertices[i]);
                knifeVerts[i] = goVictim.transform.InverseTransformPoint(worldPos);
            }

            _cKnife = new CurvedKnife(knifeVerts, knifeMesh.triangles);

            return CutCurved(goVictim, _cKnife, capMat);
        }

        public static GameObject CutCurved (GameObject goVictim, CurvedKnife cKnife, Material capMat = null) {

            bool isHalfCutted = false;

            if (cKnife != null)
                _cKnife = cKnife;

            _mesh = goVictim.GetComponent<MeshFilter>().mesh;


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

                for (int i = 0; i < tris.Length; i += 3) {

                    for (int indOfTri = 0; indOfTri < 3; indOfTri++) {

                        int ind = tris[i + indOfTri];

                        _triCache.vertices[indOfTri] = meshVertices[ind];
                        _triCache.uvs[indOfTri]      = meshUV[ind];
                        _triCache.normals[indOfTri]  = meshNormals[ind];
                        _triCache.tangents[indOfTri] = meshTangents[ind];

                        _isPosSideCache[indOfTri] = _cKnife.GetSide( meshVertices[ind] );
                    }

                    if (CutTriangleCurved(ref _triCache, subI) == false) {
                        isHalfCutted = true;
                        break;
                    }
                }

                if (isHalfCutted)
                    break;
            }

            if (isHalfCutted)
                Debug.Log(goVictim.name + " is half-cutted!");


            if (_newVertsCache.Count > 0 && !isHalfCutted) {

                Material[] mats = GetMaterialsWithCap(goVictim, capMat);

                CapCurvedCuttedFace();

                return GetNewPartAfterSplitVictim(goVictim, _cuttedMarker[0].GetMesh(), _cuttedMarker[1].GetMesh(), mats);
            }
            else
                return null;

        }

        // Cut Triangle
        static List<CurvedBladeInTriangle>   _cBladesInTri = new List<CurvedBladeInTriangle>();
        static List<CurvedBladeInTriangle>[] _cBladesInTriSorted = new List<CurvedBladeInTriangle>[6] {
            new List<CurvedBladeInTriangle>(),  // 0
            new List<CurvedBladeInTriangle>(),  // 1  (0, 1, 2 for vertices of triangle)
            new List<CurvedBladeInTriangle>(),  // 2
            new List<CurvedBladeInTriangle>(),  // 3
            new List<CurvedBladeInTriangle>(),  // 4  (3, 4, 5 for edges of triangle)
            new List<CurvedBladeInTriangle>()   // 5
        };

        static bool CutTriangleCurved (ref MeshMarker.Triangle tri, int subMesh) {

            // ==  _isPosSideCache[3] Are Already Set Before   ==

            List<CurvedKnife.Polygon> knifePolys = _cKnife.polygons;

            Plane triPlane = new Plane(tri.vertices[0], tri.vertices[1], tri.vertices[2]);

            if (triPlane.normal == Vector3.zero) {
                Debug.Log("Mesh scale is too small!!");
                return false;
            }

            _cBladesInTri.Clear();
            for (int i = 0 ; i < _cBladesInTriSorted.Length ; i++)
                _cBladesInTriSorted[i].Clear();


            bool triNotCutted = true;

            // Find out the curve of knife on the Triangle plane
            for (int polyI = 0 ; polyI < knifePolys.Count ; polyI++) {

                CurvedKnife.Polygon knPoly    = knifePolys[polyI];

                Vector3[] newEdgeOnPlane      = new Vector3[2];
                int[]     edgeOfPolyNewEdgeOn = new int[2];

                // Find the line segment which this polygon casted on the triPlane
                int castedCount = 0;

                for (int i = 0 ; i < knPoly.vertices.Count ; i++) {

                    Vector3[] polygonEdge = {
                        knPoly.vertices[i],
                        knPoly.vertices[(i + 1) % knPoly.vertices.Count]
                    };

                    Vector3 polygonEdgeVect = polygonEdge[1] - polygonEdge[0];
                    Ray     polygonEdgeRay  = new Ray(polygonEdge[0], polygonEdgeVect.normalized);

                    float dist  = 0f;
                    float ratio = 0f;

                    triPlane.Raycast(polygonEdgeRay, out dist);
                    ratio = dist / polygonEdgeVect.magnitude;

                    if ( (ratio > 0) && (ratio <= 1f) ) {

                        newEdgeOnPlane[castedCount] = Vector3.LerpUnclamped(polygonEdge[0], polygonEdge[1], ratio);
                        edgeOfPolyNewEdgeOn[castedCount] = i;
                        castedCount++;

                        if (castedCount == 2) {

                            if (newEdgeOnPlane[0] == newEdgeOnPlane[1])
                                castedCount = 0;

                            break;
                        }
                    }
                }


                // Find intersections between casted edges of polygon and triangle edges
                List<Vector3> intersections = new List<Vector3>();
                List<Vector3> intersectionNormals = new List<Vector3>();
                List<int>     intersectedSide = new List<int>();

                if (castedCount == 0) // The Polygon doesn't cast a line segment on triPlane
                    continue;

                // j = 0, 1, 2
                for (int j = 0 ; j < 3 ; j++) {

                    int[] edgeIndOfTri = { j, (j + 1) % 3 };

                    // line-segments: -AB- & -CD-   :   casted edge of polygon & edge of triangle
                    Vector3 vectAB = newEdgeOnPlane[1] - newEdgeOnPlane[0];
                    Vector3 vectCD = tri.vertices[ edgeIndOfTri[1] ] - tri.vertices[ edgeIndOfTri[0] ];
                    Vector3 vectAC = tri.vertices[ edgeIndOfTri[0] ] - newEdgeOnPlane[0];
                    Vector4 vectCA = -vectAC;

                    float ratioOfAB = Vector3.Dot(Vector3.Cross(vectAC, vectCD), triPlane.normal) / Vector3.Dot(Vector3.Cross(vectAB, vectCD), triPlane.normal);
                    float ratioOfCD = Vector3.Dot(Vector3.Cross(vectCA, vectAB), triPlane.normal) / Vector3.Dot(Vector3.Cross(vectCD, vectAB), triPlane.normal);

                    if ( ratioOfAB >= 0 && (ratioOfAB <= 1) && ratioOfCD >= 0 && ratioOfCD <= 1 ) {
                        // Intersected!
                        intersections.Add(newEdgeOnPlane[0] + ratioOfAB * vectAB);
                        intersectionNormals.Add( Vector3.Lerp(tri.normals[ edgeIndOfTri[0] ], tri.normals[ edgeIndOfTri[1] ], ratioOfCD) );
                        intersectedSide.Add(j);
                    }
                }



                if (intersections.Count == 2) {

                    if (intersections[0] != intersections[1]) {

                        _newVertsCache.Add(intersections[0]);
                        _newVertsCache.Add(intersections[1]);

                        _newNormsChahe.Add(intersectionNormals[0]);
                        _newNormsChahe.Add(intersectionNormals[1]);

                        List<int> newVertsInd = new List<int>();
                        newVertsInd.Add(_newVertsCache.Count - 2);
                        newVertsInd.Add(_newVertsCache.Count - 1);

                        knPoly.AddNewVertInd(newVertsInd[0], -1);
                        knPoly.AddNewVertInd(newVertsInd[1], -1);


                        _cBladesInTri.Add( new CurvedBladeInTriangle(newVertsInd, intersectedSide) );

                        triNotCutted = false;
                    }

                }
                else {
                    // Find how many are inside
                    List<int> insidePointsInd = new List<int>();
                    for (int d = 0 ; d < 2 ; d++) {

                        if ( edgeOfPolyNewEdgeOn[d] != -1 && tri.IsPointInside(newEdgeOnPlane[d]) )
                            insidePointsInd.Add(d);
                    }

                    if (insidePointsInd.Count == 1) {

                        _newVertsCache.Add(intersections[0]);
                        _newVertsCache.Add(newEdgeOnPlane[ insidePointsInd[0] ]);

                        _newNormsChahe.Add(intersectionNormals[0]);
                        _newNormsChahe.Add(triPlane.normal);

                        List<int> newVertsInd = new List<int>();
                        newVertsInd.Add(_newVertsCache.Count - 2);
                        newVertsInd.Add(_newVertsCache.Count - 1);

                        knPoly.AddNewVertInd(newVertsInd[0], -1);
                        knPoly.AddNewVertInd(newVertsInd[1], edgeOfPolyNewEdgeOn[ insidePointsInd[0] ]);


                        _cBladesInTri.Add( new CurvedBladeInTriangle(newVertsInd[0], newVertsInd[1], intersectedSide[0], -1) );

                        triNotCutted = false;

                    }
                    else if (insidePointsInd.Count == 2) {

                        _newVertsCache.Add(newEdgeOnPlane[ insidePointsInd[0] ]);
                        _newVertsCache.Add(newEdgeOnPlane[ insidePointsInd[1] ]);

                        _newNormsChahe.Add(triPlane.normal);
                        _newNormsChahe.Add(triPlane.normal);

                        List<int> newVertsInd = new List<int>();
                        newVertsInd.Add(_newVertsCache.Count - 2);
                        newVertsInd.Add(_newVertsCache.Count - 1);

                        knPoly.AddNewVertInd(newVertsInd[0], edgeOfPolyNewEdgeOn[ insidePointsInd[0] ]);
                        knPoly.AddNewVertInd(newVertsInd[1], edgeOfPolyNewEdgeOn[ insidePointsInd[1] ]);


                        _cBladesInTri.Add( new CurvedBladeInTriangle(newVertsInd[0], newVertsInd[1], -1, -1) );

                        triNotCutted = false;

                    }
                    // else: this Triangle is not cutted by this Polygon
                }

                // END of one Polygon of cKnife
            }

            if (triNotCutted) {

                if (_isPosSideCache[0] == _isPosSideCache[1] && _isPosSideCache[1] == _isPosSideCache[2]) {

                    if (_isPosSideCache[0])
                        _cuttedMarker[0].AddTriangle(tri, subMesh);

                    else
                        _cuttedMarker[1].AddTriangle(tri, subMesh);

                    return true;
                }
                else {
                    // the victim mesh is half-cutted
                    return false;
                }
            }

            // Merge to the Chained Curved Blades
            for (int thisI = 0 ; thisI < _cBladesInTri.Count ; thisI++) {

                int otherI = (thisI + 1) % _cBladesInTri.Count;

                while (_cBladesInTri[thisI].endSide[0] == -1 || _cBladesInTri[thisI].endSide[1] == -1) {

                    if (thisI == otherI)
                        // this Triangle is half-cutted (not completely cutted);
                        return false;

                    if ( _cBladesInTri[thisI].CheckIfConnectedWith(_cBladesInTri[otherI], _newVertsCache) ) {
                        // Connected and Merged
                        _cBladesInTri.RemoveAt(otherI);

                        otherI = (thisI + 1) % _cBladesInTri.Count;
                    }
                    else
                        // Not Connected
                        otherI = (otherI + 1) % _cBladesInTri.Count;

                }
            }


            // Sort them to 3 tip-sides
            for (int i = 0 ; i < _cBladesInTri.Count ; i++) {

                if (_cBladesInTri[i].endSide[0] != _cBladesInTri[i].endSide[1])
                    _cBladesInTri[i].ReArrangeWhenDifferentEndSide();

                else
                    _cBladesInTri[i].ReArrangeWhenSameEndSide(tri.vertices[ _cBladesInTri[i].endSide[0] ], _newVertsCache);

                _cBladesInTri[i].InitTipIndOfTri();
                _cBladesInTri[i].InitSqrDistHeadToTail(_newVertsCache);

                List<CurvedBladeInTriangle> thisTipSide = _cBladesInTriSorted[ _cBladesInTri[i].tipIndOfTri ];

                if (thisTipSide.Count == 0)
                    thisTipSide.Add(_cBladesInTri[i]);

                else
                    for (int j = 0 ; j <= thisTipSide.Count ; j++) {

                        if (j < thisTipSide.Count)

                            if (_cBladesInTri[i].sqrDistHeadToTail < thisTipSide[j].sqrDistHeadToTail) {
                                thisTipSide.Insert(j, _cBladesInTri[i]);
                                break;
                            }

                        else
                            thisTipSide.Add(_cBladesInTri[i]);
                    }

            }

            // Split Triangle to Polygons
            List<PolygonIndices> newPolysInds        = new List<PolygonIndices>();
            List<bool>           isPosSideOfNewPolys = new List<bool>();

            List<int> centerPolyInds = new List<int>();

            for (int indOfTri = 0 ; indOfTri < 3 ; indOfTri++) {

                for (int sortedInd = indOfTri ; sortedInd < 6 ; sortedInd += 3) {

                    for (int i = 0 ; i < _cBladesInTriSorted[sortedInd].Count ; i++) {

                        CurvedBladeInTriangle thisCBlade = _cBladesInTriSorted[sortedInd][i];

                        List<int> thisPolyInds = new List<int>();
                        bool      thisPolyIsPosSide;

                        if (i == 0) {
                            if (sortedInd < 3) {

                                _newVertsCache.Add(tri.vertices[indOfTri]);
                                _newNormsChahe.Add(tri.normals[indOfTri]);

                                thisPolyInds.Add(_newVertsCache.Count - 1);
                                thisPolyIsPosSide = _isPosSideCache[indOfTri];
                            }
                            else {

                                Vector3 midPoint = (_newVertsCache[ thisCBlade.vertsInd[0] ] + _newVertsCache[ thisCBlade.vertsInd[thisCBlade.vertsInd.Count - 1] ]) / 2f;
                                thisPolyIsPosSide = _cKnife.GetSide(midPoint);
                            }
                        }
                        else {

                            thisPolyInds = new List<int>(_cBladesInTriSorted[sortedInd][i - 1].vertsInd);
                            thisPolyIsPosSide = !isPosSideOfNewPolys[isPosSideOfNewPolys.Count - 1];
                        }

                        for (int j = 0 ; j < thisCBlade.vertsInd.Count ; j++)

                            thisPolyInds.Add(thisCBlade.vertsInd[j]);


                        newPolysInds.Add( new PolygonIndices(thisPolyInds) );
                        isPosSideOfNewPolys.Add(thisPolyIsPosSide);

                        thisCBlade.vertsInd.Reverse();

                    }

                    if (_cBladesInTriSorted[sortedInd].Count == 0) {

                        if (sortedInd < 3) {

                            _newVertsCache.Add(tri.vertices[indOfTri]);
                            _newNormsChahe.Add(tri.normals[indOfTri]);

                            centerPolyInds.Add(_newVertsCache.Count - 1);
                        }
                    }
                    else {

                        CurvedBladeInTriangle lastCBlade = _cBladesInTriSorted[sortedInd][_cBladesInTriSorted[sortedInd].Count - 1];

                        for (int i = 0 ; i < lastCBlade.vertsInd.Count ; i++) {

                            centerPolyInds.Add(lastCBlade.vertsInd[i]);
                        }
                    }
                }

            }

            newPolysInds.Add( new PolygonIndices(centerPolyInds) );
            isPosSideOfNewPolys.Add(!isPosSideOfNewPolys[isPosSideOfNewPolys.Count - 1]);

            // separate mesh1 mesh2
            List<PolygonIndices>[] polysAtSide = new List<PolygonIndices>[2] {
                new List<PolygonIndices>(),
                new List<PolygonIndices>()
            };

            for (int i = 0 ; i < newPolysInds.Count ; i++) {

                if(isPosSideOfNewPolys[i])
                    polysAtSide[0].Add(newPolysInds[i]);

                else
                    polysAtSide[1].Add(newPolysInds[i]);

            }

            TriangulatePolygons(polysAtSide[0], triPlane.normal, 0, subMesh);
            TriangulatePolygons(polysAtSide[1], triPlane.normal, 1, subMesh);

            return true;
        }

        // Cap Cutted Face
        static void CapCurvedCuttedFace () {

            List<CurvedKnife.Polygon> knifePolys    = _cKnife.polygons;

            _polysCache.Clear();

            // Make sure the inner polygons of knife will be capped
            List<CurvedKnife.Polygon>  unharmedCKnifePolys = new List<CurvedKnife.Polygon>();
            List<List<PolygonIndices>> cappedMultiPolys    = new List<List<PolygonIndices>>();
            List<Vector3>              cappedNorms         = new List<Vector3>();

            for (int i = 0 ; i < knifePolys.Count ; i++) {

                _polysCache = knifePolys[i].GetReShapedPolygonForFitCap(_newVertsCache, _newNormsChahe);

                if (_polysCache != null) {

                    cappedMultiPolys.Add(_polysCache);
                    cappedNorms.Add(knifePolys[i].normal);
                }
                else
                    unharmedCKnifePolys.Add(knifePolys[i]);

            }


            for (int polysI = 0 ; polysI < cappedMultiPolys.Count ; polysI++) {

                for (int i = 0 ; i < cappedMultiPolys[polysI].Count ; i++) {

                    PolygonIndices thisPoly = cappedMultiPolys[polysI][i];

                    for (int j = 0 ; j < unharmedCKnifePolys.Count ; j++) {

                        if ( thisPoly.isConnectedWith(unharmedCKnifePolys[j].vertices, _newVertsCache) ) {

                            cappedMultiPolys.Add( unharmedCKnifePolys[j].GetCapOfWholePolygon(_newVertsCache) );
                            cappedNorms.Add(unharmedCKnifePolys[j].normal);

                            unharmedCKnifePolys.RemoveAt(j);
                            j--;
                        }
                    }
                }

            }

            for (int polysI = 0 ; polysI < cappedMultiPolys.Count ; polysI++)

                TriangulatePolygons(cappedMultiPolys[polysI], cappedNorms[polysI], 2, _capMatSub);

        }


        // Nested Class
        public class CurvedBladeInTriangle {

            public List<int>  vertsInd;
            public int[]      endSide;
            public int        tipIndOfTri;
            public float      sqrDistHeadToTail;

            public CurvedBladeInTriangle (int vertInd1, int vertInd2, int endSide0, int endSide1) {

                vertsInd = new List<int>();
                vertsInd.Add(vertInd1);
                vertsInd.Add(vertInd2);
                endSide = new int[2] {endSide0, endSide1};
                DefaultInit();
            }

            public CurvedBladeInTriangle (List<int> vertsInd, List<int> endSide) {

                this.vertsInd = new List<int>(vertsInd);
                this.endSide = new int[2] {endSide[0], endSide[1]};
                DefaultInit();
            }

            private void DefaultInit () {
                tipIndOfTri = -1;
            }

            public void ReArrangeWhenDifferentEndSide () {
                // re-arrange to {0,2} or {1,0} or {2,1}
                if (endSide[0] == 2 && endSide[1] == 0 ||
                    endSide[0] == 0 && endSide[1] == 1 ||
                    endSide[0] == 1 && endSide[1] == 2) {

                    vertsInd.Reverse();
                    System.Array.Reverse(endSide);
                }

            }

            public void ReArrangeWhenSameEndSide (Vector3 vert1OfTheEdge, List<Vector3> verts) {

                float sqrDistToHead = (verts[ vertsInd[0] ] - vert1OfTheEdge).sqrMagnitude;
                float sqrDistToTail = (verts[ vertsInd[vertsInd.Count - 1] ] - vert1OfTheEdge).sqrMagnitude;

                if (sqrDistToHead < sqrDistToTail)
                    vertsInd.Reverse();

            }

            public void InitTipIndOfTri () {

                if (endSide[0] != endSide[1]) {

                    int sideSum = endSide[0] + endSide[1];  // {0,1} = 1 ; {1,2} = 3 ; {2,0} = 2

                    if      (sideSum == 2) tipIndOfTri = 0;
                    else if (sideSum == 1) tipIndOfTri = 1;
                    else if (sideSum == 3) tipIndOfTri = 2;
                }
                else
                    tipIndOfTri = endSide[0] + 3;

            }

            public void InitSqrDistHeadToTail(List<Vector3> verts) {
                sqrDistHeadToTail = (verts[ vertsInd[0] ] - verts[ vertsInd[vertsInd.Count - 1] ]).sqrMagnitude;
            }

            public bool CheckIfConnectedWith (CurvedBladeInTriangle other, List<Vector3> verts) {

                if (endSide[0] == -1) {
                    if (other.endSide[0] == -1)
                        if (verts[ vertsInd[0] ] == verts[ other.vertsInd[0] ])
                            return ConnectWith(0, other, 0);

                    if (other.endSide[1] == -1)
                        if (verts[ vertsInd[0] ] == verts[ other.vertsInd[other.vertsInd.Count - 1] ])
                            return ConnectWith(0, other, 1);

                }

                if (endSide[1] == -1) {
                    if (other.endSide[0] == -1)
                        if (verts[ vertsInd[vertsInd.Count - 1] ] == verts[ other.vertsInd[0] ])
                            return ConnectWith(1, other, 0);

                    if (other.endSide[1] == -1)
                        if (verts[ vertsInd[vertsInd.Count - 1] ] == verts[ other.vertsInd[other.vertsInd.Count - 1] ])
                            return ConnectWith(1, other, 1);

                }
                return false;
            }

            public bool ConnectWith (int selfConnectSide, CurvedBladeInTriangle other, int otherConnectSide) {

                if (selfConnectSide == 0) {
                    if (otherConnectSide == 0) {

                        for (int i = 1 ; i < other.vertsInd.Count ; i++)
                            vertsInd.Insert(0, other.vertsInd[i]);

                        endSide[0] = other.endSide[1];

                    }
                    else {

                        for (int i = other.vertsInd.Count - 2 ; i >= 0 ; i--)
                            vertsInd.Insert(0, other.vertsInd[i]);

                        endSide[0] = other.endSide[0];

                    }
                }
                else {
                    if (otherConnectSide == 0) {

                        for (int i = 1 ; i < other.vertsInd.Count ; i++)
                             vertsInd.Add(other.vertsInd[i]);

                         endSide[1] = other.endSide[1];

                    }
                    else {

                        for (int i = other.vertsInd.Count - 2 ; i >= 0 ; i--)
                            vertsInd.Add(other.vertsInd[i]);

                        endSide[1] = other.endSide[0];

                    }
                }

                return true;
            }

        }

    }  // End of the (partial) Class

}
