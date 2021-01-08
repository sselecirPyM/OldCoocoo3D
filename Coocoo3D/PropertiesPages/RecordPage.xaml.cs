using Coocoo3D.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
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
    public sealed partial class RecordPage : Page
    {
        public RecordPage()
        {
            this.InitializeComponent();
        }
        Coocoo3DMain appBody;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Coocoo3DMain _appBody)
            {
                appBody = _appBody;
            }
            else
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "显示属性错误");
            }
        }

        private async void Record_Click(object sender, RoutedEventArgs e)
        {
            if (!appBody.Recording)
            {
                FolderPicker folderPicker = new FolderPicker()
                {
                    FileTypeFilter =
                    {
                        "*"
                    },
                    SuggestedStartLocation = PickerLocationId.VideosLibrary,
                    ViewMode = PickerViewMode.Thumbnail,
                    SettingsIdentifier = "RecordFolder",
                };
                Windows.Storage.StorageFolder folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null) return;
                appBody._RecorderGameDriver.saveFolder = folder;
                appBody._RecorderGameDriver.SwitchEffect();
                appBody.GameDriver = appBody._RecorderGameDriver;
                appBody.Recording = true;
            }
            else
            {
                appBody.GameDriver = appBody._GeneralGameDriver;
                appBody.Recording = false;
            }
        }

        public float VRFPS
        {
            get { return appBody.GameDriverContext.recordSettings.FPS; }
            set { appBody.GameDriverContext.recordSettings.FPS = Math.Clamp(value, 1, 1000); }
        }

        public float VPStart
        {
            get { return appBody.GameDriverContext.recordSettings.StartTime; }
            set { appBody.GameDriverContext.recordSettings.StartTime = value; }
        }
        public float VPStop
        {
            get { return appBody.GameDriverContext.recordSettings.StopTime; }
            set { appBody.GameDriverContext.recordSettings.StopTime = value; }
        }

        public int VRWidth
        {
            get { return appBody.GameDriverContext.recordSettings.Width; }
            set { appBody.GameDriverContext.recordSettings.Width = value; }
        }
        public int VRHeight
        {
            get { return appBody.GameDriverContext.recordSettings.Height; }
            set { appBody.GameDriverContext.recordSettings.Height = value; }
        }
    }
}
