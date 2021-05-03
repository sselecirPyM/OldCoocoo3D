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
    }
}
