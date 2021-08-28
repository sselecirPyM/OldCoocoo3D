using Coocoo3D.Core;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Coocoo3D.Components;
using Windows.Storage;

namespace Coocoo3D.UI
{
    static class UIImGui
    {
        public static void GUI(Coocoo3DMain appBody)
        {
            var io = ImGui.GetIO();
            if (!initialized)
            {
                InitKeyMap();
                initialized = true;
            }
            Vector2 mouseMoveDelta = new Vector2();
            float mouseWheelDelta = 0.0f;
            while (Input.inputDatas.TryDequeue(out InputData inputData))
            {
                if (inputData.inputType == InputType.MouseMove)
                    io.MousePos = inputData.point;
                else if (inputData.inputType == InputType.MouseLeftDown)
                    io.MouseDown[0] = inputData.mouseDown;
                else if (inputData.inputType == InputType.MouseRightDown)
                {
                    io.MouseDown[1] = inputData.mouseDown;
                }
                else if (inputData.inputType == InputType.MouseMiddleDown)
                {
                    io.MouseDown[2] = inputData.mouseDown;
                }
                else if (inputData.inputType == InputType.MouseWheelChanged)
                {
                    io.MouseWheel += inputData.mouseWheelDelta / 120.0f;
                    mouseWheelDelta += inputData.mouseWheelDelta;
                }
                else if (inputData.inputType == InputType.MouseMoveDelta)
                    mouseMoveDelta += inputData.point;
            }

            while (Input.keyInputDatas.TryDequeue(out KeyInputData inputData))
            {
                if (inputData.keyEventType == KeyEventType.KeyDown)
                {
                    io.KeysDown[inputData.key] = true;
                    io.AddInputCharacter((uint)inputData.key);
                }
                if (inputData.keyEventType == KeyEventType.KeyUp)
                    io.KeysDown[inputData.key] = false;
            }
            var context = appBody.RPContext;
            io.DisplaySize = new Vector2(context.screenSize.X, context.screenSize.Y);
            io.DeltaTime = (float)context.dynamicContextRead.RealDeltaTime;
            Present.GameObject selectedObject = null;
            LightingComponent lightingComponent = null;
            VolumeComponent volumeComponent = null;
            MMDRendererComponent rendererComponent = null;
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
                rendererComponent = selectedObject.GetComponent<MMDRendererComponent>();
            }
            ImGui.NewFrame();
            if (appBody.settings.ViewerUI)
            {
                if (demoWindowOpen)
                    ImGui.ShowDemoWindow(ref demoWindowOpen);
                ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.Once);
                if (ImGui.Begin("常用"))
                {
                    Common(appBody);
                }
                ImGui.End();
                ImGui.SetNextWindowSize(new Vector2(500, 300), ImGuiCond.Once);
                ImGui.SetNextWindowPos(new Vector2(100, 400), ImGuiCond.Once);
                if (ImGui.Begin("资源"))
                {
                    Resources(appBody);
                }
                ImGui.End();
                ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.Once);
                ImGui.SetNextWindowPos(new Vector2(200, 100), ImGuiCond.Once);
                if (ImGui.Begin("场景"))
                {
                    Scene(appBody);
                }
                ImGui.End();
                ImGui.SetNextWindowSize(new Vector2(605, 520), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(new Vector2(200, 200), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("场景视图"))
                {
                    SceneView(appBody);
                }
                ImGui.End();
                if (selectedObject != null)
                {
                    ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.Once);
                    ImGui.SetNextWindowPos(new Vector2(200, 200), ImGuiCond.Once);
                    if (ImGui.Begin("物体"))
                    {
                        ImGui.Text(selectedObject.Name);
                        if (ImGui.TreeNode("描述"))
                        {
                            ImGui.Text(selectedObject.Description);
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
                        if (rendererComponent != null)
                        {
                            RendererComponent(appBody, rendererComponent);
                        }
                        if (lightingComponent != null)
                        {
                            if (ImGui.TreeNode("光照"))
                            {
                                int current = (int)lightingComponent.LightingType;
                                ImGui.ColorEdit4("颜色", ref lightingComponent.Color, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float);
                                ImGui.DragFloat("范围", ref lightingComponent.Range);
                                ImGui.Combo("类型", ref current, new[] { "方向光", "点光" }, 2);
                                ImGui.TreePop();
                                lightingComponent.LightingType = (Present.LightingType)current;
                            }
                        }
                        if (volumeComponent != null)
                        {
                            if (ImGui.TreeNode("体积"))
                            {
                                ImGui.DragFloat3("尺寸", ref volumeComponent.Size);
                                ImGui.TreePop();
                            }
                        }
                    }
                    ImGui.End();
                }
            }
            ImGui.Render();
            if (!appBody.settings.ViewerUI || !io.WantCaptureMouse)
            {
                if (io.MouseDown[1])
                    appBody.camera.RotateDelta(new Vector3(-mouseMoveDelta.Y, -mouseMoveDelta.X, 0) / 200);
                if (io.MouseDown[2])
                    appBody.camera.MoveDelta(new Vector3(-mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 50);

                appBody.camera.Distance += mouseWheelDelta / 20.0f;
            }
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
                ImGui.DragFloat("距离", ref appBody.camera.Distance);
                ImGui.DragFloat3("观察点", ref appBody.camera.LookAtPoint, 0.05f);
                Vector3 a = appBody.camera.Angle / MathF.PI * 180;
                if (ImGui.DragFloat3("角度", ref a))
                    appBody.camera.Angle = a * MathF.PI / 180;
                float fov = appBody.camera.Fov / MathF.PI * 180;
                if (ImGui.DragFloat("FOV", ref fov))
                    appBody.camera.Fov = fov * MathF.PI / 180;
                ImGui.Checkbox("使用镜头运动文件", ref appBody.camera.CameraMotionOn);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("录制"))
            {
                if (ImGui.Button("开始录制"))
                {
                    requireRecord = true;
                }
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("设置"))
            {
                ImGui.Checkbox("垂直同步", ref appBody.performaceSettings.VSync);
                ImGui.Checkbox("节省CPU", ref appBody.performaceSettings.SaveCpuPower);
                ImGui.Checkbox("多线程渲染", ref appBody.performaceSettings.MultiThreadRendering);
                ImGui.Checkbox("线框", ref appBody.settings.Wireframe);
                ImGui.SetNextItemWidth(150);
                ImGui.DragInt("阴影分辨率", ref appBody.settings.ShadowMapResolution, 128, 512, 8192);
                float a = (float)(1.0 / appBody.GameDriverContext.FrameInterval);
                if (!(a == a))
                    a = 2000;

                if (ImGui.DragFloat("帧率限制", ref a, 10, 1, 5000))
                {
                    if (a == a)
                        appBody.GameDriverContext.FrameInterval = 1 / a;
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
        }

        static void Resources(Coocoo3DMain appBody)
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
            ImGui.Begin("资源");

            lock (storageItems)
            {
                bool _requireClear = false;
                foreach (var item in storageItems)
                {
                    if (ImGui.Selectable(item.Name, false, ImGuiSelectableFlags.AllowDoubleClick) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (item is StorageFolder folder)
                        {
                            viewRequest = folder;
                            _requireClear = true;
                            viewStack.Push(currentFolder);
                        }
                        else if (item is StorageFile file)
                        {
                            var requireOpen1 = new _openRequest();
                            requireOpen1.file = file;
                            requireOpen1.folder = currentFolder;
                            openRequest = requireOpen1;
                        }
                    }
                }
                if (_requireClear)
                    storageItems.Clear();
            }
            ImGui.End();
        }

        static void Help()
        {
            if (ImGui.TreeNode("基本操作"))
            {
                ImGui.Text(@"旋转视角 - 按住鼠标右键拖动
平移镜头 - 按住鼠标中键拖动
拉近、拉远镜头 - 鼠标滚轮
修改物体位置、旋转 - 在ui上按住左键然后拖动");
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
                ImGui.TextWrapped(@"请点击帮助菜单下的""示例着色器""来导出示例着色器文件夹。可以加载coocoox格式的渲染配置文件。
此配置文件极易阅读和修改，你可以快速的迭代渲染效果。
示例着色器已经包含前向渲染以及延迟渲染的示例，有问题或想法请在github上提交。");
                ImGui.TreePop();
            }
            if (ImGui.Button("显示ImGuiDemoWindow"))
            {
                demoWindowOpen = true;
            }
        }

        static void Scene(Coocoo3DMain appBody)
        {
            if (ImGui.Button("新光源"))
            {
                UISharedCode.NewLighting(appBody);
            }
            ImGui.SameLine();
            if (ImGui.Button("新体积"))
            {
                UISharedCode.NewVolume(appBody);
            }
            ImGui.SameLine();
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
            if (ImGui.TreeNode("渲染"))
            {
                if (ImGui.TreeNode("材质"))
                {
                    if (ImGui.BeginChild("materials", new Vector2(140, 300)))
                    {
                        RuntimeMaterial runtimeMaterial = null;
                        ImGui.PushItemWidth(120);
                        for (int i = 0; i < rendererComponent.Materials.Count; i++)
                        {
                            RuntimeMaterial material = rendererComponent.Materials[i];
                            bool selected = i == materialSelectIndex;
                            ImGui.Selectable(material.Name, ref selected);
                            if (selected) materialSelectIndex = i;
                        }
                        ImGui.PopItemWidth();
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();
                    if (ImGui.BeginChild("materialProperty", new Vector2(140, 300)))
                    {
                        if (materialSelectIndex >= 0 && materialSelectIndex < rendererComponent.Materials.Count)
                        {
                            var material = rendererComponent.Materials[materialSelectIndex];
                            ImGui.Text(material.Name);
                            ImGui.SliderFloat("金属性  ", ref material.innerStruct.Metallic, 0, 1);
                            ImGui.SliderFloat("粗糙度  ", ref material.innerStruct.Roughness, 0, 1);
                            ImGui.SliderFloat("高光  ", ref material.innerStruct.Specular, 0, 1);
                            ImGui.Checkbox("透明材质", ref material.Transparent);
                            foreach (var tex in material.textures)
                            {
                                string key = "imgui/" + tex.Key;
                                appBody.RPAssetsManager.texture2ds[key] = tex.Value;
                                Vector2 pos = ImGui.GetCursorScreenPos();
                                Vector2 imageSize = new Vector2(120, 120);
                                IntPtr imageId = appBody.RPAssetsManager.GetPtr(key);
                                ImGui.Image(imageId, imageSize);
                                if (ImGui.IsItemHovered())
                                {
                                    Vector2 uv0 = (io.MousePos - pos) / imageSize;
                                    Vector2 uv1 = uv0 + new Vector2(20) / imageSize;

                                    ImGui.BeginTooltip();
                                    ImGui.Image(imageId, new Vector2(100, 100), uv0, uv1);
                                    ImGui.EndTooltip();
                                }
                            }
                        }
                    }
                    ImGui.EndChild();
                    ImGui.TreePop();
                }
                if (ImGui.TreeNode("变形"))
                {
                    if (ImGui.Checkbox("锁定动作", ref rendererComponent.LockMotion)) ;
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
                ImGui.TreePop();
            }
        }

        static void SceneView(Coocoo3DMain appBody)
        {
            var io = ImGui.GetIO();
            IntPtr imageId = appBody.RPAssetsManager.GetPtr("_Final0");
            Vector2 pos = ImGui.GetCursorScreenPos();
            var tex = appBody.RPAssetsManager.GetTexture(imageId);
            Vector2 spaceSize = Vector2.Max(ImGui.GetWindowSize() - new Vector2(5, 40), new Vector2(100, 100));
            appBody.RPContext.sceneViewSize = new Numerics.Int2((int)spaceSize.X, (int)spaceSize.Y);
            Vector2 texSize = new Vector2(tex.GetWidth(), tex.GetHeight());
            float factor = MathF.Max(MathF.Min(spaceSize.X / texSize.X, spaceSize.Y / texSize.Y), 0.01f);
            Vector2 imageSize = texSize * factor;



            ImGui.InvisibleButton("X", imageSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
            ImGui.GetWindowDrawList().AddImage(imageId, pos, pos + imageSize);
            if (ImGui.IsItemActive())
            {
                if (io.MouseDown[1])
                    appBody.camera.RotateDelta(new Vector3(-io.MouseDelta.Y, -io.MouseDelta.X, 0) / 200);
                if (io.MouseDown[2])
                    appBody.camera.MoveDelta(new Vector3(-io.MouseDelta.X, io.MouseDelta.Y, 0) / 50);
            }
            //if (ImGui.IsItemHovered())
            //{
            //    Vector2 uv0 = (io.MousePos - pos) / imageSize - new Vector2(100, 100) / new Vector2(tex.GetWidth(), tex.GetHeight());
            //    Vector2 uv1 = uv0 + new Vector2(200, 200) / new Vector2(tex.GetWidth(), tex.GetHeight());

            //    ImGui.BeginTooltip();
            //    ImGui.Image(imageId, new Vector2(100, 100), uv0, uv1);
            //    ImGui.EndTooltip();
            //}
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
        public static bool requireExport;
        public static bool requireSaveFolder;
        public static bool requireRecord;

        public static StorageFolder rootFolder;
        public static Stack<StorageFolder> viewStack = new Stack<StorageFolder>();
        public static List<IStorageItem> storageItems = new List<IStorageItem>();
        public static StorageFolder currentFolder;
        public static StorageFolder viewRequest;
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

        static void InitKeyMap()
        {
            var io = ImGui.GetIO();

            io.KeyMap[(int)ImGuiKey.Tab] = (int)Windows.System.VirtualKey.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Windows.System.VirtualKey.Left;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Windows.System.VirtualKey.Right;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Windows.System.VirtualKey.Up;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Windows.System.VirtualKey.Down;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)Windows.System.VirtualKey.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)Windows.System.VirtualKey.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int)Windows.System.VirtualKey.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)Windows.System.VirtualKey.End;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)Windows.System.VirtualKey.Insert;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)Windows.System.VirtualKey.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)Windows.System.VirtualKey.Back;
            io.KeyMap[(int)ImGuiKey.Space] = (int)Windows.System.VirtualKey.Space;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)Windows.System.VirtualKey.Enter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)Windows.System.VirtualKey.Escape;
            //io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)Windows.System.VirtualKey.RETURN;

            io.KeyMap[(int)ImGuiKey.A] = 'A';
            io.KeyMap[(int)ImGuiKey.C] = 'C';
            io.KeyMap[(int)ImGuiKey.V] = 'V';
            io.KeyMap[(int)ImGuiKey.X] = 'X';
            io.KeyMap[(int)ImGuiKey.Y] = 'Y';
            io.KeyMap[(int)ImGuiKey.Z] = 'Z';
        }
    }
    class _openRequest
    {
        public StorageFile file;
        public StorageFolder folder;
    }
}
