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
using Coocoo3D.Core;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Coocoo3D.PropertiesPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LightingPropertiesPage : Page, INotifyPropertyChanged
    {
        public LightingPropertiesPage()
        {
            this.InitializeComponent();
        }

        Coocoo3DMain appBody;
        Lighting lighting;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (e.Parameter is Coocoo3DMain _appBody)
            {
                appBody = _appBody;
                lighting = _appBody.SelectedLighting[0];
                appBody.FrameUpdated += FrameUpdated;
                _cachePos = lighting.Position;
                _cacheRot = QuaternionToEularYXZ(lighting.Rotation) / MathF.PI * 180;
                _cacheRotQ = lighting.Rotation;
                _cachedRange = lighting.Range;
                if (lighting.LightingType == LightingType.Directional)
                    radio1.IsChecked = true;
                else if (lighting.LightingType == LightingType.Point)
                    radio2.IsChecked = true;
            }
            else
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "显示属性错误");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        PropertyChangedEventArgs eaVPX = new PropertyChangedEventArgs("VPX");//不进行gc
        PropertyChangedEventArgs eaVPY = new PropertyChangedEventArgs("VPY");
        PropertyChangedEventArgs eaVPZ = new PropertyChangedEventArgs("VPZ");
        PropertyChangedEventArgs eaVRX = new PropertyChangedEventArgs("VRX");
        PropertyChangedEventArgs eaVRY = new PropertyChangedEventArgs("VRY");
        PropertyChangedEventArgs eaVRZ = new PropertyChangedEventArgs("VRZ");
        PropertyChangedEventArgs eaVCR = new PropertyChangedEventArgs("VCR");
        PropertyChangedEventArgs eaVCG = new PropertyChangedEventArgs("VCG");
        PropertyChangedEventArgs eaVCB = new PropertyChangedEventArgs("VCB");
        PropertyChangedEventArgs eaVCA = new PropertyChangedEventArgs("VCA");
        PropertyChangedEventArgs eaVRange = new PropertyChangedEventArgs("VRange");

        private void FrameUpdated(object sender, EventArgs e)
        {
            if (_cachePos != lighting.Position)
            {
                _cachePos = lighting.Position;
                PropertyChanged?.Invoke(this, eaVPX);
                PropertyChanged?.Invoke(this, eaVPY);
                PropertyChanged?.Invoke(this, eaVPZ);
            }
            if (_cacheRotQ != lighting.Rotation)
            {
                _cacheRot = QuaternionToEularYXZ(_cacheRotQ) / MathF.PI * 180;
                _cacheRotQ = lighting.Rotation;
                PropertyChanged?.Invoke(this, eaVRX);
                PropertyChanged?.Invoke(this, eaVRY);
                PropertyChanged?.Invoke(this, eaVRZ);
            }
            if (_cacheColor != lighting.Color)
            {
                _cacheColor = lighting.Color;
                PropertyChanged?.Invoke(this, eaVCR);
                PropertyChanged?.Invoke(this, eaVCG);
                PropertyChanged?.Invoke(this, eaVCB);
                PropertyChanged?.Invoke(this, eaVCA);
            }
            if (_cachedRange != lighting.Range)
            {
                _cachedRange = lighting.Range;
                PropertyChanged?.Invoke(this, eaVRange);
            }
        }

        public float VPX
        {
            get => _cachePos.X; set
            {
                _cachePos.X = value;
                UpdatePositionFromUI();
            }
        }
        public float VPY
        {
            get => _cachePos.Y; set
            {
                _cachePos.Y = value;
                UpdatePositionFromUI();
            }
        }
        public float VPZ
        {
            get => _cachePos.Z; set
            {
                _cachePos.Z = value;
                UpdatePositionFromUI();
            }
        }

        public float VRX
        {
            get => _cacheRot.X; set
            {
                _cacheRot.X = value;
                UpdateRotationFromUI();
            }
        }
        public float VRY
        {
            get => _cacheRot.Y; set
            {
                _cacheRot.Y = value;
                UpdateRotationFromUI();
            }
        }
        public float VRZ
        {
            get => _cacheRot.Z; set
            {
                _cacheRot.Z = value;
                UpdateRotationFromUI();
            }
        }
        Vector3 _cachePos;
        Vector3 _cacheRot;
        Quaternion _cacheRotQ;
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
        void UpdateRotationFromUI()
        {
            var t1 = _cacheRot / 180 * MathF.PI;
            _cacheRotQ = Quaternion.CreateFromYawPitchRoll(t1.Y, t1.X, t1.Z);

            lighting.Rotation = _cacheRotQ;
            appBody.RequireRender();
        }

        void UpdatePositionFromUI()
        {
            lighting.Position = _cachePos;
            appBody.RequireRender();
        }

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
                lighting.Range = value;
                _cachedRange = value;
                appBody.RequireRender();
            }
        }
        float _cachedRange;


        void UpdateColorFromUI()
        {
            lighting.Color = _cacheColor;
            appBody.RequireRender();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (appBody != null)
            {
                appBody.FrameUpdated -= FrameUpdated;
            }
        }

        PropertyChangedEventArgs eaName = new PropertyChangedEventArgs("Name");
        public string vName
        {
            get => lighting.Name;
            set { lighting.Name = value; lighting.PropChange(eaName); }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            if ((string)radioButton.Tag == "directional")
            {
                lighting.LightingType = LightingType.Directional;
            }
            else if ((string)radioButton.Tag == "point")
            {
                lighting.LightingType = LightingType.Point;
            }
            appBody.RequireRender();
        }
        Random random = new Random();
        private void RandomPositionButton_Click(object sender, RoutedEventArgs e)
        {
            lighting.Position.X = (float)random.Next(int.MinValue, int.MaxValue) / int.MaxValue * 100;
            lighting.Position.Z = (float)random.Next(int.MinValue, int.MaxValue) / int.MaxValue * 100;
            _cachePos = lighting.Position;
            PropertyChanged?.Invoke(this, eaVPX);
            PropertyChanged?.Invoke(this, eaVPY);
            PropertyChanged?.Invoke(this, eaVPZ);
            appBody.RequireRender();
        }
    }
}
