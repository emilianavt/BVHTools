using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniHumanoid;
using System.IO;
using System;

public class BVHAnimationLoader : MonoBehaviour {
    [Header("Loader settings")]
    [Tooltip("This is the target avatar for which the animation should be loaded. Bone names should be identical to those in the BVH file and unique. All bones should be initialized with zero rotations. This is usually the case for VRM avatars.")]
    public Animator targetAvatar;
    //[Tooltip("This is the root bone of the target avatar, usually the hips.")]
    //public Transform rootBone;
    [Tooltip("This is the path to the BVH file that should be loaded. Bone offsets are currently being ignored by this loader.")]
    public string bvhFile;
    [Tooltip("The frame time is the number of milliseconds per frame.")]
    public float frameTime = 1000f/60f;
    [Header("Animation settings")]
    [Tooltip("This is the Animation component to which the clip will be added. If left empty, a new Animation component will be added to the target avatar.")]
    public Animation anim;
    [Tooltip("This field can be used to read out the the animation clip after being loaded. A new clip will always be created when loading.")]
    public AnimationClip clip;
    [Tooltip("When this option is set, the animation start playing automatically after being loaded.")]
    public bool autoPlay = false;
    [Tooltip("When this option is set, the animation will be loaded and start playing as soon as the script starts running. This also implies the option above being enabled.")]
    public bool autoStart = false;

    private Transform rootBone;
    private string prefix;
    private Bvh bvh;
    private int frames;
    private Dictionary<string, string> pathToBone;
    private Dictionary<string, string[]> boneToMuscles;

    // BVH to Unity
    Quaternion fromEulerYXZ(Vector3 euler) {
        return Quaternion.AngleAxis(euler.y, Vector3.up) * Quaternion.AngleAxis(euler.x, Vector3.right) * Quaternion.AngleAxis(euler.z, Vector3.forward);
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

    private void getCurves(string path, BvhNode node) {
        bool posX = false;
        bool posY = false;
        bool posZ = false;
        bool rotX = false;
        bool rotY = false;
        bool rotZ = false;
        
        Keyframe[][] keyframes = new Keyframe[6][];
        string[] props = new string[6];

        if (path != prefix) {
            path += "/";
        }
        path += node.Name;

        // This needs to be changed to gather from all channels into two vector3, invert the coordinate system transformation and then make keyframes from it
        foreach (Channel channel in node.Channels) {
            ChannelCurve nodeChannel = bvh.GetChannel(node, channel);
            if (nodeChannel.Keys.Length != frames) {
                continue;
            }

            int k = -1;
            switch (channel) {
                case Channel.Xposition:
                    posX = true;
                    k = 0;
                    break;
                case Channel.Yposition:
                    posY = true;
                    k = 1;
                    break;
                case Channel.Zposition:
                    posZ = true;
                    k = 2;
                    break;
                case Channel.Xrotation:
                    rotX = true;
                    k = 3;
                    break;
                case Channel.Yrotation:
                    rotY = true;
                    k = 4;
                    break;
                case Channel.Zrotation:
                    rotZ = true;
                    k = 5;
                    break;
            }
            if (k == -1) {
                continue;
            }
            props[k] = ChannelExtensions.ToProperty(channel);

            int i = 0;
            float time = 0;
            keyframes[k] = new Keyframe[frames];
            foreach (float keyValue in nodeChannel.Keys) {
                keyframes[k][i++] = new Keyframe(time, keyValue);
                time += frameTime / 1000f;
            }
        }

        if (posX && posY && posZ) {
            for (int i = 0; i < frames; i++) {
                Vector3 posBVH = new Vector3(keyframes[0][i].value, keyframes[1][i].value, keyframes[2][i].value);
                Vector3 pos = new Vector3(-posBVH.y, posBVH.z, posBVH.x);
                keyframes[0][i].value = pos.x;
                keyframes[1][i].value = pos.y;
                keyframes[2][i].value = pos.z;
            }
            clip.SetCurve(path, typeof(Transform), props[0], new AnimationCurve(keyframes[0]));
            clip.SetCurve(path, typeof(Transform), props[1], new AnimationCurve(keyframes[1]));
            clip.SetCurve(path, typeof(Transform), props[2], new AnimationCurve(keyframes[2]));
        }

        if (rotX && rotY && rotZ) { 
            for (int i = 0; i < frames; i++) {
                Vector3 eulerBVH = new Vector3(keyframes[3][i].value, keyframes[4][i].value, keyframes[5][i].value);
                Quaternion rot = fromEulerYXZ(eulerBVH);
                Quaternion rot2 = new Quaternion(rot.y, -rot.z, -rot.x, rot.w);
                Vector3 euler = rot2.eulerAngles;
                keyframes[3][i].value = wrapAngle(euler.x);
                keyframes[4][i].value = wrapAngle(euler.y);
                keyframes[5][i].value = wrapAngle(euler.z);
            }
            clip.SetCurve(path, typeof(Transform), props[3], new AnimationCurve(keyframes[3]));
            clip.SetCurve(path, typeof(Transform), props[4], new AnimationCurve(keyframes[4]));
            clip.SetCurve(path, typeof(Transform), props[5], new AnimationCurve(keyframes[5]));
        }

        foreach (BvhNode child in node.Children) {
            getCurves(path, child);
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
        rootBone = BVHRecorder.getRootBone(targetAvatar);
        if (rootBone == null) {
            throw new InvalidOperationException("No root bone found.");
        }

        bvh = Bvh.Parse(bvhData);
        frames = bvh.FrameCount;
        clip = new AnimationClip();
        clip.name = "BVHClip";
        clip.legacy = true;
        prefix = getPathBetween(rootBone, targetAvatar.transform, true, true);

        getCurves(prefix, bvh.Root);
        if (anim == null) {
            anim = targetAvatar.gameObject.AddComponent<Animation>();
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
