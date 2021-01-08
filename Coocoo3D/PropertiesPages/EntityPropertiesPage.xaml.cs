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
using Coocoo3D.Present;
using System.ComponentModel;
using System.Numerics;
using Coocoo3D.Core;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Coocoo3D.Components;
using Windows.UI.Popups;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Coocoo3D.PropertiesPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class EntityPropertiesPage : Page, INotifyPropertyChanged
    {
        public EntityPropertiesPage()
        {
            this.InitializeComponent();
        }
        Coocoo3DMain appBody;
        MMD3DEntity entity;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (e.Parameter is Coocoo3DMain _appBody)
            {
                appBody = _appBody;
                entity = _appBody.SelectedEntities[0];
                appBody.FrameUpdated += FrameUpdated;
                _cacheP = entity.PositionNextFrame;
                _cacheR = QuaternionToEularYXZ(entity.RotationNextFrame) * 180 / MathF.PI;
                _cacheRQ = entity.RotationNextFrame;
                ViewMaterials.ItemsSource = entity.rendererComponent.Materials;
                ViewMorph.ItemsSource = entity.morphStateComponent.morphs;
                ViewBone.ItemsSource = entity.boneComponent.bones;
            }
            else
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "显示属性错误");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        PropertyChangedEventArgs eaVPX = new PropertyChangedEventArgs("VPX");
        PropertyChangedEventArgs eaVPY = new PropertyChangedEventArgs("VPY");
        PropertyChangedEventArgs eaVPZ = new PropertyChangedEventArgs("VPZ");
        PropertyChangedEventArgs eaVRX = new PropertyChangedEventArgs("VRX");
        PropertyChangedEventArgs eaVRY = new PropertyChangedEventArgs("VRY");
        PropertyChangedEventArgs eaVRZ = new PropertyChangedEventArgs("VRZ");
        private void FrameUpdated(object sender, EventArgs e)
        {
            if (_cacheP != entity.PositionNextFrame)
            {
                _cacheP = entity.PositionNextFrame;
                PropertyChanged?.Invoke(this, eaVPX);
                PropertyChanged?.Invoke(this, eaVPY);
                PropertyChanged?.Invoke(this, eaVPZ);
            }
            if (_cacheRQ != entity.RotationNextFrame)
            {
                _cacheRQ = entity.RotationNextFrame;
                _cacheR = QuaternionToEularYXZ(_cacheRQ) * 180 / MathF.PI;
                PropertyChanged?.Invoke(this, eaVRX);
                PropertyChanged?.Invoke(this, eaVRY);
                PropertyChanged?.Invoke(this, eaVRZ);
            }
            if (currentSelectedMorph != null && !entity.LockMotion)
            {
                int index = entity.morphStateComponent.stringMorphIndexMap[currentSelectedMorph.Name];
                if (_cahceMorphValue != entity.morphStateComponent.WeightOrigin[index])
                {
                    PropertyChanged?.Invoke(this, eaVMorphValue);
                }
            }
        }

        public float VPX
        {
            get => _cacheP.X; set
            {
                _cacheP.X = value;
                UpdatePositionFromUI();
            }
        }
        public float VPY
        {
            get => _cacheP.Y; set
            {
                _cacheP.Y = value;
                UpdatePositionFromUI();
            }
        }
        public float VPZ
        {
            get => _cacheP.Z; set
            {
                _cacheP.Z = value;
                UpdatePositionFromUI();
            }
        }
        Vector3 _cacheP;

        public float VRX
        {
            get => _cacheR.X; set
            {
                _cacheR.X = value;
                UpdateRotationFromUI();
            }
        }
        public float VRY
        {
            get => -_cacheR.Y; set
            {
                _cacheR.Y = -value;
                UpdateRotationFromUI();
            }
        }
        public float VRZ
        {
            get => -_cacheR.Z; set
            {
                _cacheR.Z = -value;
                UpdateRotationFromUI();
            }
        }
        Vector3 _cacheR;
        Quaternion _cacheRQ;

        PropertyChangedEventArgs eaVLockMotion = new PropertyChangedEventArgs("VLockMotion");
        public bool VLockMotion
        {
            get => entity.LockMotion;
            set
            {
                if (entity.LockMotion == value) return;
                entity.LockMotion = value;
                appBody.RequireRender(true);
                PropertyChanged?.Invoke(this, eaVLockMotion);
            }
        }

        void UpdatePositionFromUI()
        {
            entity.PositionNextFrame = _cacheP;
            entity.NeedTransform = true;
            appBody.RequireRender();
        }
        void UpdateRotationFromUI()
        {
            //_cacheRQ = EularToQuaternionYXZ(_cacheR / 180 * MathF.PI);
            var t1 = _cacheR / 180 * MathF.PI;
            _cacheRQ = Quaternion.CreateFromYawPitchRoll(t1.Y, t1.X, t1.Z);

            entity.RotationNextFrame = _cacheRQ;
            entity.NeedTransform = true;
            appBody.RequireRender();
        }

        PropertyChangedEventArgs eaName = new PropertyChangedEventArgs("Name");
        public string vName
        {
            get => entity.Name;
            set { entity.Name = value; entity.PropChange(eaName); }
        }
        public string vDesc
        {
            get => entity.Description;
            set { entity.Description = value; }
        }
        public string vModelInfo
        {
            get
            {
                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                return string.Format(resourceLoader.GetString("Message_ModelInfo"),
                    entity.rendererComponent.mesh.m_vertexCount, entity.rendererComponent.mesh.m_indexCount / 3, entity.boneComponent.bones.Count);
            }
        }

        public MorphDesc currentSelectedMorph { get; set; }
        PropertyChangedEventArgs eaMorph = new PropertyChangedEventArgs("currentSelectedMorph");
        PropertyChangedEventArgs eaVMorphValue = new PropertyChangedEventArgs("VMorphValue");
        public float _cahceMorphValue;
        public float VMorphValue
        {
            get
            {
                if (currentSelectedMorph == null)
                    return 0;
                else
                {
                    int index = entity.morphStateComponent.stringMorphIndexMap[currentSelectedMorph.Name];
                    _cahceMorphValue = entity.morphStateComponent.WeightOrigin[index];
                    return _cahceMorphValue;
                }
            }
            set
            {
                if (currentSelectedMorph == null)
                    return;
                else
                {
                    int index = entity.morphStateComponent.stringMorphIndexMap[currentSelectedMorph.Name];
                    _cahceMorphValue = value;
                    entity.morphStateComponent.WeightOrigin[index] = value;
                    entity.morphStateComponent.WeightOriginA[index] = value;
                    entity.morphStateComponent.WeightOriginB[index] = value;
                    appBody.RequireRender(true);
                }
            }
        }
        private void ViewMorph_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            currentSelectedMorph = (e.AddedItems[0] as MorphDesc);
            PropertyChanged?.Invoke(this, eaMorph);
            PropertyChanged?.Invoke(this, eaVMorphValue);
        }


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
        static Quaternion EularToQuaternionYXZ(Vector3 euler)
        {
            double cx = Math.Cos(euler.X * 0.5);
            double sx = Math.Sin(euler.X * 0.5);
            double cy = Math.Cos(euler.Y * 0.5);
            double sy = Math.Sin(euler.Y * 0.5);
            double cz = Math.Cos(euler.Z * 0.5);
            double sz = Math.Sin(euler.Z * 0.5);
            Quaternion result;
            result.W = (float)(cx * cy * cz + sx * sy * sz);
            result.X = (float)(sx * cy * cz + cx * sy * sz);
            result.Y = (float)(cx * sy * cz - sx * cy * sz);
            result.Z = (float)(cx * cy * sz - sx * sy * cz);
            return result;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (appBody != null)
            {
                appBody.FrameUpdated -= FrameUpdated;
            }
        }

        private async void Page_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Properties.TryGetValue("ExtName", out object object1))
            {
                string extName = object1 as string;
                if (extName != null)
                {
                    e.DataView.Properties.TryGetValue("File", out object object2);
                    StorageFile storageFile = object2 as StorageFile;
                    e.DataView.Properties.TryGetValue("Folder", out object object3);
                    StorageFolder storageFolder = object3 as StorageFolder;
                    var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                    if (".vmd".Equals(extName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        try
                        {
                            await UI.UISharedCode.LoadVMD(appBody, storageFile, entity);
                        }
                        catch (Exception exception)
                        {
                            MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_VMDError"), exception));
                            await dialog.ShowAsync();
                        }
                    }
                    if (".hlsl".Equals(extName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        UI.UISharedCode.LoadShaderForEntities1(appBody, storageFile, storageFolder, new MMD3DEntity[] { entity });
                    }
                }
            }
        }

        private void Page_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Properties.TryGetValue("ExtName", out object object1))
            {
                string extName = object1 as string;
                if (extName != null)
                {
                    if (".vmd".Equals(extName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                    }
                    else if (".hlsl".Equals(extName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                    }
                }
            }
        }

        private void NumberBox_ValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
        {
            appBody.RequireRender();
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            appBody.RequireRender();
        }

        PropertyChangedEventArgs eaCurrentMat = new PropertyChangedEventArgs("CurrentMat");
        RuntimeMaterial CurrentMat;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            CurrentMat = ((RuntimeMaterial)button.DataContext);
            PropertyChanged?.Invoke(this, eaCurrentMat);
            flyout1.ShowAt(button);
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
    public struct Bundle_Main_Entity_Mat
    {
        public Coocoo3DMain main;
        public MMD3DEntity entity;
        public RuntimeMaterial matLit;
    }
}
