using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuriTools
{
    public class DifferenceToObject : EditorWindow
    {
        private enum DiffType { Same, Different, Obj1Extra, Obj2Extra, DifferentChildren };

        #region Helper Classes
        // 差异存储在树形结构中，包括DiffNodes。每个DiffNode包含与比较对象相同的propertyPath的SerializedProperties，
        // 或者如果该SerializedProperty的propertyPath在另一个对象上不存在，则包含单个SerializedProperty
        private class DiffNode
        {
            [NonSerialized] // We don't need these to be serialized while serializing RootDiffNode
            public DiffType type;
            [NonSerialized]
            public DiffNode[] children;
            [NonSerialized]
            public SerializedProperty prop1, prop2;

            public DiffNode(DiffType type, SerializedProperty prop1, SerializedProperty prop2)
            {
                this.type = type;
                this.prop1 = prop1;
                this.prop2 = prop2;
            }

            public void SetExpandedState()
            {
                if (prop1 != null)
                    prop1.isExpanded = type == DiffType.DifferentChildren;
                if (prop2 != null)
                    prop2.isExpanded = type == DiffType.DifferentChildren;

                if (children != null)
                {
                    for (int i = 0; i < children.Length; i++)
                        children[i].SetExpandedState();
                }
            }
        }

        [Serializable]
        private class RootDiffNode : DiffNode
        {
            // 虽然我们可以从SerializedObject.targetObject中获取这些，但是SerializedObject在程序集重新加载后不会保持持久状态，而UnityEngine.Object会保持持久状态，
            // 我们需要这些差异化的对象持续存在
            public Object diffObject1, diffObject2;
            public bool hasAnyDiffs;

            [NonSerialized] // 以防万一...
            public SerializedObject serializedObject1, serializedObject2;

            public RootDiffNode(SerializedObject serializedObject1, SerializedObject serializedObject2, DiffNode[] children) : base(DiffType.DifferentChildren, null, null)
            {
                this.serializedObject1 = serializedObject1;
                this.serializedObject2 = serializedObject2;
                this.diffObject1 = serializedObject1.targetObject;
                this.diffObject2 = serializedObject2.targetObject;
                this.children = children;

                hasAnyDiffs = false;
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].type != DiffType.Same)
                    {
                        hasAnyDiffs = true;
                        break;
                    }
                }
            }
        }

        [Serializable]
        private struct DiffExtraComponent
        {
            public Component component;
            [NonSerialized]
            public GameObject missingGameObject;
            [NonSerialized]
            public bool isComponentInDiffObject1;
        }
        #endregion

        private const float DIFF_RESULTS_EDGE_PADDING = 5f;
        private const float HEADER_PADDING = 6f;
        private const float COPY_VALUE_BUTTON_PADDING = 3f;
        private const float COPY_VALUE_BUTTON_WIDTH = 20f;

        private readonly Color COLUMN1_COLOR = new Color32(0, 0, 0, 0);
        private readonly Color COLUMN2_COLOR = new Color32(128, 128, 128, 25);
        private readonly Color DIFF_HEADER_COLOR = new Color32(0, 100, 255, 100);
        private readonly Color DIFFERENT_PROPERTY_COLOR_LIGHT_SKIN = new Color32(255, 255, 0, 100);
        private readonly Color DIFFERENT_PROPERTY_COLOR_DARK_SKIN = new Color32(255, 255, 0, 40);
        private readonly Color MISSING_PROPERTY_COLOR = new Color32(255, 0, 0, 100);

        private readonly GUIContent COPY_TO_OBJ1_BUTTON = new GUIContent("<", "Copy the value from right to left");
        private readonly GUIContent COPY_TO_OBJ2_BUTTON = new GUIContent(">", "Copy the value from left to right");
        private readonly GUIContent COPY_COMPONENT_TO_LEFT_BUTTON = new GUIContent("<", "Copy the component");
        private readonly GUIContent COPY_COMPONENT_TO_RIGHT_BUTTON = new GUIContent(">", "Copy the component");
        private readonly GUIContent DESTROY_COMPONENT_BUTTON = new GUIContent("X", "Destroy (remove) the component");

#pragma warning disable 0649
        [SerializeField] // 需要SerializeField来通过windowSerialized访问这些属性。
        private Object obj1, obj2;
        [SerializeField]
        private RootDiffNode[] rootDiffNodes;
        [SerializeField]
        private DiffExtraComponent[] diffExtraComponents;

        [SerializeField]
        private bool showSameValues = true;
        [SerializeField]
        private bool includeChild;
#pragma warning restore 0649

        private SerializedObject windowSerialized;

        private GUIStyle centerAlignedText;

        private Rect scrollViewRect = new Rect();
        private Vector2 scrollViewRange;
        private Vector2 scrollPosition;

        [MenuItem("RuriTools/DifferenceToObject", priority = 400)]
        public static new void Show()
        {
            DifferenceToObject window = GetWindow<DifferenceToObject>();
            window.titleContent = new GUIContent("DifferenceToObject");
            window.minSize = new Vector2(500f, 150f);
            ((EditorWindow)window).Show();
        }

        private void OnEnable()
        {
            windowSerialized = new SerializedObject(this);

            // Easiest way to preserve data between assembly reloads is to recalculate the diff
            RefreshDiff();

            Undo.undoRedoPerformed -= RefreshDiff;
            Undo.undoRedoPerformed += RefreshDiff;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshDiff;
        }

        private void RefreshDiff()
        {
            if (rootDiffNodes != null && rootDiffNodes.Length > 0 && rootDiffNodes[0].diffObject1 && rootDiffNodes[0].diffObject2)
            {
                Transform aTransform = (obj1 as GameObject).transform;
                Transform bTransform = (obj2 as GameObject).transform;

                List<string> childTreeA = GetObjectTree(aTransform);
                List<string> childTreeB = GetObjectTree(bTransform);
                var sameChild = childTreeB.Intersect(childTreeA).ToArray();

                CalcAllDiffNode(aTransform, bTransform, sameChild);
            }

            Repaint();
        }

        private void OnGUI()
        {
            scrollViewRect.x = 0f; // We must add DIFF_RESULTS_EDGE_PADDING after BeginScrollView, not here
            scrollViewRect.width = EditorGUIUtility.currentViewWidth - 2f * DIFF_RESULTS_EDGE_PADDING;

            // If vertical scrollbar is visible, decrease width
            if (position.height < scrollViewRect.height)
                scrollViewRect.width -= GUI.skin.verticalScrollbar.CalcSize(GUIContent.none).x;

            scrollPosition = GUI.BeginScrollView(new Rect(Vector2.zero, position.size), scrollPosition, scrollViewRect);

            scrollViewRect.x = DIFF_RESULTS_EDGE_PADDING;
            scrollViewRect.height = DIFF_RESULTS_EDGE_PADDING; // We'll recalculate height inside scroll view

            GUI.Box(GetRect(EditorGUIUtility.singleLineHeight * 1.25f, 2f), "对象差异检查器");

            // Show obj1 and obj2 properties via PropertyField so that right clicking the property shows Copy/Paste context menu
            Rect rect = GetRect(EditorGUIUtility.singleLineHeight, 3f);
            windowSerialized.Update();
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height), windowSerialized.FindProperty("obj1"), GUIContent.none, false);
            EditorGUI.PropertyField(new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, rect.height), windowSerialized.FindProperty("obj2"), GUIContent.none, false);
            windowSerialized.ApplyModifiedPropertiesWithoutUndo();

            GUI.enabled = obj1 && obj2 && obj1 != obj2;
            if (GUI.Button(GetRect(EditorGUIUtility.singleLineHeight * 1.5f), "Calculate Differences"))
            {
                Transform aTransform = (obj1 as GameObject).transform;
                Transform bTransform = (obj2 as GameObject).transform;

                List<string> childTreeA = GetObjectTree(aTransform);
                List<string> childTreeB = GetObjectTree(bTransform);
                var sameChild = childTreeB.Intersect(childTreeA).ToArray();

                CalcAllDiffNode(aTransform, bTransform, sameChild);

                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;
            includeChild = GUI.Toggle(GetRect(EditorGUIUtility.singleLineHeight * 1.5f), includeChild, "包含子对象(极慢) Very slow!");

            GUI.enabled = obj1 && obj2 && obj1 != obj2;
            if (GUI.Button(GetRect(EditorGUIUtility.singleLineHeight * 1.5f), "Apply Differences to Left Object"))///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            {
                string srcName = obj1.name;
                // 缺失对象树预处理
                Transform aTransform = (obj1 as GameObject).transform;
                Transform bTransform = (obj2 as GameObject).transform;
                CreateDiffObjectTree(aTransform, bTransform);

                var newBTransform = Instantiate(bTransform);
                newBTransform.name = bTransform.name;
                newBTransform.SetSiblingIndex(bTransform.GetSiblingIndex());

                DestroyImmediate(bTransform.gameObject);

                bTransform = newBTransform;
                obj2 = newBTransform.gameObject;

                var a = GetObjectTree(aTransform);
                var b = GetObjectTree(bTransform);
                var sameChild = b.Intersect(a).ToArray();
                CalcAllDiffNode(aTransform, bTransform, sameChild, true);

                // 先修复缺失组件 不然后面找不到组件之间的错误引用
                if (diffExtraComponents != null && diffExtraComponents.Length > 0)
                {
                    for (int i = 0; i < diffExtraComponents.Length; i++)
                    {
                        DiffExtraComponent extraComponent = diffExtraComponents[i];
                        bool copyOperation = !diffExtraComponents[i].isComponentInDiffObject1;
                        if (extraComponent.component)
                        {
                            if (!copyOperation)
                                Undo.DestroyObjectImmediate(extraComponent.component);
                            else if (extraComponent.missingGameObject)
                            {
                                if (UnityEditorInternal.ComponentUtility.CopyComponent(extraComponent.component))
                                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(extraComponent.missingGameObject);
                                else
                                    EditorUtility.CopySerialized(extraComponent.component, Undo.AddComponent(extraComponent.missingGameObject, extraComponent.component.GetType()));
                            }
                        }
                        Debug.Log($"找到差异 组件 : {diffExtraComponents[i]}");
                    }
                    newBTransform = Instantiate(bTransform);
                    newBTransform.name = bTransform.name;
                    newBTransform.SetSiblingIndex(bTransform.GetSiblingIndex());

                    DestroyImmediate(bTransform.gameObject);

                    bTransform = newBTransform;
                    obj2 = newBTransform.gameObject;

                    CalcAllDiffNode(aTransform, bTransform, sameChild, true);
                }

                if (rootDiffNodes != null && rootDiffNodes.Length > 0 && rootDiffNodes[0].serializedObject1 != null)
                {
                    for (int i = 0; i < rootDiffNodes.Length; i++)
                    {
                        if (rootDiffNodes[i].serializedObject1.targetObject)
                            rootDiffNodes[i].serializedObject1.Update();
                        if (rootDiffNodes[i].serializedObject2.targetObject)
                            rootDiffNodes[i].serializedObject2.Update();
                    }

                    // Draw diff results
                    for (int i = 0; i < rootDiffNodes.Length; i++)
                    {
                        // Draw diffed objects' diffs
                        if (rootDiffNodes[i].hasAnyDiffs)
                        {
                            ReplaceDiffNode(rootDiffNodes[i]);
                        }
                    }
                }

                // Apply any changes made to the displayed SerializedProperties
                for (int i = 0; i < rootDiffNodes.Length; i++)
                {
                    if (rootDiffNodes[i].serializedObject1.targetObject)
                        rootDiffNodes[i].serializedObject1.ApplyModifiedProperties();
                    if (rootDiffNodes[i].serializedObject2.targetObject)
                        rootDiffNodes[i].serializedObject2.ApplyModifiedProperties();
                }

                aTransform.name = srcName;
                RefreshDiff();
                GUIUtility.ExitGUI();
            }//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            GUI.enabled = true;

            if (rootDiffNodes != null && rootDiffNodes.Length > 0 && rootDiffNodes[0].serializedObject1 != null)
            {
                for (int i = 0; i < rootDiffNodes.Length; i++)
                {
                    if (rootDiffNodes[i].serializedObject1.targetObject)
                        rootDiffNodes[i].serializedObject1.Update();
                    if (rootDiffNodes[i].serializedObject2.targetObject)
                        rootDiffNodes[i].serializedObject2.Update();
                }

                scrollViewRect.height += 10f;
                EditorGUI.HelpBox(GetRect(EditorGUIUtility.singleLineHeight * 2f, 2f), "Diff results are NOT refreshed automatically.", MessageType.Info);

                // Paint each column with different color
                EditorGUI.DrawRect(new Rect(scrollViewRect.x, scrollViewRect.yMax, scrollViewRect.width * 0.5f, 10000f), COLUMN1_COLOR);
                EditorGUI.DrawRect(new Rect(scrollViewRect.x + scrollViewRect.width * 0.5f, scrollViewRect.yMax, scrollViewRect.width * 0.5f, 10000f), COLUMN2_COLOR);

                showSameValues = GUI.Toggle(GetRect(EditorGUIUtility.singleLineHeight * 1.5f, 3f), showSameValues, showSameValues ? "Show Same Values: ON" : "Show Same Values: OFF", GUI.skin.button);

                // Draw diff results
                for (int i = 0; i < rootDiffNodes.Length; i++)
                {
                    // Draw diffed objects (these properties are drawn with PropertyField to support Copy/Paste context menu)
                    rect = GetRect(EditorGUIUtility.singleLineHeight + HEADER_PADDING);

                    // Paint the diffed objects' background
                    EditorGUI.DrawRect(new Rect(rect.x - DIFF_RESULTS_EDGE_PADDING, rect.y, rect.width + 2f * DIFF_RESULTS_EDGE_PADDING, rect.height), DIFF_HEADER_COLOR);

                    // Apply padding from top and bottom
                    rect.y += HEADER_PADDING * 0.5f;
                    rect.height -= HEADER_PADDING;

                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height), windowSerialized.FindProperty("rootDiffNodes").GetArrayElementAtIndex(i).FindPropertyRelative("diffObject1"), GUIContent.none, false);
                    EditorGUI.PropertyField(new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, rect.height), windowSerialized.FindProperty("rootDiffNodes").GetArrayElementAtIndex(i).FindPropertyRelative("diffObject2"), GUIContent.none, false);

                    // Draw diffed objects' diffs
                    if (showSameValues || rootDiffNodes[i].hasAnyDiffs)
                        DrawDiffNode(rootDiffNodes[i]);
                    else
                    {
                        if (centerAlignedText == null)
                            centerAlignedText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

                        EditorGUI.LabelField(GetRect(EditorGUIUtility.singleLineHeight), "No differences...", centerAlignedText);
                    }

                    // Draw separator line
                    if (i < rootDiffNodes.Length - 1)
                    {
                        scrollViewRect.height += 10f;

                        GUI.Box(GetRect(1f), GUIContent.none);

                        scrollViewRect.height += 10f;
                    }
                }

                // Draw extra components that don't exist on both of the diffed GameObjects
                if (diffExtraComponents != null && diffExtraComponents.Length > 0)
                {
                    scrollViewRect.height += 20f;

                    GUI.Box(GetRect(EditorGUIUtility.singleLineHeight * 1.25f, 2f), "EXTRA COMPONENTS");

                    for (int i = 0; i < diffExtraComponents.Length; i++)
                    {
                        Rect component1Rect, component2Rect;
                        if (GetDiffRects(EditorGUIUtility.singleLineHeight, Color.clear, out component1Rect, out component2Rect))
                        {
                            // Draw extra components
                            if (diffExtraComponents[i].isComponentInDiffObject1)
                            {
                                EditorGUI.PropertyField(component1Rect, windowSerialized.FindProperty("diffExtraComponents").GetArrayElementAtIndex(i).FindPropertyRelative("component"), GUIContent.none, false);
                                EditorGUI.DrawRect(component2Rect, MISSING_PROPERTY_COLOR);
                            }
                            else
                            {
                                EditorGUI.DrawRect(component1Rect, MISSING_PROPERTY_COLOR);
                                EditorGUI.PropertyField(component2Rect, windowSerialized.FindProperty("diffExtraComponents").GetArrayElementAtIndex(i).FindPropertyRelative("component"), GUIContent.none, false);
                            }

                            // Draw buttons to copy/destroy extra Components
                            GetCopyValueButtonRects(ref component1Rect, ref component2Rect);

                            if (GUI.Button(component1Rect, diffExtraComponents[i].isComponentInDiffObject1 ? DESTROY_COMPONENT_BUTTON : COPY_COMPONENT_TO_LEFT_BUTTON))
                                DiffExtraComponentCopyOrDestroyButtonClicked(diffExtraComponents[i], !diffExtraComponents[i].isComponentInDiffObject1);
                            if (GUI.Button(component2Rect, diffExtraComponents[i].isComponentInDiffObject1 ? COPY_COMPONENT_TO_RIGHT_BUTTON : DESTROY_COMPONENT_BUTTON))
                                DiffExtraComponentCopyOrDestroyButtonClicked(diffExtraComponents[i], diffExtraComponents[i].isComponentInDiffObject1);
                        }
                    }
                }

                // Apply any changes made to the displayed SerializedProperties
                for (int i = 0; i < rootDiffNodes.Length; i++)
                {
                    if (rootDiffNodes[i].serializedObject1.targetObject)
                        rootDiffNodes[i].serializedObject1.ApplyModifiedProperties();
                    if (rootDiffNodes[i].serializedObject2.targetObject)
                        rootDiffNodes[i].serializedObject2.ApplyModifiedProperties();
                }
            }

            scrollViewRect.height += DIFF_RESULTS_EDGE_PADDING;
            GUI.EndScrollView();
        }
        private void CreateDiffObjectTree(Transform aTransform, Transform bTransform)
        {
            // 这里是先创建差异层 然后在下面生成A对象缺失的子对象
            List<string> childTreeA = GetObjectTree(aTransform);
            List<string> childTreeB = GetObjectTree(bTransform);

            var diffPathTree = childTreeB.Except(childTreeA).ToList();

            Undo.RecordObject(aTransform, "Add new objects");

            foreach (var path in diffPathTree)
            {
                /* 层级顺序创建对象 但实际上意义不大 因为缺失的引用还是会直接复制过来
                string[] names = path.Split('/');
				GameObject parent = aTransform.gameObject;
                foreach (string name in names)
                {
                    GameObject child = parent.transform.Find(name)?.gameObject;

                    if (child == null)
                    {
                        Transform diffTransform = bTransform.Find(path);
                        child = new GameObject(name);
                        child.transform.SetParent(parent.transform);
                        child.name = diffTransform.name;

                        int diffIndex = diffTransform.GetSiblingIndex();
                        child.transform.SetSiblingIndex(diffIndex);

						// 将对变换所做的更改注册为可撤销的
                        Undo.RegisterCreatedObjectUndo(child.gameObject, "Add new object");
                    }

                    parent = child;
                }*/
                //直接从b对象复制目录 缺点和上面一样 会导致对象引用到复制前对象上
                bool isSubPath = diffPathTree.Any(p => path.StartsWith(p + "/"));
                if (!isSubPath)
                {
                    Transform diffTransform = bTransform.Find(path);
                    //*****************非正常手段************************
                    // 如果使用 Instantiate 会导致对象参数引用到旧的对象上 所以必须使用这个非公开Api
                    // 但这还是有一个问题 如果创建的时候其他节点还没生成的话 还是会用旧的对象引用

                    /*
                    Selection.activeGameObject = diffTransform.gameObject;
                    Unsupported.DuplicateGameObjectsUsingPasteboard();
					Transform newTransform = Selection.activeTransform;
                    newTransform.parent = aTransform;
					*/

                    //***************************************************
                    Transform newTransform = Instantiate(diffTransform, aTransform);
                    newTransform.name = diffTransform.name;

                    int diffIndex = diffTransform.GetSiblingIndex();
                    newTransform.SetSiblingIndex(diffIndex);

                    string parentPath = path.Contains("/") ? path.Substring(0, path.LastIndexOf('/')) : path;
                    newTransform.parent = aTransform.Find(parentPath);

                    // 将对变换所做的更改注册为可撤销的
                    Undo.RegisterCreatedObjectUndo(newTransform.gameObject, "Add new object");
                }
            }
        }
        private void ReplaceDiffNode(DiffNode node)
        {
            if (node.type == DiffType.DifferentChildren)
            {
                bool isRootNode = node is RootDiffNode;
                if (!isRootNode)
                {
                    Debug.Log($"找到子差异 对象1: {node.prop1.CopyValue()} 对象2: {node.prop2.CopyValue()}");
                    ReplaceDiffNodeValue(node);
                }

                if (isRootNode)
                {
                    DiffNode[] children = node.children;
                    for (int j = 0; j < children.Length; j++)
                    {
                        try
                        {
                            ReplaceDiffNode(children[j]);
                        }
                        catch (InvalidOperationException)
                        {
                            // 一个 DiffNode 的 SerializedProperty 已经无效（例如，如果它是一个数组元素，那么该数组元素现在已被删除）
                            // 删除有问题的 DiffNode 并重新绘制窗口以反映这些更改
                            if (children.Length == 1)
                            {
                                node.type = DiffType.Same;
                                node.children = null;
                            }
                            else
                            {
                                RemoveArrayElement(ref children, j);

                                DiffType? diffType = GetCombinedDiffType(children);
                                if (diffType.HasValue && diffType.Value != DiffType.DifferentChildren)
                                {
                                    // 所有的子节点都有相同的差异类型，将该差异类型传递给该父差异节点
                                    node.type = diffType.Value;
                                    node.children = null;
                                }
                            }

                            EditorApplication.delayCall += Repaint;
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
            else if(node.type == DiffType.Different)
{
                Debug.Log($"找到差异：对象1：{node.prop1.CopyValue()}，对象2：{node.prop2.CopyValue()}");
                if (!node.prop1.serializedObject.targetObject || !node.prop2.serializedObject.targetObject)
                    return; // TODO: 处理同级对象，注意 GUID

                object value2 = node.prop2.CopyValue();
                if (value2 is GameObject || value2 is Component)
                {
                    // 如果 value2 是 GameObject 或 Component，则进行层级树查找，而不是使用旧对象的引用
                    var component2 = value2 as Component;
                    var gameObj2 = value2 as GameObject;
                    Transform transform2 = component2 ? component2.transform : gameObj2.transform;
                    // 如果值的根对象不在对象2里面 那就取消查找
                    var targetObject2 = node.prop2.serializedObject.targetObject as Component;
                    var serializedObject2 = node.prop2.serializedObject.targetObject as GameObject;
                    Transform serializedObjectTransform2 = targetObject2 ? targetObject2.transform : serializedObject2.transform;
                    if (transform2.root == serializedObjectTransform2.transform.root)
                    {
                        if (node.prop2.propertyType == SerializedPropertyType.ObjectReference && (gameObj2 || component2))
                        {
                            string path2 = GetParentTree(component2 ? component2.transform : gameObj2.transform);
                            var root1 = (node.prop1.serializedObject.targetObject as Component).transform.root;
                            var tempObj = root1.Find(path2);
                            if (!tempObj) // 如果子类找不到，那就是父类本身
                                tempObj = root1;
                            ReplaceDiffNodeValue(node, tempObj);
                        }
                    }
                    else
                    {
                        ReplaceDiffNodeValue(node, value2);
                    }
                }
                else
                {
                    ReplaceDiffNodeValue(node, value2);
                }
            }
        }
        private void ReplaceDiffNodeValue(DiffNode node, object value = null)
        {
            if (!node.prop1.serializedObject.targetObject || !node.prop2.serializedObject.targetObject)
                return; // Todo 处理同级对象 注意Guid

            // 把修改过的对象赋值给prefab
            object objValue = value == null ? node.prop2.CopyValue() : value;
            if (node.prop1.CanPasteValue(objValue, true))
            {
                node.prop1.PasteValue(objValue, true);
            }
        }

        // 获取Transform组件的层级路径
        private List<string> GetObjectTree(Transform rootTransform, bool includeInactive = true)
        {
            Transform[] objChildTree = rootTransform.GetComponentsInChildren<Transform>(includeInactive);
            List<string> childTree = new List<string>();

            foreach (Transform treeTransform in objChildTree)
            {
                if (treeTransform == rootTransform)
                    continue;

                string path = treeTransform.name;
                Transform parentTransform = treeTransform.parent;

                while (parentTransform != null && parentTransform != rootTransform)
                {
                    path = parentTransform.name + "/" + path;
                    parentTransform = parentTransform.parent;
                }
                childTree.Add(path);
            }
            return childTree;
        }
        private string GetParentTree(Transform childTransform)
        {
            if (childTransform == childTransform.root) return "";
            string parentPath = childTransform.name;
            Transform parentTransform = childTransform.parent;

            while (parentTransform != null && parentTransform != childTransform.root)
            {
                parentPath = parentTransform.name + "/" + parentPath;
                parentTransform = parentTransform.parent;
            }
            return parentPath;
        }
        private void CalcAllDiffNode(Transform aTransform, Transform bTransform, string[] sameChild, bool forceIncludeChild = false)
        {
            List<RootDiffNode> listDiff = new List<RootDiffNode>();
            DiffExtraComponent[] listComp = new DiffExtraComponent[0];
            // Find无法找到自己所以先调用一次
            var (tempNode, tempExtraComp) = CalculateDiff(aTransform.gameObject, bTransform.gameObject, true);
            if (tempNode != null)
                listDiff.AddRange(tempNode);
            if (tempExtraComp != null)
                listComp = listComp.Concat(tempExtraComp).ToArray();
            if (includeChild || forceIncludeChild)
            {
                for (int a = 0; a < sameChild.Length; a++)
                {
                    (tempNode, tempExtraComp) = CalculateDiff(aTransform.Find(sameChild[a]).gameObject, bTransform.Find(sameChild[a]).gameObject, true);
                    if (tempNode != null)
                        listDiff.AddRange(tempNode);
                    if (tempExtraComp != null)
                        listComp = listComp.Concat(tempExtraComp).ToArray();
                }
            }
            rootDiffNodes = listDiff.ToArray();
            diffExtraComponents = listComp.ToArray();
        }
        private void DrawDiffNode(DiffNode node)
        {
            scrollViewRange = new Vector2(scrollPosition.y, scrollPosition.y + position.height);

            // Diff nodes' expandable SerializedProperties should be expanded or collapsed simultaneously
            bool prop1Expanded = node.prop1 != null ? node.prop1.isExpanded : false;
            bool prop2Expanded = node.prop2 != null ? node.prop2.isExpanded : false;
            if (prop1Expanded != prop2Expanded && node.prop1 != null && node.prop2 != null)
                node.prop2.isExpanded = prop2Expanded = prop1Expanded;

            Rect prop1Rect, prop2Rect;
            if (node.type == DiffType.DifferentChildren)
            {
                bool isRootNode = node is RootDiffNode;
                if (!isRootNode)
                {
                    // Highlight the background only if the SerializedProperty isn't expanded (to let the user know that there is a diff inside this DiffNode)
                    if (GetDiffRects(Mathf.Max(EditorGUI.GetPropertyHeight(node.prop1, null, false), EditorGUI.GetPropertyHeight(node.prop2, null, false)), prop1Expanded || prop2Expanded ? Color.clear : (EditorGUIUtility.isProSkin ? DIFFERENT_PROPERTY_COLOR_DARK_SKIN : DIFFERENT_PROPERTY_COLOR_LIGHT_SKIN), out prop1Rect, out prop2Rect))
                    {
                        DrawCopyValueButtons(node, prop1Rect, prop2Rect);

                        EditorGUI.PropertyField(prop1Rect, node.prop1, false);
                        EditorGUI.PropertyField(prop2Rect, node.prop2, false);
                    }
                }

                // Don't draw child nodes if SerializedProperty is collapsed
                if (isRootNode || prop1Expanded)
                {
                    if (!isRootNode)
                        EditorGUI.indentLevel++;

                    DiffNode[] children = node.children;
                    for (int i = 0; i < children.Length; i++)
                    {
                        try
                        {
                            DrawDiffNode(children[i]);
                        }
                        catch (InvalidOperationException)
                        {
                            // A DiffNode's SerializedProperty became invalid (e.g. if it was an array element, that array element is now deleted)
                            // Remove the problematic DiffNode and repaint the window to reflect the changes
                            if (children.Length == 1)
                            {
                                node.type = DiffType.Same;
                                node.children = null;
                            }
                            else
                            {
                                RemoveArrayElement(ref children, i);

                                DiffType? diffType = GetCombinedDiffType(children);
                                if (diffType.HasValue && diffType.Value != DiffType.DifferentChildren)
                                {
                                    // All children have the same diff type, transfer that diff type to this parent diff node
                                    node.type = diffType.Value;
                                    node.children = null;
                                }
                            }

                            EditorApplication.delayCall += Repaint;
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (!isRootNode)
                        EditorGUI.indentLevel--;
                }
            }
            else if (node.type == DiffType.Obj1Extra)
            {
                if (GetDiffRects(EditorGUI.GetPropertyHeight(node.prop1, null, true), Color.clear, out prop1Rect, out prop2Rect))
                {
                    EditorGUI.PropertyField(prop1Rect, node.prop1, true);
                    EditorGUI.DrawRect(prop2Rect, MISSING_PROPERTY_COLOR);
                }
            }
            else if (node.type == DiffType.Obj2Extra)
            {
                if (GetDiffRects(EditorGUI.GetPropertyHeight(node.prop2, null, true), Color.clear, out prop1Rect, out prop2Rect))
                {
                    EditorGUI.DrawRect(prop1Rect, MISSING_PROPERTY_COLOR);
                    EditorGUI.PropertyField(prop2Rect, node.prop2, true);
                }
            }
            else if (showSameValues || node.type != DiffType.Same)
            {
                if (GetDiffRects(Mathf.Max(EditorGUI.GetPropertyHeight(node.prop1, null, true), EditorGUI.GetPropertyHeight(node.prop2, null, true)), node.type == DiffType.Same ? Color.clear : (EditorGUIUtility.isProSkin ? DIFFERENT_PROPERTY_COLOR_DARK_SKIN : DIFFERENT_PROPERTY_COLOR_LIGHT_SKIN), out prop1Rect, out prop2Rect))
                {
                    if (node.type != DiffType.Same)
                        DrawCopyValueButtons(node, prop1Rect, prop2Rect);

                    EditorGUI.PropertyField(prop1Rect, node.prop1, true);
                    EditorGUI.PropertyField(prop2Rect, node.prop2, true);
                }
            }

            // Diff nodes' expandable SerializedProperties should be expanded or collapsed simultaneously
            if (node.prop1 != null && node.prop1.isExpanded != prop1Expanded)
            {
                if (node.prop2 != null)
                    node.prop2.isExpanded = node.prop1.isExpanded;
            }
            else if (node.prop2 != null && node.prop2.isExpanded != prop2Expanded)
            {
                if (node.prop1 != null)
                    node.prop1.isExpanded = prop1Expanded = node.prop2.isExpanded;
            }
        }

        // Draw buttons to copy values from one SerializedProperty to another
        private void DrawCopyValueButtons(DiffNode node, Rect prop1Rect, Rect prop2Rect)
        {
            if (!node.prop1.serializedObject.targetObject || !node.prop2.serializedObject.targetObject)
                return;

            GetCopyValueButtonRects(ref prop1Rect, ref prop2Rect);

            if (GUI.Button(prop1Rect, COPY_TO_OBJ1_BUTTON))
            {
                object obj2Value = node.prop2.CopyValue();
                if (node.prop1.CanPasteValue(obj2Value, true))
                {
                    node.prop1.PasteValue(obj2Value, true);

                    RefreshDiff();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUI.Button(prop2Rect, COPY_TO_OBJ2_BUTTON))
            {
                object obj1Value = node.prop1.CopyValue();
                if (node.prop2.CanPasteValue(obj1Value, true))
                {
                    node.prop2.PasteValue(obj1Value, true);

                    RefreshDiff();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DiffExtraComponentCopyOrDestroyButtonClicked(DiffExtraComponent extraComponent, bool copyOperation)
        {
            if (extraComponent.component)
            {
                if (!copyOperation)
                    Undo.DestroyObjectImmediate(extraComponent.component);
                else if (extraComponent.missingGameObject)
                {
                    if (UnityEditorInternal.ComponentUtility.CopyComponent(extraComponent.component))
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(extraComponent.missingGameObject);
                    else
                        EditorUtility.CopySerialized(extraComponent.component, Undo.AddComponent(extraComponent.missingGameObject, extraComponent.component.GetType()));
                }
            }

            RefreshDiff();
            GUIUtility.ExitGUI();
        }

        // Calculate Rects to draw DiffNodes' SerializedProperties into
        private bool GetDiffRects(float propertyHeight, Color backgroundColor, out Rect prop1Rect, out Rect prop2Rect)
        {
            Rect rect = GetRect(propertyHeight + EditorGUIUtility.standardVerticalSpacing);

            // Cull SerializedProperty if it isn't visible
            if (rect.yMax < scrollViewRange.x || rect.y > scrollViewRange.y)
            {
                prop1Rect = new Rect();
                prop2Rect = new Rect();

                return false;
            }

            float halfWidth = rect.width * 0.5f;

            if (backgroundColor.a > 0f)
                EditorGUI.DrawRect(rect, backgroundColor);

            rect.yMin += EditorGUIUtility.standardVerticalSpacing;
            rect.width = halfWidth - COPY_VALUE_BUTTON_WIDTH - COPY_VALUE_BUTTON_PADDING;
            prop1Rect = rect;

            rect.x += halfWidth + COPY_VALUE_BUTTON_WIDTH + COPY_VALUE_BUTTON_PADDING;
            prop2Rect = rect;

            return true;
        }

        // Calculate Rects to draw copy value buttons for SerializerProperties into
        private void GetCopyValueButtonRects(ref Rect prop1Rect, ref Rect prop2Rect)
        {
            prop1Rect.x = prop1Rect.xMax + COPY_VALUE_BUTTON_PADDING;
            prop2Rect.x = prop2Rect.x - COPY_VALUE_BUTTON_WIDTH - COPY_VALUE_BUTTON_PADDING;
            prop1Rect.width = COPY_VALUE_BUTTON_WIDTH;
            prop2Rect.width = COPY_VALUE_BUTTON_WIDTH;
        }

        private Rect GetRect(float height)
        {
            Rect result = new Rect(scrollViewRect.x, scrollViewRect.yMax, scrollViewRect.width, height);
            scrollViewRect.height += height;

            return result;
        }

        private Rect GetRect(float height, float extraSpace)
        {
            Rect result = new Rect(scrollViewRect.x, scrollViewRect.yMax, scrollViewRect.width, height);
            scrollViewRect.height += height + extraSpace;

            return result;
        }

        private (List<RootDiffNode>, DiffExtraComponent[]) CalculateDiff(Object obj1, Object obj2, bool isManualRefresh)
        {
            if (!obj1 || !obj2 || obj1 == obj2)
                return (null, null);

            double startTime = isManualRefresh ? EditorApplication.timeSinceStartup : 0.0;
            bool calculatingNewDiff = rootDiffNodes == null || rootDiffNodes.Length == 0 || obj1 != rootDiffNodes[0].diffObject1 || obj2 != rootDiffNodes[0].diffObject2;

            List<RootDiffNode> _rootDiffNodes = new List<RootDiffNode>(8);
            DiffExtraComponent[] _diffExtraComponents = new DiffExtraComponent[8];
            _rootDiffNodes.Add(CalculateDiffInternal(obj1, obj2, calculatingNewDiff));

            // 在对两个GameObjects进行比较时，也要对它们的组件进行比较。
            if (obj1 as GameObject && obj2 as GameObject)
            {
                // Get components
                List<Component> components1 = new List<Component>(8);
                List<Component> components2 = new List<Component>(8);

                ((GameObject)obj1).GetComponents(components1);
                ((GameObject)obj2).GetComponents(components2);

                // Remove components with missing scripts from the lists
                for (int i = components1.Count - 1; i >= 0; i--)
                {
                    if (!components1[i])
                        components1.RemoveAt(i);
                }

                for (int i = components2.Count - 1; i >= 0; i--)
                {
                    if (!components2[i])
                        components2.RemoveAt(i);
                }

                // 第一个组件可以是Transform或RectTransform，不管是哪种类型，它们都应该是不同的。
                _rootDiffNodes.Add(CalculateDiffInternal(components1[0], components2[0], calculatingNewDiff));
                components1.RemoveAt(0);
                components2.RemoveAt(0);

                // 只有当组件在两个游戏对象上都存在时，其余的组件才应该被区别对待。
                for (int i = 0; i < components1.Count; i++)
                {
                    Type componentType = components1[i].GetType();
                    for (int j = 0; j < components2.Count; j++)
                    {
                        if (components2[j].GetType() == componentType)
                        {
                            _rootDiffNodes.Add(CalculateDiffInternal(components1[i], components2[j], calculatingNewDiff));
                            components1.RemoveAt(i--);
                            components2.RemoveAt(j);

                            break;
                        }
                    }
                }

                // Store the remaining components (extra components) in an array so that they can be drawn as missing components
                _diffExtraComponents = new DiffExtraComponent[components1.Count + components2.Count];
                for (int i = 0; i < components1.Count; i++)
                    _diffExtraComponents[i] = new DiffExtraComponent() { component = components1[i], missingGameObject = (GameObject)obj2, isComponentInDiffObject1 = true };
                for (int i = 0, offset = components1.Count; i < components2.Count; i++)
                    _diffExtraComponents[i + offset] = new DiffExtraComponent() { component = components2[i], missingGameObject = (GameObject)obj1, isComponentInDiffObject1 = false };
            }
            else
                _diffExtraComponents = null;

            return (_rootDiffNodes, _diffExtraComponents);
        }

        private RootDiffNode CalculateDiffInternal(Object obj1, Object obj2, bool calculatingNewDiff)
        {
            SerializedObject diffedSO1 = new SerializedObject(obj1);
            SerializedObject diffedSO2 = new SerializedObject(obj2);

            List<DiffNode> diffNodes;
            CompareProperties(diffedSO1.EnumerateDirectChildren(), diffedSO2.EnumerateDirectChildren(), diffedSO1, diffedSO2, out diffNodes);

            RootDiffNode rootDiffNode = new RootDiffNode(diffedSO1, diffedSO2, diffNodes.ToArray());
            if (calculatingNewDiff)
                rootDiffNode.SetExpandedState();

            return rootDiffNode;
        }

        private void CompareProperties(IEnumerable<SerializedProperty> properties1, IEnumerable<SerializedProperty> properties2, SerializedObject diffedSO1, SerializedObject diffedSO2, out List<DiffNode> diffNodes)
        {
            diffNodes = new List<DiffNode>(8);
            Dictionary<string, SerializedProperty> childProperties2 = new Dictionary<string, SerializedProperty>(32);
            foreach (SerializedProperty property in properties2)
            {
                string propertyPath = property.propertyPath;
                if (propertyPath != "m_Script")
                    childProperties2[propertyPath] = property.Copy();
            }

            foreach (SerializedProperty childProp1 in properties1)
            {
                string propertyPath = childProp1.propertyPath;
                if (propertyPath == "m_Script")
                    continue;

                SerializedProperty childProp2;
                if (!childProperties2.TryGetValue(propertyPath, out childProp2))
                    diffNodes.Add(new DiffNode(DiffType.Obj1Extra, diffedSO1.FindProperty(propertyPath), null));
                else
                {
                    childProperties2.Remove(propertyPath);

                    if (childProp1.propertyType != SerializedPropertyType.Generic || childProp2.propertyType != SerializedPropertyType.Generic)
                    {
                        object childProp1Value = childProp1.CopyValue();
                        object childProp2Value = childProp2.CopyValue();
                        if ((childProp1Value != null && childProp1Value.Equals(childProp2Value)) || (childProp1Value == null && childProp2Value == null))
                            diffNodes.Add(new DiffNode(DiffType.Same, diffedSO1.FindProperty(propertyPath), diffedSO2.FindProperty(propertyPath)));
                        else
                            diffNodes.Add(new DiffNode(DiffType.Different, diffedSO1.FindProperty(propertyPath), diffedSO2.FindProperty(propertyPath)));
                    }
                    else
                    {
                        List<DiffNode> _diffNodes;
                        if (childProp1.isArray && childProp2.isArray)
                            CompareProperties(EnumerateArrayElements(childProp1.Copy()), EnumerateArrayElements(childProp2.Copy()), diffedSO1, diffedSO2, out _diffNodes);
#if UNITY_2017_1_OR_NEWER
                        else if (childProp1.isFixedBuffer && childProp2.isFixedBuffer)
                            CompareProperties(EnumerateFixedBufferElements(childProp1.Copy()), EnumerateFixedBufferElements(childProp2.Copy()), diffedSO1, diffedSO2, out _diffNodes);
#endif
                        else if (childProp1.hasChildren && childProp2.hasChildren)
                            CompareProperties(childProp1.EnumerateDirectChildren(), childProp2.EnumerateDirectChildren(), diffedSO1, diffedSO2, out _diffNodes);
                        else
                        {
                            diffNodes.Add(new DiffNode(childProp1.hasChildren || childProp2.hasChildren ? DiffType.Different : DiffType.Same, diffedSO1.FindProperty(propertyPath), diffedSO2.FindProperty(propertyPath)));
                            continue;
                        }

                        if (_diffNodes.Count == 0)
                            diffNodes.Add(new DiffNode(DiffType.Same, diffedSO1.FindProperty(propertyPath), diffedSO2.FindProperty(propertyPath)));
                        else
                        {
                            DiffType? diffType = GetCombinedDiffType(_diffNodes);
                            if (!diffType.HasValue || diffType.Value == DiffType.DifferentChildren)
                                diffNodes.Add(new DiffNode(DiffType.DifferentChildren, diffedSO1.FindProperty(propertyPath), diffedSO2.FindProperty(propertyPath)) { children = _diffNodes.ToArray() });
                            else
                            {
                                // If childProp1 and childProp2's diff results are grouped in a single category, replace those results with a single root DiffNode
                                switch (diffType.Value)
                                {
                                    case DiffType.Same: diffNodes.Add(new DiffNode(DiffType.Same, diffedSO1.FindProperty(propertyPath), diffedSO2.FindProperty(propertyPath))); break;
                                    case DiffType.Different:
                                    case DiffType.Obj1Extra:
                                    case DiffType.Obj2Extra: diffNodes.Add(new DiffNode(DiffType.Different, diffedSO1.FindProperty(propertyPath), diffedSO2.FindProperty(propertyPath))); break;
                                }
                            }
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, SerializedProperty> kvPair in childProperties2)
                diffNodes.Add(new DiffNode(DiffType.Obj2Extra, null, diffedSO2.FindProperty(kvPair.Key)));
        }

        private IEnumerable<SerializedProperty> EnumerateArrayElements(SerializedProperty property)
        {
            for (int i = 0, length = property.arraySize; i < length; i++)
                yield return property.GetArrayElementAtIndex(i);
        }

#if UNITY_2017_1_OR_NEWER
        private IEnumerable<SerializedProperty> EnumerateFixedBufferElements(SerializedProperty property)
        {
            for (int i = 0, length = property.fixedBufferSize; i < length; i++)
                yield return property.GetFixedBufferElementAtIndex(i);
        }
#endif

        private DiffType? GetCombinedDiffType(IList<DiffNode> nodes)
        {
            DiffType? diffType = nodes[0].type;
            for (int j = 0; j < nodes.Count; j++)
            {
                if (nodes[j].type != diffType.Value)
                    return null;
            }

            return diffType;
        }

        private void RemoveArrayElement<T>(ref T[] array, int index)
        {
            for (int i = index + 1; i < array.Length; i++)
                array[i - 1] = array[i];

            Array.Resize(ref array, array.Length - 1);
        }
    }
}