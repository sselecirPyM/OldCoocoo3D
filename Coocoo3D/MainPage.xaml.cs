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
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Controls;
using Coocoo3DGraphics;
using Coocoo3D.FileFormat;
using Coocoo3D.Core;

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

        private void AddPage(TabView tabView, string header, Type pageType, object navParam)
        {
            Frame frame1 = new Frame();
            frame1.Navigate(pageType, navParam);
            tabView.TabItems.Add(new TabViewItem()
            {
                Header = header,
                Content = frame1,
            });
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            ;
            AddPage(tabViewL1, resourceLoader.GetString("Tab_Title_Common"), typeof(PropertiesPages.CommonPage), appBody);
            AddPage(tabViewL1, resourceLoader.GetString("Tab_Title_SkyBox"), typeof(PropertiesPages.SkyBoxPage), appBody);
            AddPage(tabViewL1, resourceLoader.GetString("Tab_Title_Record"), typeof(PropertiesPages.RecordPage), appBody);
            AddPage(tabViewR1, resourceLoader.GetString("Tab_Title_Scene"), typeof(PropertiesPages.ScenePage), appBody);
            AddPage(tabViewR1, resourceLoader.GetString("Tab_Title_PostProcess"), typeof(PropertiesPages.PostProcessPage), appBody);
            AddPage(tabViewB1, resourceLoader.GetString("Tab_Title_Resources"), typeof(PropertiesPages.ResourcesPage), appBody);

            Frame frame1 = new Frame();
            frame1.Navigate(typeof(PropertiesPages.EmptyPropertiesPage));
            appBody.frameViewProperties = frame1;
            tabViewR2.TabItems.Add(new TabViewItem()
            {
                Header = resourceLoader.GetString("Tab_Title_Detail"),
                Content = frame1,
            });
            Window.Current.Activated += Current_Activated;
        }

        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (appBody.Recording) return;
            if (appBody.performaceSettings.AutoReloadShaders)
                appBody.mainCaches.ReloadShaders(appBody.ProcessingList, appBody.RPAssetsManager,appBody.RequireRender);
            if (appBody.performaceSettings.AutoReloadTextures)
                appBody.mainCaches.ReloadTextures(appBody.ProcessingList,appBody.RequireRender);
            if (appBody.performaceSettings.AutoReloadModels)
                appBody.GameDriverContext.ReqireReloadModel();
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            await UI.UISharedCode.OpenResourceFolder(appBody);
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


        private void TabView_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            var x = args.Tab;
            args.Data.Properties.Add("Tab", x);
            args.Data.Properties.Add("Owner", sender);
        }

        private void TabView_DragOver(object sender, DragEventArgs e)
        {
            var container = (sender as TabView);
            if (e.DataView.Properties.TryGetValue("Owner", out object ownerData) &&
                container != ownerData)
            {
                if (e.DataView.Properties.TryGetValue("Tab", out object tab))
                {
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                }
            }
        }

        private void TabView_Drop(object sender, DragEventArgs e)
        {
            var container = (sender as TabView);
            if (e.DataView.Properties.TryGetValue("Owner", out object ownerData) &&
                container != ownerData)
            {
                if (e.DataView.Properties.TryGetValue("Tab", out object tab))
                {
                    (ownerData as TabView).TabItems.Remove(tab);
                    container.TabItems.Add(tab);
                }
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            appBody.ShowDetailPage(typeof(PropertiesPages.SoftwareInfoPropertiesPage), appBody);
        }
        private async void SampleShader_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker fileSavePicker = new FileSavePicker()
            {
                FileTypeChoices =
                    {
                        { ".hlsl",new string[]{".hlsl"} }
                    },
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "Sample.hlsl",
                SettingsIdentifier = "SampleShader",
            };
            var sf = await fileSavePicker.PickSaveFileAsync();
            if (sf == null) return;
            var f = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Samples/SampleShader.hlsl"));
            await f.CopyAndReplaceAsync(sf);
        }

        private void worldViewer_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void RadioMenuFlyoutItem1_Click(object sender, RoutedEventArgs e)
        {
            x1.Width = new GridLength(240);
            x2.Height = new GridLength(180);
            x3.Width = new GridLength(240);
        }
        private void RadioMenuFlyoutItem2_Click(object sender, RoutedEventArgs e)
        {
            x1.Width = new GridLength(360);
            x2.Height = new GridLength(270);
            x3.Width = new GridLength(360);
        }
        private void RadioMenuFlyoutItem3_Click(object sender, RoutedEventArgs e)
        {
            x1.Width = new GridLength(360);
            x2.Height = new GridLength(360);
            x3.Width = new GridLength(360);
        }
    }
}
