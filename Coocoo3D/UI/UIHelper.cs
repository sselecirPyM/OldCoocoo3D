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
using Windows.Storage.Pickers;
using Coocoo3D.Utility;

namespace Coocoo3D.UI
{
    public static class UIHelper
    {
        public static async Task OnFrame(Coocoo3DMain appBody)
        {
            if (UIImGui.requireOpenFolder.SetFalse())
            {
                var folder = await OpenResourceFolder(appBody);
                if (folder != null)
                {
                    UIImGui.currentFolder = folder;
                    var items = await folder.GetItemsAsync();
                    SetViewFolder(items);
                }
                appBody.RequireRender();
            }
            if (UIImGui.requireExport.SetFalse())
            {
                var picker = new FileSavePicker()
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,

                };
                picker.FileTypeChoices.Add("gltf", new[] { ".gltf" });

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {

                }
            }
            if (UIImGui.viewRequest != null)
            {
                var view = UIImGui.viewRequest;
                UIImGui.viewRequest = null;
                UIImGui.currentFolder = view;
                var items = await view.GetItemsAsync();
                SetViewFolder(items);
                appBody.RequireRender();
            }
            if (UIImGui.openRequest != null)
            {
                var requireOpen = UIImGui.openRequest;
                UIImGui.openRequest = null;
                var file = requireOpen.file;
                var folder = requireOpen.folder;

                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                string ext = file.FileType.ToLower();
                switch (ext)
                {
                    case ".pmx":
                        try
                        {
                            await UI.UISharedCode.LoadEntityIntoScene(appBody, appBody.CurrentScene, file, folder);
                        }
                        catch (Exception exception)
                        {
                            MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_PMXError"), exception));
                            await dialog.ShowAsync();
                        }
                        break;
                    case ".vmd":
                        try
                        {
                            BinaryReader reader = new BinaryReader((await file.OpenReadAsync()).AsStreamForRead());
                            VMDFormat motionSet = VMDFormat.Load(reader);
                            if (motionSet.CameraKeyFrames.Count != 0)
                            {
                                appBody.RPContext.currentChannel.camera.cameraMotion.cameraKeyFrames = motionSet.CameraKeyFrames;
                                appBody.RPContext.currentChannel.camera.CameraMotionOn = true;
                            }
                            else
                            {

                                Components.MMDMotion motion = new Components.MMDMotion();
                                motion.Reload(motionSet);
                                appBody.mainCaches.motions[file.Path] = motion;

                                foreach (var gameObject in appBody.SelectedGameObjects)
                                {
                                    var renderer = gameObject.GetComponent<Components.MMDRendererComponent>();
                                    if (renderer != null) { renderer.motionPath = file.Path; }
                                }

                                appBody.GameDriverContext.RequireResetPhysics = true;
                            }
                        }
                        catch (Exception exception)
                        {
                            MessageDialog dialog = new MessageDialog(string.Format(resourceLoader.GetString("Error_Message_VMDError"), exception));
                            await dialog.ShowAsync();
                        }
                        break;
                    case ".jpg":
                    case ".png":
                    case ".tga":
                        try
                        {
                            UISharedCode.LoadTexture(appBody, file, folder);
                        }
                        catch
                        {

                        }
                        break;
                    //case ".coocoox":
                    //    try
                    //    {
                    //        await UI.UISharedCode.LoadPassSetting(appBody, file, folder);
                    //    }
                    //    catch (Exception exception)
                    //    {
                    //        MessageDialog dialog = new MessageDialog(string.Format("error{0}", exception));
                    //        await dialog.ShowAsync();
                    //    }
                    //    break;
                }
                appBody.RequireRender(true);
            }
            if (UIImGui.requireRecord.SetFalse())
            {
                await UISharedCode.Record(appBody);
            }
        }

        static void Export()
        {

        }

        static async Task<StorageFolder> OpenResourceFolder(Coocoo3DMain appBody)
        {
            FolderPicker folderPicker = new FolderPicker()
            {
                FileTypeFilter =
                {
                    "*"
                },
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail,
                SettingsIdentifier = "ResourceFolder",
            };
            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null) return null;
            return folder;
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
