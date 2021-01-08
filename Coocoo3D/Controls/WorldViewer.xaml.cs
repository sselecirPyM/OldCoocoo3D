using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Coocoo3DGraphics;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.Devices.Input;
using System.Numerics;
using Coocoo3D.Core;

namespace Coocoo3D.Controls
{
    public sealed partial class WorldViewer : UserControl
    {
        public Coocoo3DMain AppBody
        {
            get => _appBody;
            set { _appBody = value; SetupSwapChain(); }
        }
        Coocoo3DMain _appBody;

        public WorldViewer()
        {
            this.InitializeComponent();
        }

        private void SwapChainPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppBody == null) return;
            SetupSwapChain();
        }
        private void SetupSwapChain()
        {
            if (!swapChainPanel.IsLoaded) return;
            if (_appBody == null) return;
            AppBody.GameDriverContext.AspectRatio = (float)(ActualWidth / ActualHeight);
            AppBody.deviceResources.SetSwapChainPanel(swapChainPanel);
            AppBody.GameDriverContext.NewSize = new Size(ActualWidth, ActualHeight);
            AppBody.GameDriverContext.RequireResizeOuter = true;
            AppBody.swapChainReady = true;
            AppBody.RequireRender();
            swapChainPanel.SizeChanged -= SwapChainPanel_SizeChanged;
            swapChainPanel.SizeChanged += SwapChainPanel_SizeChanged;
        }

        private void SwapChainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AppBody.GameDriverContext.AspectRatio = (float)(ActualWidth / ActualHeight);
            AppBody.GameDriverContext.NewSize = e.NewSize;
            AppBody.GameDriverContext.RequireResizeOuter = true;
            AppBody.RequireRender();
        }

        private void InkCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            InkCanvas inkCanvas = sender as InkCanvas;
            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch | CoreInputDeviceTypes.Pen;
            inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.None;
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += Canvas_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += Canvas_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += Canvas_PointerReleased;
            inkCanvas.PointerWheelChanged += InkCanvas_PointerWheelChanged;
        }

        readonly Vector2 c_buttonSize = new Vector2(64, 64);

        public bool touched;
        public bool moveX;
        public bool moveY;
        public bool moveZ;
        public bool dragMove;
        public bool dragRot;
        MouseDevice currentMouse;
        TypedEventHandler<MouseDevice, MouseEventArgs> CurrentMouseMovedDelegate;
        Rect GetRectAlignRightBottom(Vector2 canvasSize, Vector2 offset, Vector2 rectSize)
        {
            return new Rect(canvasSize.X - offset.X - rectSize.X, canvasSize.Y - offset.Y - rectSize.Y, rectSize.X, rectSize.Y);
        }
        private void Canvas_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            args.Handled = true;
            this.Focus(FocusState.Pointer);
            if (currentMouse != null)
                return;
            currentMouse = MouseDevice.GetForCurrentView();

            Vector2 canvasSize = this.ActualSize;
            Rect rectRotX = GetRectAlignRightBottom(canvasSize, new Vector2(128, 64), c_buttonSize);
            Rect rectRotY = GetRectAlignRightBottom(canvasSize, new Vector2(64, 64), c_buttonSize);
            Rect rectRotZ = GetRectAlignRightBottom(canvasSize, new Vector2(0, 64), c_buttonSize);
            Rect rectMoveX = GetRectAlignRightBottom(canvasSize, new Vector2(128, 0), c_buttonSize);
            Rect rectMoveY = GetRectAlignRightBottom(canvasSize, new Vector2(64, 0), c_buttonSize);
            Rect rectMoveZ = GetRectAlignRightBottom(canvasSize, new Vector2(0, 0), c_buttonSize);

            Point position = args.CurrentPoint.Position;
            bool _UIMoveTest()
            {
                if (!AppBody.settings.ViewerUI || AppBody.Recording) return false;
                if (rectMoveX.Contains(position))
                {
                    moveX = true;
                    dragMove = true;
                    return true;
                }
                else if (rectMoveY.Contains(position))
                {
                    moveY = true;
                    dragMove = true;
                    return true;
                }
                else if (rectMoveZ.Contains(position))
                {
                    moveZ = true;
                    dragMove = true;
                    return true;
                }
                else if (rectRotX.Contains(position))
                {
                    moveX = true;
                    dragRot = true;
                    return true;
                }
                else if (rectRotY.Contains(position))
                {
                    moveY = true;
                    dragRot = true;
                    return true;
                }
                else if (rectRotZ.Contains(position))
                {
                    moveZ = true;
                    dragRot = true;
                    return true;
                }
                return false;
            }

            var pointerType = args.CurrentPoint.PointerDevice.PointerDeviceType;
            if (pointerType == PointerDeviceType.Mouse)
            {
                var PointerProperties = args.CurrentPoint.Properties;
                if (PointerProperties.IsRightButtonPressed)
                    CurrentMouseMovedDelegate = WorldViewer_MouseMoved_Rotate;
                else if (PointerProperties.IsMiddleButtonPressed)
                    CurrentMouseMovedDelegate = WorldViewer_MouseMoved_Drag;
                else if (_UIMoveTest())
                {
                    CurrentMouseMovedDelegate = WorldViewer_MouseMoved_DragAxis;
                }
                else
                {
                    CurrentMouseMovedDelegate = null;
                }

                currentMouse.MouseMoved += CurrentMouseMovedDelegate;
            }
            else if (pointerType == PointerDeviceType.Touch || pointerType == PointerDeviceType.Pen)
            {
                touched = true;
                lastPosition = args.CurrentPoint.Position;
                _UIMoveTest();
            }
        }

        private void WorldViewer_MouseMoved_Rotate(MouseDevice sender, MouseEventArgs args)
        {
            Vector3 delta = new Vector3();
            delta.X = -args.MouseDelta.Y;
            delta.Y = -args.MouseDelta.X;
            AppBody.camera.RotateDelta(delta / 200);
            AppBody.RequireRender();
        }

        private void WorldViewer_MouseMoved_Drag(MouseDevice sender, MouseEventArgs args)
        {
            Vector3 delta = new Vector3();
            delta.X = -args.MouseDelta.X;
            delta.Y = args.MouseDelta.Y;
            AppBody.camera.MoveDelta(delta / 50);
            AppBody.RequireRender();
        }

        private void WorldViewer_MouseMoved_DragAxis(MouseDevice sender, MouseEventArgs args)
        {
            Vector3 delta = new Vector3();
            if (moveX)
                delta.X = -args.MouseDelta.Y;
            else if (moveY)
                delta.Y = -args.MouseDelta.Y;
            else if (moveZ)
                delta.Z = -args.MouseDelta.Y;
            if (dragMove)
            {
                delta = delta / 50.0f;
                for (int i = 0; i < AppBody.SelectedEntities.Count; i++)
                {
                    var entity = AppBody.SelectedEntities[i];
                    entity.PositionNextFrame += delta;
                    entity.NeedTransform = true;
                }
                for (int i = 0; i < AppBody.SelectedLighting.Count; i++)
                {
                    var lighting = AppBody.SelectedLighting[i];
                    lighting.Position += delta;
                }
                AppBody.RequireRender(true);
            }
            else if (dragRot)
            {
                delta = delta / 200.0f;
                Quaternion quat = Quaternion.CreateFromYawPitchRoll(delta.Y, delta.X, delta.Z);
                for (int i = 0; i < AppBody.SelectedEntities.Count; i++)
                {
                    var entity = AppBody.SelectedEntities[i];
                    entity.RotationNextFrame = Quaternion.Normalize(entity.RotationNextFrame * quat);
                    entity.NeedTransform = true;
                }
                for (int i = 0; i < AppBody.SelectedLighting.Count; i++)
                {
                    var lighting = AppBody.SelectedLighting[i];
                    lighting.Rotation = Quaternion.Normalize(lighting.Rotation * quat);
                }
                AppBody.RequireRender(true);
            }
        }


        Point lastPosition;
        private void Canvas_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (touched)
            {
                if (dragMove)
                {
                    Vector3 delta = new Vector3();
                    if (moveX)
                        delta.X = (float)(args.CurrentPoint.Position.Y - lastPosition.Y) / 12.5f;
                    if (moveY)
                        delta.Y = (float)(args.CurrentPoint.Position.Y - lastPosition.Y) / 12.5f;
                    if (moveZ)
                        delta.Z = (float)(args.CurrentPoint.Position.Y - lastPosition.Y) / 12.5f;
                    for (int i = 0; i < AppBody.SelectedEntities.Count; i++)
                    {
                        var entity = AppBody.SelectedEntities[i];
                        entity.PositionNextFrame += delta;
                        entity.NeedTransform = true;
                    }
                    for (int i = 0; i < AppBody.SelectedLighting.Count; i++)
                    {
                        var lighting = AppBody.SelectedLighting[i];
                        lighting.Position += delta;
                    }
                    AppBody.RequireRender(true);
                }
                else if(dragRot)
                {
                    Vector3 delta = new Vector3();
                    if (moveX)
                        delta.X = (float)(args.CurrentPoint.Position.Y - lastPosition.Y) / 50.0f;
                    if (moveY)
                        delta.Y = (float)(args.CurrentPoint.Position.Y - lastPosition.Y) / 50.0f;
                    if (moveZ)
                        delta.Z = (float)(args.CurrentPoint.Position.Y - lastPosition.Y) / 50.0f;
                    Quaternion quat = Quaternion.CreateFromYawPitchRoll(delta.Y, delta.X, delta.Z);
                    for (int i = 0; i < AppBody.SelectedEntities.Count; i++)
                    {
                        var entity = AppBody.SelectedEntities[i];
                        entity.RotationNextFrame = Quaternion.Normalize(entity.RotationNextFrame * quat);
                        entity.NeedTransform = true;
                    }
                    for (int i = 0; i < AppBody.SelectedLighting.Count; i++)
                    {
                        var lighting = AppBody.SelectedLighting[i];
                        lighting.Rotation = Quaternion.Normalize(lighting.Rotation * quat);
                    }
                    AppBody.RequireRender(true);
                }
                else
                {
                    Vector2 delta = args.CurrentPoint.Position.ToVector2() - lastPosition.ToVector2();
                    if (!args.CurrentPoint.Properties.IsEraser)
                        AppBody.camera.RotateDelta(-new Vector3(delta.Y, delta.X, 0) / 50);
                    else
                        AppBody.camera.MoveDelta(new Vector3(-delta.X, delta.Y, 0) / 50);
                    AppBody.RequireRender();
                }
            }
            lastPosition = args.CurrentPoint.Position;
        }

        private void Canvas_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            currentMouse.MouseMoved -= CurrentMouseMovedDelegate;
            currentMouse = null;
            touched = false;
            moveX = false;
            moveY = false;
            moveZ = false;
            dragMove = false;
            dragRot = false;
        }

        private void InkCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
            AppBody.camera.Distance += e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta / 20.0f;
            AppBody.RequireRender();
        }
    }
}
