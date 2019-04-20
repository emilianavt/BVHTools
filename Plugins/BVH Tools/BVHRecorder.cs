using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// Something about the end result in blender is still messed up. Lowering my arms is fine, but raising them doesn't work??

public class BVHRecorder : MonoBehaviour {
    [Header("Recorder settings")]
    [Tooltip("The bone rotations will be recorded every frame time milliseconds. Bone locations are recorded when this script starts running or genHierarchy() is called.")]
    public float frameTime = 1000.0f / 60.0f;
    [Tooltip("This is the filename to which the BVH file will be saved. If no filename is given, a random one will be generated when the script starts running.")]
    public string filename;
    [Tooltip("When this option is set, the BVH file will have the Z axis as up and the Y axis as forward instead of the normal BVH conventions.")]
    public bool blender = true;
    [Tooltip("When this box is checked, motion data will be recorded. It is possible to uncheck and check this box to pause and resume the capturing process.")]
    public bool capturing = false;
    [Header("Advanced settings")]
    [Tooltip("When this option is enabled, only humanoid bones will be targeted for detecting bones. This means that things like hair bones will not be added to the list of bones when detecting bones.")]
    public bool enforceHumanoidBones = false;
    [Tooltip("This option can be used to rename humanoid bones to standard bone names. If you don't know what this means, just leave it unticked.")]
    public bool renameBones = false;
    [Tooltip("When this is enabled, after a drop in frame rate, multiple frames may be recorded in quick succession. When it is disabled, at least frame time milliseconds will pass before the next frame is recorded. Enabling it will help ensure that your recorded clip has the correct duration.")]
    public bool catchUp = true;
    [Tooltip("Coordinates are recorded with six decimals. If you require a BVH file where only two decimals are required, you can turn this on.")]
    public bool lowPrecision = false;
    [Tooltip("This should be checked when BVHRecorder is used through its API. It will disable its Start() and Update() functions. If you don't know what this means, just leave it unticked.")]
    public bool scripted = false;

    [Header("Motion target")]
    [Tooltip("This is the avatar for which motion should be captured. All skinned meshes that are part of the avatar should be children of this object. All bones should be initialized with zero rotations. This is usually the case for VRM avatars.")]
    public Animator targetAvatar = null;
    [Tooltip("This is the root bone for the avatar, usually the hips. If this is not set, it will be detected automatically.")]
    public Transform rootBone = null;
    [Tooltip("This list contains all the bones for which motion will be recorded. If nothing is assigned, it will be automatically generated when the script starts. When manually setting up an avatar the Unity Editor, you can press the corresponding button at the bottom of this component to automatically populate the list and add or remove bones manually if necessary.")]
    public List<Transform> bones;

    private SkelTree skel = null;
    private List<SkelTree> boneOrder = null;
    private string hierarchy;
    private float lastFrame;
    private bool first = false;
    private List<string> frames = null;
    private Dictionary<Transform, string> boneMap;
    

    class SkelTree {
        public String name;
        public Transform transform;
        public List<SkelTree> children;
        
        public SkelTree(Transform bone, Dictionary<Transform, string> boneMap) {
            name = bone.gameObject.name;
            if (boneMap != null) {
                if (boneMap.ContainsKey(bone)) {
                    name = boneMap[bone];
                } else if (boneMap.ContainsValue(name)) {
                    //Debug.LogWarning("A non-humanoid bone was added to the skeleton: " + name);
                    name = name + "_";
                }
            }
            if (bone.localRotation.eulerAngles.x < 0 || bone.localRotation.eulerAngles.x > 0 || bone.localRotation.eulerAngles.y < 0 || bone.localRotation.eulerAngles.y > 0 || bone.localRotation.eulerAngles.z < 0 || bone.localRotation.eulerAngles.z > 0) {
                //throw new InvalidOperationException("Bone " + bone.name + " with non-zero rotation not supported.");
            }
            transform = bone;
            children = new List<SkelTree>();
        }
    }

    public static void populateBoneMap(out Dictionary<Transform, string> boneMap, Animator targetAvatar) {
        if (!targetAvatar.avatar.isHuman) {
            throw new InvalidOperationException("Enforce humanoid bones and rename bones can only be used with humanoid avatars.");
        }

        Dictionary<string, int> usedNames = new Dictionary<string, int>();
        RuntimeAnimatorController rac = targetAvatar.runtimeAnimatorController;
        targetAvatar.runtimeAnimatorController = null;
        boneMap = new Dictionary<Transform, string>();
        HumanBodyBones[] bones = (HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones));
        foreach (HumanBodyBones bone in bones) {
            if (bone < 0 || bone >= HumanBodyBones.LastBone) {
                continue;
            }
            Transform bodyBone = targetAvatar.GetBoneTransform(bone);
            if (bodyBone != null && bone != HumanBodyBones.LastBone) {
                if (usedNames.ContainsKey(bone.ToString())) {
                    throw new InvalidOperationException("Multiple bones were assigned to the same standard bone name.");
                } else {
                    boneMap.Add(bodyBone, bone.ToString());
                    usedNames.Add(bone.ToString(), 1);
                }
            }
        }
        targetAvatar.runtimeAnimatorController = rac;
    }

    public static Transform getRootBone (Animator avatar) {;
        List<Component> meshes = new List<Component>(avatar.GetComponents<SkinnedMeshRenderer>());
        meshes.AddRange(avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));

        Transform root = null;
        foreach (SkinnedMeshRenderer smr in meshes) {
            if (root == null && smr.bones.Length > 0) {
                root = smr.bones[0];
            }
            foreach (Transform bone in smr.bones) {
                if (root.IsChildOf(bone) && bone != root) {
                    root = bone;
                }
            }
        }

        return root;
    }

    // This function tries to find all Transforms that are bones of the character
    public void getBones() {
        if (targetAvatar == null) {
            throw new InvalidOperationException("Target avatar has to be set before calling getBones().");
        }

        if (enforceHumanoidBones) {
            populateBoneMap(out boneMap, targetAvatar);
        }

        rootBone = getRootBone(targetAvatar);
        if (rootBone == null) {
            throw new InvalidOperationException("No root bone found.");
        }

        List<Component> meshes = new List<Component>(targetAvatar.GetComponents<SkinnedMeshRenderer>());
        meshes.AddRange(targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        
        HashSet<Transform> boneSet = new HashSet<Transform>();
        
        foreach (SkinnedMeshRenderer smr in meshes) {
            foreach (Transform bone in smr.bones) {
                if (bone.IsChildOf(rootBone) && bone != rootBone) {
                    if (enforceHumanoidBones) {
                        if (boneMap.ContainsKey(bone)) {
                            boneSet.Add(bone);
                        }
                    } else {
                        boneSet.Add(bone);
                    }
                }
            }
        }
        
        bones = boneSet.OrderBy(bone => bone.name).ToList();
    }

    // This function removes empty entries from the bone list, in case the user deleted some that were misdetected
    public void cleanupBones() {
        List<Transform> clean = new List<Transform>();
        for (int i = 0; i < bones.Count; i++) {
            if (bones[i] != null) {
                clean.Add(bones[i]);
            }
        }
        bones = clean;
    }

    // This returns a queue of all child Transforms of a Transform
    private static Queue<Transform> getChildren(Transform parent) {
        Queue<Transform> children = new Queue<Transform>();
        for (int i = 0; i < parent.childCount; i++) {
            children.Enqueue(parent.GetChild(i));
        }
        return children;
    }

    // This checks if any bones from the boneSet are below the given bone. If the bone is part of the boneSet it is removed from the set.
    private static bool hasBone(HashSet<Transform> boneSet, Transform bone) {
        bool result = false;
        foreach (Transform other in boneSet) {
            if (bone == other) {
                boneSet.Remove(bone);
                return true;
            } else {
                if (other.IsChildOf(bone)) {
                    result = true;
                }
            }
        }
        return result;
    }
    
    // This builds a minimal tree covering all detected bones that will be used to generate the hierarchy section of the BVH file
    public void buildSkeleton() {
        rootBone = getRootBone(targetAvatar);
        if (rootBone == null) {
            throw new InvalidOperationException("No root bone found.");
        }

        cleanupBones();
        if (bones.Count == 0 || rootBone == null) {
            throw new InvalidOperationException("Target avatar, root bone and the bones list have to be set before calling buildSkeleton(). You can initialize bones list by calling getBones().");
        }

        if (enforceHumanoidBones) {
            populateBoneMap(out boneMap, targetAvatar);
        } else {
            boneMap = null;
        }

        HashSet<Transform> boneSet = new HashSet<Transform>(bones);
        skel = new SkelTree(rootBone, boneMap);

        Queue<SkelTree> queue = new Queue<SkelTree>();
        queue.Enqueue(skel);

        while (queue.Any()) {
            SkelTree bone = queue.Dequeue();
            Queue<Transform> children = getChildren(bone.transform);
            foreach (Transform child in children) {
                if (hasBone(boneSet, child)) {
                    SkelTree childBone = new SkelTree(child, boneMap);
                    queue.Enqueue(childBone);
                    bone.children.Add(childBone);
                }
            }
        }
    }

    // This adds tabs according to the level of indentation
    private static string tabs(int n) {
        string tabs = "";
        for (int i = 0; i < n; i++) {
            tabs += "\t";
        }
        return tabs;
    }

    // This formats local translation vectors
    private string getOffset(Vector3 offset) {
        Vector3 offset2 = new Vector3(-offset.x, offset.y, offset.z);
        if (blender) {
            offset2 = new Vector3(-offset.x, -offset.z, offset.y);
        }
        if (lowPrecision) {
            return string.Format(CultureInfo.InvariantCulture, "{0: 0.00;-0.00}\t{1: 0.00;-0.00}\t{2: 0.00;-0.00}", offset2.x, offset2.y, offset2.z);
        } else {
            return string.Format(CultureInfo.InvariantCulture, "{0: 0.000000;-0.000000}\t{1: 0.000000;-0.000000}\t{2: 0.000000;-0.000000}", offset2.x, offset2.y, offset2.z);
        }
    }

    // From: http://bediyap.com/programming/convert-quaternion-to-euler-rotations/
    Vector3 manualEuler(float a, float b, float c, float d, float e) {
        Vector3 euler = new Vector3();
        euler.z = Mathf.Atan2(a, b) * Mathf.Rad2Deg; // Z
        euler.x = Mathf.Asin(Mathf.Clamp(c, -1f, 1f)) * Mathf.Rad2Deg;     // Y
        euler.y = Mathf.Atan2(d, e) * Mathf.Rad2Deg; // X
        return euler;
    }

    // Unity to BVH
    Vector3 eulerZXY(Vector4 q) {
        return manualEuler(-2 * (q.x * q.y - q.w * q.z),
                      q.w * q.w - q.x * q.x + q.y * q.y - q.z * q.z,
                      2 * (q.y * q.z + q.w * q.x),
                     -2 * (q.x * q.z - q.w * q.y),
                      q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z); // ZXY
    }


    private string getRotation(Quaternion rot) {
        Vector4 rot2 = new Vector4(rot.x, -rot.y, -rot.z, rot.w).normalized;
        if (blender) {
            rot2 = new Vector4(rot.x, rot.z, -rot.y, rot.w).normalized;
        }
        Vector3 angles = eulerZXY(rot2);
        // This does convert to XZY order, but it should be ZXY?

        if (lowPrecision) {
            return string.Format(CultureInfo.InvariantCulture, "{0: 0.00;-0.00}\t{1: 0.00;-0.00}\t{2: 0.00;-0.00}", wrapAngle(angles.z), wrapAngle(angles.x), wrapAngle(angles.y));
        } else {
            return string.Format(CultureInfo.InvariantCulture, "{0: 0.000000;-0.000000}\t{1: 0.000000;-0.000000}\t{2: 0.000000;-0.000000}", wrapAngle(angles.z), wrapAngle(angles.x), wrapAngle(angles.y));
        }
    }

    // Angels should be -180 to 180
    private float wrapAngle(float a) {
        if (a > 180f) {
            return a - 360f;
        }
        if (a < -180f) {
            return 360f + a;
        }
        return a;
    }

    // This function recursively generates JOINT entries for the hierarchy section of the BVH file
    private string genJoint(int level, SkelTree bone) {
        // I thought I'd pretend to be a bone with zero rotation, but it did nothing.
        Quaternion rot = bone.transform.localRotation;
        bone.transform.localRotation = Quaternion.identity;

        string result = tabs(level) + "JOINT " + bone.name + "\n" + tabs(level) + "{\n" + tabs(level) + "\tOFFSET\t" + getOffset(bone.transform.position - bone.transform.parent.position) + "\n" + tabs(level) + "\tCHANNELS 3 Zrotation Xrotation Yrotation\n";
        boneOrder.Add(bone);

        if (bone.children.Any()) {
            foreach (SkelTree child in bone.children) {
                result += genJoint(level + 1, child);
            }
        } else {
            // I don't really know what to put here. UniVRM's importer ignores this node type anyway. Blender doesn't and uses it for the bone tails.
            result += tabs(level + 1) + "End Site\n" + tabs(level + 1) + "{\n" + tabs(level + 1) + "\tOFFSET\t" + getOffset(bone.transform.position - bone.transform.parent.position) + "\n" + tabs(level + 1) + "}\n";
        }

        result += tabs(level) + "}\n";
        bone.transform.localRotation = rot;

        return result;
    }

    // This function generates the hierarchy section of the BVH file
    public void genHierarchy() {
        if (skel == null) {
            throw new InvalidOperationException("Skeleton not initialized. You can initialize the skeleton by calling buildSkeleton().");
        }

        Quaternion rot = skel.transform.localRotation;
        skel.transform.localRotation = Quaternion.identity;
        boneOrder = new List<SkelTree>() { skel };
        hierarchy = "HIERARCHY\nROOT " + skel.name + "\n{\n\tOFFSET\t0.00\t0.00\t0.00\n\tCHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation\n";

        if (skel.children.Any()) {
            foreach (SkelTree child in skel.children) {
                hierarchy += genJoint(1, child);
            }
        } else {
            // I don't really know what to put here. UniVRM's importer ignores this node type anyway. Blender doesn't and uses it for the bone tails.
            hierarchy += "\tEnd Site\n\t{\n\t\tOFFSET\t1.0\t0.0\t0.0\n\t}\n";
        }

        hierarchy += "}\n";
        skel.transform.localRotation = rot;

        frames = new List<string>();
        lastFrame = Time.time;
        first = true;
    }

    // This function stores the current frame's bone positions as a string
    public void captureFrame() {
        if (frames == null || hierarchy == "") {
            throw new InvalidOperationException("Hierarchy not initialized. You can initialize the hierarchy by calling genHierarchy().");
        }

        string frame = getOffset(rootBone.localPosition);
        foreach (SkelTree bone in boneOrder) {
            frame += "\t";
            frame += getRotation(bone.transform.localRotation);
        }
        frame += "\n";
        frames.Add(frame);
    }

    // Just what it says
    public void clearCapture() {
        frames.Clear();
    }

    // This file attaches frame data to the hierarchy section
    public string genBVH() {
        if (frames == null || hierarchy == "") {
            throw new InvalidOperationException("Hierarchy not initialized. You can initialize the hierarchy by calling genHierarchy().");
        }

        string bvh = hierarchy;
        bvh += "MOTION\nFrames:    " + frames.Count + "\nFrame Time: " + string.Format(CultureInfo.InvariantCulture, "{0}", frameTime / 1000f) + "\n";

        foreach (string frame in frames) {
            bvh += frame;
        }

        return bvh;
    }

    // This saves  the full BVH file to the filename set in the component
    public void saveBVH() {
        if (frames == null || hierarchy == "") {
            throw new InvalidOperationException("Hierarchy not initialized. You can initialize the hierarchy by calling genHierarchy().");
        }

        File.WriteAllText(filename, genBVH());
    }

    void Start () {
        if (scripted) {
            return;
        }

        if (bones.Count == 0) {
            getBones();
        }
        buildSkeleton();
        genHierarchy();

        if (filename == "") {
            // If no filename is set, make one up
            filename = Application.persistentDataPath + "/" + System.Guid.NewGuid() + ".bvh";
        }
	}
	
	void LateUpdate () {
        if (scripted || frames == null || hierarchy == "" || !capturing) {
            lastFrame = Time.time;
            first = true;
            return;
        }
        if (first || 1000 * lastFrame + frameTime < 1000 * Time.time) {
            if (catchUp) {
                lastFrame = lastFrame + frameTime / 1000f;
            } else {
                lastFrame = Time.time;
            }
            captureFrame();
            first = false;
            if (frames.Count == 582) { capturing = false; }
            //capturing = false;
        }
	}
}
