using UnityEditor;
using UnityEngine;

public class PreviewSystemCamera : MonoBehaviour
{
    public void Awake()
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            GetComponent<Camera>().enabled = false; 
        }
    }
}
