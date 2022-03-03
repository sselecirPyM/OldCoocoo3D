using Coocoo3D.Core;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Coocoo3D.Components;
using Coocoo3D.Utility;

namespace Coocoo3D.UI
{
    static class UIImGui
    {
        public static void GUI(Coocoo3DMain appBody)
        {
            if (!initialized)
            {
                InitTex(appBody);
                InitKeyMap();
                initialized = true;
            }
            var io = ImGui.GetIO();
            Vector2 mouseMoveDelta = new Vector2();
            while (ImguiInput.mouseMoveDelta.TryDequeue(out var moveDelta))
            {
                mouseMoveDelta += moveDelta;
            }

            var context = appBody.RPContext;
            io.DisplaySize = new Vector2(context.screenSize.X, context.screenSize.Y);
            io.DeltaTime = (float)context.dynamicContextRead.RealDeltaTime;
            Present.GameObject selectedObject = null;
            LightingComponent lightingComponent = null;
            VolumeComponent volumeComponent = null;
            MMDRendererComponent renderer = null;
            if (appBody.SelectedGameObjects.Count == 1)
            {
                selectedObject = appBody.SelectedGameObjects[0];
                position = selectedObject.PositionNextFrame;
                if (rotationCache != selectedObject.RotationNextFrame)
                {
                    rotation = QuaternionToEularYXZ(selectedObject.RotationNextFrame);
                    rotationCache = selectedObject.RotationNextFrame;
                }
                lightingComponent = selectedObject.GetComponent<LightingComponent>();
                volumeComponent = selectedObject.GetComponent<VolumeComponent>();
                renderer = selectedObject.GetComponent<MMDRendererComponent>();
            }


            ImGui.NewFrame();


            if (demoWindowOpen)
                ImGui.ShowDemoWindow(ref demoWindowOpen);

            DockSpace(appBody);
            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("常用"))
            {
                Common(appBody);
            }
            ImGui.End();
            ImGui.SetNextWindowSize(new Vector2(500, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("资源"))
            {
                var _openRequest = Resources(appBody);
                if (openRequest == null)
                    openRequest = _openRequest;
            }
            ImGui.End();
            ImGui.SetNextWindowPos(new Vector2(800, 0), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("设置"))
            {
                SettingsPanel(appBody);
            }
            ImGui.End();
            ImGui.SetNextWindowSize(new Vector2(350, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(750, 0), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("场景层级"))
            {
                SceneHierarchy(appBody);
            }
            ImGui.End();
            int d = 0;
            foreach (var visualChannel in appBody.RPContext.visualChannels.Values)
            {
                ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(new Vector2(300 + d, 0), ImGuiCond.FirstUseEver);
                if (visualChannel.Name != "main")
                {
                    bool open = true;
                    if (ImGui.Begin(string.Format("场景视图 - {0}###SceneView/{0}", visualChannel.Name), ref open))
                    {
                        SceneView(appBody, visualChannel, io.MouseWheel, mouseMoveDelta);
                    }
                    if (!open)
                    {
                        context.DelayRemoveVisualChannel(visualChannel.Name);
                    }
                }
                else
                {
                    if (ImGui.Begin(string.Format("场景视图 - {0}###SceneView/{0}", visualChannel.Name)))
                    {
                        SceneView(appBody, visualChannel, io.MouseWheel, mouseMoveDelta);
                    }
                }
                ImGui.End();
                d += 50;
            }
            ImGui.SetNextWindowSize(new Vector2(300, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(0, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("物体"))
            {
                if (selectedObject != null)
                {
                    ImGui.InputText("名称", ref selectedObject.Name, 256);
                    if (ImGui.TreeNode("描述"))
                    {
                        ImGui.Text(selectedObject.Description);
                        if (renderer != null)
                            ImGui.Text(string.Format("顶点数：{0} 索引数：{1} 材质数：{2}\n模型文件：{3}\n动作文件：{4}",
                                renderer.meshVertexCount, renderer.meshIndexCount, renderer.Materials.Count,
                                renderer.meshPath, renderer.motionPath));

                        ImGui.TreePop();
                    }
                    if (ImGui.TreeNode("transform"))
                    {
                        ImGui.DragFloat3("位置", ref position, 0.1f);
                        Vector3 a = rotation / MathF.PI * 180;
                        rotationChange = ImGui.DragFloat3("旋转", ref a);
                        if (rotationChange) rotation = a * MathF.PI / 180;
                        ImGui.TreePop();
                    }
                    if (renderer != null)
                    {
                        RendererComponent(appBody, renderer);
                    }
                    if (lightingComponent != null && ImGui.TreeNode("光照"))
                    {
                        int current = (int)lightingComponent.LightingType;
                        ImGui.ColorEdit3("颜色", ref lightingComponent.Color, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float);
                        ImGui.DragFloat("范围", ref lightingComponent.Range);
                        ImGui.Combo("类型", ref current, lightTypeString, 2);
                        ImGui.TreePop();
                        lightingComponent.LightingType = (Present.LightingType)current;
                    }
                    if (volumeComponent != null && ImGui.TreeNode("体积"))
                    {
                        ImGui.DragFloat3("尺寸", ref volumeComponent.Size);
                        ImGui.TreePop();
                    }
                }
            }
            ImGui.End();
            Popups(appBody);
            ImGui.Render();
            if (selectedObject != null)
            {
                selectedObject.PositionNextFrame = position;
                if (rotationChange)
                {
                    rotationCache = selectedObject.RotationNextFrame = Quaternion.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z);
                }
            }

        }

        static void Common(Coocoo3DMain appBody)
        {
            var camera = appBody.RPContext.currentChannel.camera;
            if (ImGui.TreeNode("transform"))
            {
                ImGui.DragFloat3("位置", ref position, 0.1f);
                Vector3 a = rotation / MathF.PI * 180;
                rotationChange = ImGui.DragFloat3("旋转", ref a);
                if (rotationChange) rotation = a * MathF.PI / 180;
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("相机"))
            {
                ImGui.DragFloat("距离", ref camera.Distance);
                ImGui.DragFloat3("焦点", ref camera.LookAtPoint, 0.05f);
                Vector3 a = camera.Angle / MathF.PI * 180;
                if (ImGui.DragFloat3("角度", ref a))
                    camera.Angle = a * MathF.PI / 180;
                float fov = camera.Fov / MathF.PI * 180;
                if (ImGui.DragFloat("FOV", ref fov, 0.5f, 0.1f, 179.9f))
                    camera.Fov = fov * MathF.PI / 180;
                ImGui.DragFloat("近裁剪", ref camera.nearClip, 0.5f, 0.1f, float.MaxValue);
                ImGui.DragFloat("远裁剪", ref camera.farClip, 10.0f, 0.1f, float.MaxValue);

                ImGui.Checkbox("使用镜头运动文件", ref camera.CameraMotionOn);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("录制"))
            {
                ImGui.DragFloat("开始时间", ref appBody.GameDriverContext.recordSettings.StartTime);
                ImGui.DragFloat("结束时间", ref appBody.GameDriverContext.recordSettings.StopTime);
                ImGui.DragInt("宽度", ref appBody.GameDriverContext.recordSettings.Width, 32, 32, 16384);
                ImGui.DragInt("高度", ref appBody.GameDriverContext.recordSettings.Height, 8, 8, 16384);
                ImGui.DragFloat("FPS", ref appBody.GameDriverContext.recordSettings.FPS, 1, 1, 1000);
                if (ImGui.Button("开始录制"))
                {
                    requireRecord = true;
                }
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("帮助"))
            {
                Help();
                ImGui.TreePop();
            }
            if (ImGui.Button("播放"))
            {
                PlayControl.Play(appBody);
            }
            ImGui.SameLine();
            if (ImGui.Button("暂停"))
            {
                PlayControl.Pause(appBody);
            }
            ImGui.SameLine();
            if (ImGui.Button("停止"))
            {
                PlayControl.Stop(appBody);
            }
            if (ImGui.Button("跳到最前"))
            {
                PlayControl.Front(appBody);
            }
            ImGui.SameLine();
            if (ImGui.Button("重置物理"))
            {
                appBody.RPContext.gameDriverContext.RequireResetPhysics = true;
            }
            if (ImGui.Button("快进"))
            {
                PlayControl.FastForward(appBody);
            }
            ImGui.Text("Fps:" + appBody.framePerSecond);
        }

        static void SettingsPanel(Coocoo3DMain appBody)
        {
            ImGui.Checkbox("垂直同步", ref appBody.performanceSettings.VSync);
            ImGui.Checkbox("节省CPU", ref appBody.performanceSettings.SaveCpuPower);
            ImGui.Checkbox("多线程渲染", ref appBody.performanceSettings.MultiThreadRendering);
            float a = (float)(1.0 / Math.Clamp(appBody.GameDriverContext.FrameInterval, 1e-4, 1));
            if (ImGui.DragFloat("帧率限制", ref a, 10, 1, 5000))
            {
                if (a == a)
                    appBody.GameDriverContext.FrameInterval = 1 / a;
            }
            var scene = appBody.CurrentScene;
            ref Settings settings = ref scene.settings;
            ImGui.Checkbox("线框", ref settings.Wireframe);
            ImGui.DragInt("阴影分辨率", ref settings.ShadowMapResolution, 128, 512, 8192);
            ImGui.SliderInt("天空盒最高质量", ref settings.SkyBoxMaxQuality, 256, 2048);//大于1512时fp16下会有可观测的精度损失(亮度降低)

            ComboBox("调试渲染", ref scene.settings.DebugRenderType);

            var currentPassSetting = appBody.RPContext.dynamicContextRead.currentPassSetting;
            ShowParams(currentPassSetting.ShowSettingParameters, settings.Parameters, settings.ParametersModifyIndex);
            ShowTextures(appBody, "settings", currentPassSetting.ShowSettingTextures, settings.textures);

            if (appBody.mainCaches.PassSettings.Count != renderPipelines.Length)
            {
                renderPipelines = new string[appBody.mainCaches.PassSettings.Count];
                renderPipelineKeys = new string[appBody.mainCaches.PassSettings.Count];
            }
            int _i = 0;
            foreach (var pair in appBody.mainCaches.PassSettings)
            {
                renderPipelines[_i] = pair.Value.Name;
                renderPipelineKeys[_i] = pair.Key;
                _i++;
            }
            for (int i = 0; i < renderPipelineKeys.Length; i++)
            {
                if (renderPipelineKeys[i] == appBody.RPContext.currentPassSetting1)
                    renderPipelineIndex = i;
            }
            if (ImGui.Combo("渲染管线", ref renderPipelineIndex, renderPipelines, renderPipelines.Length))
            {
                appBody.RPContext.currentPassSetting1 = renderPipelineKeys[renderPipelineIndex];
            }
            if (ImGui.Button("添加视口"))
            {
                int c = 1;
                while (true)
                {
                    if (!appBody.RPContext.visualChannels.ContainsKey(c.ToString()))
                    {
                        appBody.RPContext.DelayAddVisualChannel(c.ToString());
                        break;
                    }
                    c++;
                }
            }
            if (ImGui.Button("保存场景"))
            {
                requireSave = true;
            }
            if (ImGui.Button("重新加载纹理"))
            {
                appBody.mainCaches.ReloadTextures1 = true;
            }
            if (ImGui.Button("重新加载Shader"))
            {
                appBody.mainCaches.ReloadShaders = true;
            }
        }

        static void ShowParams(Dictionary<string, RenderPipeline.PassParameter> showParams, Dictionary<string, object> values, Dictionary<string, int> modifyIndexs = null)
        {
            if (showParams != null)
            {
                bool tempB;
                float tempF;
                Vector2 tempF2;
                Vector3 tempF3;
                Vector4 tempF4;
                int tempI;
                foreach (var param in showParams)
                {
                    var val = param.Value;
                    if (val.IsHidden) continue;
                    switch (val.Type)
                    {
                        case "bool":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is bool x1)
                                    tempB = x1;
                                else
                                    tempB = (bool)val.defaultValue;
                                if (ImGui.Checkbox(val.Name, ref tempB))
                                {
                                    values[param.Key] = tempB;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "int":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is int x1)
                                    tempI = x1;
                                else
                                    tempI = (int)val.defaultValue;

                                if (ImGui.DragInt(val.Name, ref tempI, 1, (int)val.minValue, (int)val.maxValue, val.Format))
                                {
                                    values[param.Key] = tempI;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "sliderInt":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is int x1)
                                    tempI = x1;
                                else
                                    tempI = (int)val.defaultValue;
                                if (ImGui.SliderInt(val.Name, ref tempI, (int)val.minValue, (int)val.maxValue, val.Format))
                                {
                                    values[param.Key] = tempI;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "float":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is float x1)
                                    tempF = x1;
                                else
                                    tempF = (float)val.defaultValue;
                                if (ImGui.DragFloat(val.Name, ref tempF, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                                {
                                    values[param.Key] = tempF;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "sliderFloat":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is float x1)
                                    tempF = x1;
                                else
                                    tempF = (float)val.defaultValue;
                                if (ImGui.SliderFloat(val.Name, ref tempF, (float)val.minValue, (float)val.maxValue, val.Format))
                                {
                                    values[param.Key] = tempF;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "float2":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is Vector2 x1)
                                    tempF2 = x1;
                                else
                                    tempF2 = (Vector2)val.defaultValue;
                                if (ImGui.DragFloat2(val.Name, ref tempF2, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                                {
                                    values[param.Key] = tempF2;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "float3":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is Vector3 x1)
                                    tempF3 = x1;
                                else
                                    tempF3 = (Vector3)val.defaultValue;
                                if (ImGui.DragFloat3(val.Name, ref tempF3, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                                {
                                    values[param.Key] = tempF3;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "float4":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is Vector4 x1)
                                    tempF4 = x1;
                                else
                                    tempF4 = (Vector4)val.defaultValue;
                                if (ImGui.DragFloat4(val.Name, ref tempF4, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                                {
                                    values[param.Key] = tempF4;
                                    if (modifyIndexs != null)
                                    {
                                        modifyIndexs.Increase(param.Key);
                                        if (modifyIndexs != null)
                                            modifyIndexs.Increase(param.Key);
                                    }
                                }
                            }
                            break;
                        case "color3":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is Vector3 x1)
                                    tempF3 = x1;
                                else
                                    tempF3 = (Vector3)val.defaultValue;
                                if (ImGui.ColorEdit3(val.Name, ref tempF3, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.HDR))
                                {
                                    values[param.Key] = tempF3;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                        case "color4":
                            {
                                if (values.TryGetValue(param.Key, out object obj1) && obj1 is Vector4 x1)
                                    tempF4 = x1;
                                else
                                    tempF4 = (Vector4)val.defaultValue;
                                if (ImGui.ColorEdit4(val.Name, ref tempF4, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.HDR))
                                {
                                    values[param.Key] = tempF4;
                                    if (modifyIndexs != null)
                                        modifyIndexs.Increase(param.Key);
                                }
                            }
                            break;
                    }
                    if (val.Tooltip != null && ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text(val.Tooltip);
                        ImGui.EndTooltip();
                    }
                }
            }
        }

        static void ShowTextures(Coocoo3DMain appBody, string id, Dictionary<string, string> showTextures, Dictionary<string, string> textures)
        {
            if (showTextures != null)
                foreach (var texSlot in showTextures)
                {
                    string key = "imgui/" + texSlot.Key;
                    Vector2 imageSize = new Vector2(120, 120);
                    IntPtr imageId = appBody.mainCaches.GetPtr(key);
                    ImGui.Text(texSlot.Key);
                    if (ImGui.ImageButton(imageId, imageSize))
                    {
                        requireOpenSelectResource = true;
                        stringValues["fileOpen"] = id;
                        stringValues["material"] = texSlot.Key;
                    }
                    if (textures.TryGetValue(texSlot.Key, out var texture0) && appBody.mainCaches.TryGetTexture(texture0, out var texture))
                    {
                        appBody.mainCaches.SetTexture(key, texture);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text(texture0);
                            ImGui.EndTooltip();
                        }
                    }
                    else
                    {
                        appBody.mainCaches.SetTexture(key, null);
                    }
                }
            if (filePropSelect != null && stringValues.GetOrCreate("fileOpen", (string a) => "") == id)
            {
                stringValues["fileOpen"] = "";
                appBody.mainCaches.Texture(filePropSelect);
                textures[stringValues["material"]] = filePropSelect;
                filePropSelect = null;
            }
        }

        static void DockSpace(Coocoo3DMain appBody)
        {
            ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBackground;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            var viewPort = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(viewPort.Size, ImGuiCond.Always);
            ImGui.SetNextWindowViewport(viewPort.ID);

            if (ImGui.Begin("Dockspace", window_flags))
            {
                string texName = appBody.RPContext.visualChannels.FirstOrDefault().Value.GetTexName("Output");
                ImGuiDockNodeFlags dockNodeFlag = ImGuiDockNodeFlags.PassthruCentralNode;
                IntPtr imageId = appBody.mainCaches.GetPtr(texName);
                ImGui.GetWindowDrawList().AddImage(imageId, viewPort.WorkPos, viewPort.WorkPos + viewPort.WorkSize);
                ImGui.DockSpace(ImGui.GetID("MyDockSpace"), Vector2.Zero, dockNodeFlag);
            }
            ImGui.End();
            ImGui.PopStyleVar(3);
        }

        static _openRequest Resources(Coocoo3DMain appBody)
        {
            if (ImGui.Button("打开文件夹"))
            {
                requireOpenFolder = true;
            }
            if (ImGui.Button("刷新"))
            {
                viewRequest = currentFolder;
            }
            ImGui.SameLine();
            if (ImGui.Button("后退"))
            {
                if (viewStack.Count > 0)
                    viewRequest = viewStack.Pop();
            }
            ImGui.BeginChild("资源");
            _openRequest open1 = null;
            lock (storageItems)
            {
                bool _requireClear = false;
                foreach (var item in storageItems)
                {
                    if (ImGui.Selectable(item.Name, false, ImGuiSelectableFlags.AllowDoubleClick) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (item is DirectoryInfo folder)
                        {
                            viewStack.Push(currentFolder);
                            viewRequest = folder;
                            _requireClear = true;
                        }
                        else if (item is FileInfo file)
                        {
                            var requireOpen1 = new _openRequest();
                            requireOpen1.file = file;
                            requireOpen1.folder = currentFolder;
                            open1 = requireOpen1;
                        }
                    }
                }
                if (_requireClear)
                    storageItems.Clear();
            }
            ImGui.EndChild();
            return open1;
        }

        static void Help()
        {
            if (ImGui.TreeNode("基本操作"))
            {
                ImGui.Text(@"旋转视角 - 按住鼠标右键拖动
平移镜头 - 按住鼠标中键拖动
拉近、拉远镜头 - 鼠标滚轮
修改物体位置、旋转 - 双击修改，或者在数字上按住左键然后拖动");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("支持格式"))
            {
                ImGui.Text(@"当前版本支持pmx格式模型，
vmd格式动作");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("编写着色器"))
            {
                ImGui.TextWrapped(@"首先把Samples文件夹里的内容，复制粘贴到硬盘上的其他位置，然后开始修改。
双击.coocoox文件来加载。
点击设置里的重新加载着色器来重新加载。
文件的格式每个版本都可能有些许改动。
");
                ImGui.TreePop();
            }
            if (ImGui.Button("显示ImGuiDemoWindow"))
            {
                demoWindowOpen = true;
            }
        }

        static void SceneHierarchy(Coocoo3DMain appBody)
        {
            if (ImGui.Button("新光源"))
            {
                UISharedCode.NewLighting(appBody);
            }
            ImGui.SameLine();
            //if (ImGui.Button("新体积"))
            //{
            //    UISharedCode.NewVolume(appBody);
            //}
            //ImGui.SameLine();
            if (ImGui.Button("移除物体"))
            {
                foreach (var gameObject in appBody.SelectedGameObjects)
                    appBody.CurrentScene.RemoveGameObject(gameObject);
            }
            //while (gameObjectSelected.Count < appBody.CurrentScene.gameObjects.Count)
            //{
            //    gameObjectSelected.Add(false);
            //}
            var gameObjects = appBody.CurrentScene.gameObjects;
            for (int i = 0; i < gameObjects.Count; i++)
            {
                Present.GameObject gameObject = gameObjects[i];
                bool selected = gameObjectSelectIndex == i;
                ImGui.Selectable(gameObject.Name + "###" + gameObject.GetHashCode(), ref selected);
                if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                {
                    int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                    if (n_next >= 0 && n_next < gameObjects.Count)
                    {
                        gameObjects[i] = gameObjects[n_next];
                        gameObjects[n_next] = gameObject;
                        ImGui.ResetMouseDragDelta();
                    }
                }
                if (selected && (appBody.SelectedGameObjects.Count < 1 || appBody.SelectedGameObjects[0] != gameObject))
                {
                    gameObjectSelectIndex = i;
                    //lock (appBody.SelectedGameObjects)
                    //{
                    appBody.SelectedGameObjects.Clear();
                    appBody.SelectedGameObjects.Add(gameObject);
                    //}
                }
            }
        }

        static void RendererComponent(Coocoo3DMain appBody, MMDRendererComponent rendererComponent)
        {
            var io = ImGui.GetIO();
            if (ImGui.TreeNode("材质"))
            {
                if (ImGui.BeginChild("materials", new Vector2(120, 400)))
                {
                    ImGui.PushItemWidth(120);
                    for (int i = 0; i < rendererComponent.Materials.Count; i++)
                    {
                        RuntimeMaterial material = rendererComponent.Materials[i];
                        bool selected = i == materialSelectIndex;
                        ImGui.Selectable(string.Format("{0}##{1}", material.Name, i), ref selected);
                        if (selected) materialSelectIndex = i;
                    }
                    ImGui.PopItemWidth();
                }
                ImGui.EndChild();
                ImGui.SameLine();
                if (ImGui.BeginChild("materialProperty", new Vector2(180, 400)))
                {
                    if (materialSelectIndex >= 0 && materialSelectIndex < rendererComponent.Materials.Count)
                    {
                        var material = rendererComponent.Materials[materialSelectIndex];
                        ImGui.Text(material.Name);

                        //ImGui.Checkbox("蒙皮", ref material.Skinning);

                        ImGui.Checkbox("透明材质", ref material.Transparent);
                        var currentPassSetting = appBody.RPContext.dynamicContextRead.currentPassSetting;
                        ShowParams(currentPassSetting.ShowParameters, material.Parameters);
                        ShowTextures(appBody, "material", currentPassSetting.ShowTextures, material.textures);
                    }
                }
                ImGui.EndChild();
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("变形"))
            {
                ImGui.Checkbox("蒙皮", ref rendererComponent.skinning);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("关闭蒙皮可以提高性能");
                ImGui.Checkbox("锁定动作", ref rendererComponent.LockMotion);
                if (rendererComponent.LockMotion)
                    for (int i = 0; i < rendererComponent.morphStateComponent.morphs.Count; i++)
                    {
                        MorphDesc morpth = rendererComponent.morphStateComponent.morphs[i];
                        if (ImGui.SliderFloat(morpth.Name, ref rendererComponent.morphStateComponent.Weights.Origin[i], 0, 1))
                        {
                            appBody.GameDriverContext.RequireResetPhysics = true;
                        }
                    }
                ImGui.TreePop();
            }
        }

        static void SceneView(Coocoo3DMain appBody, RenderPipeline.VisualChannel channel, float mouseWheelDelta, Vector2 mouseMoveDelta)
        {
            var io = ImGui.GetIO();
            IntPtr imageId = appBody.mainCaches.GetPtr(channel.GetTexName("Output"));
            Vector2 pos = ImGui.GetCursorScreenPos();
            var tex = appBody.mainCaches.GetTexture(imageId);
            Vector2 spaceSize = Vector2.Max(ImGui.GetWindowSize() - new Vector2(20, 40), new Vector2(100, 100));
            channel.sceneViewSize = new Numerics.Int2((int)spaceSize.X, (int)spaceSize.Y);
            Vector2 texSize = new Vector2(tex.width, tex.height);
            float factor = MathF.Max(MathF.Min(spaceSize.X / texSize.X, spaceSize.Y / texSize.Y), 0.01f);
            Vector2 imageSize = texSize * factor;


            ImGui.InvisibleButton("X", imageSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddImage(imageId, pos, pos + imageSize);
            var vpMatrix = channel.cameraData.vpMatrix;
            ImGui.PushClipRect(pos, pos + imageSize, true);
            foreach (var light in appBody.RPContext.dynamicContextRead.pointLights)
            {
                Vector4 p1 = Vector4.Transform(new Vector4(light.Position, 1), vpMatrix);
                p1 /= p1.W;
                p1.Y = -p1.Y;
                Vector2 basePos = pos + (new Vector2(p1.X, p1.Y) * 0.5f + new Vector2(0.5f, 0.5f)) * imageSize;
                if (p1.X > -1 && p1.X < 1 && p1.Y > -1 && p1.Y < 1)
                    drawList.AddTriangle(basePos + new Vector2(-0.8660254f, -0.5f) * 10, basePos + new Vector2(0, 1) * 10, basePos + new Vector2(0.8660254f, -0.5f) * 10, 0xffffffff, 2);
            }
            int hoveredIndex = -1;
            string toolTipMessage = "";
            Vector2 mousePos = ImGui.GetMousePos();
            for (int i = 0; i < appBody.RPContext.dynamicContextRead.gameObjects.Count; i++)
            {
                Present.GameObject obj = appBody.RPContext.dynamicContextRead.gameObjects[i];
                Vector4 p1 = Vector4.Transform(new Vector4(obj.Position, 1), vpMatrix);
                p1 /= p1.W;
                p1.Y = -p1.Y;
                Vector2 basePos = pos + (new Vector2(p1.X, p1.Y) * 0.5f + new Vector2(0.5f, 0.5f)) * imageSize;
                Vector2 p2 = Vector2.Abs(basePos - mousePos);
                if (p2.X < 10 && p2.Y < 10)
                {
                    toolTipMessage += obj.Name + "\n";
                    hoveredIndex = i;
                    drawList.AddNgon(basePos, 10, 0xffffffff, 4);
                }
                if (gameObjectSelectIndex == i)
                    drawList.AddNgon(basePos, 10, 0xffffff77, 4);
            }
            ImGui.PopClipRect();
            if (ImGui.IsItemActive())
            {
                if (io.MouseDown[1])
                    channel.camera.RotateDelta(new Vector3(-mouseMoveDelta.Y, mouseMoveDelta.X, 0) / 200);
                if (io.MouseDown[2])
                    channel.camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 50);
                appBody.RPContext.currentChannel = channel;
            }
            if (ImGui.IsItemHovered())
            {
                channel.camera.Distance += mouseWheelDelta * 6.0f;
                if (io.MouseReleased[0] && ImGui.IsItemFocused())
                {
                    gameObjectSelectIndex = hoveredIndex;
                }
            }
            if (!string.IsNullOrEmpty(toolTipMessage))
            {
                ImGui.BeginTooltip();
                ImGui.Text(toolTipMessage);
                ImGui.EndTooltip();
            }
        }

        static void Popups(Coocoo3DMain appBody)
        {
            if (requireOpenSelectResource.SetFalse())
            {
                ImGui.OpenPopup("选择资源");
                openResourcePopup = true;
            }
            ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal("选择资源", ref openResourcePopup))
            {
                if (ImGui.Button("关闭")) openResourcePopup = false;
                var _open = Resources(appBody);
                if (_open != null)
                {
                    filePropSelect = _open.file.FullName;
                    openResourcePopup = false;
                }
                ImGui.EndPopup();
            }
        }

        public static bool ComboBox<T>(string label, ref T val) where T : struct, Enum
        {
            string typeName = (typeof(T)).ToString();
            string valName = val.ToString();
            string[] enums = Enum.GetNames<T>();
            string[] enumsTranslation = enums;

            int sourceI = Array.FindIndex(enums, u => u == valName);
            int sourceI2 = sourceI;

            bool result = ImGui.Combo(string.Format("{1}###{0}", label, label), ref sourceI, enumsTranslation, enumsTranslation.Length);
            if (sourceI != sourceI2)
                val = Enum.Parse<T>(enums[sourceI]);

            return result;
        }

        public static bool initialized = false;

        public static bool demoWindowOpen = false;
        public static Vector3 position;
        public static Vector3 rotation;
        public static Quaternion rotationCache;
        public static bool rotationChange;

        public static int materialSelectIndex = 0;
        public static int gameObjectSelectIndex = 0;
        public static bool requireOpenFolder;
        public static bool requireRecord;
        public static bool requireSave;

        public static Stack<DirectoryInfo> viewStack = new Stack<DirectoryInfo>();
        public static List<FileSystemInfo> storageItems = new List<FileSystemInfo>();
        public static DirectoryInfo currentFolder;
        public static DirectoryInfo viewRequest;
        public static _openRequest openRequest;
        //public static List<bool> gameObjectSelected = new List<bool>();

        static Vector3 QuaternionToEularYXZ(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Asin(2.0 * (ei - jk));
            result.Y = (float)Math.Atan2(2.0 * (ej + ik), 1 - 2.0 * (ii + jj));
            result.Z = (float)Math.Atan2(2.0 * (ek + ij), 1 - 2.0 * (ii + kk));
            return result;
        }

        static void InitTex(Coocoo3DMain appbody)
        {
            var caches = appbody.mainCaches;
            ImGui.SetCurrentContext(ImGui.CreateContext());
            Coocoo3DGraphics.Uploader uploader = new Coocoo3DGraphics.Uploader();
            var io = ImGui.GetIO();
            io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 14, null, io.Fonts.GetGlyphRangesChineseFull());
            unsafe
            {
                byte* data;
                io.Fonts.GetTexDataAsRGBA32(out data, out int width, out int height, out int bytesPerPixel);
                int size = width * height * bytesPerPixel;
                Span<byte> spanByte1 = new Span<byte>(data, size);

                uploader.Texture2DRaw(spanByte1, Vortice.DXGI.Format.R8G8B8A8_UNorm, width, height);
            }
            var texture2D = new Coocoo3DGraphics.Texture2D();
            io.Fonts.TexID = caches.GetPtr("imgui_font");
            caches.SetTexture("imgui_font", texture2D);
            caches.TextureReadyToUpload.Enqueue(new(texture2D, uploader));
        }

        static void InitKeyMap()
        {
            var io = ImGui.GetIO();

            io.KeyMap[(int)ImGuiKey.Tab] = (int)ImGuiKey.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)ImGuiKey.LeftArrow;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)ImGuiKey.RightArrow;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)ImGuiKey.UpArrow;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)ImGuiKey.DownArrow;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)ImGuiKey.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)ImGuiKey.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int)ImGuiKey.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)ImGuiKey.End;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)ImGuiKey.Insert;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)ImGuiKey.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)ImGuiKey.Backspace;
            io.KeyMap[(int)ImGuiKey.Space] = (int)ImGuiKey.Space;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)ImGuiKey.Enter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)ImGuiKey.Escape;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)ImGuiKey.KeyPadEnter;
            io.KeyMap[(int)ImGuiKey.A] = 'A';
            io.KeyMap[(int)ImGuiKey.C] = 'C';
            io.KeyMap[(int)ImGuiKey.V] = 'V';
            io.KeyMap[(int)ImGuiKey.X] = 'X';
            io.KeyMap[(int)ImGuiKey.Y] = 'Y';
            io.KeyMap[(int)ImGuiKey.Z] = 'Z';

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        }

        static bool requireOpenSelectResource = false;
        static bool openResourcePopup;
        static string filePropSelect;

        static string[] lightTypeString = new[] { "方向光", "点光" };
        static int renderPipelineIndex = 0;
        static string[] renderPipelines = new string[0] { };
        static string[] renderPipelineKeys = new string[0] { };

        static Dictionary<string, string> stringValues = new Dictionary<string, string>();
    }
    class _openRequest
    {
        public FileInfo file;
        public DirectoryInfo folder;
    }
}
