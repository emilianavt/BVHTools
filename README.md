BVH Tools for Unity
===================

BVH Tools for Unity let you record and export motion data from avatars or
skeletons to BVH files so they can be edited with Blender or other programs.
The included animation loading component makes it possible import BVH files
into Unity at runtime.

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
