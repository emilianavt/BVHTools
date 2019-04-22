using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BVHAnimationLoader))]
public class BVHAnimationLoaderEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        BVHAnimationLoader bvhLoader = (BVHAnimationLoader)target;

        if (GUILayout.Button("Load animation")) {
            bvhLoader.parseFile();
            bvhLoader.loadAnimation();
            Debug.Log("Loading animation done.");
        }

        if (GUILayout.Button("Play animation")) {
            bvhLoader.playAnimation();
            Debug.Log("Playing animation.");
        }

        if (GUILayout.Button("Stop animation")) {
            Debug.Log("Stopping animation.");
            bvhLoader.stopAnimation();
        }

        if (GUILayout.Button("Initialize renaming map with humanoid bone names")) {
            HumanBodyBones[] bones = (HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones));
            bvhLoader.boneRenamingMap = new BVHAnimationLoader.FakeDictionary[bones.Length - 1];
            for (int i = 0; i < bones.Length - 1; i++) {
                if (bones[i] != HumanBodyBones.LastBone) {
                    bvhLoader.boneRenamingMap[i].bvhName = "";
                    bvhLoader.boneRenamingMap[i].targetName = bones[i].ToString();
                }
            }
        }
    }
}
