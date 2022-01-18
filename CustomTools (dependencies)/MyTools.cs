using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomToolFunctions {

    public static class MyTools {

        public static int GetIndexByValue (bool[] arr, bool value) {
            for (int i = 0; i < arr.Length; i++) {
                if (arr[i] == value) return i;
            }
            return -1;
        }

        public static int GetIndexByValue (List<bool> list, bool value) {
            for (int i = 0; i < list.Count; i++) {
                if (list[i] == value) return i;
            }
            return -1;
        }

        public static int GetIndexByValue (int[] arr, int value) {
            for (int i = 0; i < arr.Length; i++) {
                if (arr[i] == value) return i;
            }
            return -1;
        }

        public static int GetIndexByValue (List<int> list, int value) {
            for (int i = 0; i < list.Count; i++) {
                if (list[i] == value) return i;
            }
            return -1;
        }

        public static int GetIndexByValue (Vector3[] arr, Vector3 value) {
            for (int i = 0; i < arr.Length; i++) {
                if (arr[i] == value) return i;
            }
            return -1;
        }

        public static int GetIndexByValue (List<Vector3> list, Vector3 value) {
            for (int i = 0; i < list.Count; i++) {
                if (list[i] == value) return i;
            }
            return -1;
        }

        public static int[] GetAllIndexByValue (int[] arr, int value) {
            List<int> resultList = new List<int>();
            for (int i = 0; i < arr.Length; i++) {
                if (arr[i] == value) {
                    resultList.Add(i);
                }
            }
            return resultList.ToArray();
        }

        public static int[] GetAllIndexByValue (List<int> list, int value) {
            List<int> resultList = new List<int>();
            for (int i = 0; i < list.Count; i++) {
                if (list[i] == value) {
                    resultList.Add(i);
                }
            }
            return resultList.ToArray();
        }

    }

}
