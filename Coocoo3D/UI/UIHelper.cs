using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Windows.Storage;
using Windows.UI.Popups;

namespace Coocoo3D.UI
{
    public static class UIHelper
    {
        public static async Task Code(Coocoo3DMain appBody)
        {
            if (UIImGui.requireOpenFolder)
            {
                UIImGui.requireOpenFolder = false;
                var folder = await UISharedCode.OpenResourceFolder(appBody);
                if (folder != null)
                {
                    UIImGui.currentFolder = folder;
                    var items = await folder.GetItemsAsync();
                    SetViewFolder(items);
                }
                appBody.RequireRender();
            }
            if (UIImGui.requireView != null)
            {
                var view = UIImGui.requireView;
                UIImGui.requireView = null;
                UIImGui.currentFolder = view;
                var items = await view.GetItemsAsync();
                SetViewFolder(items);
                appBody.RequireRender();
            }
            if (UIImGui.requireOpen != null)
            {
                var requireOpen = UIImGui.requireOpen;
                UIImGui.requireOpen = null;
                var file = requireOpen.file;
                var folder = requireOpen.folder;

                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                bool ExtEquals(string ext) => ext.Equals(file.FileType, StringComparison.CurrentCultureIgnoreCase);
                if (ExtEquals(".pmx"))
                {
                    try
                    {
                        await UI.UISharedCode.LoadEntityIntoScene(appBody, appBody.CurrentScene, file, folder);
                    }
                    catch (Exception exception)
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_PMXError"), exception));
                        await dialog.ShowAsync();
                    }
                }
                else if (ExtEquals(".vmd"))
                {
                    try
                    {
                        BinaryReader reader = new BinaryReader((await file.OpenReadAsync()).AsStreamForRead());
                        VMDFormat motionSet = VMDFormat.Load(reader);
                        if (motionSet.CameraKeyFrames.Count != 0)
                        {
                            appBody.camera.cameraMotion.cameraKeyFrames = motionSet.CameraKeyFrames;
                            appBody.camera.CameraMotionOn = true;
                        }
                        else
                        {
                            foreach (var gameObject in appBody.SelectedGameObjects)
                            {
                                gameObject.GetComponent<Components.MMDMotionComponent>()?.Reload(motionSet);
                            }
                            appBody.GameDriverContext.RequireResetPhysics = true;
                        }
                    }
                    catch (Exception exception)
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_VMDError"), exception));
                        await dialog.ShowAsync();
                    }
                }
                else if (ExtEquals(".coocoox"))
                {
                    try
                    {
                        await UI.UISharedCode.LoadPassSetting(appBody, file, folder);
                    }
                    catch (Exception exception)
                    {
                        MessageDialog dialog = new MessageDialog(string.Format("error{0}", exception));
                        await dialog.ShowAsync();
                    }
                }
                appBody.RequireRender(true);
            }
            if (UIImGui.requireRecord)
            {
                UIImGui.requireRecord = false;
                await UISharedCode.Record(appBody);
            }
        }

        static void SetViewFolder(IReadOnlyList<IStorageItem> items)
        {
            lock (UIImGui.storageItems)
            {
                UIImGui.storageItems.Clear();
                foreach (var item in items)
                {
                    UIImGui.storageItems.Add(item);
                }
            }
        }
    }
}
