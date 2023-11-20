using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SFramework.Threading.Tasks.Editor
{
    internal static class SplitterGUILayout
    {
        private static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static Lazy<Type> splitterStateType = new Lazy<Type>(() =>
        {
            Type type = typeof(EditorWindow).Assembly.GetTypes().First(x => x.FullName == "UnityEditor.SplitterState");
            return type;
        });

        private static Lazy<ConstructorInfo> splitterStateCtor = new Lazy<ConstructorInfo>(() =>
        {
            Type type = splitterStateType.Value;
            return type.GetConstructor(flags, null, new Type[] { typeof(float[]), typeof(int[]), typeof(int[]) }, null);
        });

        private static Lazy<Type> splitterGUILayoutType = new Lazy<Type>(() =>
        {
            Type type = typeof(EditorWindow).Assembly.GetTypes().First(x => x.FullName == "UnityEditor.SplitterGUILayout");
            return type;
        });

        private static Lazy<MethodInfo> beginVerticalSplit = new Lazy<MethodInfo>(() =>
        {
            Type type = splitterGUILayoutType.Value;
            return type.GetMethod("BegingVerticalSplit", flags, null, new Type[] { splitterStateType.Value, typeof(GUILayoutOption[]) }, null);
        });

        private static Lazy<MethodInfo> endVerticalSplit = new Lazy<MethodInfo>(() =>
        {
            Type type = splitterStateType.Value;
            return type.GetMethod("EndVerticalSplit", flags, null, Type.EmptyTypes, null);
        });

        public static object CreateSplitterState(float[] relativeSizes, int[] minSizes, int[] maxSizes)
        {
            return splitterStateCtor.Value.Invoke(new object[] { relativeSizes, minSizes, maxSizes });
        }

        public static void BeginVerticalSplit(object splitterState, params GUILayoutOption[] options)
        {
            beginVerticalSplit.Value.Invoke(null, new object[] { splitterState, options });
        }

        public static void EndVerticalSplit()
        {
            endVerticalSplit.Value.Invoke(null, Type.EmptyTypes);
        }
    }
}