

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PreviewSystemCamera))]
public class PreviewSystemCameraInspector : UnityEditor.Editor
{

    private void OnEnable()
    {
        var current = (PreviewSystemCamera)target;
        if (current) 
        {
            var camera = current.GetComponent<Camera>();
            camera.enabled = true;
            camera.depth = 100f ;
        }
    }

    private void OnDisable()
    {
        var current = (PreviewSystemCamera)target;
        if (current)
        {
            var camera = current.GetComponent<Camera>();
            camera.enabled = false;
            camera.depth = -10f;
        }
    }

}