using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomMathMethods;

namespace MeshCutting {

    public partial class MeshCutter {

        static Mesh _mesh;

        // Caching
        static MeshMarker[]         _cuttedMarker   = new MeshMarker[2] {new MeshMarker(), new MeshMarker()}; //  [0]: Positive Side , [1]: Negative Side
        static MeshMarker.Triangle  _triCache       = new MeshMarker.Triangle(new Vector3[3], new Vector2[3], new Vector3[3], new Vector4[3]);
        static List<Vector3>        _newVertsCache  = new List<Vector3>();
        static List<Vector3>        _newNormsChahe  = new List<Vector3>();  // for Polygon Arraged Clockwise use
        static bool[]               _isPosSideCache = new bool[3];
        static int                  _capMatSub      = 1;
        static List<PolygonIndices> _polysCache     = new List<PolygonIndices>();  // for capped Polygons


        static Material[] GetMaterialsWithCap (GameObject goVictim, Material capMat) {

            Material[] mats = goVictim.GetComponent<MeshRenderer>().sharedMaterials;
            // Does it already contains cap material ?
            if (capMat != null && mats[ mats.Length - 1 ].name != capMat.name) {
                Material[] newMats = new Material[mats.Length + 1];
                mats.CopyTo(newMats, 0);
                newMats[newMats.Length - 1] = capMat;
                mats = newMats;
            }

            _capMatSub = mats.Length - 1;  // for AddTriangle in Capping

            return mats;
        }

        static GameObject GetNewPartAfterSplitVictim (GameObject goVictim, Mesh m0, Mesh m1, Material[] mats) {

            GameObject newPart = (GameObject) Object.Instantiate(goVictim, goVictim.transform.parent);

            string origName = goVictim.name;

            goVictim.name += "-part0";
            goVictim.GetComponent<MeshFilter>().mesh        = m0;
            goVictim.GetComponent<MeshRenderer>().materials = mats;

            newPart.name = origName + "-part1";
            newPart.GetComponent<MeshFilter>().mesh         = m1;
            newPart.GetComponent<MeshRenderer>().materials  = mats;

            return newPart;
        }


        // Triangulate Polygons
        static readonly Vector3    baseUpForTriangulate = new Vector3(0.5f, 2.3f, 0.3f);    // just a series of random value
        static readonly Quaternion smallRotation        = new Quaternion(0.1f, 0f, 0f, 1f);

        static List<ChainPair> _chainPairs = new List<ChainPair>();

        static void TriangulatePolygons (List<PolygonIndices> polys, Vector3 normal, int toSide, int subMesh) {     // toSide = 0, 1, 2
            // polysInds are clockwise

            // The Center of the Cap (for UV)
            Vector3 center = Vector3.zero;

            int vertsCount = 0;

            for (int polyI = 0; polyI < polys.Count; polyI++) {

                foreach(int ind in polys[polyI].inds)
                    center += _newVertsCache[ind];

                vertsCount += polys[polyI].VertsCount;
            }
            center /= vertsCount;

            // Leftward and Upward of the Cutted Face
            Vector3 leftward = Vector3.zero;

            if (normal == baseUpForTriangulate || normal == -baseUpForTriangulate)
                leftward = Vector3.Cross(smallRotation * baseUpForTriangulate, normal).normalized;

            else
                leftward = Vector3.Cross(baseUpForTriangulate, normal).normalized;

            Vector3 upward = Vector3.Cross(normal, leftward).normalized;


            // === Use Sweep-Line Approach to Partition Non-Monotone polygon into Monotone Polygons and Triangulate the Monotone Polygons === //

            IndexOfPolygons[] sortedIndsOfPolys = new IndexOfPolygons[vertsCount];

            /// Step.1 - Sort the Vertices by Vertical Direction from Center
            for (int polyI = 0; polyI < polys.Count; polyI++) {

                polys[polyI].CalculateFromCenters(center, _newVertsCache);
                polys[polyI].CalculateDotValues(-upward, _newVertsCache);
            }

            for (int i = 0; i < sortedIndsOfPolys.Length; i++) {
                // Find the Minimum
                int minPolyI = -1;
                int minJ = 0;

                for (int polyI = 0 ; polyI < polys.Count ; polyI++) {

                    for (int j = 0 ; j < polys[polyI].dotValues.Length; j++) {

                        if (!polys[polyI].isComparedAt[j]) {

                            if (minPolyI == -1) {
                                minPolyI = polyI;
                                minJ = j;
                            }
                            else if (polys[polyI].dotValues[j] < polys[minPolyI].dotValues[minJ]) {
                                minPolyI = polyI;
                                minJ     = j;
                            }
                        }

                    }
                }

                // Add it
                sortedIndsOfPolys[i] = new IndexOfPolygons(minPolyI, minJ);
                polys[minPolyI].isComparedAt[minJ] = true;
            }


            /// Step.2 - Go Sweep=Line
            List<ChainPair[]> mergePendingsChainPairs = new List<ChainPair[]>();     // ChainPair[2] {left side chain, right side chain}

            _chainPairs.Clear();
            _chainPairs.Add( new ChainPair(sortedIndsOfPolys[0]) );

            for (int i = 1; i < sortedIndsOfPolys.Length; i++) {

                bool isNewStart = true;
                bool isEndPoint = false;

                List<ChainPairSideInd> connectionsSideOfChain = new List<ChainPairSideInd>();

                for (int pairI = 0 ; pairI < _chainPairs.Count ; pairI++) {

                    ChainPair thisChainPair = _chainPairs[pairI];

                    int polyVertsCount = polys[sortedIndsOfPolys[i].polyI].inds.Count;
                    int connectSide = thisChainPair.GetConnectedSide(sortedIndsOfPolys[i], polyVertsCount);  // -1: none, 0: left, 1: right, 2: both

                    if (connectSide == -1)
                        continue;

                    isNewStart = false;
                    connectionsSideOfChain.Add( new ChainPairSideInd(thisChainPair, connectSide) );

                    AddPointAndTriangle(sortedIndsOfPolys[i], thisChainPair, connectSide, polys, normal, upward, leftward, toSide, subMesh, out isEndPoint);

                    if (isEndPoint)
                        break;

                }

                //// CASE.V-Shape - Merge
                // 1.Merge Pending
                if (!isEndPoint && connectionsSideOfChain.Count == 2) {

                    for (int d = 0 ; d < 2 ; d++) {

                        int dd = (d + 1) % 2;

                        if (connectionsSideOfChain[d].side == 1)   // connected at left side
                            mergePendingsChainPairs.Add( new ChainPair[2] {connectionsSideOfChain[d].chainPair, connectionsSideOfChain[dd].chainPair} ); // {Left ChainPair, Right ChainPair}

                    }
                }
                // 2.Checking & Merging (Normal Situation)
                for (int j = 0 ; j < mergePendingsChainPairs.Count ; j++) {

                    IndexOfPolygons[] indsOfPolys = {
                        mergePendingsChainPairs[j][0].LeftTop,
                        mergePendingsChainPairs[j][0].RightLast,
                        mergePendingsChainPairs[j][1].RightTop,
                        mergePendingsChainPairs[j][1].LeftLast
                    };

                    if (polys[indsOfPolys[0].polyI].dotValues[indsOfPolys[0].ind] > polys[indsOfPolys[1].polyI].dotValues[indsOfPolys[1].ind])

                        mergePendingsChainPairs[j][0].readyToMerge[1] = true;


                    if (polys[indsOfPolys[2].polyI].dotValues[indsOfPolys[2].ind] > polys[indsOfPolys[3].polyI].dotValues[indsOfPolys[3].ind])

                        mergePendingsChainPairs[j][1].readyToMerge[0] = true;


                    if (mergePendingsChainPairs[j][0].readyToMerge[1] && mergePendingsChainPairs[j][1].readyToMerge[0]) {

                        ChainPair.Merge(mergePendingsChainPairs[j][0], mergePendingsChainPairs[j][1]);
                        _chainPairs.Remove( mergePendingsChainPairs[j][1] );

                        mergePendingsChainPairs.RemoveAt(j);
                        j--;
                    }
                }

                // Not Connected With Any Current Chain
                if (isNewStart) {

                    int polyI = sortedIndsOfPolys[i].polyI;
                    int ind   = sortedIndsOfPolys[i].ind;
                    int indLeftEdge  = MyMath.Mod(ind - 1, polys[polyI].inds.Count);
                    int indRightEdge = MyMath.Mod(ind + 1, polys[polyI].inds.Count);

                    Vector3 dirLeftEdge  = (_newVertsCache[ polys[polyI].inds[indLeftEdge] ] - _newVertsCache[ polys[polyI].inds[ind] ]).normalized;
                    Vector3 dirRightEdge = (_newVertsCache[ polys[polyI].inds[indRightEdge] ] - _newVertsCache[ polys[polyI].inds[ind] ]).normalized;

                    if (Vector3.Dot(dirLeftEdge - dirRightEdge, leftward) > 0) {

                        // CASE.Outer - New
                        _chainPairs.Add( new ChainPair(sortedIndsOfPolys[i]) );
                    }
                    else {

                        // CASE.Inner - Split
                        List<ChainPair> matchedChainPairs = new List<ChainPair>();

                        for (int pairI = 0; pairI < _chainPairs.Count; pairI++) {

                            ChainPair thisChainPair = _chainPairs[pairI];

                            Vector3[] startPoint = new Vector3[2];
                            Vector3[] angleRange = new Vector3[2];

                            // Step.1 - Prepare Some Vectors for Later Check
                            IndexOfPolygons[] indsOfPolys = {
                                thisChainPair.LeftLast,
                                thisChainPair.RightLast,
                                thisChainPair.LeftLast,
                                thisChainPair.RightLast
                            };

                            indsOfPolys[2].ind = MyMath.Mod(indsOfPolys[2].ind - 1, polys[indsOfPolys[2].polyI].inds.Count);
                            indsOfPolys[3].ind = MyMath.Mod(indsOfPolys[3].ind + 1, polys[indsOfPolys[3].polyI].inds.Count);

                            startPoint[0] = _newVertsCache[ polys[indsOfPolys[0].polyI].inds[indsOfPolys[0].ind] ];
                            startPoint[1] = _newVertsCache[ polys[indsOfPolys[1].polyI].inds[indsOfPolys[1].ind] ];
                            angleRange[0] = (_newVertsCache[ polys[indsOfPolys[2].polyI].inds[indsOfPolys[2].ind] ] - startPoint[0]).normalized;
                            angleRange[1] = (_newVertsCache[ polys[indsOfPolys[3].polyI].inds[indsOfPolys[3].ind] ] - startPoint[1]).normalized;

                            // turn 90 degree
                            angleRange[0] = Vector3.Dot(angleRange[0], upward) > 0 ? -upward : Vector3.Cross(angleRange[0], normal);
                            angleRange[1] = Vector3.Dot(angleRange[1], upward) > 0 ? -upward : Vector3.Cross(normal, angleRange[1]);

                            Vector3 thisPoint = _newVertsCache[ polys[sortedIndsOfPolys[i].polyI].inds[sortedIndsOfPolys[i].ind] ];

                            // Step.2 - Check if thisPoint is Belong to Here
                            if (Vector3.Dot(thisPoint - startPoint[0], angleRange[0]) >= 0 && Vector3.Dot(thisPoint - startPoint[1], angleRange[1]) >= 0)
                                matchedChainPairs.Add(thisChainPair);

                        }

                        List<ChainPair[]> matchedMergePendings    = new List<ChainPair[]>();
                        List<ChainPair>   shouldIgnoredChainPairs = new List<ChainPair>();

                        for (int j = 0 ; j < mergePendingsChainPairs.Count ; j++) {

                            if ( matchedChainPairs.Contains(mergePendingsChainPairs[j][0]) && matchedChainPairs.Contains(mergePendingsChainPairs[j][1]) )
                                matchedMergePendings.Add(mergePendingsChainPairs[j]);

                            else if ( matchedChainPairs.Contains(mergePendingsChainPairs[j][0]) )
                                shouldIgnoredChainPairs.Add(mergePendingsChainPairs[j][0]);

                            else if ( matchedChainPairs.Contains(mergePendingsChainPairs[j][1]) )
                                shouldIgnoredChainPairs.Add(mergePendingsChainPairs[j][1]);

                        }

                        if (matchedMergePendings.Count == 0) {
                            // Normal Situation

                            for (int j = 0 ; j < shouldIgnoredChainPairs.Count ; j++)
                                matchedChainPairs.Remove(shouldIgnoredChainPairs[j]);

                            ChainPair thisChainPair = matchedChainPairs[0];

                            IndexOfPolygons newTipIndOfPolys = thisChainPair.GetLastOfSide(thisChainPair.lastestSide);

                            AddPointAndTriangle(sortedIndsOfPolys[i], thisChainPair, thisChainPair.lastestSide, polys, normal, upward, leftward, toSide, subMesh, out _);

                            ChainPair newPair = new ChainPair(newTipIndOfPolys);
                            newPair.AddPoint(sortedIndsOfPolys[i], (thisChainPair.lastestSide + 1) % 2);

                            _chainPairs.Add(newPair);
                        }
                        else {
                            // Split along this point to the lastest mergePending point

                            int maxJ = 0;
                            for (int j = 1 ; j < matchedMergePendings.Count ; j++) {

                                IndexOfPolygons indOfPolys = matchedMergePendings[j][0].RightLast;
                                IndexOfPolygons maxIndOfPolys = matchedMergePendings[maxJ][0].RightLast;

                                if (polys[indOfPolys.polyI].dotValues[indOfPolys.ind] > polys[maxIndOfPolys.polyI].dotValues[maxIndOfPolys.ind])
                                    maxJ = j;

                            }

                            ChainPair[] mergePendingPair = matchedMergePendings[maxJ];

                            AddPointAndTriangle(sortedIndsOfPolys[i], mergePendingPair[0], 1, polys, normal, upward, leftward, toSide, subMesh, out _);
                            AddPointAndTriangle(sortedIndsOfPolys[i], mergePendingPair[1], 0, polys, normal, upward, leftward, toSide, subMesh, out _);

                            mergePendingsChainPairs.Remove(mergePendingPair);
                        }

                    }

                }
            }

        }

        static void AddPointAndTriangle (IndexOfPolygons thisPointIndOfPolys, ChainPair thisChainPair, int connectSide, List<PolygonIndices> polys, Vector3 normal, Vector3 upward, Vector3 leftward, int toSide, int subMesh, out bool isEndPoint) {

            IndexOfPolygons[] triIndsOfPolys  = new IndexOfPolygons[3];
            Vector2[]         newTriUV        = new Vector2[3] {Vector2.zero, Vector2.zero, Vector2.zero};

            isEndPoint = false;

            while (thisChainPair.PointCount > 1) {
                // Maybe We Can Add a Triangle Now, Maybe

                // init p0 and p1
                int side = connectSide % 2;

                PointOnChain[] p = {new PointOnChain(), new PointOnChain()};

                p[0].indOfChain = new IndexOfChain( side, thisChainPair.GetLastIndOfSide(side) );
                p[1].indOfChain = new IndexOfChain( side, thisChainPair.GetLastIndOfSide(side) - 1);

                for (int d = 0 ; d < 2 ; d++) {

                    if (p[d].indOfChain.ind < 0) {

                        p[d].indOfChain.SwitchSide();
                        p[d].indOfChain.ind = -p[d].indOfChain.ind - 1;
                    }

                    p[d].indOfPolys = thisChainPair.sidesIndsOfPolys[ p[d].indOfChain.side ][ p[d].indOfChain.ind ];
                    p[d].vertInd = polys[ p[d].indOfPolys.polyI ].inds[ p[d].indOfPolys.ind ];
                }

                // Check if the Triangle is Legal and Add it if Legal
                int thisVertInd  = polys[thisPointIndOfPolys.polyI].inds[thisPointIndOfPolys.ind];

                Vector3 dirToP0 = (_newVertsCache[p[0].vertInd] - _newVertsCache[thisVertInd]).normalized;
                Vector3 dirToP1 = (_newVertsCache[p[1].vertInd] - _newVertsCache[thisVertInd]).normalized;

                Vector3 sideVect = side == 0 ? leftward : -leftward;

                if (Vector3.Dot(dirToP0 - dirToP1, sideVect) > 0) {
                    // legal

                    //Let's Add Triangle
                    triIndsOfPolys[0] = thisPointIndOfPolys;
                    triIndsOfPolys[1] = p[0].indOfPolys;
                    triIndsOfPolys[2] = p[1].indOfPolys;

                    for (int j = 0; j < 3; j++) {

                        newTriUV[j].x = 0.5f + Vector3.Dot(polys[triIndsOfPolys[j].polyI].fromCenters[triIndsOfPolys[j].ind], leftward);
                        newTriUV[j].y = 0.5f + Vector3.Dot(polys[triIndsOfPolys[j].polyI].fromCenters[triIndsOfPolys[j].ind], upward);

                        _triCache.vertices[j] = _newVertsCache[ polys[triIndsOfPolys[j].polyI].inds[triIndsOfPolys[j].ind] ];
                        _triCache.uvs[j]      = newTriUV[j];
                        _triCache.normals[j]  = normal;
                        _triCache.tangents[j] = Vector4.zero;
                    }

                    CheckNormal(ref _triCache);

                    if (toSide == 2) {
                        // Add to Negative Side
                        _cuttedMarker[1].AddTriangle(_triCache, subMesh);

                        // Add to Positive Side
                        for (int j = 0 ; j < 3 ; j++) {
                            _triCache.normals[j] *= -1;
                        }
                        CheckNormal(ref _triCache);
                        _cuttedMarker[0].AddTriangle(_triCache, subMesh);
                    }
                    else
                        _cuttedMarker[toSide].AddTriangle(_triCache, subMesh);


                    // step.3 - Remove Mid Point
                    thisChainPair.sidesIndsOfPolys[ p[0].indOfChain.side ].RemoveAt( p[0].indOfChain.ind );
                }
                else {
                    // illegal
                    break;
                }

            }

            // Ending Point or Add this Point to the Chain
            if (connectSide == 2) {
                _chainPairs.Remove(thisChainPair);
                isEndPoint = true;
            }
            else {
                thisChainPair.AddPoint(thisPointIndOfPolys, connectSide);
            }
        }


        static void CheckNormal (ref MeshMarker.Triangle tri) {

            Vector3 nowDir = Vector3.Cross(tri.vertices[1] - tri.vertices[0], tri.vertices[2] - tri.vertices[0]);
            Vector3 avgNormal = (tri.normals[0] + tri.normals[1] + tri.normals[2]) / 3f;

            if (Vector3.Dot(nowDir, avgNormal) < 0) {
                // Swap [1] & [2]
                tri.vertices[1] -= tri.vertices[2];
                tri.uvs[1]      -= tri.uvs[2];
                tri.normals[1]  -= tri.normals[2];
                tri.tangents[1] -= tri.tangents[2];

                tri.vertices[2] += tri.vertices[1];
                tri.uvs[2]      += tri.uvs[1];
                tri.normals[2]  += tri.normals[1];
                tri.tangents[2] += tri.tangents[1];

                tri.vertices[1] -= tri.vertices[2];
                tri.uvs[1]      -= tri.uvs[2];
                tri.normals[1]  -= tri.normals[2];
                tri.tangents[1] -= tri.tangents[2];

                tri.vertices[1] *= -1;
                tri.uvs[1]      *= -1;
                tri.normals[1]  *= -1;
                tri.tangents[1] *= -1;
            }
        }


        // Nested Structs
        public struct IndexOfPolygons {

            public int polyI;
            public int ind;

            public IndexOfPolygons (int polyI, int ind) {
                this.polyI = polyI;
                this.ind   = ind;
            }

            public void Set (IndexOfPolygons indOfPolys) {
                Set(indOfPolys.polyI, indOfPolys.ind);
            }

            public void Set (int polyI, int ind) {
                this.polyI = polyI;
                this.ind   = ind;
            }

            public static bool operator == (IndexOfPolygons inds1, IndexOfPolygons inds2) {
                return inds1.polyI == inds2.polyI && inds1.ind == inds2.ind;
            }

            public static bool operator != (IndexOfPolygons inds1, IndexOfPolygons inds2) {
                return !(inds1.polyI == inds2.polyI && inds1.ind == inds2.ind);
            }


            public override bool Equals(object obj) {
                 return base.Equals(obj);
            }

            public override int GetHashCode() {
                 return base.GetHashCode();
            }

            public override string ToString () {
                return "{ polyI: " + polyI + ", ind: " + ind + " }";
            }

        }

        public struct ChainPairSideInd {

            public ChainPair chainPair;
            public int side;

            public ChainPairSideInd (ChainPair cp, int side) {
                chainPair = cp;
                this.side = side;
            }

            public void Set (ChainPair cp, int side) {
                chainPair = cp;
                this.side = side;
            }

            public static bool operator == (ChainPairSideInd cpsi1, ChainPairSideInd cpsi2) {
                return cpsi1.chainPair == cpsi2.chainPair && cpsi1.side == cpsi2.side;
            }

            public static bool operator != (ChainPairSideInd cpsi1, ChainPairSideInd cpsi2) {
                return !(cpsi1.chainPair == cpsi2.chainPair && cpsi1.side == cpsi2.side);
            }


            public override bool Equals(object obj) {
                 return base.Equals(obj);
            }

            public override int GetHashCode() {
                 return base.GetHashCode();
            }

        }

        public struct IndexOfChain {

            public int side;
            public int ind;

            public IndexOfChain (int side, int ind) {
                this.side = side;
                this.ind = ind;
            }

            public void Set (int side, int ind) {
                this.side = side;
                this.ind = ind;
            }

            public void SwitchSide () {
                side = (side + 1) % 2;
            }

            public static bool operator == (IndexOfChain a, IndexOfChain b) {
                return a.side == b.side && a.ind == b.ind;
            }

            public static bool operator != (IndexOfChain a, IndexOfChain b) {
                return !(a.side == b.side && a.ind == b.ind);
            }


            public override bool Equals(object obj) {
                 return base.Equals(obj);
            }

            public override int GetHashCode() {
                 return base.GetHashCode();
            }

            public override string ToString () {
                return "{side: " + side + ", ind: " + ind + "}";
            }
        }

        public struct PointOnChain {
            public IndexOfChain    indOfChain;
            public IndexOfPolygons indOfPolys;
            public int             vertInd;
        }


    }   // End of the (partial) Class

}
