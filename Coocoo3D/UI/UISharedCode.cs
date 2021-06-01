using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Coocoo3D.FileFormat;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System.Numerics;
using Windows.Storage;
using Coocoo3D.Utility;
using Coocoo3D.Core;
using Windows.Storage.Streams;
using System.Threading;
using Coocoo3D.ResourceWarp;
using Coocoo3D.RenderPipeline;

namespace Coocoo3D.UI
{
    public static class UISharedCode
    {
        public static async Task LoadEntityIntoScene(Coocoo3DMain appBody, Scene scene, StorageFile pmxFile, StorageFolder storageFolder)
        {
            string pmxPath = pmxFile.Path;
            string relatePath = pmxFile.Name;
            ModelPack pack = null;
            lock (appBody.mainCaches.ModelPackCaches)
            {
                pack = appBody.mainCaches.ModelPackCaches.GetOrCreate(pmxPath);
                if (pack.LoadTask == null && pack.Status != GraphicsObjectStatus.loaded)
                {
                    pack.LoadTask = Task.Run(async () =>
                    {
                        BinaryReader reader = new BinaryReader((await pmxFile.OpenReadAsync()).AsStreamForRead());
                        pack.lastModifiedTime = (await pmxFile.GetBasicPropertiesAsync()).DateModified;
                        pack.Reload2(reader);
                        pack.folder = storageFolder;
                        pack.relativePath = relatePath;
                        reader.Dispose();
                        appBody.ProcessingList.AddObject(pack.GetMesh());
                        pack.Status = GraphicsObjectStatus.loaded;
                        pack.LoadTask = null;
                    });
                }
            }
            if (pack.Status != GraphicsObjectStatus.loaded && pack.LoadTask != null) await pack.LoadTask;

            GameObject gameObject = new GameObject();
            gameObject.Reload2(appBody.ProcessingList, pack, GetTextureList(appBody, storageFolder, pack.pmx), pmxPath);
            scene.AddGameObject(gameObject);

            appBody.RequireRender();
            appBody.mainCaches.ReloadTextures(appBody.ProcessingList, appBody.RequireRender);
        }
        public static void NewLighting(Coocoo3DMain appBody)
        {
            //var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            GameObject lighting = new GameObject();
            Components.LightingComponent lightingComponent = new Components.LightingComponent();
            lighting.AddComponent(lightingComponent);
            //lighting.Name = resourceLoader.GetString("Object_Name_Lighting");
            lighting.Name = "Lighting";
            lighting.Rotation = Quaternion.CreateFromYawPitchRoll(0, 1.570796326794f, 0);
            lighting.Position = new Vector3(0, 1, 0);
            lightingComponent.Color = new Vector4(3, 3, 3, 1);
            lightingComponent.Range = 10;
            appBody.CurrentScene.AddGameObject(lighting);
            appBody.RequireRender();
        }
        public static void NewVolume(Coocoo3DMain appBody)
        {
            //var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            GameObject volume = new GameObject();
            Components.VolumeComponent volumeComponent = new Components.VolumeComponent();
            volume.AddComponent(volumeComponent);
            //volume.Name = resourceLoader.GetString("Object_Name_Volume");
            volume.Name = "Volume";
            volume.Rotation = Quaternion.Identity;
            volume.Position = new Vector3(0, 25, 0);
            volumeComponent.Size = new Vector3(100, 50, 100);
            appBody.CurrentScene.AddGameObject(volume);
            appBody.RequireRender();
        }
        public static void RemoveSceneObject(Coocoo3DMain appBody, Scene scene, GameObject gameObject)
        {
            if (scene.sceneObjects.Remove(gameObject))
            {
                scene.RemoveGameObject(gameObject);
            }
            appBody.RequireRender();
        }
        public static async Task<StorageFolder> OpenResourceFolder(Coocoo3DMain appBody)
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
            appBody.OpenedStorageFolderChange(folder);
            return folder;
        }
        public static async Task Record(Coocoo3DMain appBody)
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

        public static List<Texture2D> GetTextureList(Coocoo3DMain appBody, StorageFolder storageFolder, PMXFormat pmx)
        {
            List<Texture2D> textures = new List<Texture2D>();
            List<string> paths = new List<string>();
            List<string> relativePaths = new List<string>();
            foreach (var vTex in pmx.Textures)
            {
                string relativePath = vTex.TexturePath.Replace("//", "\\").Replace('/', '\\');
                string texPath = Path.Combine(storageFolder.Path, relativePath);
                paths.Add(texPath);
                relativePaths.Add(relativePath);
            }
            lock (appBody.mainCaches.TextureCaches)
            {
                for (int i = 0; i < pmx.Textures.Count; i++)
                {
                    Texture2DPack tex = appBody.mainCaches.TextureCaches.GetOrCreate(paths[i]);
                    if (tex.Status != GraphicsObjectStatus.loaded)
                        tex.Mark(GraphicsObjectStatus.loading);
                    tex.relativePath = relativePaths[i];
                    tex.folder = storageFolder;
                    textures.Add(tex.texture2D);
                }
            }
            return textures;
        }

        public static async Task<Texture2D> LoadTexture(Coocoo3DMain appBody, StorageFile file)
        {
            Texture2DPack tex;
            lock (appBody.mainCaches.TextureCaches)
            {
                tex = appBody.mainCaches.TextureCaches.GetOrCreate(file.Path);
                if (tex.Status != GraphicsObjectStatus.loaded)
                    tex.Mark(GraphicsObjectStatus.loading);
            }
            await ReloadTexture2D(tex.texture2D, appBody.ProcessingList, file);
            return tex.texture2D;
        }

        public static async Task LoadPassSetting(Coocoo3DMain appBody, StorageFile file, StorageFolder storageFolder)
        {
            var stream = (await file.OpenReadAsync()).AsStreamForRead();
            var passSetting = (RenderPipeline.PassSetting)appBody.RPContext.PassSettingSerializer.Deserialize(stream);
            var rpc = appBody.RPContext;
            if (passSetting.VertexShaders != null)
                foreach (var v1 in passSetting.VertexShaders)
                {
                    VertexShader vertexShader = new VertexShader();
                    if (!vertexShader.CompileInitialize1(await FileIO.ReadBufferAsync(await storageFolder.GetFileAsync(v1.Path)), v1.EntryPoint == null ? "main" : v1.EntryPoint, new MacroEntry[0])) throw new Exception("Compile vertex shader failed.");
                    rpc.RPAssetsManager.VSAssets[v1.Name] = vertexShader;
                }
            if (passSetting.GeometryShaders != null)
                foreach (var g1 in passSetting.GeometryShaders)
                {
                    GeometryShader geometryShader = new GeometryShader();
                    if (!geometryShader.CompileInitialize1(await FileIO.ReadBufferAsync(await storageFolder.GetFileAsync(g1.Path)), g1.EntryPoint == null ? "main" : g1.EntryPoint, new MacroEntry[0])) throw new Exception("Compile gemoetry shader failed.");
                    rpc.RPAssetsManager.GSAssets[g1.Name] = geometryShader;
                }
            if (passSetting.PixelShaders != null)
                foreach (var p1 in passSetting.PixelShaders)
                {
                    PixelShader pixelShader = new PixelShader();
                    if (!pixelShader.CompileInitialize1(await FileIO.ReadBufferAsync(await storageFolder.GetFileAsync(p1.Path)), p1.EntryPoint == null ? "main" : p1.EntryPoint, new MacroEntry[0])) throw new Exception("Compile pixel shader failed.");
                    rpc.RPAssetsManager.PSAssets[p1.Name] = pixelShader;
                }
            if (passSetting.Texture2Ds != null)
                foreach (var t1 in passSetting.Texture2Ds)
                {
                    Texture2D texture = null;
                    rpc.RPAssetsManager.texture2ds.TryGetValue(t1.Name, out texture);
                    if (texture == null)
                    {
                        texture = new Texture2D();
                        rpc.RPAssetsManager.texture2ds[t1.Name] = texture;
                    }
                    texture.Status = GraphicsObjectStatus.loading;
                    await ReloadTexture2DNoMip(texture, rpc.processingList, await storageFolder.GetFileAsync(t1.Path));
                }
            if (passSetting.TextureCubes != null)
                foreach (var t1 in passSetting.TextureCubes)
                {
                    if (t1.Path == null || t1.Path.Length != 6) throw new Exception("TextureCubeError");
                    TextureCube textureCube = null;
                    rpc.RPAssetsManager.textureCubes.TryGetValue(t1.Name, out textureCube);
                    if(textureCube==null)
                    {
                        textureCube = new TextureCube();
                        rpc.RPAssetsManager.textureCubes[t1.Name] = textureCube;
                    }
                    IBuffer[] buffers = new IBuffer[t1.Path.Length];
                    for (int i = 0; i < t1.Path.Length; i++)
                    {
                        string path = t1.Path[i];
                        buffers[i] = await FileIO.ReadBufferAsync(await storageFolder.GetFileAsync(path));
                    }
                    Uploader uploader = new Uploader();
                    uploader.TextureCube(buffers);
                    appBody.ProcessingList.AddObject(new TextureCubeUploadPack(textureCube, uploader));
                }

            rpc.SetCurrentPassSetting(passSetting);
            rpc.customPassSetting = passSetting;

        }
        private static async Task ReloadTexture2D(Texture2D texture2D, ProcessingList processingList, StorageFile storageFile)
        {
            Uploader uploader = new Uploader();
            uploader.Texture2D(await FileIO.ReadBufferAsync(storageFile), true, true);
            processingList.AddObject(new Texture2DUploadPack(texture2D, uploader));
        }
        private static async Task ReloadTexture2DNoMip(Texture2D texture2D, ProcessingList processingList, StorageFile storageFile)
        {
            Uploader uploader = new Uploader();
            uploader.Texture2D(await FileIO.ReadBufferAsync(storageFile), false, false);
            processingList.AddObject(new Texture2DUploadPack(texture2D, uploader));
        }
    }
}
