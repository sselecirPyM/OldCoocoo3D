using Coocoo3D.FileFormat;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Coocoo3D.Core;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Coocoo3D.PropertiesPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class ResourcesPage : Page
    {
        public ResourcesPage()
        {
            this.InitializeComponent();
        }
        Coocoo3DMain appBody;

        List<StorageFolder> viewFolderStack = new List<StorageFolder>();

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            appBody = e.Parameter as Coocoo3DMain;
            if (appBody == null)
            {
                Frame.Navigate(typeof(ErrorPropertiesPage), "error");
                return;
            }
            appBody.OpenedStorageFolderChanged += AppBody_OpenedStorageFolderChanged;
        }

        private async void AppBody_OpenedStorageFolderChanged(object sender, EventArgs e)
        {
            viewFolderStack.Clear();
            viewFolderStack.Add(appBody.openedStorageFolder);
            vPath.Text = appBody.openedStorageFolder.Name;
            viewResource.ItemsSource = await appBody.openedStorageFolder.GetItemsAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            appBody.OpenedStorageFolderChanged -= AppBody_OpenedStorageFolderChanged;
        }
        bool HaveLoadTask = false;
        private async void Grid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            Grid grid = sender as Grid;
            if (grid.DataContext is StorageFolder folder)
            {
                viewFolderStack.Add(folder);
                await SetFolder();
            }
            else if (grid.DataContext is StorageFile file && !HaveLoadTask)
            {
                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                if (file.FileType.Equals(".pmx", StringComparison.CurrentCultureIgnoreCase))
                {
                    try
                    {
                        await UI.UISharedCode.LoadEntityIntoScene(appBody, appBody.CurrentScene, file, viewFolderStack.Last());
                    }
                    catch (Exception exception)
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_PMXError"), exception));
                        await dialog.ShowAsync();
                    }
                }
                else if (file.FileType.Equals(".vmd", StringComparison.CurrentCultureIgnoreCase))
                {
                    HaveLoadTask = true;
                    try
                    {
                        BinaryReader reader = new BinaryReader((await file.OpenReadAsync()).AsStreamForRead());
                        VMDFormat motionSet = VMDFormat.Load(reader);
                        lock (appBody.deviceResources)
                        {
                            foreach (var entity in appBody.SelectedEntities)
                            {
                                entity.motionComponent.Reload(motionSet);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_VMDError"), exception));
                        await dialog.ShowAsync();
                        HaveLoadTask = false;
                        return;
                    }
                    appBody.GameDriverContext.RequireResetPhysics = true;
                    appBody.RequireRender(true);
                    HaveLoadTask = false;
                }
                else if (file.FileType.Equals(".hlsl", StringComparison.CurrentCultureIgnoreCase))
                {
                    UI.UISharedCode.LoadShaderForEntities1(appBody, file, viewFolderStack.Last(), new List<Present.MMD3DEntity>(appBody.SelectedEntities));
                }
            }
        }

        private async void FolderBack_Click(object sender, RoutedEventArgs e)
        {
            if (viewFolderStack.Count > 1)
            {
                viewFolderStack.RemoveAt(viewFolderStack.Count - 1);
                await SetFolder();
            }
        }
        private async void FolderRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (viewFolderStack.Count > 0)
            {
                await SetFolder();
            }
        }
        async Task SetFolder()
        {
            try
            {
                viewResource.ItemsSource = await viewFolderStack.Last().GetItemsAsync();
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(viewFolderStack[0].Name);
                for (int i = 1; i < viewFolderStack.Count; i++)
                {
                    stringBuilder.Append('/');
                    stringBuilder.Append(viewFolderStack[i].Name);
                }
                vPath.Text = stringBuilder.ToString();
            }
            catch
            {
                if (viewFolderStack.Count > 0)
                {
                    viewFolderStack.RemoveAt(viewFolderStack.Count - 1);
                    await SetFolder();
                }
                else
                {
                    viewResource.ItemsSource = null;
                    vPath.Text = "";
                }
            }
        }

        private void ViewResource_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count == 1 && e.Items.First() is StorageFile storageFile)
            {
                e.Data.Properties.Add("File", storageFile);
                e.Data.Properties.Add("Folder", viewFolderStack.Last());
                e.Data.Properties.Add("ExtName", storageFile.FileType);
            }
        }
    }
    public class ViewFileTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FileTemplate { get; set; }
        public DataTemplate FolderTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is StorageFile) return FileTemplate;
            if (item is StorageFolder) return FolderTemplate;
            else return null;
        }
    }
}
