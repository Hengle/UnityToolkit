using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder;
using UnityEditor;
using UnityEngine;
using System.Collections;

namespace Tools.PreviewSystem
{
    public partial class PreviewSystem
    {
        private void Plugin_BasicProcess()
        {
            actions.Add("PrintModelInfo", () =>
            {
                PrintModelInfo(targetChara);
                isActionComplete = true;
            });

            actions.Add("CaptureImage", () =>
            {
                previewCamera.transform.position = targetChara.position;
                previewCamera.transform.LookAt(targetChara, Vector3.up);
                Debug.Log($"フォルダー: {config.outputFolder}/Image へのスクリーンショットが開始されました。");

                Shader shader = Shader.Find("UnityLibrary/Effects/Wireframe");
                foreach (var key in allMaterals)
                {
                    key.Key.shader = shader;
                }
                PreviewCaptureProcess(targetChara, "WireFrame");

                foreach (var key in allMaterals)
                {
                    key.Key.shader = key.Value;
                }

                PreviewCaptureProcess(targetChara, "Default");
                isActionComplete = true;
            });

            actions.Add("RecordMovie", () =>
            {
                InitMovieRecorder(targetChara);
                _recorderController.PrepareRecording();
                _recorderController.StartRecording();

                foreach (var setting in _recorderController.Settings.RecorderSettings)
                {
                    Debug.Log($"ファイル: {setting.OutputFile}.mp4 への録画が開始されました。");
                }
                StartCoroutine(PreviewAnimProcess(targetChara));
            });
        }

        /// <summary>
        /// 動画録画初期化
        /// </summary>
        private void InitMovieRecorder(Transform target)
        {
            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _recorderController = new RecorderController(controllerSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = 60.0f;

            RecorderOptions.VerboseMode = false;

            // Video
            var videoRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            videoRecorder.name = "Ruri Video Recorder";
            videoRecorder.Enabled = true;

            videoRecorder.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            videoRecorder.VideoBitRateMode = VideoBitrateMode.High;

            videoRecorder.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = config.outputWidth,
                OutputHeight = config.outputHeight
            };

            //videoRecorder.AudioInputSettings.PreserveAudio = true;
            videoRecorder.OutputFile = Path.Combine(config.outputFolder, target.name, "Preview");

            //前回生成途切れた動画を削除しないとエラーになる
            var path = videoRecorder.OutputFile + ".mp4";
            if (File.Exists(path))
                File.Delete(path);

            // Setup Recording
            controllerSettings.AddRecorderSettings(videoRecorder);
        }
        private void CapturePostProcessTexture(Camera camera)
        {
            var rt = RenderTexture.active;
            Texture2D tex = new Texture2D(config.outputWidth, config.outputHeight, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, config.outputWidth, config.outputHeight), 0, 0);
            tex.Apply();

            rawTexture = tex;
        }
        private void PreviewCaptureProcess(Transform target, string mode)
        {
            if (isPostProcess)
            {
                Camera.onPostRender += CapturePostProcessTexture;
            }
            if (!previewCamera.targetTexture)
            {
                previewCamera.targetTexture = RenderTexture.GetTemporary(config.outputWidth, config.outputHeight, -1);//创建一个RenderTexture对象 
            }
            int indexNum = 0;
            //画像カウンター
            int num = 0;
            Texture2D tex = new Texture2D(config.outputWidth, config.outputHeight, config.outputAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);//新建一个Texture2D对象
            for (int pos = 0; pos < cameraPositions.Length; pos++)
            {
                Vector3 oldPos = previewCamera.transform.position;
                Quaternion oldRot = previewCamera.transform.rotation;
                var vec = cameraPositions[pos].position + target.position;
                vec.z = target.position.z;
                //遠近カメラ座標
                previewCamera.transform.position += cameraPositions[pos].position;

                for (int previewAngle = 0; previewAngle < config.cameraPreviewAngle.Length; previewAngle++)
                {
                    Vector3 oldPos1 = previewCamera.transform.position;
                    Quaternion oldRot1 = previewCamera.transform.rotation;

                    Quaternion oldLightRot1 = previewLight.transform.rotation;
                    //正面俯瞰アオリ回転
                    previewCamera.transform.RotateAround(vec, Vector3.left, config.cameraPreviewAngle[previewAngle]);
                    previewLight.transform.Rotate(Vector3.up * config.cameraPreviewAngle[previewAngle], Space.World);
                    if (cameraOffset != null && cameraOffset.isEnabled)
                    {
                        previewCamera.transform.position = cameraOffset.positions[indexNum].position;
                    }
                    if (lightOffset != null && lightOffset.isEnabled)
                    {
                        previewLight.transform.rotation = lightOffset.transform.transform.rotation;
                    }
                    indexNum++;
                    for (int angle = 0; angle < (360f / config.screenshotIntervalAngle); angle++)
                    {
                        //最終360回転
                        CameraCapture(new Rect(0, 0, config.outputWidth, config.outputHeight), Path.Combine(config.outputFolder, target.name, mode + "Image", num++ + ".png"), ref tex);

                        previewCamera.transform.RotateAround(vec, Vector3.up, config.screenshotIntervalAngle);
                        previewLight.transform.Rotate(Vector3.up * config.screenshotIntervalAngle, Space.World);
                    }
                    previewLight.transform.rotation = oldLightRot1;

                    previewCamera.transform.position = oldPos1;
                    previewCamera.transform.rotation = oldRot1;
                }

                previewCamera.transform.position = oldPos;
                previewCamera.transform.rotation = oldRot;
            }
            DestroyImmediate(tex);
            if (isPostProcess)
            {
                Camera.onPostRender -= CapturePostProcessTexture;
            }
        }
        private IEnumerator PreviewAnimProcess(Transform target)
        {
            RenderTexture.ReleaseTemporary(previewCamera.targetTexture);
            previewCamera.targetTexture = null;
            int indexNum = 0;
            //画像カウンター
            int num = 0;
            for (int pos = 0; pos < cameraPositions.Length; pos++)
            {
                Vector3 oldPos = previewCamera.transform.position;
                Quaternion oldRot = previewCamera.transform.rotation;
                var vec = cameraPositions[pos].position + target.position;
                vec.z = target.position.z;
                //遠近カメラ座標
                previewCamera.transform.position += cameraPositions[pos].position;

                for (int previewAngle = 0; previewAngle < config.cameraPreviewAngle.Length; previewAngle++)
                {

                    Vector3 oldPos1 = previewCamera.transform.position;
                    Quaternion oldRot1 = previewCamera.transform.rotation;

                    Quaternion oldLightRot1 = previewLight.transform.rotation;
                    //正面俯瞰アオリ回転
                    previewCamera.transform.RotateAround(vec, Vector3.left, config.cameraPreviewAngle[previewAngle]);
                    previewLight.transform.Rotate(Vector3.up * config.cameraPreviewAngle[previewAngle], Space.World);
                    if (cameraOffset != null && cameraOffset.isEnabled)
                    {
                        previewCamera.transform.position = cameraOffset.positions[indexNum].position;
                        Debug.Log(previewCamera.transform.position);
                    }
                    if (lightOffset != null && lightOffset.isEnabled)
                    {
                        previewLight.transform.rotation = lightOffset.transform.transform.rotation;
                    }
                    indexNum++;
                    yield return new WaitForSeconds(config.paddingSec);

                    //最終360回転
                    float currentAngle = 0;
                    while (currentAngle < 360f)
                    {
                        float rotAngle = 360f / (config.rotationSec * 60f);
                        previewCamera.transform.RotateAround(vec, Vector3.up, rotAngle);
                        previewLight.transform.Rotate(Vector3.up * rotAngle, Space.World);
                        currentAngle += 1.5f;
                        yield return null;
                    }

                    yield return new WaitForSeconds(config.paddingSec);
                    previewLight.transform.rotation = oldLightRot1;

                    previewCamera.transform.position = oldPos1;
                    previewCamera.transform.rotation = oldRot1;
                }

                previewCamera.transform.position = oldPos;
                previewCamera.transform.rotation = oldRot;
            }
            _recorderController.StopRecording();
            Debug.Log(targetChara.name + "のプレビューが終了しました");
            isActionComplete = true;
        }
        /// <summary>
        /// 对相机拍摄区域进行截图，如果需要多个相机，可类比添加，可截取多个相机的叠加画面
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public void CameraCapture(Rect rect, string fileName, ref Texture2D tex)
        {
            GL.ClearWithSkybox(true, previewCamera);
            previewCamera.Render();//手动开启截图相机的渲染

            RenderTexture.active = previewCamera.targetTexture;//激活RenderTexture

            tex.ReadPixels(rect, 0, 0);//读取像素
            if (config.outputAlpha)
            {
                Color[] colors = tex.GetPixels();
                Color[] rawColors = null;
                if (isPostProcess)
                {
                    rawColors = rawTexture.GetPixels();
                }
                for (int i = 0; i < colors.Length; i++)
                {
                    if (isPostProcess)
                        colors[i].a = rawColors[i].a;
                    if (colors[i].a > 0)
                        colors[i].a = 1;
                }
                tex.SetPixels(colors);
            }
            tex.Apply();//保存像素信息

            RenderTexture.active = null;//关闭RenderTexture的激活状态

            byte[] bytes = tex.EncodeToPNG();//将纹理数据，转化成一个png图片
            if (!Directory.Exists(Path.GetDirectoryName(fileName)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            }
            System.IO.File.WriteAllBytes(fileName, bytes);//写入数据
        }
        public void PrintModelInfo(Transform chara)
        {
            int allMeshActiveCount = 0;
            int allMeshHideCount = 0;
            int allMateralCount = 0;
            var allMateralNames = new List<string>();  // change the key type to string
            var sb = new StringBuilder();
            var meshs = chara.GetComponentsInChildren<MeshFilter>(true);
            if (meshs.Length != 0)
            {
                foreach (var mesh in meshs)
                {
                    int trianglesCount = mesh.sharedMesh.triangles.Length / 3;
                    string meshName = mesh.name;
                    if (mesh.gameObject.activeSelf)
                    {
                        allMeshActiveCount += trianglesCount;
                    }
                    else
                    {
                        allMeshHideCount += trianglesCount;
                        meshName += "_(Hide)";
                    }
                    sb.AppendLine("MeshFilter: " + meshName + " のポリゴン数は: " + trianglesCount);
                }
            }
            var meshs2 = chara.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (meshs2.Length != 0)
            {
                foreach (var mesh in meshs2)
                {
                    int trianglesCount = mesh.sharedMesh.triangles.Length / 3;
                    string meshName = mesh.name;
                    if (mesh.gameObject.activeSelf)
                    {
                        allMeshActiveCount += trianglesCount;
                    }
                    else
                    {
                        allMeshHideCount += trianglesCount;
                        meshName += "_(Hide)";
                    }
                    sb.AppendLine("SkinnedMeshRenderer: " + meshName + " のポリゴン数は: " + trianglesCount);
                    //  sharedMaterials是项目材质的实际引用 不能做任何修改!
                    for (int i = 0; i < mesh.sharedMaterials.Length; i++)
                    {
                        var mat = mesh.sharedMaterials[i];
                        if (!allMaterals.ContainsKey(mat))
                        {
                            allMaterals.Add(mat, mat.shader);
                        }
                        if (!allMateralNames.Contains(mat.name))
                        {
                            allMateralNames.Add(mat.name);
                            allMateralCount++;  // Only increase the count if the material is not in the dictionary
                        }
                    }
                }
            }

            sb.AppendLine("すべてのポリゴン数は: " + (allMeshActiveCount + allMeshHideCount));
            sb.AppendLine("すべてのポリゴン数(隠しオブジェクト除く)は: " + (allMeshActiveCount + allMeshHideCount - allMeshHideCount));
            sb.AppendLine("すべてのポリゴン数(隠しオブジェクトのみ)は: " + allMeshHideCount);
            sb.AppendLine("すべてのマテリアル数は: " + allMateralCount);
            sb.AppendLine("すべてのマテリアル(重複無し)数は: " + allMaterals.Count);

            string filePath = Path.Combine(config.outputFolder, chara.name, "ModelInfo.txt");
            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            File.WriteAllText(filePath, sb.ToString());
        }
    }
}