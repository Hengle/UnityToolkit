using System.IO;
using VRM;
using System.Collections;
using UnityEngine;
using System;

namespace Tools.PreviewSystem
{
    public partial class PreviewSystem
    {
        public int fontSize = 100;

        private VRMBlendShapeProxy blendShapeProxy;
        private BlendShapeAvatar blendShapeAvatar;
        private string vrmTitle;
        private string currentClipName = "";

        private void Plugin_VRMProcess()
        {
            actions.Add("LoopBlendShape", () =>
            {
                StartCoroutine(BlendShapeProcess());
            });

            onGuiActions.Add("VRMTextDraw", () =>
            {
                if (!string.IsNullOrEmpty(currentClipName))
                {
                    GUIStyle style = new GUIStyle();
                    style.fontSize = fontSize; // Use the custom font size
                    GUI.Label(new Rect(10, 10, 100, 20), currentClipName, style);
                }
            });
        }

        private IEnumerator BlendShapeProcess()
        {
            blendShapeProxy = targetChara.GetComponent<VRMBlendShapeProxy>();
            if (!blendShapeProxy) yield break;
            var meta = targetChara.GetComponent<VRMMeta>();
            vrmTitle = meta.Meta.Title;
            blendShapeAvatar = blendShapeProxy.BlendShapeAvatar;

            Vector3 oldPos = previewCamera.transform.position;
            previewCamera.transform.position = cameraPositions[0].position;
            Debug.Log(cameraPositions[0].position);
            string savePath = Path.Combine(config.outputFolder, targetChara.name, "BlendShape");
            // 检查路径是否存在，如果不存在则创建文件夹
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            foreach (var blendShapeClip in blendShapeAvatar.Clips)
            {
                string clipName = blendShapeClip.name;
                int dotIndex = clipName.IndexOf('.');
                clipName = clipName.Substring(dotIndex + 1);

                BlendShapeKey key = BlendShapeKey.CreateFromClip(blendShapeClip);
                blendShapeProxy.ImmediatelySetValue(key, 1f);
                yield return new WaitForEndOfFrame();

                currentClipName = clipName;
                yield return new WaitForEndOfFrame(); // Wait for OnGUI to finish drawing
                var screenshotName = vrmTitle + "_" + clipName + ".png";
                ScreenCapture.CaptureScreenshot(Path.Combine(savePath, screenshotName), 2);

                blendShapeProxy.ImmediatelySetValue(key, 0f);
                yield return new WaitForEndOfFrame();
            }
            previewCamera.transform.position = oldPos;
            currentClipName = "";
            isActionComplete = true;
        }
    }
}