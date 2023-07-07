using UnityEditor;
using UnityEngine;

public class SceneCameraSync
{
    private static bool _isEnable = false;
    private static bool _isSync = false;
    private static string _buttonName = "EnableSyncEditorCamera";
    private static float _farClipPlane = 1000;

    private static bool _isResetMove;
    private static Vector3 _oldPosition;
    private static Quaternion _oldRotation;
    private static Vector3 _oldLocalScale;

    [MenuItem("RuriTools/SyncEditorCamera/ShowSyncEditorCameraButton")]
    private static void ShowSyncEditorCameraButton()
    {
        SceneCameraSync.SetEnable(true);
    }

    [MenuItem("RuriTools/SyncEditorCamera/HideSyncEditorCameraButton")]
    private static void HideSyncEditorCameraButton()
    {
        SceneCameraSync.SetEnable(false);
    }

    [MenuItem("RuriTools/SyncEditorCamera/SwitchResetMove")]
    private static void SwitchResetMove()
    {
        SceneCameraSync._isResetMove = !SceneCameraSync._isResetMove;
    }

    [UnityEditor.InitializeOnLoadMethod]
    private static void _OnInitInternal()
    {
        SceneCameraSync.SetEnable(true);
    }

    public static void SetEnable(bool isEnable)
    {
        if (isEnable != SceneCameraSync._isEnable)
        {
            SceneCameraSync._isEnable = isEnable;
            if (SceneCameraSync._isEnable)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                UnityEditor.EditorApplication.update += OnUpdate;
            }
            else
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                UnityEditor.EditorApplication.update -= OnUpdate;
                SceneCameraSync._isSync = false;
                SceneCameraSync._buttonName = "EnableSyncEditorCamera";
            }

            if (null != SceneView.lastActiveSceneView)
            {
                SceneView.lastActiveSceneView.Repaint();
            }
        }
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 0, 160, 50));

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(SceneCameraSync._buttonName))
        {
            if (SceneCameraSync._isEnable)
            {
                SceneCameraSync._isSync = !SceneCameraSync._isSync;

                if (SceneCameraSync._isSync)
                {
                    SceneCameraSync._buttonName = "DisableSyncEditorCamera";
                    UnityEditor.EditorApplication.update += OnUpdate;

                    if (null != Camera.main)
                    {
                        Camera mainCamera = Camera.main.GetComponent<Camera>();
                        if (null != mainCamera)
                        {
                            SceneCameraSync._farClipPlane = mainCamera.farClipPlane;
                            mainCamera.farClipPlane = 10000;
                        }
                        SceneCameraSync._oldPosition = Camera.main.transform.position;
                        SceneCameraSync._oldRotation = Camera.main.transform.rotation;
                        SceneCameraSync._oldLocalScale = Camera.main.transform.localScale;
                    }
                }
                else
                {
                    SceneCameraSync._buttonName = "EnableSyncEditorCamera";
                    UnityEditor.EditorApplication.update -= OnUpdate;

                    if (null != Camera.main)
                    {
                        Camera mainCamera = Camera.main.GetComponent<Camera>();
                        if (null != mainCamera)
                        {
                            mainCamera.farClipPlane = SceneCameraSync._farClipPlane;
                        }
                        if (SceneCameraSync._isResetMove)
                        {
                            Camera.main.transform.position = SceneCameraSync._oldPosition;
                            Camera.main.transform.rotation = SceneCameraSync._oldRotation;
                            Camera.main.transform.localScale = SceneCameraSync._oldLocalScale;
                        }
                    }
                }

            }
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.EndArea();

        Handles.EndGUI();
    }

    private static void OnUpdate()
    {
        if (SceneCameraSync._isSync)
        {
            if (Camera.main != null)
            {
                Camera.main.transform.position = UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;
                Camera.main.transform.rotation = UnityEditor.SceneView.lastActiveSceneView.camera.transform.rotation;
                Camera.main.transform.localScale = UnityEditor.SceneView.lastActiveSceneView.camera.transform.localScale;
            }
        }
    }
}