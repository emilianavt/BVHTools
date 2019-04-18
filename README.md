BVH Tools for Unity/VRM
=======================

This package provides a component to record motion data of VRM avatars in BVH
format files. A second component allows loading such files as (legacy)
animation clips back into Unity.

## Requirements

The animation loader depends on the Bvh.cs file from UniVRM, so make sure you
have [UniVRM](https://github.com/dwango/UniVRM) in your Unity project.

## Recording

The most simple way to get started is to attach the "BVH Recorder" component
to a VRM avatar. Set the "Target Avatar" field to refer to the avatar, and set
a filename and path for the BVH file. Then play the scene and check the
"Capturing" box. You can uncheck and check it as you like. Captured motion data
will be added at the end. Once you are happy with your motion data, press the
save button in the inspector panel.

All fields have tooltips, so if you want to delve deeper, please take a look at
them. The component also provides a simple API. Looking at the corresponding
Editor script should give something of an overview.

## Editing

You can load and edit your BVH file in Blender. When importing it, please use
the following settings:

    Forward: -Y Forward
    Up: Z Up
    Rotation: Euler (YXZ)

## Loading

To load the file back into Unity, attach the "BVH Animation Loader" component
to your avatar, set it as the "Target Avatar" and enter the filename. Also
check the "Auto Start" box and then play the scene. Your animation should play.
