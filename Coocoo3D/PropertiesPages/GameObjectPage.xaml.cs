using Coocoo3D.Controls;
using Coocoo3D.Core;
using Coocoo3D.Present;
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

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Coocoo3D.PropertiesPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class GameObjectPage : Page, System.ComponentModel.INotifyPropertyChanged
    {
        public GameObjectPage()
        {
            this.InitializeComponent();
        }
        Coocoo3DMain appBody;
        GameObject selectedGameObject;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (e.Parameter is Coocoo3DMain _appBody)
            {
                appBody = _appBody;
                selectedGameObject = _appBody.SelectedGameObjects[0];
                appBody.FrameUpdated += FrameUpdated;
                _cachePos = selectedGameObject.PositionNextFrame;
                _cacheRot = QuaternionToEularYXZ(selectedGameObject.RotationNextFrame) / MathF.PI * 180;
                _cacheRotQ = selectedGameObject.RotationNextFrame;
            }
            else
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "显示属性错误");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (appBody != null)
            {
                appBody.FrameUpdated -= FrameUpdated;
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        System.ComponentModel.PropertyChangedEventArgs eaVPX = new System.ComponentModel.PropertyChangedEventArgs("VPX");//不进行gc
        System.ComponentModel.PropertyChangedEventArgs eaVPY = new System.ComponentModel.PropertyChangedEventArgs("VPY");
        System.ComponentModel.PropertyChangedEventArgs eaVPZ = new System.ComponentModel.PropertyChangedEventArgs("VPZ");
        System.ComponentModel.PropertyChangedEventArgs eaVRX = new System.ComponentModel.PropertyChangedEventArgs("VRX");
        System.ComponentModel.PropertyChangedEventArgs eaVRY = new System.ComponentModel.PropertyChangedEventArgs("VRY");
        System.ComponentModel.PropertyChangedEventArgs eaVRZ = new System.ComponentModel.PropertyChangedEventArgs("VRZ");

        bool _mute = false;
        private void _muteProp(Action action)
        {
            _mute = true;
            action.Invoke();
            _mute = false;
        }
        private void FrameUpdated(object sender, EventArgs e)
        {
            if (_cachePos != selectedGameObject.PositionNextFrame)
            {
                _cachePos = selectedGameObject.PositionNextFrame;
                _muteProp(() =>
                {
                    PropertyChanged?.Invoke(this, eaVPX);
                    PropertyChanged?.Invoke(this, eaVPY);
                    PropertyChanged?.Invoke(this, eaVPZ);
                });
            }
            if (_cacheRotQ != selectedGameObject.RotationNextFrame)
            {
                _cacheRot = QuaternionToEularYXZ(_cacheRotQ) / MathF.PI * 180;
                _cacheRotQ = selectedGameObject.RotationNextFrame;
                _muteProp(() =>
                {
                    PropertyChanged?.Invoke(this, eaVRX);
                    PropertyChanged?.Invoke(this, eaVRY);
                    PropertyChanged?.Invoke(this, eaVRZ);
                });
            }
        }

        public float VPX
        {
            get => _cachePos.X; set
            {
                if (_mute) return;
                _cachePos.X = value;
                UpdatePositionFromUI();
            }
        }
        public float VPY
        {
            get => _cachePos.Y; set
            {
                if (_mute) return;
                _cachePos.Y = value;
                UpdatePositionFromUI();
            }
        }
        public float VPZ
        {
            get => _cachePos.Z; set
            {
                if (_mute) return;
                _cachePos.Z = value;
                UpdatePositionFromUI();
            }
        }

        public float VRX
        {
            get => _cacheRot.X; set
            {
                if (_mute) return;
                _cacheRot.X = value;
                UpdateRotationFromUI();
            }
        }
        public float VRY
        {
            get => _cacheRot.Y; set
            {
                if (_mute) return;
                _cacheRot.Y = value;
                UpdateRotationFromUI();
            }
        }
        public float VRZ
        {
            get => _cacheRot.Z; set
            {
                if (_mute) return;
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

            selectedGameObject.RotationNextFrame = _cacheRotQ;
            appBody.RequireRender();
        }

        void UpdatePositionFromUI()
        {
            selectedGameObject.PositionNextFrame = _cachePos;
            appBody.RequireRender();
        }

        System.ComponentModel.PropertyChangedEventArgs eaName = new System.ComponentModel.PropertyChangedEventArgs("Name");
        public string vName
        {
            get => selectedGameObject.Name;
            set { selectedGameObject.Name = value; selectedGameObject.PropChange(eaName); }
        }

    }
}
