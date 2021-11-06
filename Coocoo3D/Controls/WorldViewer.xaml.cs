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
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.Devices.Input;
using System.Numerics;
using Coocoo3D.Core;
using Windows.System.Threading;
using ImGuiNET;
using Windows.Graphics.Display;

namespace Coocoo3D.Controls
{
    public sealed partial class WorldViewer : UserControl
    {
        public Coocoo3DMain AppBody;
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
            if (AppBody == null) return;
            var info = DisplayInformation.GetForCurrentView();
            AppBody.graphicsDevice.SetSwapChainPanel(swapChainPanel, (float)swapChainPanel.ActualWidth, (float)swapChainPanel.ActualHeight, swapChainPanel.CompositionScaleX, swapChainPanel.CompositionScaleY, info.LogicalDpi);
            AppBody.GameDriverContext.NewSize = new Vector2((float)ActualWidth, (float)ActualHeight);
            AppBody.GameDriverContext.RequireResize = true;
            AppBody.swapChainReady = true;
            AppBody.RequireRender();
            swapChainPanel.SizeChanged -= SwapChainPanel_SizeChanged;
            swapChainPanel.SizeChanged += SwapChainPanel_SizeChanged;
        }

        private void SwapChainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AppBody.GameDriverContext.NewSize = e.NewSize.ToVector2();
            AppBody.GameDriverContext.RequireResize = true;
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
            inkCanvas.InkPresenter.UnprocessedInput.PointerHovered += Canvas_PointerHovered;
            inkCanvas.PointerWheelChanged += InkCanvas_PointerWheelChanged;
            inkCanvas.PointerMoved += InkCanvas_PointerMoved;
        }

        private void InkCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            process1(e);
            e.Handled = true;
        }

        MouseDevice currentMouse;
        TypedEventHandler<MouseDevice, MouseEventArgs> CurrentMouseMovedDelegate;

        private void Canvas_PointerPressed(object sender, PointerEventArgs args)
        {
            process1(args);
            args.Handled = true;
            this.Focus(FocusState.Pointer);
            if (currentMouse != null)
                return;
            currentMouse = MouseDevice.GetForCurrentView();

            Vector2 canvasSize = this.ActualSize;

            Point position = args.CurrentPoint.Position;

            var pointerType = args.CurrentPoint.PointerDevice.PointerDeviceType;
            if (pointerType == PointerDeviceType.Mouse)
            {
                var PointerProperties = args.CurrentPoint.Properties;
                CurrentMouseMovedDelegate = WorldViewer_MouseMoved_Rotate;
                currentMouse.MouseMoved += CurrentMouseMovedDelegate;
            }
        }

        private void WorldViewer_MouseMoved_Rotate(MouseDevice sender, MouseEventArgs args)
        {
            Input.EnqueueMouseMoveDelta(new Vector2(args.MouseDelta.X, args.MouseDelta.Y));
            AppBody.RequireRender();
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs args)
        {
            process1(args);
        }

        private void Canvas_PointerReleased(object sender, PointerEventArgs args)
        {
            process1(args);
            currentMouse.MouseMoved -= CurrentMouseMovedDelegate;
            currentMouse = null;
        }

        private void Canvas_PointerHovered(object sender, PointerEventArgs args)
        {
            var pointer = args.CurrentPoint;
            Vector2 position = pointer.Position.ToVector2() * AppBody.RPContext.logicScale;
            Input.EnqueueMouseMove(position);
            AppBody.RequireRender();
        }

        private void process1(PointerEventArgs args)
        {
            var pointer = args.CurrentPoint;
            Vector2 position = pointer.Position.ToVector2() * AppBody.RPContext.logicScale;
            Input.EnqueueMouseMove(position);
            Input.EnqueueMouseClick(position, pointer.Properties.IsLeftButtonPressed, InputType.MouseLeftDown);
            Input.EnqueueMouseClick(position, pointer.Properties.IsRightButtonPressed, InputType.MouseRightDown);
            Input.EnqueueMouseClick(position, pointer.Properties.IsMiddleButtonPressed, InputType.MouseMiddleDown);
            AppBody.RequireRender();
        }

        private void process1(PointerRoutedEventArgs args)
        {
            var pointer = args.GetCurrentPoint(this);
            Vector2 position = pointer.Position.ToVector2() * AppBody.RPContext.logicScale;
            Input.EnqueueMouseMove(position);
            Input.EnqueueMouseClick(position, pointer.Properties.IsLeftButtonPressed, InputType.MouseLeftDown);
            Input.EnqueueMouseClick(position, pointer.Properties.IsRightButtonPressed, InputType.MouseRightDown);
            Input.EnqueueMouseClick(position, pointer.Properties.IsMiddleButtonPressed, InputType.MouseMiddleDown);
            AppBody.RequireRender();
        }

        private void InkCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            Vector2 position = pointer.Position.ToVector2() * AppBody.RPContext.logicScale;
            Input.EnqueueMouseMove(position);
            Input.EnqueueMouseWheel(position, pointer.Properties.MouseWheelDelta);

            e.Handled = true;
            AppBody.RequireRender();
        }

        private void swapChainPanel_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            Input.KeyDown((int)e.Key);
            AppBody.RequireRender();
        }

        private void swapChainPanel_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            Input.KeyUp((int)e.Key);
            AppBody.RequireRender();
        }
    }
}
