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

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = viewSceneObjects.SelectedItems;
            while (0 < selectedItems.Count)
            {
                UI.UISharedCode.RemoveSceneObject(appBody, appBody.CurrentScene, (ISceneObject)selectedItems[0]);
            }
        }

        private void ViewSceneObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IList<object> selectedItem = (sender as ListView).SelectedItems;
            lock (appBody.selectedObjcetLock)
            {
                appBody.SelectedEntities.Clear();
                appBody.SelectedLighting.Clear();
                for (int i = 0; i < selectedItem.Count; i++)
                {
                    if (selectedItem[i] is MMD3DEntity entity)
                        appBody.SelectedEntities.Add(entity);
                    else if (selectedItem[i] is Lighting lighting)
                        appBody.SelectedLighting.Add(lighting);

                }
                if (selectedItem.Count == 1)
                {
                    if (appBody.SelectedEntities.Count == 1)
                    {
                        appBody.ShowDetailPage(typeof(EntityPropertiesPage), appBody);
                    }
                    else if (appBody.SelectedLighting.Count == 1)
                    {
                        appBody.ShowDetailPage(typeof(LightingPropertiesPage), appBody);
                    }
                }
                else
                {
                    appBody.ShowDetailPage(typeof(EmptyPropertiesPage), null);
                }
            }
            appBody.RequireRender();
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
        public DataTemplate EntityTemplate { get; set; }
        public DataTemplate LightingTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is MMD3DEntity) return EntityTemplate;
            if (item is Lighting) return LightingTemplate;
            else return null;
        }
    }
}
