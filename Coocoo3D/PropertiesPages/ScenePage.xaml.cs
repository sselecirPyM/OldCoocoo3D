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
using Coocoo3D.Core;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Coocoo3D.PropertiesPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class ScenePage : Page
    {
        public ScenePage()
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
            viewSceneObjects.ItemsSource = appBody.CurrentScene.sceneObjects;
        }

        private void NewLighting_Click(object sender, RoutedEventArgs e)
        {
            UI.UISharedCode.NewLighting(appBody);
        }

        private void NewVolume_Click(object sender, RoutedEventArgs e)
        {
            UI.UISharedCode.NewVolume(appBody);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = viewSceneObjects.SelectedItems;
            while (0 < selectedItems.Count)
            {
                UI.UISharedCode.RemoveSceneObject(appBody, appBody.CurrentScene, (GameObject)selectedItems[0]);
            }
        }

        private void ViewSceneObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //IList<object> selectedItem = (sender as ListView).SelectedItems;
            //lock (appBody.SelectedGameObjects)
            //{
            //    appBody.SelectedGameObjects.Clear();
            //    for (int i = 0; i < selectedItem.Count; i++)
            //    {
            //        if (selectedItem[i] is GameObject gameObject)
            //            appBody.SelectedGameObjects.Add(gameObject);

            //    }
            //    if (selectedItem.Count == 1)
            //    {
            //        if (appBody.SelectedGameObjects.Count == 1)
            //        {
            //            appBody.ShowDetailPage(typeof(GameObjectPage), appBody);
            //        }
            //        else
            //        {
            //            appBody.ShowDetailPage(typeof(EmptyPropertiesPage), null);
            //        }
            //    }
            //    else
            //    {
            //        appBody.ShowDetailPage(typeof(EmptyPropertiesPage), null);
            //    }
            //}
            //appBody.RequireRender();
        }

        private void ViewSceneObjects_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
            {
                appBody.CurrentScene.SortObjects();
                appBody.RequireRender();
            }
        }
    }
    public class SceneObjectTemplateSelector : DataTemplateSelector
    {
        //public DataTemplate EntityTemplate { get; set; }
        public DataTemplate GameObjectTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            //if (item is MMD3DEntity) return EntityTemplate;
            if (item is GameObject) return GameObjectTemplate;
            else return null;
        }
    }
}
