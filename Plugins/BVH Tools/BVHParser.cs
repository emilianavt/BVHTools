using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

// This class uses no Unity data types and should be completely safe to use in another thread
public class BVHParser {
    public int frames = 0;
    public float frameTime = 1000f / 60f;
    public BVHBone root;
    private List<BVHBone> boneList;

    private float[][] channels;
    private string bvhText;
    private int pos = 0;

    public class BVHBone {
        public string name;
        public List<BVHBone> children;
        public float offsetX, offsetY, offsetZ;
        public int[] channelOrder;
        public int channelNumber;
        public BVHChannel[] channels;

        private BVHParser bp;

        // 0 = Xpos, 1 = Ypos, 2 = Zpos, 3 = Xrot, 4 = Yrot, 5 = Zrot
        public struct BVHChannel {
            public bool enabled;
            public float[] values;
        }

        public BVHBone(BVHParser parser, bool rootBone) {
            bp = parser;
            bp.boneList.Add(this);
            channels = new BVHChannel[6];
            channelOrder = new int[6] { 0, 1, 2, 5, 3, 4 };
            children = new List<BVHBone>();

            bp.skip();
            if (rootBone) {
                bp.assureExpect("ROOT");
            } else {
                bp.assureExpect("JOINT");
            }
            bp.assure("joint name", bp.getString(out name));
            bp.skip();
            bp.assureExpect("{");
            bp.skip();
            bp.assureExpect("OFFSET");
            bp.skip();
            bp.assure("offset X", bp.getFloat(out offsetX));
            bp.skip();
            bp.assure("offset Y", bp.getFloat(out offsetY));
            bp.skip();
            bp.assure("offset Z", bp.getFloat(out offsetZ));
            bp.skip();
            bp.assureExpect("CHANNELS");

            bp.skip();
            bp.assure("channel number", bp.getInt(out channelNumber));
            bp.assure("valid channel number", channelNumber >= 1 && channelNumber <= 6);

            for (int i = 0; i < channelNumber; i++) {
                bp.skip();
                int channelId;
                bp.assure("channel ID", bp.getChannel(out channelId));
                channelOrder[i] = channelId;
                channels[channelId].enabled = true;
            }

            char peek = ' ';
            do {
                float ignored;
                bp.skip();
                bp.assure("child joint", bp.peek(out peek));
                switch (peek) {
                    case 'J':
                        BVHBone child = new BVHBone(bp, false);
                        children.Add(child);
                        break;
                    case 'E':
                        bp.assureExpect("End Site");
                        bp.skip();
                        bp.assureExpect("{");
                        bp.skip();
                        bp.assureExpect("OFFSET");
                        bp.skip();
                        bp.assure("end site offset X", bp.getFloat(out ignored));
                        bp.skip();
                        bp.assure("end site offset Y", bp.getFloat(out ignored));
                        bp.skip();
                        bp.assure("end site offset Z", bp.getFloat(out ignored));
                        bp.skip();
                        bp.assureExpect("}");
                        break;
                    case '}':
                        bp.assureExpect("}");
                        break;
                    default:
                        bp.assure("child joint", false);
                        break;
                }
            } while (peek != '}');
        }
    }

    private bool peek(out char c) {
        c = ' ';
        if (pos >= bvhText.Length) {
            return false;
        }
        c = bvhText[pos];
        return true;
    }

    private bool expect(string text) {
        foreach (char c in text) {
            if (pos >= bvhText.Length || c != bvhText[pos++]) {
                return false;
            }
        }
        return true;
    }

    private bool getString(out string text) {
        text = "";
        while (pos < bvhText.Length && bvhText[pos] != '\n' && bvhText[pos] != '\r') {
            text += bvhText[pos++];
        }
        text = text.Trim();

        return (text.Length != 0);
    }

    private bool getChannel(out int channel) {
        channel = -1;
        if (pos + 1 >= bvhText.Length) {
            return false;
        }
        switch (bvhText[pos]) {
            case 'X':
                channel = 0;
                break;
            case 'Y':
                channel = 1;
                break;
            case 'Z':
                channel = 2;
                break;
            default:
                return false;
        }
        pos++;
        switch (bvhText[pos]) {
            case 'p':
                pos++;
                return expect("osition");
            case 'r':
                pos++;
                channel += 3;
                return expect("otation");
            default:
                return false;
        }
    }

    private bool getInt(out int v) {
        bool negate = false;
        bool digitFound = false;
        v = 0;

        // Read sign
        if (pos < bvhText.Length && bvhText[pos] == '-') {
            negate = true;
            pos++;
        } else if (pos < bvhText.Length && bvhText[pos] == '+') {
            pos++;
        }

        // Read digits
        while (pos < bvhText.Length && bvhText[pos] >= '0' && bvhText[pos] <= '9') {
            v = v * 10 + (int)(bvhText[pos++] - '0');
            digitFound = true;
        }

        // Finalize
        if (negate) {
            v *= -1;
        }
        if (!digitFound) {
            v = -1;
        }
        return digitFound;
    }

    // Accuracy looks okay
    private bool getFloat(out float v) {
        bool negate = false;
        bool digitFound = false;
        int i = 0;
        v = 0f;

        // Read sign
        if (pos < bvhText.Length && bvhText[pos] == '-') {
            negate = true;
            pos++;
        } else if (pos < bvhText.Length && bvhText[pos] == '+') {
            pos++;
        }

        // Read digits before decimal point
        while (pos < bvhText.Length && bvhText[pos] >= '0' && bvhText[pos] <= '9') {
            v = v * 10 + (float)(bvhText[pos++] - '0');
            digitFound = true;
        }

        // Read decimal point
        if (pos < bvhText.Length && bvhText[pos] == '.') {
            pos++;

            // Read digits after decimal
            float fac = 0.1f;
            while (pos < bvhText.Length && bvhText[pos] >= '0' && bvhText[pos] <= '9' && i < 128) {
                v += fac * (float)(bvhText[pos++] - '0');
                fac *= 0.1f;
                digitFound = true;
            }
        }

        // Finalize
        if (negate) {
            v *= -1f;
        }
        if (!digitFound) {
            v = float.NaN;
        }
        return digitFound;
    }

    private void skip() {
        while (pos < bvhText.Length && (bvhText[pos] == ' ' || bvhText[pos] == '\t' || bvhText[pos] == '\n' || bvhText[pos] == '\r')) {
            pos++;
        }
    }

    private void skipInLine() {
        while (pos < bvhText.Length && (bvhText[pos] == ' ' || bvhText[pos] == '\t')) {
            pos++;
        }
    }

    private void newline() {
        bool foundNewline = false;
        skipInLine();
        while (pos < bvhText.Length && (bvhText[pos] == '\n' || bvhText[pos] == '\r')) {
            foundNewline = true;
            pos++;
        }
        assure("newline", foundNewline);
    }

    private void assure(string what, bool result) {
        if (!result) {
            string errorRegion = "";
            for (int i = Math.Max(0, pos - 15); i < Math.Min(bvhText.Length, pos + 15); i++) {
                if (i == pos - 2) {
                    errorRegion += ">>>";
                }
                errorRegion += bvhText[i];
                if (i == pos) {
                    errorRegion += "<<<";
                }
            }
            throw new ArgumentException("Failed to parse BVH data at position " + pos + ". Expected " + what + " around here: " + errorRegion);
        }
    }

    private void assureExpect(string text) {
        assure(text, expect(text));
    }

    /*private void tryCustomFloats(string[] floats) {
        float total = 0f;
        foreach (string f in floats) {
            pos = 0;
            bvhText = f;
            float v;
            getFloat(out v);
            total += v;
        }
        Debug.Log("Custom: " + total);
    }

    private void tryStandardFloats(string[] floats) {
        IFormatProvider fp = CultureInfo.InvariantCulture;
        float total = 0f;
        foreach (string f in floats) {
            float v = float.Parse(f, fp);
            total += v;
        }
        Debug.Log("Standard: " + total);
    }

    private void tryCustomInts(string[] ints) {
        int total = 0;
        foreach (string i in ints) {
            pos = 0;
            bvhText = i;
            int v;
            getInt(out v);
            total += v;
        }
        Debug.Log("Custom: " + total);
    }

    private void tryStandardInts(string[] ints) {
        IFormatProvider fp = CultureInfo.InvariantCulture;
        int total = 0;
        foreach (string i in ints) {
            int v = int.Parse(i, fp);
            total += v;
        }
        Debug.Log("Standard: " + total);
    }

    public void benchmark () {
        string[] floats = new string[105018];
        string[] ints = new string[105018];
        for (int i = 0; i < floats.Length; i++) {
            floats[i] = UnityEngine.Random.Range(-180f, 180f).ToString();
        }
        for (int i = 0; i < ints.Length; i++) {
            ints[i] = ((int)Mathf.Round(UnityEngine.Random.Range(-180f, 18000f))).ToString();
        }
        tryCustomFloats(floats);
        tryStandardFloats(floats);
        tryCustomInts(ints);
        tryStandardInts(ints);
    }*/

    private void parse(bool overrideFrameTime, float time) {
        // Parse skeleton
        skip();
        assureExpect("HIERARCHY");

        boneList = new List<BVHBone>();
        root = new BVHBone(this, true);

        // Parse meta data
        skip();
        assureExpect("MOTION");
        skip();
        assureExpect("Frames:");
        skip();
        assure("frame number", getInt(out frames));
        skip();
        assureExpect("Frame Time:");
        skip();
        assure("frame time", getFloat(out frameTime));

        if (overrideFrameTime) {
            frameTime = time;
        }

        // Prepare channels
        int totalChannels = 0;
        foreach (BVHBone bone in boneList) {
            totalChannels += bone.channelNumber;
        }
        int channel = 0;
        channels = new float[totalChannels][];
        foreach (BVHBone bone in boneList) {
            for (int i = 0; i < bone.channelNumber; i++) {
                channels[channel++] = bone.channels[bone.channelOrder[i]].values;
            }
        }
        
        // Parse frames
        for (int i = 0; i < frames; i++) {
            newline();
            for (channel = 0; channel < totalChannels; channel++) {
                skipInLine();
                assure("channel value", getFloat(out channels[channel][i]));
            }
        }
    }

    public BVHParser(string bvhText) {
        this.bvhText = bvhText;

        parse(false, 0f);
    }

    public BVHParser(string bvhText, float time) {
        this.bvhText = bvhText;

        parse(true, time);
    }
}
