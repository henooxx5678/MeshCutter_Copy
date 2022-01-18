using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomMathMethods;

namespace MeshCutting {

    public class ChainPair {

        // ============= Properties ============= ///
        public MeshCutter.IndexOfPolygons          tipIndOfPolys;
        public List<MeshCutter.IndexOfPolygons>[]  sidesIndsOfPolys = new List<MeshCutter.IndexOfPolygons>[2];  //  0: left side , 1: right side
        public bool[]                              readyToMerge = {false, false};
        public int                                 lastestSide = 0;

        public List<MeshCutter.IndexOfPolygons> leftIndsOfPoly {
            get => sidesIndsOfPolys[0];
            set => sidesIndsOfPolys[0] = value;
        }

        public List<MeshCutter.IndexOfPolygons> rightIndsOfPoly {
            get => sidesIndsOfPolys[1];
            set => sidesIndsOfPolys[1] = value;
        }

        public MeshCutter.IndexOfPolygons LeftTop {
            get {
                if (leftIndsOfPoly.Count > 0)
                    return leftIndsOfPoly[0];
                else
                    return rightIndsOfPoly[0];
            }
        }

        public MeshCutter.IndexOfPolygons RightTop {
            get {
                if (rightIndsOfPoly.Count > 0)
                    return rightIndsOfPoly[0];
                else
                    return leftIndsOfPoly[0];
            }
        }

        public MeshCutter.IndexOfPolygons LeftLast {
            get {
                if (leftIndsOfPoly.Count > 0)
                    return leftIndsOfPoly[leftIndsOfPoly.Count - 1];
                else
                    return rightIndsOfPoly[0];
            }
        }

        public MeshCutter.IndexOfPolygons RightLast {
            get {
                if (rightIndsOfPoly.Count > 0)
                    return rightIndsOfPoly[rightIndsOfPoly.Count - 1];
                else
                    return leftIndsOfPoly[0];
            }
        }

        public bool ContainTip {
            get => sidesIndsOfPolys[0][0] == tipIndOfPolys;
        }

        public int PointCount {
            get => sidesIndsOfPolys[0].Count + sidesIndsOfPolys[1].Count;
        }

        /// =========== Constructor =========== ///
        public ChainPair (MeshCutter.IndexOfPolygons tipIndOfPolys) {

            this.tipIndOfPolys = tipIndOfPolys;

            sidesIndsOfPolys[0] = new List<MeshCutter.IndexOfPolygons>();
            sidesIndsOfPolys[1] = new List<MeshCutter.IndexOfPolygons>();

            sidesIndsOfPolys[0].Add(tipIndOfPolys);
            lastestSide = 0;
        }

        /// ============ Methods ============== ///
        public int GetLastIndOfSide (int side) {
            return sidesIndsOfPolys[side].Count - 1;
        }

        public MeshCutter.IndexOfPolygons GetLastOfSide (int side) {
            return side == 0 ? LeftLast : RightLast;
        }

        public int GetConnectedSide (MeshCutter.IndexOfPolygons indOfPolys, int polyVertsCount) {

            int result = -1;

            if (LeftLast.polyI == indOfPolys.polyI && MyMath.Mod(LeftLast.ind - indOfPolys.ind, polyVertsCount) == 1) {
                // at Left
                result = 0;
            }

            if (RightLast.polyI == indOfPolys.polyI && MyMath.NegMod(RightLast.ind - indOfPolys.ind, polyVertsCount) == -1) {
                // At Right
                if (result == 0) result = 2;  // End Point
                else             result = 1;
            }

            return result;
        }

        public void AddPoint (MeshCutter.IndexOfPolygons indOfPolys, int side) {

            sidesIndsOfPolys[side].Add(indOfPolys);
            lastestSide = side;
        }

        public ChainPair Split (MeshCutter.IndexOfPolygons thisPointIndOfPolys) {

            // Split along thisPoint to the lastest point of this ChainPair
            AddPoint(thisPointIndOfPolys, lastestSide);

            ChainPair newPair = new ChainPair( GetLastOfSide(lastestSide) );
            newPair.AddPoint(thisPointIndOfPolys, (lastestSide + 1) % 2);

            return newPair;
        }

        public static void Merge (ChainPair cpL, ChainPair cpR) {
            // Keep cpL left-side & cpR right-side, add tip-point to cpL, result save at cpL, delete cpR
            cpL.leftIndsOfPoly.Insert(0, cpL.RightLast);
            cpL.rightIndsOfPoly = cpR.rightIndsOfPoly;
            cpL.readyToMerge[1] = cpR.readyToMerge[1];
        }

    }

}
