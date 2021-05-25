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
using Windows.Storage.Pickers;

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
                ViewBone.ItemsSource = entity.rendererComponent.bones;
            }
            else
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "显示属性错误");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        Dictionary<string, PropertyChangedEventArgs> xArg = new Dictionary<string, PropertyChangedEventArgs>();
        void propChange(string s)
        {
            if (!xArg.TryGetValue(s, out var v))
            {
                v = new PropertyChangedEventArgs(s);
                xArg[s] = v;
            }
            PropertyChanged?.Invoke(this, v);
        }
        private void FrameUpdated(object sender, EventArgs e)
        {
            if (_cacheP != entity.PositionNextFrame)
            {
                _cacheP = entity.PositionNextFrame;
                propChange("VPX");
                propChange("VPY");
                propChange("VPZ");
            }
            if (_cacheRQ != entity.RotationNextFrame)
            {
                _cacheRQ = entity.RotationNextFrame;
                _cacheR = QuaternionToEularYXZ(_cacheRQ) * 180 / MathF.PI;
                propChange("VRX");
                propChange("VRY");
                propChange("VRZ");
            }
            if (currentSelectedMorph != null && !entity.rendererComponent.LockMotion)
            {
                int index = entity.morphStateComponent.stringMorphIndexMap[currentSelectedMorph.Name];
                if (_cahceMorphValue != entity.morphStateComponent.Weights.Origin[index])
                {
                    propChange(nameof(VMorphValue));
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

        public bool VLockMotion
        {
            get => entity.rendererComponent.LockMotion;
            set
            {
                if (entity.rendererComponent.LockMotion == value) return;
                entity.rendererComponent.LockMotion = value;
                appBody.RequireRender(true);
                propChange(nameof(VLockMotion));
            }
        }

        void UpdatePositionFromUI()
        {
            entity.PositionNextFrame = _cacheP;
            appBody.RequireRender();
        }
        void UpdateRotationFromUI()
        {
            //_cacheRQ = EularToQuaternionYXZ(_cacheR / 180 * MathF.PI);
            var t1 = _cacheR / 180 * MathF.PI;
            _cacheRQ = Quaternion.CreateFromYawPitchRoll(t1.Y, t1.X, t1.Z);

            entity.RotationNextFrame = _cacheRQ;
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
                    entity.rendererComponent.mesh.GetVertexCount(), entity.rendererComponent.mesh.GetIndexCount() / 3, entity.rendererComponent.bones.Count);
            }
        }

        public MorphDesc currentSelectedMorph { get; set; }
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
                    _cahceMorphValue = entity.morphStateComponent.Weights.Origin[index];
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
                    entity.morphStateComponent.Weights.Origin[index] = value;
                    appBody.RequireRender(true);
                }
            }
        }
        private void ViewMorph_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            currentSelectedMorph = (e.AddedItems[0] as MorphDesc);
            propChange(nameof(currentSelectedMorph));
            propChange(nameof(VMorphValue));
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

        private void NumberBox_ValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
        {
            appBody.RequireRender();
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            appBody.RequireRender();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            appBody.RequireRender();
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        RuntimeMaterial SelectedMat;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SelectedMat = ((RuntimeMaterial)button.DataContext);
            propChange(nameof(SelectedMat));
            viewTex.ItemsSource = SelectedMat.textures;
            flyout1.ShowAt(button);
            _VTextureName = "";
            propChange(nameof(VTextureName));
            RenameButton.Visibility = Visibility.Collapsed;
        }

        public string VTextureName
        {
            get { return _VTextureName; }
            set { _VTextureName = value; }
        }
        string _VTextureName = string.Empty;

        private void viewTex_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (viewTex.SelectedItem == null)
            {
                RenameButton.Visibility = Visibility.Collapsed;
                return;
            }
            var p1 = (KeyValuePair<string, Coocoo3DGraphics.ITexture2D>)viewTex.SelectedItem;
            _VTextureName = p1.Key;
            propChange(nameof(VTextureName));
            RenameButton.Visibility = Visibility.Visible;
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (viewTex.SelectedItem == null) return;
            var p1 = (KeyValuePair<string, Coocoo3DGraphics.ITexture2D>)viewTex.SelectedItem;
            SelectedMat.textures[_VTextureName] = p1.Value;
            viewTex.ItemsSource = null;
            viewTex.ItemsSource = SelectedMat.textures;
            appBody.RequireRender();
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker imagePicker = new FileOpenPicker
            {
                FileTypeFilter =
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".bmp",
                    ".tif",
                    ".tiff",
                    ".tga",
                },
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SettingsIdentifier = "image",
            };
            var file = await imagePicker.PickSingleFileAsync();
            if (file == null) return;
            Random random = new Random();
            try
            {

                SelectedMat.textures[System.IO.Path.GetFileNameWithoutExtension(file.Name)] = await Coocoo3D.UI.UISharedCode.LoadTexture(appBody, file);
            }
            catch (Exception exception)
            {
                MessageDialog dialog = new MessageDialog(string.Format("error{0}", exception));
                await dialog.ShowAsync();
            }
            finally
            {
                appBody.RequireRender();
            }
            viewTex.ItemsSource = null;
        }
    }
}
