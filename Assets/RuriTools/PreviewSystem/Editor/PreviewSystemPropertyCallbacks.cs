using Tools.PreviewSystem;
using System.IO;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Tilemaps;
using static PreviewSystemConfig;
using static UnityEditor.Recorder.OutputPath;
using static UnityEngine.GameObject;
public static class PreviewSystemPropertyCallbacks
{

    private static GameObject FindPreviewSystemRoot()
    {
        var root = GameObject.Find("PreviewAuxObject");
        return root ? root : new GameObject("PreviewAuxObject");
    }
    public static bool Config(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                editor.LoadConfig(property.objectReferenceValue as PreviewSystemConfig);
                break;
            case "PostRender":
                if (GUILayout.Button(editor.DeserializeLocalizedLanguage("SaveConfig")))
                    editor.SaveConfig();
                break;
        }
        return isShow;
    }
    public static bool OutputFolder(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                break;
            case "PostRender":
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(editor.DeserializeLocalizedLanguage("OpenFileButton"))))
                {
                    try
                    {
                        string winPath = property.stringValue.Replace("/", "\\");
                        System.Diagnostics.Process.Start("explorer.exe", (Directory.Exists(winPath) ? "/root," : "/select,") + winPath);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Error opening location {property.stringValue} in the file browser. Exception: {ex.Message}");
                    }
                }

                if (GUILayout.Button(editor.DeserializeLocalizedLanguage("SeleteSaveFileButton")))
                {
                    property.stringValue = UnityEditor.EditorUtility.OpenFolderPanel(editor.DeserializeLocalizedLanguage("SeleteSaveFileButton"), Application.dataPath, "");
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button(editor.DeserializeLocalizedLanguage("ModelInfoExporter")))
                {
                    previewSystem.PrintModelInfo(previewSystem.targetChara);
                }
                break;
        }
        return isShow;
    }
    public static bool TargetChara(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        var configProperties = editor.configProperties;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                var root = FindPreviewSystemRoot();

                var outputFolder = configProperties["outputFolder"];
                if (string.IsNullOrEmpty(outputFolder.stringValue))
                    outputFolder.stringValue = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PreviewRecordings").Replace('\\', '/');

                if (!Directory.Exists(outputFolder.stringValue))
                    Directory.CreateDirectory(outputFolder.stringValue);

                var previewCamera1 = configProperties["previewCamera"];
                if (previewCamera1.objectReferenceValue == null)
                {
                    var vCameraTemp = GameObject.Find("VirtualPreviewCamera");
                    var vCamera = vCameraTemp ? vCameraTemp.GetComponent<Camera>() : new GameObject("VirtualPreviewCamera").AddComponent<Camera>();
                    vCamera.transform.SetParent(root.transform);
                    vCamera.usePhysicalProperties = true;
                    vCamera.nearClipPlane = 0.001f;
                    vCamera.transform.eulerAngles = new Vector3(0f, 180f, 0f);

                    previewCamera1.objectReferenceValue = vCamera;
                }

                var previewLight = configProperties["previewLight"];
                if (previewLight.objectReferenceValue == null)
                {
                    var diretLight = GameObject.Find(PreviewSystem.DIRECTIONALLIGHT);
                    if (!diretLight)
                    {
                        diretLight = GameObject.Find(PreviewSystem.PREVIEWLIGHT);
                    }
                    else
                    {
                        var temp = GameObject.Find(PreviewSystem.PREVIEWLIGHT);
                        if (!temp)
                        {
                            diretLight = GameObject.Instantiate(diretLight);
                            diretLight.name = PreviewSystem.PREVIEWLIGHT;
                            diretLight.transform.SetParent(root.transform);
                        }
                        else
                        {
                            diretLight = temp;
                        }
                    }
                    var vLight = diretLight ? diretLight.GetComponent<Light>() : new GameObject(PreviewSystem.PREVIEWLIGHT).AddComponent<Light>();
                    vLight.enabled = false;
                    vLight.transform.position = Vector3.up;
                    vLight.transform.eulerAngles = new Vector3(30f, 180f, 0f);
                    vLight.type = LightType.Directional;

                    previewLight.objectReferenceValue = vLight;
                }

                // 初始化预览相机坐标
                var cameraPositions = configProperties["cameraPositions"];
                string camera0 = "CameraPosition0";
                string camera1 = "CameraPosition1";
                cameraPositions.arraySize = 2;
                var tempPreviewCamera = (previewCamera1.objectReferenceValue as Camera).gameObject;

                var vPosition0Temp = GameObject.Find(previewSystem.targetChara.name + camera0);
                var vPosition0 = (GameObject)(vPosition0Temp ? vPosition0Temp : GameObject.Instantiate(tempPreviewCamera));
                if (!vPosition0.GetComponent<PreviewSystemCamera>())
                {
                    vPosition0.AddComponent<PreviewSystemCamera>();
                }
                vPosition0.transform.SetParent(root.transform);
                vPosition0.name = previewSystem.targetChara.name + camera0;
                vPosition0.transform.position = Vector3.forward;
                var vc0 = vPosition0.GetComponent<Camera>();
                vc0.depth = -10f;
                vc0.enabled = false;
                cameraPositions.GetArrayElementAtIndex(0).objectReferenceValue = vPosition0;

                var vPosition1Temp = GameObject.Find(previewSystem.targetChara.name + camera1);
                var vPosition1 = (GameObject)(vPosition1Temp ? vPosition1Temp : GameObject.Instantiate(tempPreviewCamera));
                if (!vPosition1.GetComponent<PreviewSystemCamera>())
                {
                    vPosition1.AddComponent<PreviewSystemCamera>();
                }
                vPosition1.transform.SetParent(root.transform);
                vPosition1.name = previewSystem.targetChara.name + camera1;
                vPosition1.transform.position = Vector3.forward * 2;
                var vc1 = vPosition0.GetComponent<Camera>();
                vc1.depth = -10f;
                vc1.enabled = false;
                cameraPositions.GetArrayElementAtIndex(1).objectReferenceValue = vPosition1;
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool UsePhysicalCamera(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                previewCamera.usePhysicalProperties = property.boolValue;
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool FocalLength(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                isShow = previewCamera.usePhysicalProperties;
                break;
            case "ChangeValue":
                previewCamera.focalLength = property.floatValue;
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool UseOrthographic(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                previewCamera.orthographic = property.boolValue;
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool OutputAlpha(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                if (property.boolValue)
                    previewCamera.clearFlags = CameraClearFlags.SolidColor;
                else
                    previewCamera.clearFlags = CameraClearFlags.Skybox;
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool UseBackgroundColor(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                if (property.boolValue)
                    previewCamera.clearFlags = CameraClearFlags.SolidColor;
                else
                    previewCamera.clearFlags = CameraClearFlags.Skybox;
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool BackgroundColor(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                isShow = config.useBackgroundColor;
                break;
            case "ChangeValue":
                previewCamera.backgroundColor = property.colorValue;
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool CameraOffset_isEnabled(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        var cameraOffset = editor.configProperties["cameraOffset"];
        SerializedProperty cameraOffsetPositionsConfig = cameraOffset.FindPropertyRelative("positions");
        SerializedProperty offsetDefaultPositionsConfig = cameraOffset.FindPropertyRelative("defaultPositions");
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                if (property.boolValue)
                {
                    var cameraPositionsProperty = editor.configProperties["cameraPositions"];
                    for (int i = 0; i < cameraPositionsProperty.arraySize; i++)
                    {
                        Transform elementProperty = cameraPositionsProperty.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        SceneVisibilityManager.instance.Hide(elementProperty.gameObject, false);
                    }

                    Camera dummyCamera = GameObject.Instantiate(previewCamera);
                    int length = previewSystem.cameraPositions.Length * config.cameraPreviewAngle.Length;
                    cameraOffsetPositionsConfig.arraySize = length;
                    offsetDefaultPositionsConfig.arraySize = length;

                    int indexNum = 0;
                    for (int pos = 0; pos < previewSystem.cameraPositions.Length; pos++)
                    {
                        dummyCamera.transform.position = previewSystem.targetChara.transform.position;
                        dummyCamera.transform.rotation = previewCamera.transform.rotation;
                        Vector3 oldPos = dummyCamera.transform.position;
                        Quaternion oldRot = dummyCamera.transform.rotation;
                        var vec = previewSystem.cameraPositions[pos].position + previewSystem.targetChara.transform.position;
                        vec.z = previewSystem.targetChara.transform.position.z;
                        //遠近カメラ座標
                        dummyCamera.transform.position += previewSystem.cameraPositions[pos].position;

                        for (int previewAngle = 0; previewAngle < config.cameraPreviewAngle.Length; previewAngle++)
                        {
                            Vector3 oldPos1 = dummyCamera.transform.position;
                            Quaternion oldRot1 = dummyCamera.transform.rotation;
                            //正面俯瞰アオリ回転
                            dummyCamera.transform.RotateAround(vec, Vector3.left, config.cameraPreviewAngle[previewAngle]);
                            // 创建虚拟相机偏移
                            string name = previewSystem.targetChara.name + "CameraOffset" + indexNum;
                            var vCamera = GameObject.Find(name);
                            var vCameraOffset = vCamera ? vCamera.GetComponent<Camera>() : GameObject.Instantiate(dummyCamera);
                            vCameraOffset.gameObject.AddComponent<PreviewSystemCamera>();
                            vCameraOffset.transform.SetParent(FindPreviewSystemRoot().transform);
                            vCameraOffset.gameObject.SetActive(true);
                            vCameraOffset.gameObject.transform.position = dummyCamera.transform.position;
                            vCameraOffset.gameObject.transform.rotation = dummyCamera.transform.rotation;

                            vCameraOffset.enabled = false;
                            vCameraOffset.name = name;

                            cameraOffsetPositionsConfig.GetArrayElementAtIndex(indexNum).objectReferenceValue = vCameraOffset.transform; // 将新元素赋值为vCameraOffset.gameObject
                                                                                                                                         //没有设置默认坐标的时候才自动设置
                            if (offsetDefaultPositionsConfig.arraySize > indexNum && offsetDefaultPositionsConfig.GetArrayElementAtIndex(indexNum).vector3Value == Vector3.zero)
                            {
                                offsetDefaultPositionsConfig.GetArrayElementAtIndex(indexNum).vector3Value = dummyCamera.transform.position;
                            }
                            // 如果CameraOffset的位置不是zero，那么使用CameraOffset里的值
                            if (cameraOffsetPositionsConfig.arraySize > indexNum && (cameraOffsetPositionsConfig.GetArrayElementAtIndex(indexNum).objectReferenceValue as Transform).position != Vector3.zero)
                            {
                                (cameraOffsetPositionsConfig.GetArrayElementAtIndex(indexNum).objectReferenceValue as Transform).position = (cameraOffsetPositionsConfig.GetArrayElementAtIndex(indexNum).objectReferenceValue as Transform).position;
                            }
                            (cameraOffsetPositionsConfig.GetArrayElementAtIndex(indexNum).objectReferenceValue as Transform).position = vCameraOffset.gameObject.transform.position; // 将新元素赋值为vCameraOffset.gameObject.transform.position

                            indexNum++;
                            dummyCamera.transform.position = oldPos1;
                            dummyCamera.transform.rotation = oldRot1;

                        }

                        dummyCamera.transform.position = oldPos;
                        dummyCamera.transform.rotation = oldRot;
                    }
                    GameObject.DestroyImmediate(dummyCamera.gameObject);
                }
                else
                {
                    var cameraPositionsProperty = editor.configProperties["cameraPositions"];
                    for (int i = 0; i < cameraPositionsProperty.arraySize; i++)
                    {
                        Transform elementProperty = cameraPositionsProperty.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        SceneVisibilityManager.instance.Show(elementProperty.gameObject, false);
                    }

                    var root = FindPreviewSystemRoot();
                    var tran = root.GetComponentsInChildren<Transform>();
                    for (int i = 0; i < tran.Length; i++)
                    {
                        if (tran[i].name.Contains(previewSystem.targetChara.name + "CameraOffset"))
                        {
                            GameObject.DestroyImmediate(tran[i].gameObject);
                        }
                    }
                    if (previewSystem.cameraOffset != null)
                    {
                        cameraOffsetPositionsConfig.ClearArray();
                        //offsetPositionsConfig.ClearArray();
                        offsetDefaultPositionsConfig.ClearArray();
                    }
                }
                break;
            case "PostRender":
                if (GUILayout.Button(editor.DeserializeLocalizedLanguage("CameraOffsetRestone")))
                {
                    var root = FindPreviewSystemRoot();
                    var tran = root.GetComponentsInChildren<Transform>();
                    for (int i = 0; i < tran.Length; i++)
                    {
                        if (tran[i].name.Contains(previewSystem.targetChara.name + "CameraOffset"))
                        {
                            string nameWithoutOffset = tran[i].name.Replace(previewSystem.targetChara.name + "CameraOffset", "");
                            int numericValue;
                            if (int.TryParse(nameWithoutOffset, out numericValue))
                            {
                                GameObject obj = tran[i].gameObject;
                                if (obj != null)
                                {
                                    Undo.RecordObject(obj.transform, "Restone Transform Position");
                                    obj.transform.position = offsetDefaultPositionsConfig.GetArrayElementAtIndex(numericValue).vector3Value;
                                }
                            }
                        }
                    }
                }

                break;
        }
        return isShow;
    }
    public static bool CameraOffset_isHidden(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                var root = FindPreviewSystemRoot();
                var tran = root.GetComponentsInChildren<Transform>();
                for (int i = 0; i < tran.Length; i++)
                {
                    if (tran[i].name.Contains(previewSystem.targetChara.name + "CameraOffset"))
                    {
                        tran[i].hideFlags = property.boolValue ? HideFlags.HideInHierarchy : HideFlags.None;
                        EditorUtility.SetDirty(tran[i]);
                    }
                }
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
    public static bool LightOffset_isEnabled(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        var lightOffset = editor.configProperties["lightOffset"].FindPropertyRelative("transform");
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                //平行光源オフセット初期化
                if (property.boolValue)
                {
                    var vLight = GameObject.Find(previewSystem.targetChara.name + "PreviewLightOffset");
                    var vLightOffset = vLight ? vLight.GetComponent<Light>() : GameObject.Instantiate(previewSystem.previewLight);
                    vLightOffset.transform.SetParent(FindPreviewSystemRoot().transform);
                    vLightOffset.gameObject.SetActive(true);
                    vLightOffset.gameObject.transform.position = Vector3.up * 2;
                    vLightOffset.gameObject.transform.rotation = Quaternion.identity;
                    vLightOffset.enabled = false;
                    vLightOffset.name = previewSystem.targetChara.name + "PreviewLightOffset";
                    lightOffset.objectReferenceValue = vLightOffset;
                    var rot = lightOffset.objectReferenceValue;
                    previewSystem.lightOffset.transform = lightOffset.objectReferenceValue as Light;

                }
                else
                {
                    if (lightOffset != null && lightOffset.objectReferenceValue != null)
                    {
                        GameObject.DestroyImmediate((lightOffset.objectReferenceValue as Light).gameObject);
                    }
                }
                break;
            case "PostRender":
                if (GUILayout.Button(editor.DeserializeLocalizedLanguage("LightOffsetRestone")))
                {
                    var root = FindPreviewSystemRoot();
                    var tran = root.GetComponentsInChildren<Transform>();
                    for (int i = 0; i < tran.Length; i++)
                    {
                        if (tran[i].name.Equals(previewSystem.targetChara.name + "PreviewLightOffset"))
                        {
                            GameObject obj = tran[i].gameObject;
                            if (obj != null)
                            {
                                Undo.RecordObject(obj.transform, "Restone Transform Position");
                                obj.transform.eulerAngles = Vector3.zero;
                            }
                        }
                    }
                }
                break;
        }
        return isShow;
    }
    public static bool LightOffset_isHidden(PreviewSystemEditor editor, SerializedProperty property, string mode)
    {
        bool isShow = true;
        var previewSystem = editor.previewSystem;
        var previewCamera = editor.previewSystem.previewCamera;
        var config = editor.previewSystem.config;
        switch (mode)
        {
            case "PreRender":
                break;
            case "ChangeValue":
                var root = FindPreviewSystemRoot();
                var tran = root.GetComponentsInChildren<Transform>();
                for (int i = 0; i < tran.Length; i++)
                {
                    if (tran[i].name.Contains(previewSystem.targetChara.name + "PreviewLightOffset"))
                    {
                        tran[i].hideFlags = property.boolValue ? HideFlags.HideInHierarchy : HideFlags.None;
                        EditorUtility.SetDirty(tran[i]);
                    }
                }
                break;
            case "PostRender":
                break;
        }
        return isShow;
    }
}