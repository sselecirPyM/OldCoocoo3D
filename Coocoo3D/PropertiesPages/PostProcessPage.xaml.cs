using Coocoo3D.Core;
using Coocoo3D.ResourceWarp;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Coocoo3D.PropertiesPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class PostProcessPage : Page
    {
        public PostProcessPage()
        {
            this.InitializeComponent();
        }
        Coocoo3DMain appBody;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            appBody = e.Parameter as Coocoo3DMain;
            if (appBody == null)
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "error");
                return;
            }
        }


        private bool IsImageExtName(string extName)
        {
            string lower = extName.ToLower();
            switch (lower)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".tif":
                case ".tiff":
                case ".gif":
                //case ".tga":
                //case ".hdr":
                    return true;
                default:
                    return false;
            }
        }
        private void _img0_DragOver(object sender, DragEventArgs e)
        {
            Image image = sender as Image;
            if (e.DataView.Properties.TryGetValue("ExtName", out object object1))
            {
                string extName = object1 as string;
                if (extName != null && IsImageExtName(extName))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
            }
        }
        private async void _img0_Drop(object sender, DragEventArgs e)
        {
            Image image = sender as Image;
            if (e.DataView.Properties.TryGetValue("ExtName", out object object1))
            {
                string extName = object1 as string;
                if (extName != null)
                {
                    e.DataView.Properties.TryGetValue("File", out object object2);
                    StorageFile storageFile = object2 as StorageFile;
                    e.DataView.Properties.TryGetValue("Folder", out object object3);
                    StorageFolder storageFolder = object3 as StorageFolder;
                    if (IsImageExtName(extName))
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(await storageFile.OpenReadAsync());
                        image.Source = bitmap;
                        file = storageFile;
                        imgSize.x = bitmap.PixelWidth;
                        imgSize.y = bitmap.PixelHeight;
                    }
                }
            }
        }
        StorageFile file;
        int2 imgSize;
        struct int2
        {
            public int x;
            public int y;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (file == null)
            {
                return;
            }
            Uploader uploader = new Uploader();
            uploader.Texture2D(await FileIO.ReadBufferAsync(file), true, true);
            appBody.ProcessingList.AddObject(new Texture2DUploadPack(appBody.RPContext.postProcessBackground, uploader));
            appBody.RequireRender();
        }


        public float VGammaCorrection
        {
            get => appBody.postProcess.innerStruct.GammaCorrection;
            set
            {
                appBody.postProcess.innerStruct.GammaCorrection = value;
                appBody.RequireRender();
            }
        }

        public float VSaturation1
        {
            get => appBody.postProcess.innerStruct.Saturation1;
            set
            {
                appBody.postProcess.innerStruct.Saturation1 = value;
                appBody.RequireRender();
            }
        }

        public float VSaturation2
        {
            get => appBody.postProcess.innerStruct.Saturation2;
            set
            {
                appBody.postProcess.innerStruct.Saturation2 = value;
                appBody.RequireRender();
            }
        }

        public float VSaturation3
        {
            get => appBody.postProcess.innerStruct.Saturation3;
            set
            {
                appBody.postProcess.innerStruct.Saturation3 = value;
                appBody.RequireRender();
            }
        }

        public float VThreshold1
        {
            get => appBody.postProcess.innerStruct.Threshold1;
            set
            {
                appBody.postProcess.innerStruct.Threshold1 = value;
                appBody.RequireRender();
            }
        }

        public float VThreshold2
        {
            get => appBody.postProcess.innerStruct.Threshold2;
            set
            {
                appBody.postProcess.innerStruct.Threshold2 = value;
                appBody.RequireRender();
            }
        }

        public float VTransition1
        {
            get => appBody.postProcess.innerStruct.Transition1;
            set
            {
                appBody.postProcess.innerStruct.Transition1 = value;
                appBody.RequireRender();
            }
        }

        public float VTransition2
        {
            get => appBody.postProcess.innerStruct.Transition2;
            set
            {
                appBody.postProcess.innerStruct.Transition2 = value;
                appBody.RequireRender();
            }
        }

        public float VBackgroundFactory
        {
            get => appBody.postProcess.innerStruct.BackgroundFactory;
            set
            {
                appBody.postProcess.innerStruct.BackgroundFactory = value;
                appBody.RequireRender();
            }
        }
    }
}
