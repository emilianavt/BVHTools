using System.IO;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BVHAnimationLoader))]
public class BVHAnimationLoaderEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        BVHAnimationLoader bvhLoader = (BVHAnimationLoader)target;

        if (GUILayout.Button("Load animation")) {
            bvhLoader.loadAnimation(File.ReadAllText(bvhLoader.bvhFile));
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
    }
}
