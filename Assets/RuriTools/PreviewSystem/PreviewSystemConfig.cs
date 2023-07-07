using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PreviewSystemConfig", menuName = "Create Preview System Config")]
[Serializable]
public class PreviewSystemConfig : ScriptableObject
{
    [Serializable]
    public class CameraOffset
    {
        public bool isEnabled;
        public bool isHidden;
        public Transform[] positions;
        public Vector3[] defaultPositions;
    }

    [Serializable]
    public class LightOffset
    {
        public bool isEnabled;
        public bool isHidden;
        public Light transform;
    }

    public string outputFolder;
    public bool isEnbale;

    public bool usePhysicalCamera;
    public float focalLength;
    public bool useOrthographic;

    public bool outputAlpha;
    public bool useBackgroundColor;
    public Color backgroundColor;

    public int outputWidth;
    public int outputHeight;

    public float paddingSec;
    public float rotationSec;

    public float screenshotIntervalAngle;
    public float[] cameraPreviewAngle;

}