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
using Windows.Storage.Pickers;
using Coocoo3DGraphics;
using Coocoo3D.FileFormat;
using Coocoo3D.Core;
using Windows.UI.Popups;
using Windows.ApplicationModel;
using Windows.Storage;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace Coocoo3D
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Coocoo3DMain appBody;
        public MainPage()
        {
            this.InitializeComponent();
            appBody = new Coocoo3DMain();
            worldViewer.AppBody = appBody;
            appBody.FrameUpdated += AppBody_FrameUpdated;
        }

        private void AppBody_FrameUpdated(object sender, EventArgs e)
        {
            ForceAudioAsync();
        }

        public void ForceAudioAsync() => AudioAsync(appBody.GameDriverContext.PlayTime, appBody.GameDriverContext.Playing);
        TimeSpan audioMaxInaccuracy = TimeSpan.FromSeconds(1.0 / 30.0);
        private void AudioAsync(double time, bool playing)
        {
            if (playing && appBody.GameDriverContext.PlaySpeed == 1.0f)
            {
                if (mediaElement.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Paused ||
                    mediaElement.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Stopped)
                {
                    mediaElement.Play();
                }
                if (mediaElement.IsAudioOnly)
                {
                    if (TimeSpan.FromSeconds(time) - mediaElement.Position > audioMaxInaccuracy ||
                        mediaElement.Position - TimeSpan.FromSeconds(time) > audioMaxInaccuracy)
                    {
                        mediaElement.Position = TimeSpan.FromSeconds(time);
                    }
                }
                else
                {
                    if (TimeSpan.FromSeconds(time) - mediaElement.Position > audioMaxInaccuracy ||
                           mediaElement.Position - TimeSpan.FromSeconds(time) > audioMaxInaccuracy)
                    {
                        mediaElement.Position = TimeSpan.FromSeconds(time);
                    }
                }
            }
            else if (mediaElement.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Playing)
            {
                mediaElement.Pause();
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Window.Current.Activated += Current_Activated;
        }

        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (appBody.Recording) return;
            if (appBody.performaceSettings.AutoReloadTextures)
                appBody.mainCaches.ReloadTextures();
            //if (appBody.performaceSettings.AutoReloadModels)
            //    appBody.GameDriverContext.ReqireReloadModel();
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            UI.UIImGui.requireOpenFolder = true;
            //await UI.UISharedCode.OpenResourceFolder(appBody);
        }
        private async void OpenMedia_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker mediaPicker = new FileOpenPicker
            {
                FileTypeFilter =
                {
                    ".mp3",
                    ".m4a",
                    ".wav",
                    ".mp4",
                },
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
                SettingsIdentifier = "media",
            };
            var file = await mediaPicker.PickSingleFileAsync();
            if (file == null) return;
            mediaElement.SetSource(await file.OpenReadAsync(), "");
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            UI.PlayControl.Play(appBody);
            ForceAudioAsync();

        }
        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            UI.PlayControl.Pause(appBody);
            ForceAudioAsync();
        }
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            UI.PlayControl.Stop(appBody);
            ForceAudioAsync();
        }
        private void Rewind_Click(object sender, RoutedEventArgs e)
        {
            UI.PlayControl.Rewind(appBody);
            ForceAudioAsync();
        }
        private void FastForward_Click(object sender, RoutedEventArgs e)
        {
            UI.PlayControl.FastForward(appBody);
            ForceAudioAsync();
        }
        private void Front_Click(object sender, RoutedEventArgs e)
        {
            if (appBody.Recording)
            {
                appBody.GameDriver = appBody._GeneralGameDriver;
                appBody.Recording = false;
            }
            appBody.GameDriverContext.PlayTime = 0;
            appBody.GameDriverContext.RequireResetPhysics = true;
            appBody.RequireRender(true);
        }
        private void Rear_Click(object sender, RoutedEventArgs e)
        {
            if (appBody.Recording)
            {
                appBody.GameDriver = appBody._GeneralGameDriver;
                appBody.Recording = false;
            }
            appBody.GameDriverContext.PlayTime = 9999;
            appBody.GameDriverContext.RequireResetPhysics = true;
            appBody.RequireRender(true);
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
                StorageFolder folder = await folderPicker.PickSingleFolderAsync();
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


        private async void SampleShader_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new FolderPicker()
            {
                FileTypeFilter =
                    {
                        "*"
                    },
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail,
                SettingsIdentifier = "SampleShader",
            };
            try
            {
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null) return;
                var sampleFolder = await Package.Current.InstalledLocation.GetFolderAsync("Samples\\");
                foreach (var item in await sampleFolder.GetFilesAsync())
                {
                    var file = await folder.CreateFileAsync(item.Name, CreationCollisionOption.OpenIfExists);
                    await item.CopyAndReplaceAsync(file);
                }
            }
            catch (Exception exception)
            {
                MessageDialog dialog = new MessageDialog(string.Format("error:{0}", exception));
                await dialog.ShowAsync();
            }
        }

        private void worldViewer_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            UI.UIImGui.requireExport = true;
        }
    }
}
