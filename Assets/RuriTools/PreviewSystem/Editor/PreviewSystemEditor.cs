using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;
using Tools.PreviewSystem;
using System.Reflection;

[CustomEditor(typeof(PreviewSystem))]
public class PreviewSystemEditor : Editor
{
    // 声明和初始化变量
    private HashSet<string> _ruriDebug = new HashSet<string>();
    private static Texture2D _logo;

    public PreviewSystem previewSystem;
    public SerializedObject configSerialized;
    public SerializedProperty configProperty;

    private Dictionary<string, string> _localizedText = new Dictionary<string, string>();
    private Dictionary<string, string> _languageOptions = new Dictionary<string, string>();
    private int _previousLanguageIndex;
    private int _selectedLanguageIndex;

    // 配置对象和属性的GUI待渲染列表
    Dictionary<string, SerializedProperty> _configProperties = new Dictionary<string, SerializedProperty>();
    public Dictionary<string, SerializedProperty> configProperties { get => _configProperties; }

    // 属性变更回调
    private Dictionary<string, Func<PreviewSystemEditor, SerializedProperty, string, bool>> _propertyCallbacks = new Dictionary<string, Func<PreviewSystemEditor, SerializedProperty, string, bool>>();
    public Dictionary<string, Func<PreviewSystemEditor, SerializedProperty, string, bool>> propertyCallbacks { get => _propertyCallbacks; }

    private void OnEnable()
    {
        _logo = (Texture2D)EditorGUIUtility.Load("PreviewSystem/Texture/Preview.png");
        previewSystem = serializedObject.targetObject as PreviewSystem;
        configProperty = serializedObject.FindProperty("config");
        LoadLocalizedFiles();
        InitPropertyChangeCallbacks(typeof(PreviewSystemPropertyCallbacks), ref _propertyCallbacks);
        LoadConfig(previewSystem.config);
    }
    private void InitPropertyChangeCallbacks(Type type, ref Dictionary<string, Func<PreviewSystemEditor, SerializedProperty, string, bool>> propertyChangeCallbacks)
    {
        // 获取传入类的所有公共静态方法
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

        foreach (var method in methods)
        {
            // 将方法名的第一个字符转换为小写
            string methodName = char.ToLower(method.Name[0]) + method.Name.Substring(1);
            methodName = methodName.Replace("_", ".");

            // 添加方法到字典，方法名作为键，方法委托作为值
            propertyChangeCallbacks[methodName] = (Func<PreviewSystemEditor, SerializedProperty, string, bool>)Delegate.CreateDelegate(typeof(Func<PreviewSystemEditor, SerializedProperty, string, bool>), method);
        }
    }
    private void DrawPropertiesRecursively(SerializedProperty property, int depth = 0)
    {
        _ruriDebug.Add(property.propertyPath);
        string propertyName = DeserializeLocalizedLanguage(property.propertyPath, property.displayName);
        
        if (!string.IsNullOrEmpty(propertyName) && propertyName.StartsWith("Hide_"))
            return;

        EditorGUI.indentLevel = depth;
        _propertyCallbacks.TryGetValue(property.propertyPath, out var propertyCallback);
        bool isShow = true;
        if (propertyCallback != null)
            isShow = propertyCallback(this, property, "PreRender");
        if (isShow)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, new GUIContent(propertyName), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (propertyCallback != null)
                    propertyCallback(this, property, "ChangeValue");
            }
            if (propertyCallback != null)
                propertyCallback(this, property, "PostRender");
        }
        if (property.isExpanded)
        {
            SerializedProperty childProperty = property.Copy();
            childProperty.NextVisible(true);
            while (childProperty.propertyPath.StartsWith(property.propertyPath))
            {
                DrawPropertiesRecursively(childProperty, depth + 1);
                if (!childProperty.NextVisible(false)) break;
            }
        }
        EditorGUI.indentLevel = depth;
    }

    public override void OnInspectorGUI()
    {
        DisplayLogo();

        ManageLanguageOptions();

        if (!InitializeCheck()) return;

        UpdateConfigProperties();
    }
    private void DisplayLogo()
    {
        var centeredStyle = GUI.skin.GetStyle("Box");
        centeredStyle.alignment = TextAnchor.MiddleCenter;
        GUILayout.Label(_logo, centeredStyle);
    }
    private void ManageLanguageOptions()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(DeserializeLocalizedLanguage("Language"));
        _previousLanguageIndex = _selectedLanguageIndex;
        _selectedLanguageIndex = EditorGUILayout.Popup(_selectedLanguageIndex, _languageOptions.Keys.ToArray());
        if (_selectedLanguageIndex != _previousLanguageIndex)
        {
            UpdateLocalizedText();
            _previousLanguageIndex = _selectedLanguageIndex;
        }
        EditorGUILayout.EndHorizontal();
    }
    private bool InitializeCheck()
    {
        if (!previewSystem.targetChara)
        {
            var targetChara = serializedObject.FindProperty("targetChara");
            _propertyCallbacks.TryGetValue(targetChara.propertyPath, out var propertyCallback);
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            EditorGUILayout.PropertyField(targetChara, new GUIContent(DeserializeLocalizedLanguage(targetChara.propertyPath, targetChara.displayName)), false);
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.Update();
                LoadConfig();
                serializedObject.ApplyModifiedProperties();

                serializedObject.Update();
                configSerialized.Update();
                if (propertyCallback != null)
                    propertyCallback(this, targetChara, "ChangeValue");

                serializedObject.ApplyModifiedProperties();
                configSerialized.ApplyModifiedProperties();
                // 初始化默认设置
                serializedObject.Update();
                configSerialized.Update();
                var configPropertiesCopy = new Dictionary<string, SerializedProperty>(_configProperties);
                foreach (var property in configPropertiesCopy.Values)
                {
                    _propertyCallbacks.TryGetValue(property.propertyPath, out var callback);
                    if (callback != null)
                        callback(this, property, "ChangeValue");
                }
                serializedObject.ApplyModifiedProperties();
                configSerialized.ApplyModifiedProperties();
            }

            EditorGUILayout.HelpBox(DeserializeLocalizedLanguage("MissingPreviewChara"), MessageType.Warning);
            return false;
        }
        return true;
    }
    private void UpdateConfigProperties()
    {
        if (!previewSystem.config || !previewSystem.previewCamera)
        {
            Unsupported.SmartReset(previewSystem);
            return;
        }
        if (configSerialized != null)
        {
            serializedObject.Update();
            configSerialized.Update();

            var configPropertiesCopy = new Dictionary<string, SerializedProperty>(_configProperties);
            foreach (var property in configPropertiesCopy.Values)
                DrawPropertiesRecursively(property);

            //DebugDictionaryKeys(_ruriDebug);
            serializedObject.ApplyModifiedProperties();
            configSerialized.ApplyModifiedProperties();
        }
    }
    public string DeserializeLocalizedLanguage(string text, string text2 = "")
    {
        _localizedText.TryGetValue(text, out var value);
        if (value == null)
        {
            if (!string.IsNullOrEmpty(text2))
                return text2;
            return text;
        }
        return value;
    }
    private void AddPropertiesToDictionary(ref Dictionary<string, SerializedProperty> configProperties, SerializedObject serializedObject)
    {
        SerializedProperty property = serializedObject.GetIterator();
        if (property.NextVisible(true))
        {
            do
            {
                if (!property.propertyPath.Equals("m_Script"))
                {
                    configProperties.Add(property.propertyPath, property.Copy());
                }
            }
            while (property.NextVisible(false));
        }
    }
    public void LoadConfig(PreviewSystemConfig config = null)
    {
        if (!config)
            config = GameObject.Instantiate((PreviewSystemConfig)EditorGUIUtility.Load("PreviewSystem/Config/DefaultPreviewSettings.asset"));
        configSerialized = new SerializedObject(config);
        serializedObject.FindProperty("config").objectReferenceValue = configSerialized.targetObject;

        _configProperties.Clear();
        AddPropertiesToDictionary(ref _configProperties, serializedObject);
        AddPropertiesToDictionary(ref _configProperties, configSerialized);
    }
    private void DebugDictionaryKeys(HashSet<string> configProperties)
    {
        string filePath = @"D:\debug.txt";
        List<string> outputs = new List<string>();
        List<string> outputs2 = new List<string>();

        foreach (var key in configProperties)
        {
            // 将方法名的第一个字符转换为大写
            string methodName = char.ToUpper(key[0]) + key.Substring(1);
            // 替换.为_
            methodName = methodName.Replace(".", "_");

            string output = $"public static bool {methodName}(PreviewSystemEditor editor, SerializedProperty property, string mode)\n" +
                "{\n" +
                "    bool isShow = true;\n" +
                "    var previewSystem = editor.previewSystem;\n" +
                "    var previewCamera = editor.previewSystem.previewCamera;\n" +
                "    var config = editor.previewSystem.config;\n" +
                "    switch (mode)\n" +
                "    {\n" +
                "        case \"PreRender\":\n" +
                "            break;\n" +
                "        case \"ChangeValue\":\n" +
                "            break;\n" +
                "        case \"PostRender\":\n" +
                "            break;\n" +
                "    }\n" +
                "    return isShow;\n" +
                "}";
            outputs.Add(output);
            outputs2.Add(methodName + "\b");
        }

        // 将所有输出内容一次性写入文件
        File.WriteAllLines(filePath, outputs);
        File.WriteAllLines(filePath + "da", outputs2);
    }
    public void SaveConfig()
    {
        string assetPath = EditorUtility.SaveFilePanel("Create Config File", Application.dataPath, "NewPreviewSettings", "asset");

        if (!string.IsNullOrEmpty(assetPath))
        {
            string relativePath = assetPath.Replace(Application.dataPath, "Assets");
            if (previewSystem.config == null)
            {
                previewSystem.config = ScriptableObject.CreateInstance<PreviewSystemConfig>();
            }
            else
            {
                previewSystem.config = Instantiate(previewSystem.config);
            }

            AssetDatabase.CreateAsset(previewSystem.config, relativePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(previewSystem.config);

            Debug.Log("Config file created at " + relativePath);
        }
    }
    private void LoadLocalizedText(string filePath)
    {
        _localizedText = new Dictionary<string, string>();
        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim().Replace("\t", "");  // Remove leading/trailing whitespace

            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
            {
                continue;  // Skip empty lines and lines starting with #
            }

            string[] keyValue = trimmedLine.Split('=');
            if (keyValue.Length >= 2)
            {
                string key = keyValue[0].Trim();
                string value = string.Join("=", keyValue.Skip(1)).Trim();  // Allow for values that contain = character
                _localizedText[key] = value;  // Add to dictionary
            }
        }
    }
    private void LoadLocalizedFiles()
    {
        string languagesFolder = "Assets/Editor Default Resources/PreviewSystem/Language";
        var languageFiles = Directory.GetFiles(languagesFolder, "*.txt");  // Change extension back to .txt
        _languageOptions = new Dictionary<string, string>();

        for (int i = 0; i < languageFiles.Length; i++)
        {
            string languageFile = languageFiles[i];
            string languageName = Path.GetFileNameWithoutExtension(languageFile);
            _languageOptions.Add(languageName, languageFile);
        }

        // Set selected language index
        string currentLanguage = EditorPrefs.GetString("SelectedLanguage", "zh-cn");
        _selectedLanguageIndex = _languageOptions.ContainsKey(currentLanguage) ? _languageOptions.Keys.ToList().IndexOf(currentLanguage) : 0;

        // Load localized text
        string selectedLanguageFile = _languageOptions.ElementAt(_selectedLanguageIndex).Value;
        LoadLocalizedText(selectedLanguageFile);
    }
    private void UpdateLocalizedText()
    {
        if (_selectedLanguageIndex >= 0 && _selectedLanguageIndex < _languageOptions.Count)
        {
            var selectedLanguagePair = _languageOptions.ElementAt(_selectedLanguageIndex);
            string selectedLanguage = selectedLanguagePair.Key;
            string selectedLanguageFile = selectedLanguagePair.Value;
            EditorGUIUtility.Load(selectedLanguageFile);
            EditorPrefs.SetString("SelectedLanguage", selectedLanguage);

            LoadLocalizedText(selectedLanguageFile);
        }
    }
}