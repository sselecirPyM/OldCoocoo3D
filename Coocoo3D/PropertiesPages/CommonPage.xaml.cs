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
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Coocoo3D.FileFormat;
using Windows.UI.Popups;

namespace Coocoo3D.PropertiesPages
{
    public sealed partial class CommonPage : Page, INotifyPropertyChanged
    {
        public CommonPage()
        {
            this.InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        Coocoo3DMain appBody;

        uint[] comboBox1Values = new uint[6];
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Coocoo3DMain _appBody)
            {
                appBody = _appBody;
                appBody.FrameUpdated += FrameUpdated;
                _cachePos = appBody.camera.LookAtPoint;
                _cacheRot = appBody.camera.Angle;
                _cacheFOV = appBody.camera.Fov;
                _cacheDistance = appBody.camera.Distance;
                _cachePlaySpeed = appBody.GameDriverContext.PlaySpeed;
                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                string raytracingSupportMsg;
                if (appBody.deviceResources.IsRayTracingSupport())
                {
                    raytracingSupportMsg = resourceLoader.GetString("Message_GPUSupportRayTracing");
                }
                else
                {
                    raytracingSupportMsg = resourceLoader.GetString("Message_GPUNotSupportRayTracing");
                }
                VRayTracingSupport.Text = string.Format("{0}\n{1}", appBody.deviceResources.GetDeviceDescription(), raytracingSupportMsg);
                for (int i = 0; i < comboBox1Values.Length; i++)
                {
                    comboBox1Values[i] = (uint)i;
                }
                vQuality.ItemsSource = comboBox1Values;
            }
            else
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "error");
            }
        }

        #region view property
        PropertyChangedEventArgs eaVPX = new PropertyChangedEventArgs("VPX");
        PropertyChangedEventArgs eaVPY = new PropertyChangedEventArgs("VPY");
        PropertyChangedEventArgs eaVPZ = new PropertyChangedEventArgs("VPZ");
        PropertyChangedEventArgs eaVRX = new PropertyChangedEventArgs("VRX");
        PropertyChangedEventArgs eaVRY = new PropertyChangedEventArgs("VRY");
        PropertyChangedEventArgs eaVRZ = new PropertyChangedEventArgs("VRZ");
        PropertyChangedEventArgs eaVFOV = new PropertyChangedEventArgs("VFOV");
        PropertyChangedEventArgs eaVD = new PropertyChangedEventArgs("VD");
        PropertyChangedEventArgs eaVCameraMotionOn = new PropertyChangedEventArgs("VCameraMotionOn");
        PropertyChangedEventArgs eaVPlaySpeed = new PropertyChangedEventArgs("VPlaySpeed");
        //long[] txs = new long[8];
        int prevRenderCount = 0;
        int prevVirtualRenderCount = 0;
        private void FrameUpdated(object sender, EventArgs e)
        {
            if (_cachePos != appBody.camera.LookAtPoint)
            {
                _cachePos = appBody.camera.LookAtPoint;
                PropertyChanged?.Invoke(this, eaVPX);
                PropertyChanged?.Invoke(this, eaVPY);
                PropertyChanged?.Invoke(this, eaVPZ);
            }
            if (_cacheRot != appBody.camera.Angle)
            {
                _cacheRot = appBody.camera.Angle;
                PropertyChanged?.Invoke(this, eaVRX);
                PropertyChanged?.Invoke(this, eaVRY);
                PropertyChanged?.Invoke(this, eaVRZ);
            }
            if (_cacheFOV != appBody.camera.Fov)
            {
                _cacheFOV = appBody.camera.Fov;
                PropertyChanged?.Invoke(this, eaVFOV);
            }
            if (_cacheFOV != appBody.camera.Distance)
            {
                _cacheDistance = appBody.camera.Distance;
                PropertyChanged?.Invoke(this, eaVD);
            }
            if (_cacheCameraMotionOn != appBody.camera.CameraMotionOn)
            {
                _cacheCameraMotionOn = appBody.camera.CameraMotionOn;
                PropertyChanged?.Invoke(this, eaVCameraMotionOn);
            }
            if (_cachePlaySpeed != appBody.GameDriverContext.PlaySpeed)
            {
                _cachePlaySpeed = appBody.GameDriverContext.PlaySpeed;
                PropertyChanged?.Invoke(this, eaVPlaySpeed);
            }
            DateTime Now = DateTime.Now;
            if (Now - PrevUpdateTime > TimeSpan.FromSeconds(1))
            {
                int capRenderCount = appBody.CompletedRenderCount;
                int capVRenderCount = appBody.VirtualRenderCount;
                if (capVRenderCount - prevVirtualRenderCount > 0)
                {
                    ViewFrameRate.Text = string.Format("Virtual FPS: {0}", (TimeSpan.FromSeconds(1) / (Now - PrevUpdateTime) * (capVRenderCount - prevVirtualRenderCount)).ToString(".0"));
                }
                else
                {
                    ViewFrameRate.Text = string.Format("FPS: {0}", (TimeSpan.FromSeconds(1) / (Now - PrevUpdateTime) * (capRenderCount - prevRenderCount)).ToString(".0"));
                }

                PrevUpdateTime = Now;
                prevRenderCount = capRenderCount;
                prevVirtualRenderCount = capVRenderCount;
            }
            //Array.Copy(appBody.StopwatchTimes, txs, appBody.StopwatchTimes.Length);
            //showt1.Text = txs[0].ToString();
            //showt2.Text = txs[1].ToString();
            //showt3.Text = txs[2].ToString();
            //showt4.Text = txs[3].ToString();
            //showt5.Text = txs[4].ToString();
        }
        DateTime PrevUpdateTime = DateTime.Now;

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
        Vector3 _cachePos;

        public float VRX
        {
            get => _cacheRot.X * 180.0f / MathF.PI; set
            {
                _cacheRot.X = value * MathF.PI / 180.0f;
                UpdateRotationFromUI();
            }
        }
        public float VRY
        {
            get => _cacheRot.Y * 180.0f / MathF.PI; set
            {
                _cacheRot.Y = value * MathF.PI / 180.0f;
                UpdateRotationFromUI();
            }
        }
        public float VRZ
        {
            get => _cacheRot.Z * 180.0f / MathF.PI; set
            {
                _cacheRot.Z = value * MathF.PI / 180.0f;
                UpdateRotationFromUI();
            }
        }
        Vector3 _cacheRot;
        public float VFOV
        {
            get => _cacheFOV * 180.0f / MathF.PI; set
            {
                _cacheFOV = value * MathF.PI / 180.0f;
                appBody.camera.Fov = value * MathF.PI / 180.0f;
                appBody.RequireRender();
            }
        }
        float _cacheFOV;
        public float VD
        {
            get => -_cacheDistance; set
            {
                _cacheDistance = -value;
                appBody.camera.Distance = -value;
                appBody.RequireRender();
            }
        }
        float _cacheDistance;
        void UpdatePositionFromUI()
        {
            appBody.camera.LookAtPoint = _cachePos;
            appBody.RequireRender();
        }
        void UpdateRotationFromUI()
        {
            appBody.camera.Angle = _cacheRot;
            appBody.RequireRender();
        }
        float _cachePlaySpeed;
        public float VPlaySpeed
        {
            get => _cachePlaySpeed; set
            {
                _cachePlaySpeed = value;
                appBody.GameDriverContext.PlaySpeed = value;
            }
        }

        public bool VViewBone
        {
            get
            {
                return appBody.settings.viewSelectedEntityBone;
            }
            set
            {
                appBody.settings.viewSelectedEntityBone = value;
                appBody.RequireRender();
            }
        }

        public bool VViewerUI
        {
            get => appBody.settings.ViewerUI;
            set
            {
                appBody.settings.ViewerUI = value;
                appBody.RequireRender();
            }
        }

        public float VSetFps
        {
            get => appBody.Fps; set
            {
                appBody.Fps = Math.Max(value, 1);
                appBody.GameDriverContext.FrameInterval = TimeSpan.FromSeconds(1 / appBody.Fps);
            }
        }

        public bool VWireframe
        {
            get => appBody.settings.Wireframe; set
            {
                appBody.settings.Wireframe = value;
                appBody.RequireRender();
            }
        }
        public bool VEnableAO
        {
            get => appBody.settings.EnableAO; set
            {
                appBody.settings.EnableAO = value;
                appBody.RequireRender();
            }
        }
        public bool VEnableShadow
        {
            get => appBody.settings.EnableShadow; set
            {
                appBody.settings.EnableShadow = value;
                appBody.RequireRender();
            }
        }
        public bool VCameraMotionOn
        {
            get => appBody.camera.CameraMotionOn; set
            {
                appBody.camera.CameraMotionOn = value;
                appBody.RequireRender();
            }
        }
        public bool VAutoReloadShader
        {
            get => appBody.performaceSettings.AutoReloadShaders;
            set => appBody.performaceSettings.AutoReloadShaders = value;
        }
        public bool VAutoReloadTexture
        {
            get => appBody.performaceSettings.AutoReloadTextures;
            set => appBody.performaceSettings.AutoReloadTextures = value;
        }
        //public bool VAutoReloadModel
        //{
        //    get => appBody.performaceSettings.AutoReloadModels;
        //    set => appBody.performaceSettings.AutoReloadModels = value;
        //}

        bool _cacheCameraMotionOn;
        #endregion
        bool muted = false;
        private void VRenderPipeline_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (appBody == null) return;
            if (muted) return;
            int selectedIndex = (sender as ComboBox).SelectedIndex;
            if (!appBody.deviceResources.IsRayTracingSupport() && selectedIndex == 2)
            {
                (sender as ComboBox).SelectedIndex = 0;
            }
            else if(selectedIndex == 3)
            {
                appBody.SwitchToRenderPipeline(selectedIndex);
                muted = true;
                (sender as ComboBox).SelectedIndex = 0;
                muted = false;
            }
            else
            {
                appBody.SwitchToRenderPipeline(selectedIndex);
            }
            appBody.RequireRender();
        }

        private void VQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            appBody.settings.Quality = (uint)(sender as ComboBox).SelectedValue;
            appBody.RequireRender();
        }

        private void PhysicsReset_Click(object sender, RoutedEventArgs e)
        {
            appBody.GameDriverContext.RequireResetPhysics = true;
            appBody.RequireRender(true);
        }

        //private void NewFun_Click(object sender, RoutedEventArgs e)
        //{
        //    appBody.UseNewFun = !appBody.UseNewFun;
        //}

        private bool StrEq(string a, string b)
        {
            return a.Equals(b, StringComparison.CurrentCultureIgnoreCase);
        }
        private bool IsMotionExtName(string extName)
        {
            return StrEq(".vmd", extName);
        }
        private void Page_DragOver(object sender, DragEventArgs e)
        {
            Image image = sender as Image;
            if (e.DataView.Properties.TryGetValue("ExtName", out object object1))
            {
                string extName = object1 as string;
                if (extName != null && IsMotionExtName(extName))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
            }
        }

        private async void Page_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Properties.TryGetValue("ExtName", out object object1)) return;
            string extName = object1 as string;
            if (extName != null)
            {
                e.DataView.Properties.TryGetValue("File", out object object2);
                StorageFile storageFile = object2 as StorageFile;
                e.DataView.Properties.TryGetValue("Folder", out object object3);
                StorageFolder storageFolder = object3 as StorageFolder;

                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                if (StrEq(".vmd", extName))
                {
                    try
                    {
                        VMDFormat motionFile = new VMDFormat();
                        motionFile.Reload(new BinaryReader(await storageFile.OpenStreamForReadAsync()));
                        appBody.camera.cameraMotion.cameraKeyFrames = motionFile.CameraKeyFrames;
                        vCameraMotionOn.IsEnabled = true;
                        appBody.camera.CameraMotionOn = true;
                    }
                    catch (Exception exception)
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_VMDError"), exception));
                        await dialog.ShowAsync();
                    }
                }
            }
        }

        private void ReloadTextures_Click(object sender, RoutedEventArgs e)
        {
            appBody.mainCaches.ReloadTextures(appBody.ProcessingList, appBody.RequireRender);
        }
    }
}