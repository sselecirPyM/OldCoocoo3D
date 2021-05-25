using Coocoo3D.Core;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;

namespace Coocoo3D.UI
{
    static class UIImGui
    {
        public static void GUI(Coocoo3DMain appBody)
        {
            var io = ImGui.GetIO();
            Vector2 mouseMoveDelta = new Vector2();
            float mouseWheelDelta = 0.0f;
            while (Input.inputDatas.TryDequeue(out InputData inputData))
            {
                if (inputData.inputType == InputType.MouseMove)
                    io.MousePos = inputData.point;
                else if (inputData.inputType == InputType.MouseLeftDown)
                    io.MouseDown[0] = inputData.mouseDown;
                else if (inputData.inputType == InputType.MouseRightDown)
                    io.MouseDown[1] = inputData.mouseDown;
                else if (inputData.inputType == InputType.MouseMiddleDown)
                    io.MouseDown[2] = inputData.mouseDown;
                else if (inputData.inputType == InputType.MouseWheelChanged)
                {
                    io.MouseWheel += inputData.mouseWheelDelta / 120.0f;
                    mouseWheelDelta += inputData.mouseWheelDelta;
                }
                else if (inputData.inputType == InputType.MouseMoveDelta)
                    mouseMoveDelta += inputData.point;
            }
            var context = appBody.RPContext;
            io.DisplaySize = new Vector2(context.screenWidth, context.screenHeight);
            io.DeltaTime = (float)context.dynamicContextRead.DeltaTime;
            Present.GameObject selectedObject = null;
            LightingComponent lightingComponent = null;
            VolumeComponent volumeComponent = null;
            MMDRendererComponent rendererComponent = null;
            if (appBody.SelectedGameObjects.Count == 1 && appBody.SelectedEntities.Count == 0)
            {
                selectedObject = appBody.SelectedGameObjects[0];
                position = selectedObject.Position;
                if (rotationCache != selectedObject.Rotation)
                {
                    rotation = QuaternionToEularYXZ(selectedObject.Rotation);
                    rotationCache = selectedObject.Rotation;
                }
                lightingComponent = selectedObject.GetComponent<LightingComponent>();
                volumeComponent = selectedObject.GetComponent<VolumeComponent>();
            }
            else if (appBody.SelectedGameObjects.Count == 0 && appBody.SelectedEntities.Count == 1)
            {
                position = appBody.SelectedEntities[0].Position;
                if (rotationCache != appBody.SelectedEntities[0].Rotation)
                {
                    rotation = QuaternionToEularYXZ(appBody.SelectedEntities[0].Rotation);
                    rotationCache = appBody.SelectedEntities[0].Rotation;
                }
            }
            ImGui.NewFrame();
            if (appBody.settings.ViewerUI)
            {
                if (demoWindowOpen)
                    ImGui.ShowDemoWindow(ref demoWindowOpen);
                ImGui.Begin("常用");
                if (ImGui.TreeNode("transform"))
                {
                    ImGui.DragFloat3("位置", ref position, 0.1f);
                    rotationChange = ImGui.DragFloat3("旋转", ref rotation, 0.01f);
                    ImGui.TreePop();
                }
                if (ImGui.Button("播放"))
                {
                    PlayControl.Play(appBody);
                }
                ImGui.SameLine();
                if (ImGui.Button("停止"))
                {
                    PlayControl.Stop(appBody);
                }
                ImGui.End();
                if (appBody.SelectedGameObjects.Count == 1 && appBody.SelectedEntities.Count == 0)
                {
                    ImGui.SetNextWindowSize(new Vector2(300, 200), ImGuiCond.Once);
                    ImGui.Begin("物体");
                    ImGui.Text(selectedObject.Name);
                    if (ImGui.TreeNode("transform"))
                    {
                        ImGui.DragFloat3("位置", ref position, 0.1f);
                        rotationChange = ImGui.DragFloat3("旋转", ref rotation, 0.01f);
                        ImGui.TreePop();
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
                    ImGui.End();
                }

            }
            ImGui.Render();
            if (!io.WantCaptureMouse)
            {
                if (io.MouseDown[1])
                    appBody.camera.RotateDelta(new Vector3(-mouseMoveDelta.Y, -mouseMoveDelta.X, 0) / 200);
                if (io.MouseDown[2])
                    appBody.camera.MoveDelta(new Vector3(-mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 50);

                appBody.camera.Distance += mouseWheelDelta / 20.0f;
            }
            if (appBody.SelectedGameObjects.Count == 1 && appBody.SelectedEntities.Count == 0)
            {
                appBody.SelectedGameObjects[0].PositionNextFrame = position;
                if (rotationChange)
                {
                    rotationCache = appBody.SelectedGameObjects[0].RotationNextFrame = Quaternion.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z);
                }
            }
            else if (appBody.SelectedGameObjects.Count == 0 && appBody.SelectedEntities.Count == 1)
            {
                appBody.SelectedEntities[0].PositionNextFrame = position;
                if (rotationChange)
                {
                    rotationCache = appBody.SelectedEntities[0].RotationNextFrame = Quaternion.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z);
                }
            }

        }
        public static bool demoWindowOpen = true;
        public static Vector3 position;
        public static Vector3 rotation;
        public static Quaternion rotationCache;
        public static bool rotationChange;


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
    }
}
