BVH Tools for Unity
===================

BVH Tools for Unity let you record and export motion data from avatars or
skeletons to BVH files so they can be edited with Blender or other programs.
The included animation loading component makes it possible import BVH files
into Unity at runtime.

## Setup

The BVHRecorder component should usually run last, so that it can capture any
modifications to bone rotations made by other scripts. To achieve this, after
importing the scripts, add the BVHRecorder component at the end of the script
execution order list. You can find this list under `Edit`, `Project Settings`,
`Script Execution Order`. Press the `+` button and select the `BVHRecorder`. 
It should appear at the end of the list. Finally click `Apply`.

## Recording

The most simple way to get started is to attach the "BVH Recorder" component
to an avatar. Set the "Target Avatar" field to refer to the avatar, and set
a filename and path for the BVH file. Then play the scene and check the
"Capturing" box. You can uncheck and check it as you like. Captured motion data
will be added at the end. Once you are happy with your motion data, press the
save button in the inspector panel.

All fields have tooltips, so if you want to delve deeper, please take a look at
them. The component also provides a simple API. Looking at the corresponding
Editor script should give something of an overview.

### Rest pose

If the bones of the model being recorded do not have rotations of zero in the
rest pose, the rest pose in the resulting BVH file can look very odd. As a
workaround, exporting the model to VRM format using
[UniVRM](https://github.com/vrm-c/UniVRM) and importing it back into unity will
produce a model with zero rotation bones. A short guide about the process can be
found
[here](https://gist.github.com/emilianavt/51d8399987d67544fdebfe2ebd9a5149).

## Editing

If you want to edit your file in Blender, please enable the "Blender" checkbox
for both recording and loading and use the following settings during import
into Blender:

    Forward: Y Forward
    Up: Z Up

When importing files into Blender that were recorded with Blender mode disabled,
please use the following settings instead:

    Forward: -Z Forward
    Up: Y Up

When loading files that were exported from Blender, the Blender checkbox always
has to be enabled.

## Loading

To load the file back into Unity, attach the "BVH Animation Loader" component
to your avatar, set it as the "Target Avatar" and enter the filename. Also
check the "Auto Start" box and then play the scene. Your animation should play.

## Video

You can watch a quick introduction video on the usage of these Unity components
[here](https://www.youtube.com/watch?v=DM7UZuAgBJk).

## License

This software is distributed under the terms of the MIT license.

## About

BVH Tools for Unity was made by [Emiliana](https://twitter.com/emiliana_vt)
for Virtual YouTuber purposes, but it can also be used for games or other
applications.

## API

If you want to control the import and export functionality from your own
scripts, you will find all the necessary information in this section.

### BVHRecorder

This component can be used to record BVH data and save it to a file.

#### `Animator targetAvatar` (required)

This is the only required field. Set the target avatar here.

#### `Transform rootBone`

This is usually the hips or pelvis bone. It is the root bone of the skeleton.
If not set, it will be automatically detected. However, setting it can be
useful to only capture motion for a part of a skeleton.

#### `List<Transform> bones`

This list contains the bones for which motion data will be recorded. It can be
filled automatically using the `getBones()` function.

#### `float frameRate`

This the how often bone rotations will be recorded every second, when the
`capturing` flag is set to true. The frame duration written to the BVH file is
also derived from this.

#### `string directory`

This is the directory into which BVH files are written. If left empty, it will
be initialized to the standard Unity persistant data path, unless the filename
field contains a slash or a backslash, in which case this field will be ignored
completely instead.

#### `string filename`

This field is used by the `saveBVH()` function and specifies the filename of
the BVH file it will create.  If no filename is given, a new one will be
generated based on a timestamp. If the file already exists, a number will be
appended.

#### `bool overwrite`

When this flag is set to true, files will be overwritten. No numbers will be
appended to filenames.

#### `bool scripted`

Setting this flag will prevent the automatic initialization of this
component at startup.

#### `bool capturing`

When this flag is set, motion will be captured at the interval given by
`frameRate`. It is possible to pause and resume capturing by setting this
flag at any time.

#### `bool blender`

This flag changes the coordinate system to match that of Blender instead of
the default BVH coordinate system. It is recommended to set this when files
are going to be edited in Blender and later loaded back into Unity. It is
also enabled by default.

#### `bool enforceHumanoidBones`

This makes `getBones()` only include humanoid bones in the bone list. It is
mainly useful to exclude things like hair and skirt bones from the recording,
which can make the resulting file a lot bigger.

#### `bool renameBones`

Enabling this option will rename humanoid bones to their standard Unity names
in the generated file.

#### `bool catchUp`

Setting this to true will allow capturing with the `capturing` flag to speed
up after if the frame rate drops, to keep the duration of the captured
animation correct. Disabling this flag will ensure that at least as many
milliseconds pass after every captured frame, as one frame should require,
according to the given `frameRate`.

#### `int frameNumber` (read-only)

This field shows how many frames are currently captured. Clearing the capture
will reset this to 0.

#### `string lastSavedFile` (read-only)

This field will be set to the filename written to by the `saveBVH()` function.

#### `void getBones()` (usually required)

Unless the list of bones is set manually, this function has to be called
to populate the list of bones that will be recorded.

#### `void cleanupBones()`

This function removes empty entries from the bones list. It is also called
by other functions, so calling it manually is usually not necessary.

#### `void buildSkeleton()` (required)

This function builds a spanning tree of game objects covering all selected
bones. It always has to be called before capturing and after setting the bones.

#### `void genHierarchy()` (required)

This function also has to be called before motion capture. It further
prepares hierarchy information about the skeleton built by the previous
function.

#### `void captureFrame()`

Call this function to record the target avatar's pose at the current frame.
Setting the `capturing` flag to true will automatically call this every frame.

#### `void clearCapture()`

Using this function, all previously capture motion data can be discarded.

#### `string genBVH()`

Calling this function will return a string containing a BVH file for the
captured motion data.

#### `void saveBVH()`

This function calls genBVH() and writes the string to the file specified by
the `filename` field.

#### `static Transform getRootBone (Animator avatar)`

This function can be used to detect the root bone of an avatar.

#### `static Transform getRootBone (Animator avatar, List<Transform> bones)`

This function can be used to detect the root bone of an avatar, given a set of
bones.

#### `static void populateBoneMap(out Dictionary<Transform, string> boneMap, Animator targetAvatar)`

This function can be used on humanoid avatars to generate a mapping from
bones to standard Unity humanoid bone names.

#### Example

Here is a short example of how you could start the capturing process:

    BVHRecorder recorder = gameObject.AddComponent<BVHRecorder>();
    recorder.targetAvatar = GetComponent<Animator>();
    recorder.scripted = true;
    recorder.getBones();
    recorder.buildSkeleton();
    recorder.genHierarchy();
    recorder.capturing = true;

Then, after you are done capturing your animation, you save it to a file:

    recorder.capturing = false;
    recorder.filename = "motion.bvh";
    recorder.saveBVH();

If you want to capture another animation, just clear out the collected data.
You can also skip this, if you want to add to your current animation.

    recorder.clearCapture();
    recorder.capturing = true;

And save it again:

    recorder.capturing = false;
    recorder.filename = "motion2.bvh";
    recorder.saveBVH();

### BVHAnimationLoader

This component allows the creation of (legacy) animation clips from BVH files.
The skeleton defined in the BVH file should match that of the avatar for
which it is being loaded.

#### `Animator targetAvatar` (required)

This field specifies the avatar to which the animation should be applied.

#### `string filename`

This field specifies the filename from which `parseFile()` will read the BVH
data.

#### `bool blender`

This flag has to be enabled to correctly load BVH files exported from Blender
or written by the `BVHRecorder` component with the `blender` flag set. When
Blender is part of the animation workflow, it is usually best to enable this
option in both `BVHRecorder` and `BVHAnimationLoader`. It is also enabled by
default.

#### `float frameRate`

When the flag below is set, this frame rate will override that derived from the
frame time given in the BVH file.

#### `bool respectBVHTime`

When this flag is set, the frame duration will not be overridden by the value
set in `frameRate`.

#### `string clipName`

This name will be given to the newly created `AnimationClip` when loading. If
this field is left empty, a name will be generated automatically.

#### `bool standardBoneNames`

When this option is enabled, standard Unity humanoid bone names will be mapped
to the corresponding bones of the skeleton.

#### `bool flexibleBoneName`

When this option is disabled, bone names have to match exactly.

#### `FakeDictionary[] boneRenamingMap`

If the bone names of the avatar do not match those in the file and the file
doesn't use Unity's standard bone names, but the structure matches otherwise,
you can define a mapping of names in this array to rename the bones to match.

    public struct FakeDictionary {
        public string bvhName;
        public string targetName;
    }

#### `bool autoPlay`

If this flag is enabled, animations will start playing as soon as they are
loaded.

#### `bool autoStart`

If this flag is enabled, an animation is loaded as soon as the script starts
running. This also enables the `autoPlay` flag.

#### `Animation anim`

Once an animation has been loaded, the `Animation` component to which it has
been added can be accessed through this field.

#### `AnimationClip clip`

This field contains the latest loaded animation clip.

#### `void parseFile()` or `void parse(string bvhData)` (required)

First, the BVH data has to be parsed. These functions do not call any Unity
API functions and can safely be called from another thread if some of the
animation loading process should be done in the background.

In the case of `parseFile()`, the BVH data will be loaded from the file
specified through the `filename` field.

#### `void loadAnimation()` (required)

This function turns the parsed BVH data into a (legacy) animation clip. If
the `autoPlay` flag is set, it will also start playing the animation right
away.

The loaded animation will be added to an `Animation` component on the target
avatar. Calling this function multiple times (e.g. with different parsed
files) will add multiple animations to the `Animation` component, which
can all played by accessing it. The name will be assigned from the `clipName`
field or assigned automatically if it is empty.

#### `void playAnimation()`

This function plays the animation that was loaded last.

#### `stopAnimation()`

This function stops animation playing through the `Animation` component.

#### `static string getPathBetween(Transform target, Transform root, bool skipFirst, bool skipLast)`

This function generates a string containing the path from one game object to
another. If the `skipFirst` flag is set, the first element of the path is
discarded. If the `skipLast` flag is set, the last element of the path is
discarded.

#### Example

It is possible to reuse this component to load multiple animations by calling
one of the parsing functions and this function in sequence multiple times.

For example:

    BVHAnimationLoader loader = gameObject.AddComponent<BVHAnimationLoader>();
    loader.targetAvatar = GetComponent<Animator>();
    loader.clipName = "anim1";
    loader.filename = "anim1.bvh";
    loader.parseFile();
    loader.loadAnimation();
    
    loader.clipName = "anim2";
    loader.filename = "anim2.bvh";
    loader.parseFile();
    loader.loadAnimation();
    
    loader.clipName = "anim3";
    loader.filename = "anim3.bvh";
    loader.parseFile();
    loader.loadAnimation();

The loaded animations can then be played through the `Animation` component:

    loader.anim.Play("anim2");
