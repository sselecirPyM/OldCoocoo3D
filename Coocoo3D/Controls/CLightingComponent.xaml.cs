using Coocoo3D.Components;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public sealed partial class CLightingComponent : UserControl, INotifyPropertyChanged
    {
        public CLightingComponent()
        {
            this.InitializeComponent();
        }
        LightingComponent lightingComponent;
        Coocoo3D.Core.Coocoo3DMain appBody;
        public void SetTarget(Components.Component component, Coocoo3D.Core.Coocoo3DMain _appBody)
        {
            lightingComponent = (LightingComponent)component;
            appBody = _appBody;
            _cacheColor = lightingComponent.Color;
            if (lightingComponent.LightingType == LightingType.Directional)
                radio1.IsChecked = true;
            else if (lightingComponent.LightingType == LightingType.Point)
                radio2.IsChecked = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        PropertyChangedEventArgs eaVCR = new PropertyChangedEventArgs("VCR");
        PropertyChangedEventArgs eaVCG = new PropertyChangedEventArgs("VCG");
        PropertyChangedEventArgs eaVCB = new PropertyChangedEventArgs("VCB");
        PropertyChangedEventArgs eaVCA = new PropertyChangedEventArgs("VCA");
        PropertyChangedEventArgs eaVRange = new PropertyChangedEventArgs("VRange");


        public float VCR
        {
            get => _cacheColor.X; set
            {
                _cacheColor.X = value;
                UpdateColorFromUI();
            }
        }
        public float VCG
        {
            get => _cacheColor.Y; set
            {
                _cacheColor.Y = value;
                UpdateColorFromUI();
            }
        }
        public float VCB
        {
            get => _cacheColor.Z; set
            {
                _cacheColor.Z = value;
                UpdateColorFromUI();
            }
        }
        public float VCA
        {
            get => _cacheColor.W; set
            {
                _cacheColor.W = value;
                UpdateColorFromUI();
            }
        }
        Vector4 _cacheColor;
        public float VRange
        {
            get => _cachedRange; set
            {
                lightingComponent.Range = value;
                _cachedRange = value;
                appBody.RequireRender();
            }
        }
        float _cachedRange;

        void UpdateColorFromUI()
        {
            lightingComponent.Color = _cacheColor;
            appBody.RequireRender();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            if ((string)radioButton.Tag == "directional")
            {
                lightingComponent.LightingType = LightingType.Directional;
            }
            else if ((string)radioButton.Tag == "point")
            {
                lightingComponent.LightingType = LightingType.Point;
            }
            appBody.RequireRender();
        }
    }
}
