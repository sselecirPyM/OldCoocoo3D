using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Coocoo3D.Utility;

namespace Coocoo3D.UI
{
    public static class UIHelper
    {
        public static DirectoryInfo folder;

        public static void OnFrame(Coocoo3DMain appBody)
        {
            if (UIImGui.requireOpenFolder.SetFalse())
            {
                string path = OpenResourceFolder();
                if (!string.IsNullOrEmpty(path))
                {
                    folder = new DirectoryInfo(path);
                    UIImGui.viewRequest = folder;
                    appBody.mainCaches.AddFolder(folder);
                }
                appBody.RequireRender();
            }
            if (UIImGui.viewRequest != null)
            {
                var view = UIImGui.viewRequest;
                UIImGui.viewRequest = null;
                UIImGui.currentFolder = view;
                SetViewFolder(view.GetFileSystemInfos());
                appBody.RequireRender();
            }
            if (UIImGui.openRequest != null)
            {
                var requireOpen = UIImGui.openRequest;
                UIImGui.openRequest = null;
                var file = requireOpen.file;
                var folder = requireOpen.folder;

                string ext = file.Extension.ToLower();
                switch (ext)
                {
                    case ".pmx":
                        try
                        {
                            UI.UISharedCode.LoadEntityIntoScene(appBody, appBody.CurrentScene, file, folder);
                        }
                        catch (Exception exception)
                        {
                            throw;
                        }
                        break;
                    case ".vmd":
                        try
                        {
                            BinaryReader reader = new BinaryReader(file.OpenRead());
                            VMDFormat motionSet = VMDFormat.Load(reader);
                            if (motionSet.CameraKeyFrames.Count != 0)
                            {
                                appBody.RPContext.currentChannel.camera.cameraMotion.cameraKeyFrames = motionSet.CameraKeyFrames;
                                appBody.RPContext.currentChannel.camera.CameraMotionOn = true;
                            }
                            else
                            {
                                foreach (var gameObject in appBody.SelectedGameObjects)
                                {
                                    var renderer = gameObject.GetComponent<Components.MMDRendererComponent>();
                                    if (renderer != null) { renderer.motionPath = file.FullName; }
                                }

                                appBody.GameDriverContext.RequireResetPhysics = true;
                            }
                        }
                        catch (Exception exception)
                        {
                            throw;
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
                string path = OpenResourceFolder();
                if (!string.IsNullOrEmpty(path))
                {
                    DirectoryInfo folder = new DirectoryInfo(path);
                    if (folder == null) return;
                    appBody._RecorderGameDriver.saveFolder = folder;
                    appBody._RecorderGameDriver.SwitchEffect();
                    appBody.GameDriver = appBody._RecorderGameDriver;
                    appBody.Recording = true;
                }
            }
        }

        static void Export()
        {

        }



        public static string OpenResourceFile(string filter)
        {
            FileOpenDialog dialog = new FileOpenDialog();
            dialog.structSize = Marshal.SizeOf(typeof(FileOpenDialog));
            dialog.filter = filter;
            dialog.file = new string(new char[2000]);
            dialog.maxFile = dialog.file.Length;

            dialog.initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
            dialog.flags = 0x00000008;
            GetOpenFileName(dialog);
            var chars = dialog.file.ToCharArray();

            return new string(chars, 0, Array.IndexOf(chars, '\0'));
        }

        public static string OpenResourceFolder()
        {
            OpenDialogDir openDialogDir = new OpenDialogDir();
            openDialogDir.pszDisplayName = new string(new char[2000]);
            openDialogDir.lpszTitle = "Open Project";
            IntPtr pidlPtr = SHBrowseForFolder(openDialogDir);
            char[] charArray = new char[2000];
            Array.Fill(charArray, '\0');

            SHGetPathFromIDList(pidlPtr, charArray);
            int length = Array.IndexOf(charArray, '\0');
            string fullDirPath = new String(charArray, 0, length);

            return fullDirPath;
        }

        static void SetViewFolder(IReadOnlyList<FileSystemInfo> items)
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

        [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern bool GetOpenFileName([In, Out] FileOpenDialog ofn);

        [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern bool GetSaveFileName([In, Out] FileOpenDialog ofn);

        [DllImport("shell32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SHBrowseForFolder([In, Out] OpenDialogDir ofn);

        [DllImport("shell32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern bool SHGetPathFromIDList([In] IntPtr pidl, [In, Out] char[] fileName);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class FileOpenDialog
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public String filter = null;
            public String customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public String file = null;
            public int maxFile = 0;
            public String fileTitle = null;
            public int maxFileTitle = 0;
            public String initialDir = null;
            public String title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public String defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public String templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class OpenDialogDir
        {
            public IntPtr hwndOwner = IntPtr.Zero;
            public IntPtr pidlRoot = IntPtr.Zero;
            public String pszDisplayName = null;
            public String lpszTitle = null;
            public UInt32 ulFlags = 0;
            public IntPtr lpfn = IntPtr.Zero;
            public IntPtr lParam = IntPtr.Zero;
            public int iImage = 0;
        }
    }
}
