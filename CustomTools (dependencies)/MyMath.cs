using UnityEngine;

namespace CustomMathMethods {

    public static class MyMath {


        public static int Mod (int n, int m) {
            return ((n % m) + m) % m;
        }

        public static int NegMod (int m, int n) {
            return ((m % n) - n) % n;
        }

        public static void LimitInRange (ref int value, int min, int max) {

            if (value < min) value = min;
            if (value > max) value = max;
        }

        public static void LimitInRange (ref float value, float min, float max) {

            if (value < min) value = min;
            if (value > max) value = max;
        }

        public static Vector3 GetNormalOfClockwiseTriagle (Vector3[] triVerts) {

            return GetNormalOfClockwiseTriagle (triVerts[0], triVerts[1], triVerts[2]);
        }

        public static Vector3 GetNormalOfClockwiseTriagle (Vector3 v0, Vector3 v1, Vector3 v2) {

            return Vector3.Cross(v1 - v0, v2 - v0).normalized;
        }

        public static Vector3 GetFromPToEdge (Vector3 p, Vector3 edgeP1, Vector3 edgeP2, out float toTheOtherDirDotValue) {

            Vector3 edgeDir = (edgeP2 - edgeP1).normalized;
            Vector3 edgeP1ToP = p - edgeP1;
            float dotValue = Vector3.Dot(edgeP1ToP, edgeDir);

            bool tooSmall = dotValue < 0;
            bool tooLarge = Mathf.Pow(dotValue, 2) > (edgeP2 - edgeP1).sqrMagnitude;

            if (tooSmall) {

                toTheOtherDirDotValue = Vector3.Dot(edgeDir, edgeP1 - p);
                return edgeP1 - p;
            }
            else if (tooLarge) {

                toTheOtherDirDotValue = Vector3.Dot(-edgeDir, edgeP2 - p);
                return edgeP2 - p;
            }
            else {

                toTheOtherDirDotValue = 0f;
                return dotValue * edgeDir - edgeP1ToP;
            }
        }



    }   // End of the Class

}
