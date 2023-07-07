using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;
using UnityEngine.Rendering;
using static PreviewSystemConfig;

namespace Tools.PreviewSystem
{
    [Serializable]
    public partial class PreviewSystem : MonoBehaviour
    {
        private RecorderController _recorderController;

        public PreviewSystemConfig config;

        public Transform targetChara;

        public Light previewLight;

        public Camera previewCamera;

        private Dictionary<Material, Shader> allMaterals = new Dictionary<Material, Shader>();

        /// <summary>
        /// カメラからプレイヤーの距離
        /// </summary>
        public Transform[] cameraPositions;

        public CameraOffset cameraOffset = new CameraOffset();
        public LightOffset lightOffset = new LightOffset();

        private Texture2D rawTexture;
        private bool isPostProcess;

        private Dictionary<string, Action> onGuiActions = new Dictionary<string, Action>();
        private Dictionary<string, Action> actions = new Dictionary<string, Action>();
        private List<Action> pluginMethods = new List<Action>();
        private bool isActionComplete = false;

        public const string PREVIEWLIGHT = "PreviewLight";
        public const string DIRECTIONALLIGHT = "Directional Light";

        private void Awake()
        {
            if (!config.isEnbale) return;

            FindAddMethods();
            StartProcess();
        }
        private void FindAddMethods()
        {
            Type type = typeof(PreviewSystem);
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (MethodInfo method in methods)
            {
                if (method.Name.StartsWith("Plugin_"))
                {
                    pluginMethods.Add(() => method.Invoke(this, null));
                }
            }
        }
        public void StartProcess()
        {
            if (!targetChara)
            {
                UnityEditor.EditorApplication.isPlaying = false;
                return;
            }
            ParameterCheck();

            foreach (var pluginAction in pluginMethods)
            {
                pluginAction();
            }

            StartCoroutine(TaskProcess());
        }
        private void ParameterCheck()
        {
#if UNITY_EDITOR
            Selection.activeGameObject = null;
#endif
            previewLight.enabled = true;
            var directLightGameObject = GameObject.Find(DIRECTIONALLIGHT);
            if (directLightGameObject)
            {
                directLightGameObject.GetComponent<Light>().enabled = false;
            }

            if (config.outputAlpha)
            {
                // URP处理
                CommandBuffer cbBefore = new CommandBuffer();
                CommandBuffer cbAfter = new CommandBuffer();
                RenderTexture rtBeforePostProcessing = RenderTexture.GetTemporary(config.outputWidth, config.outputHeight, -1);
                RenderTexture rtAfterPostProcessing = RenderTexture.GetTemporary(config.outputWidth, config.outputHeight, -1);

                cbBefore.Blit(BuiltinRenderTextureType.CameraTarget, rtBeforePostProcessing);
                previewCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, cbBefore);

                cbAfter.Blit(BuiltinRenderTextureType.CameraTarget, rtAfterPostProcessing);
                previewCamera.AddCommandBuffer(CameraEvent.BeforeImageEffects, cbAfter);

                previewCamera.Render();

                previewCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, cbBefore);
                previewCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, cbAfter);

                // 检查两个RenderTexture的像素是否相同
                var rect = new Rect(0, 0, config.outputWidth, config.outputHeight);
                var texBefore = new Texture2D(config.outputWidth, config.outputHeight, TextureFormat.RGBA32, false);
                var texAfter = new Texture2D(config.outputWidth, config.outputHeight, TextureFormat.RGBA32, false);

                RenderTexture.active = rtBeforePostProcessing;
                texBefore.ReadPixels(rect, 0, 0);
                texBefore.Apply();

                RenderTexture.active = rtAfterPostProcessing;
                texAfter.ReadPixels(rect, 0, 0);
                texAfter.Apply();

                var centerX = config.outputWidth / 2;
                var centerY = config.outputHeight / 2;
                var centerPixelIndex = centerX + centerY * config.outputWidth;

                var postProcessTexturePixel = texBefore.GetPixels32()[centerPixelIndex]; // 获取最中间像素的颜色
                var renderTexturePixel = texAfter.GetPixels32()[centerPixelIndex]; // 获取最中间像素的颜色
                if (postProcessTexturePixel.r + postProcessTexturePixel.g + postProcessTexturePixel.b != renderTexturePixel.r + renderTexturePixel.g + renderTexturePixel.b)
                {
                    isPostProcess = true;
                }

                RenderTexture.ReleaseTemporary(rtBeforePostProcessing);
                RenderTexture.ReleaseTemporary(rtAfterPostProcessing);

                RenderTexture.active = null;
            }
        }
        IEnumerator TaskProcess()
        {
            // 遍历字典的键和值
            foreach (KeyValuePair<string, Action> pair in actions)
            {
                Console.WriteLine("开始处理: " + pair.Key);
                isActionComplete = false;
                pair.Value();
                yield return new WaitUntil(() => isActionComplete);
            }
        }

        void OnGUI()
        {
            foreach (KeyValuePair<string, Action> pair in onGuiActions)
            {
                Console.WriteLine("开始处理GUI: " + pair.Key);
                pair.Value();
            }
        }
        private void OnDisable()
        {
            if (_recorderController != null)
            {
                _recorderController.StopRecording();
            }
        }
    }
}
