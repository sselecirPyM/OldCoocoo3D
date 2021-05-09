using Coocoo3D.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
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

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace Coocoo3D.Controls
{
    public sealed partial class CVolumeComponent : UserControl
    {
        public CVolumeComponent()
        {
            this.InitializeComponent();
        }

        VolumeComponent volumeComponent;
        Coocoo3D.Core.Coocoo3DMain appBody;
        public void SetTarget(Components.Component component, Coocoo3D.Core.Coocoo3DMain _appBody)
        {
            volumeComponent = (VolumeComponent)component;
            appBody = _appBody;
            _cacheSize = volumeComponent.Size;
        }

        public float VSX
        {
            get => _cacheSize.X; set
            {
                _cacheSize.X = value;
                UpdateColorFromUI();
            }
        }
        public float VSY
        {
            get => _cacheSize.Y; set
            {
                _cacheSize.Y = value;
                UpdateColorFromUI();
            }
        }
        public float VSZ
        {
            get => _cacheSize.Z; set
            {
                _cacheSize.Z = value;
                UpdateColorFromUI();
            }
        }
        Vector3 _cacheSize;

        void UpdateColorFromUI()
        {
            volumeComponent.Size = _cacheSize;
            appBody.RequireRender();
        }
    }
}
