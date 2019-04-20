using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class BVHAnimationLoader : MonoBehaviour {
    [Header("Loader settings")]
    [Tooltip("This is the target avatar for which the animation should be loaded. Bone names should be identical to those in the BVH file and unique. All bones should be initialized with zero rotations. This is usually the case for VRM avatars.")]
    public Animator targetAvatar;
    [Tooltip("This is the path to the BVH file that should be loaded. Bone offsets are currently being ignored by this loader.")]
    public string bvhFile;
    [Tooltip("When this option is set, the BVH file will be assumed to have the Z axis as up and the Y axis as forward instead of the normal BVH conventions.")]
    public bool blender = true;
    [Tooltip("The frame time is the number of milliseconds per frame.")]
    public float frameTime = 1000f / 60f;
    [Tooltip("This is the name that will be set on the animation clip. Leaving this empty is also okay.")]
    public string clipName;
    [Header("Advanced settings")]
    [Tooltip("When this option is enabled, standard Unity humanoid bone names will be mapped to the corresponding bones of the skeleton.")]
    public bool standardBoneNames = true;
    [Tooltip("When this option is disabled, bone names have to match exactly.")]
    public bool flexibleBoneNames = true;
    [Tooltip("This allows you to give a mapping from names in the BVH file to actual bone names. If standard bone names are enabled, the target names may also be Unity humanoid bone names. Entries with empty BVH names will be ignored.")]
    public FakeDictionary[] boneRenamingMap = null;
    [Header("Animation settings")]
    [Tooltip("When this option is set, the animation start playing automatically after being loaded.")]
    public bool autoPlay = false;
    [Tooltip("When this option is set, the animation will be loaded and start playing as soon as the script starts running. This also implies the option above being enabled.")]
    public bool autoStart = false;
    [Header("Animation")]
    [Tooltip("This is the Animation component to which the clip will be added. If left empty, a new Animation component will be added to the target avatar.")]
    public Animation anim;
    [Tooltip("This field can be used to read out the the animation clip after being loaded. A new clip will always be created when loading.")]
    public AnimationClip clip;

    static private int clipCount = 0;
    private Transform rootBone;
    private string prefix;
    private int frames;
    private Dictionary<string, string> pathToBone;
    private Dictionary<string, string[]> boneToMuscles;
    private Dictionary<string, Transform> nameMap;
    private Dictionary<string, string> renamingMap;

    [Serializable]
    public struct FakeDictionary {
        public string bvhName;
        public string targetName;
    }

    // BVH to Unity
    Quaternion fromEulerZXY(Vector3 euler) {
        return Quaternion.AngleAxis(euler.z, Vector3.forward) * Quaternion.AngleAxis(euler.x, Vector3.right) * Quaternion.AngleAxis(euler.y, Vector3.up);
    }

    private float wrapAngle(float a) {
        if (a > 180f) {
            return a - 360f;
        }
        if (a < -180f) {
            return 360f + a;
        }
        return a;
    }

    private string flexibleName(string name) {
        if (!flexibleBoneNames) {
            return name;
        }
        name = name.Replace(" ", "");
        name = name.Replace("_", "");
        name = name.ToLower();
        return name;
    }

    private Transform getBoneByName(string name, Transform transform, bool first) {
        if (nameMap == null) {
            if (standardBoneNames) {
                Dictionary<Transform, string> boneMap;
                BVHRecorder.populateBoneMap(out boneMap, targetAvatar);
                nameMap = boneMap.ToDictionary(kp => flexibleName(kp.Value), kp => kp.Key);
            } else {
                nameMap = new Dictionary<string, Transform>();
            }
        }
        string targetName = flexibleName(name);
        if (renamingMap.ContainsKey(targetName)) {
            targetName = renamingMap[targetName];
        }
        if (first) { 
            if (flexibleName(transform.name) == targetName) {
                return transform;
            }
            if (nameMap.ContainsKey(targetName) && nameMap[targetName] == transform) {
                return transform;
            }
        }
        foreach (Transform child in transform.GetChildren()) {
            if (flexibleName(child.name) == targetName) {
                return child;
            }
            if (nameMap.ContainsKey(targetName) && nameMap[targetName] == child) {
                return child;
            }
        }
        throw new InvalidOperationException("Could not find bone \"" + name + "\" under bone \"" + transform.name + "\".");
    }

    private void getCurves(string path, BVHParser.BVHBone node, Transform bone, bool first) {
        bool posX = false;
        bool posY = false;
        bool posZ = false;
        bool rotX = false;
        bool rotY = false;
        bool rotZ = false;

        float[][] values = new float[6][];
        Keyframe[][] keyframes = new Keyframe[7][];
        string[] props = new string[7];
        Transform nodeTransform = getBoneByName(node.name, bone, first);

        if (path != prefix) {
            path += "/";
        }
        if (rootBone != targetAvatar.transform || !first) {
            path += nodeTransform.name;
        }

        // This needs to be changed to gather from all channels into two vector3, invert the coordinate system transformation and then make keyframes from it
        for (int channel = 0; channel < 6; channel++) {
            if (!node.channels[channel].enabled) {
                continue;
            }

            switch (channel) {
                case 0:
                    posX = true;
                    props[channel] = "localPosition.x";
                    break;
                case 1:
                    posY = true;
                    props[channel] = "localPosition.y";
                    break;
                case 2:
                    posZ = true;
                    props[channel] = "localPosition.z";
                    break;
                case 3:
                    rotX = true;
                    props[channel] = "localEulerAnglesBaked.x";
                    props[channel] = "localRotation.x";
                    break;
                case 4:
                    rotY = true;
                    props[channel] = "localEulerAnglesBaked.y";
                    props[channel] = "localRotation.y";
                    break;
                case 5:
                    rotZ = true;
                    props[channel] = "localEulerAnglesBaked.z";
                    props[channel] = "localRotation.z";
                    break;
                default:
                    channel = -1;
                    break;
            }
            if (channel == -1) {
                continue;
            }

            keyframes[channel] = new Keyframe[frames];
            values[channel] = node.channels[channel].values;
            if (rotX && rotY && rotZ && keyframes[6] == null) {
                keyframes[6] = new Keyframe[frames];
                props[6] = "localRotation.w";
            }
        }

        float time = 0f;
        if (posX && posY && posZ) {
            for (int i = 0; i < frames; i++) {
                time += frameTime / 1000f;
                keyframes[0][i].time = time;
                keyframes[1][i].time = time;
                keyframes[2][i].time = time;
                if (blender) {
                    keyframes[0][i].value = -values[0][i];
                    keyframes[1][i].value = values[2][i];
                    keyframes[2][i].value = -values[1][i];
                } else {
                    keyframes[0][i].value = -values[0][i];
                    keyframes[1][i].value = values[1][i];
                    keyframes[2][i].value = values[2][i];
                }
            }
            clip.SetCurve(path, typeof(Transform), props[0], new AnimationCurve(keyframes[0]));
            clip.SetCurve(path, typeof(Transform), props[1], new AnimationCurve(keyframes[1]));
            clip.SetCurve(path, typeof(Transform), props[2], new AnimationCurve(keyframes[2]));
        }

        time = 0f;
        if (rotX && rotY && rotZ) { 
            for (int i = 0; i < frames; i++) {
                Vector3 eulerBVH = new Vector3(wrapAngle(values[3][i]), wrapAngle(values[4][i]), wrapAngle(values[5][i]));
                Quaternion rot = fromEulerZXY(eulerBVH);
                Quaternion rot2;
                if (blender) {
                    keyframes[3][i].value = rot.x;
                    keyframes[4][i].value = -rot.z;
                    keyframes[5][i].value = rot.y;
                    keyframes[6][i].value = rot.w;
                    //rot2 = new Quaternion(rot.x, -rot.z, rot.y, rot.w);
                } else {
                    keyframes[3][i].value = rot.x;
                    keyframes[4][i].value = -rot.y;
                    keyframes[5][i].value = -rot.z;
                    keyframes[6][i].value = rot.w;
                    //rot2 = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
                }
                /*Vector3 euler = rot2.eulerAngles;

                keyframes[3][i].value = wrapAngle(euler.x);
                keyframes[4][i].value = wrapAngle(euler.y);
                keyframes[5][i].value = wrapAngle(euler.z);*/

                time += frameTime / 1000f;
                keyframes[3][i].time = time;
                keyframes[4][i].time = time;
                keyframes[5][i].time = time;
                keyframes[6][i].time = time;
            }
            clip.SetCurve(path, typeof(Transform), props[3], new AnimationCurve(keyframes[3]));
            clip.SetCurve(path, typeof(Transform), props[4], new AnimationCurve(keyframes[4]));
            clip.SetCurve(path, typeof(Transform), props[5], new AnimationCurve(keyframes[5]));
            clip.SetCurve(path, typeof(Transform), props[6], new AnimationCurve(keyframes[6]));
        }

        foreach (BVHParser.BVHBone child in node.children) {
            getCurves(path, child, nodeTransform, false);
        }
    }

    public static string getPathBetween(Transform target, Transform root, bool skipFirst, bool skipLast) {
        if (root == target) {
            if (skipLast) {
                return "";
            } else {
                return root.name;
            }
        }

        for (int i = 0; i < root.childCount; i++) {
            Transform child = root.GetChild(i);
            if (target.IsChildOf(child)) {
                if (skipFirst) {
                    return getPathBetween(target, child, false, skipLast);
                } else {
                    return root.name + "/" + getPathBetween(target, child, false, skipLast);
                }
            }
        }

        throw new InvalidOperationException("No path between transforms " + target.name + " and " + root.name + " found.");
    }

	public void loadAnimation(string bvhData) {
        if (targetAvatar == null) {
            throw new InvalidOperationException("No target avatar set.");
        }

        BVHParser bp = new BVHParser(bvhData, frameTime);

        Queue<Transform> transforms = new Queue<Transform>();
        transforms.Enqueue(targetAvatar.transform);
        while (transforms.Any()) {
            Transform transform = transforms.Dequeue();
            if (flexibleName(transform.name) == flexibleName(bp.root.name)) {
                rootBone = transform;
                break;
            }
            foreach (Transform child in transform.GetChildren()) {
                transforms.Enqueue(child);
            }
        }
        if (rootBone == null) {
            rootBone = BVHRecorder.getRootBone(targetAvatar);
            Debug.LogWarning("Using \"" + rootBone.name + "\" as the root bone.");
        }
        if (rootBone == null) {
            throw new InvalidOperationException("No root bone \"" + bp.root.name + "\" found." );
        }

        renamingMap = new Dictionary<string, string>();
        foreach (FakeDictionary entry in boneRenamingMap) {
            if (entry.bvhName != "" && entry.targetName != "") {
                renamingMap.Add(flexibleName(entry.bvhName), flexibleName(entry.targetName));
            }
        }

        frames = bp.frames;
        clip = new AnimationClip();
        clip.name = "BVHClip (" + (clipCount++) + ")";
        if (clipName != "") {
            clip.name = clipName;
        }
        clip.legacy = true;
        prefix = getPathBetween(rootBone, targetAvatar.transform, true, true);

        getCurves(prefix, bp.root, rootBone, true);
        clip.EnsureQuaternionContinuity();
        if (anim == null) {
            anim = targetAvatar.gameObject.GetComponent<Animation>();
            if (anim == null) {
                anim = targetAvatar.gameObject.AddComponent<Animation>();
            }
        }
        anim.AddClip(clip, clip.name);
        anim.clip = clip;
        anim.playAutomatically = autoPlay;
        if (autoPlay) {
            anim.Play(clip.name);
        }
    }

    public void playAnimation() {
        if (clip == null) {
            loadAnimation(File.ReadAllText(bvhFile));
        }
        anim.Play(clip.name);
    }

    public void stopAnimation() {
        if (clip != null) {
            if (anim.IsPlaying(clip.name)) {
                anim.Stop();
            }
        }
    }

    void Start () {
        if (autoStart) {
            autoPlay = true;
            loadAnimation(File.ReadAllText(bvhFile));
        }
    }
}
