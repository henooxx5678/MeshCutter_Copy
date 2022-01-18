using System;    // for Array.*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomMathMethods;

namespace MeshCutting {

    public class CurvedKnife {

        public List<Polygon> polygons = new List<Polygon>();

        // Caching
        Vector3[] _triVertsTemp = new Vector3[3];
        Vector3   _normalTemp   = Vector3.zero;
        bool      _isDoneTemp;

        public CurvedKnife (Vector3[] verts, int[] tris) {     // tris means triangles

            // Initialize this.polygons
            _triVertsTemp = new Vector3[3] { verts[tris[0]], verts[tris[1]], verts[tris[2]] };
            _normalTemp   = MyMath.GetNormalOfClockwiseTriagle(_triVertsTemp);

            polygons.Add( new Polygon(_triVertsTemp, _normalTemp) );

            for (int i = 3 ; i < tris.Length ; i += 3) {

                _triVertsTemp = new Vector3[3] { verts[tris[i + 0]], verts[tris[i + 1]], verts[tris[i + 2]] };
                _normalTemp   = MyMath.GetNormalOfClockwiseTriagle(_triVertsTemp);

                List<Polygon> connectedPolys = new List<Polygon>();
                List<int>     connectionInds = new List<int>();
                List<int>     triOutSideInds = new List<int>();

                for (int polyI = 0 ; polyI < polygons.Count ; polyI++) {

                    int indOfConnection = -1;
                    int indOfTriOutSide = -1;

                    if (polygons[polyI].isConnectedWith(_triVertsTemp, out indOfConnection, out indOfTriOutSide) && polygons[polyI].normal == _normalTemp) {

                        connectedPolys.Add(polygons[polyI]);
                        connectionInds.Add(indOfConnection);
                        triOutSideInds.Add(indOfTriOutSide);
                    }
                }

                if (connectedPolys.Count == 0) {
                    // New Polygon
                    polygons.Add( new Polygon(_triVertsTemp, _normalTemp) );
                }
                else {
                    // Expand or Merge Polygons
                    connectedPolys[0].vertices.Insert(connectionInds[0] + 1, _triVertsTemp[ triOutSideInds[0] ]);

                    for (int j = 1 ; j < connectedPolys.Count ; j++) {

                        for (int iterator = 0 ; iterator < connectionInds[j] ; iterator++) {

                            Vector3 temp = connectedPolys[j].vertices[0];

                            connectedPolys[j].vertices.RemoveAt(0);
                            connectedPolys[j].vertices.Add(temp);
                        }

                        connectedPolys[j].vertices.RemoveAt(0);
                        connectedPolys[j].vertices.RemoveAt(0);

                        if (triOutSideInds[j] - triOutSideInds[0] == 1 || triOutSideInds[j] - triOutSideInds[0] == -2) {
                            // clockwise one
                            for (int k = 0 ; k < connectedPolys[j].vertices.Count ; k++) {

                                connectedPolys[0].vertices.Insert(connectionInds[0] + 1, connectedPolys[j].vertices[k]);
                                connectionInds[0]++;
                            }

                            polygons.Remove(connectedPolys[j]);
                        }
                        else {
                            // anti-clockwise one
                            for (int k = 0 ; k < connectedPolys[j].vertices.Count ; k++) {

                                connectedPolys[0].vertices.Insert(connectionInds[0] + 2 + k, connectedPolys[j].vertices[k]);
                            }

                            polygons.Remove(connectedPolys[j]);
                        }
                    }


                    List<Vector3> thisPolyVerts = connectedPolys[0].vertices;

                    for (int j = 0 ; j < thisPolyVerts.Count ; j++) {

                        // Find and Remove the duplicated Edges
                        List<int> killedPendingInds = new List<int>();

                        _isDoneTemp = false;

                        for (int k = 1 ; !_isDoneTemp ; k++) {

                            _isDoneTemp = true;

                            int prevNInd = MyMath.Mod(j - k, thisPolyVerts.Count);
                            int nextNInd = MyMath.Mod(j + k, thisPolyVerts.Count);

                            if (thisPolyVerts[prevNInd] == thisPolyVerts[nextNInd]) {

                                _isDoneTemp = false;
                                killedPendingInds.Add(prevNInd);
                                killedPendingInds.Add(nextNInd);
                            }
                        }

                        if (killedPendingInds.Count > 0) {

                            killedPendingInds.RemoveAt(killedPendingInds.Count - 1);
                            killedPendingInds.Add(j);
                            killedPendingInds.Sort();
                            killedPendingInds.Reverse();

                            for (int k = 0 ; k < killedPendingInds.Count ; k++)

                                thisPolyVerts.RemoveAt(killedPendingInds[k]);

                            j -= killedPendingInds.Count / 2;
                        }
                    }


                }

            }

            // Remove the mid-way vertex of a straight line
            for (int i = 0 ; i < polygons.Count ; i++) {

                for (int j = 0 ; j < polygons[i].vertices.Count ; j++) {

                    Vector3 prevVert = polygons[i].vertices[ MyMath.Mod(j - 1, polygons[i].vertices.Count) ];
                    Vector3 nextVert = polygons[i].vertices[ MyMath.Mod(j + 1, polygons[i].vertices.Count) ];

                    if (Vector3.Cross(polygons[i].vertices[j] - prevVert, nextVert - prevVert) == Vector3.zero) {

                        polygons[i].vertices.RemoveAt(j);
                        j--;
                    }
                }
            }

            // // Mark up the outer edges
            // int startPolyInd = -1;
            // int startVertInd = -1;
            // int nowPolyInd = -1;
            // int nowVertInd = -1;
            //
            // for (int polyI = 0 ; polyI < polygons.Count ; polyI++) {
            //
            //     for (int edgeI = 0 ; edgeI < polygons[polyI].vertices.Count ; edgeI++) {
            //
            //         if ( CheckIfEdgeIsOuter(polyI, edgeI) ) {
            //
            //             polygons[polyI].edgesOuterOfCKnife.Add(edgeI);
            //
            //             startPolyInd = polyI;
            //             startVertInd = edgeI;
            //             nowPolyInd = startPolyInd;
            //             nowVertInd = startVertInd;
            //         }
            //     }
            // }
            //
            // if (startPolyInd != -1) {
            //
            //     int nextVertInd = 0;
            //
            //     for (bool isDone = false ; !isDone ; ) {
            //
            //         nowVertInd = (nowVertInd + 1) % polygons[nowPolyInd].vertices.Count;
            //
            //         if (nowPolyInd == startPolyInd && nowVertInd == startVertInd)
            //             isDone = true;
            //
            //         else
            //             for (int polyI = (nowPolyInd + 1) % polygons.Count ; polyI != nowPolyInd ; polyI = (polyI + 1) % polygons.Count) {
            //
            //                 if ( polygons[nowPolyInd].CheckIfVertexConnectedWith(nowVertInd, polygons[polyI], out nextVertInd) ) {
            //
            //                     if ( CheckIfEdgeIsOuter(polyI, nextVertInd) ) {
            //
            //                         polygons[polyI].edgesOuterOfCKnife.Add(nextVertInd);
            //
            //                         nowPolyInd = polyI;
            //                         nowVertInd = nextVertInd;
            //                         break;
            //                     }
            //                 }
            //             }
            //     }
            // }

        }


        public bool GetSide (Vector3 p) {

            List<int> minIs        = new List<int>();
            Vector3   minVectFromP = Vector3.positiveInfinity;

            for (int i = 0 ; i < polygons.Count ; i++) {

                Vector3 thisVectFromP = polygons[i].GetMinVectFromP(p);

                if (thisVectFromP.sqrMagnitude < minVectFromP.sqrMagnitude) {

                    minIs.Clear();
                    minIs.Add(i);
                    minVectFromP = thisVectFromP;
                }
                else if ( Mathf.Approximately(thisVectFromP.sqrMagnitude, minVectFromP.sqrMagnitude) ) {

                    minIs.Add(i);
                }
            }

            // return True if point p at the positive side
            Vector3 avgNormalDir = Vector3.zero;

            for (int i = 0 ; i < minIs.Count ; i++) {
                avgNormalDir += polygons[minIs[i]].normal;
            }

            return Vector3.Dot(minVectFromP, avgNormalDir) < 0;
        }

        public bool CheckIfEdgeIsOuter (int polyInd, int edgeInd) {

            Vector3[] thisEdge = new Vector3[2] {
                polygons[polyInd].vertices[edgeInd],
                polygons[polyInd].vertices[ (edgeInd + 1) % polygons[polyInd].vertices.Count ]
            };

            for (int polyI = (polyInd + 1) % polygons.Count ; polyI != polyInd ; polyI = (polyI + 1) % polygons.Count) {

                for (int edgeI = 0 ; edgeI < polygons[polyI].vertices.Count ; edgeI++) {

                    Vector3[] otherEdge = new Vector3[2] {
                        polygons[polyI].vertices[edgeI],
                        polygons[polyI].vertices[ (edgeI + 1) % polygons[polyI].vertices.Count ]
                    };

                    if (thisEdge[0] == otherEdge[1] && thisEdge[1] == otherEdge[0])
                        return false;

                }
            }

            return true;
        }


        public class Polygon {

            public List<Vector3> vertices;
            public Vector3       normal;
            public List<int>     newVertsInd;
            public List<int>     edgeNewVertsOn;
            // public List<int>     edgesOuterOfCKnife;

            public Polygon (Vector3[] verts, Vector3 normal) {

                vertices    = new List<Vector3>(verts);
                this.normal = normal;

                DefaultInit();
            }

            public Polygon (List<Vector3> verts, Vector3 normal) {

                vertices    = new List<Vector3>(verts);
                this.normal = normal;

                DefaultInit();
            }

            public void DefaultInit () {
                newVertsInd          = new List<int>();
                edgeNewVertsOn       = new List<int>();
                // edgesOuterOfCKnife   = new List<int>();
            }


            public void AddNewVertInd (int ind, int edgeOn) {

                newVertsInd.Add(ind);
                edgeNewVertsOn.Add(edgeOn);
            }

            public bool CheckIfVertexConnectedWith (int vertInd, Polygon other, out int otherVertInd) {

                for (int i = 0 ; i < other.vertices.Count ; i++) {

                    if (vertices[vertInd] == other.vertices[i]) {

                        otherVertInd = i;
                        return true;
                    }
                }

                otherVertInd = -1;
                return false;
            }

            public bool CheckIfEdgeConnectedWith (int edgeInd, Polygon other, out int otherEdgeInd) {

                Vector3[] thisEdge = new Vector3[2] {
                    vertices[edgeInd],
                    vertices[ (edgeInd + 1) % vertices.Count ]
                };

                for (int i = 0 ; i < other.vertices.Count ; i++) {

                    Vector3[] otherEdge = new Vector3[2] {
                        other.vertices[i],
                        other.vertices[ (i + 1) % other.vertices.Count ]
                    };

                    if (thisEdge[0] == otherEdge[1] && thisEdge[1] == otherEdge[0]) {

                        otherEdgeInd = i;
                        return true;
                    }
                }

                otherEdgeInd = -1;
                return false;
            }

            public bool isConnectedWith (Polygon other, out int thisEdgeInd, out int otherEdgeInd) {

                for (int i = 0 ; i < vertices.Count ; i++) {

                    if ( CheckIfEdgeConnectedWith(i, other, out otherEdgeInd) ) {

                        thisEdgeInd = i;
                        return true;
                    }

                }

                thisEdgeInd  = -1;
                otherEdgeInd = -1;
                return false;
            }

            public bool isConnectedWith (Vector3[] triVerts, out int indOfConnection, out int indOfTriOutSide) {

                for (int i = 0 ; i < vertices.Count ; i++) {

                    int[] thisTriEdgeInds = { Array.IndexOf(triVerts, vertices[i]), Array.IndexOf(triVerts, vertices[(i + 1) % vertices.Count]) };

                    if (thisTriEdgeInds[0] != -1 && thisTriEdgeInds[1] != -1) {

                        indOfConnection = i;
                        indOfTriOutSide = 3 - thisTriEdgeInds[0] - thisTriEdgeInds[1];

                        return true;
                    }
                }

                indOfConnection = -1;
                indOfTriOutSide = -1;
                return false;
            }

            public Vector3 GetMinVectFromP (Vector3 p) {

                int     minInd = -1;
                Vector3 minPToEdge = Vector3.positiveInfinity;
                float   minToTheOtherDirDotValue  = Mathf.Infinity;

                for (int i = 0 ; i < vertices.Count ; i++) {

                    int   nextInd = (i + 1) % vertices.Count;
                    float toTheOtherDirDotValue;

                    Vector3 pToEdge = MyMath.GetFromPToEdge(p, vertices[i], vertices[nextInd], out toTheOtherDirDotValue);

                    // Find min dist of p to vertices
                    bool sameDist     = Mathf.Approximately(pToEdge.sqrMagnitude, minPToEdge.sqrMagnitude);
                    bool isSmaller    = pToEdge.sqrMagnitude < minPToEdge.sqrMagnitude;
                    bool edgeIsCloser = toTheOtherDirDotValue < minToTheOtherDirDotValue;

                    if (!sameDist && isSmaller || sameDist && edgeIsCloser) {

                        minInd = i;
                        minPToEdge = pToEdge;
                        minToTheOtherDirDotValue = toTheOtherDirDotValue;
                    }

                }


                // check if the point p casted on the plane is inside the polygon or not
                Vector3[] nearestEdgePoints = { vertices[minInd], vertices[(minInd + 1) % vertices.Count] };

                Vector3 crossProduct = Vector3.Cross(minPToEdge, nearestEdgePoints[1] - nearestEdgePoints[0]);

                if (Vector3.Dot(crossProduct, normal) > 0)
                    // is inside
                    return Vector3.Dot(minPToEdge, normal) * normal;

                else
                    // is outside
                    return minPToEdge;

            }


            public List<PolygonIndices> GetCapOfWholePolygon (List<Vector3> newVerts) {

                List<PolygonIndices> result = new List<PolygonIndices>();
                List<int> inds = new List<int>();

                for (int i = 0 ; i < vertices.Count ; i++) {

                    newVerts.Add(vertices[i]);
                    inds.Add(newVerts.Count - 1);
                }

                result.Add( new PolygonIndices(inds) );
                return result;
            }

            public List<PolygonIndices> GetReShapedPolygonForFitCap (List<Vector3> newVerts, List<Vector3> newNorms) {

                if (newVertsInd.Count == 0)
                    return null;


                bool[] used = new bool[newVertsInd.Count];
                // init "used"
                for (int i = 0 ; i < used.Length ; i++)
                    used[i] = false;

                List<List<int>> chains       = new List<List<int>>();
                List<int[]>     attachedSides = new List<int[]>();       // elements: int[2]

                // one chain per loop
                for (int i = 0 ; i < newVertsInd.Count ; i += 2) {

                    if (used[i] || newVerts[ newVertsInd[i] ] == newVerts[ newVertsInd[i + 1] ])
                        continue;

                    used[i] = true;
                    used[i + 1] = true;


                    List<int> thisChain = new List<int>();
                    int[]     thisAttSide = new int[2];

                    // Connect newVertsInd to Chains, and arranged clockwise
                    Vector3 edgeVect = newVerts[ newVertsInd[i + 1] ] - newVerts[ newVertsInd[i] ];
                    Vector3 edgeNormalVect = newNorms[ newVertsInd[i] ] + newNorms[ newVertsInd[i + 1] ];

                    if (Vector3.Dot(Vector3.Cross(edgeNormalVect, edgeVect), normal) > 0) {

                        thisChain.Add(newVertsInd[i]);
                        thisChain.Add(newVertsInd[i + 1]);

                        thisAttSide[0] = edgeNewVertsOn[i];
                        thisAttSide[1] = edgeNewVertsOn[i + 1];
                    }
                    else {

                        thisChain.Add(newVertsInd[i + 1]);
                        thisChain.Add(newVertsInd[i]);

                        thisAttSide[0] = edgeNewVertsOn[i + 1];
                        thisAttSide[1] = edgeNewVertsOn[i];
                    }


                    // Stretch both sides
                    for (bool isDone = false ; !isDone ; ) {

                        isDone = true;

                        for (int j = 0 ; j < newVertsInd.Count ; j += 2) {

                            if (used[j])
                                continue;

                            bool[,] cond = new bool[2,2];
                            cond[0,0] = newVerts[ thisChain[0] ] == newVerts[ newVertsInd[j] ];
                            cond[0,1] = newVerts[ thisChain[0] ] == newVerts[ newVertsInd[j + 1] ];
                            cond[1,0] = newVerts[ thisChain[thisChain.Count - 1] ] == newVerts[ newVertsInd[j] ];
                            cond[1,1] = newVerts[ thisChain[thisChain.Count - 1] ] == newVerts[ newVertsInd[j + 1] ];

                            if (cond[0,0] || cond[0,1] || cond[1,0] || cond[1,1]) {

                                if (cond[0,0]) {

                                    thisChain.Insert(0, newVertsInd[j + 1]);
                                    thisAttSide[0] = edgeNewVertsOn[j + 1];
                                }
                                else if (cond[0,1]) {

                                    thisChain.Insert(0, newVertsInd[j]);
                                    thisAttSide[0] = edgeNewVertsOn[j];
                                }
                                else if (cond[1,0]) {

                                    thisChain.Add(newVertsInd[j + 1]);
                                    thisAttSide[1] = edgeNewVertsOn[j + 1];
                                }
                                else if (cond[1,1]) {

                                    thisChain.Add(newVertsInd[j]);
                                    thisAttSide[1] = edgeNewVertsOn[j];
                                }

                                used[j] = true;
                                isDone = false;
                            }

                        }
                    }

                    if (newVerts[ thisChain[0] ] == newVerts[ thisChain[thisChain.Count - 1] ]) {
                        // that's a ring
                        thisChain.RemoveAt(0);
                        thisAttSide = new int[2] {- 1, -1};
                    }

                    chains.Add(thisChain);
                    attachedSides.Add(thisAttSide);

                }

                // Get Result Polygons
                List<PolygonIndices> result = new List<PolygonIndices>();

                bool originIsBroken = false;
                List<int> usedChain = new List<int>();

                // one polygon per loop
                for (int i = 0 ; i < chains.Count ; i++) {

                    if ( usedChain.Contains(i) )
                        continue;

                    usedChain.Add(i);

                    if (attachedSides[i][0] == -1) {
                        // that's a ring (a whole polygon or a hole)
                        result.Add( new PolygonIndices(chains[i]) );
                    }
                    else {

                        // first chain
                        List<int> thisPoly    = new List<int>(chains[i]);
                        int[]     nowBothSide = new int[2] {attachedSides[i][0], attachedSides[i][1]};

                        // stretch clockwise
                        while (true) {
                            // Check if other chains attach to the same edge
                            int   minChainInd = -1;
                            float minDotValue = Mathf.Infinity;

                            Vector3 tailToNext = vertices[(nowBothSide[1] + 1) % vertices.Count] - newVerts[ thisPoly[thisPoly.Count - 1] ];

                            for (int j = 0 ; j < chains.Count ; j++) {

                                if ( usedChain.Contains(j) )
                                    continue;

                                if (attachedSides[j][0] == nowBothSide[1]) {

                                    Vector3 tailToThisHead = newVerts[ chains[j][0] ] - newVerts[ thisPoly[thisPoly.Count - 1] ];

                                    float dotValueJ = Vector3.Dot(tailToThisHead, tailToNext);

                                    if (dotValueJ > 0 && dotValueJ < minDotValue) {

                                        minChainInd = j;
                                        minDotValue = dotValueJ;
                                    }
                                }
                            }

                            // Check if is ending
                            if (nowBothSide[0] == nowBothSide[1]) {

                                Vector3 tailToHead = newVerts[ thisPoly[0] ] - newVerts[ thisPoly[thisPoly.Count - 1] ];
                                float   dotValue   = Vector3.Dot(tailToHead, tailToNext);

                                if (dotValue > 0 && (minChainInd == -1 || dotValue < minDotValue) ) {
                                    // end this polygon
                                    result.Add( new PolygonIndices(thisPoly) );
                                    break;
                                }
                            }

                            // add a vertex
                            if (minChainInd == -1) {
                                // no other chains attach to this edge
                                nowBothSide[1] = (nowBothSide[1] + 1) % vertices.Count;

                                newVerts.Add(vertices[ nowBothSide[1] ]);

                                thisPoly.Add(newVerts.Count - 1);
                            }
                            else {

                                for (int j = 0 ; j < chains[minChainInd].Count ; j++)
                                    thisPoly.Add(chains[minChainInd][j]);

                                nowBothSide[1] = attachedSides[minChainInd][1];
                                usedChain.Add(minChainInd);
                            }
                        }

                        originIsBroken = true;

                    }

                }

                if (!originIsBroken) {
                    // check if the outest ring is a whole polygon or a hole
                    int[] maxInds = {0, 0};
                    float maxSqrDist = 0f;

                    for (int i = 0 ; i < result.Count ; i++) {
                        for (int j = 0 ; j < result[i].inds.Count ; j++) {

                            float sqrDistJ = (newVerts[ result[i].inds[j] ] - newVerts[ result[0].inds[0] ]).sqrMagnitude;

                            if (sqrDistJ > maxSqrDist) {

                                maxInds = new int[2] {i, j};
                                maxSqrDist = sqrDistJ;
                            }
                        }

                    }

                    Vector3 normOfFarest  = newNorms[ result[maxInds[0]].inds[maxInds[1]] ];
                    Vector3 firstToFarest = newVerts[ result[maxInds[0]].inds[maxInds[1]] ] - newVerts[ result[0].inds[0] ];

                    if (Vector3.Dot(normOfFarest, firstToFarest) < 0) {
                        // it's a hole
                        List<int> originalPoly = new List<int>();

                        for (int i = 0 ; i < vertices.Count ; i ++) {

                            newVerts.Add(vertices[i]);
                            originalPoly.Add(newVerts.Count - 1);
                        }

                        result.Add( new PolygonIndices(originalPoly) );
                    }
                }

                return result;
            }

        }

    }

}
