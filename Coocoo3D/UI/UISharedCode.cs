using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
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
using SixLabors.ImageSharp;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace Coocoo3D.UI
{
    public static class UISharedCode
    {
        public static void LoadEntityIntoScene(Coocoo3DMain appBody, Scene scene, StorageFile pmxFile, StorageFolder storageFolder)
        {
            string pmxPath = pmxFile.Path;
            ModelPack modelPack = null;
            lock (appBody.mainCaches.ModelPackCaches)
            {
                modelPack = appBody.mainCaches.ModelPackCaches.GetOrCreate(pmxPath);
                modelPack.fullPath = pmxPath;
                if (modelPack.LoadTask == null && modelPack.Status != GraphicsObjectStatus.loaded)
                {
                    modelPack.LoadTask = Task.Run(() =>
                    {
                        BinaryReader reader = OpenReader(pmxFile).Result;
                        modelPack.Reload(reader);
                        reader.Dispose();
                        appBody.ProcessingList.AddObject(modelPack.GetMesh());
                        modelPack.Status = GraphicsObjectStatus.loaded;
                        modelPack.LoadTask = null;
                    });
                }
            }
            if (modelPack.Status != GraphicsObjectStatus.loaded && modelPack.LoadTask != null) modelPack.LoadTask.Wait();

            GameObject gameObject = new GameObject();
            gameObject.Reload2(appBody.ProcessingList, modelPack, GetTextureList(appBody, storageFolder.Path, modelPack.pmx), pmxPath);
            scene.AddGameObject(gameObject);

            appBody.RequireRender();
        }
        public static void NewLighting(Coocoo3DMain appBody)
        {
            GameObject lighting = new GameObject();
            Components.LightingComponent lightingComponent = new Components.LightingComponent();
            lighting.AddComponent(lightingComponent);
            lighting.Name = "Lighting";
            lighting.Rotation = Quaternion.CreateFromYawPitchRoll(0, 1.3962634015954636615389526147909f, 0);
            lighting.Position = new Vector3(0, 1, 0);
            lightingComponent.Color = new Vector4(3, 3, 3, 1);
            lightingComponent.Range = 10;
            appBody.CurrentScene.AddGameObject(lighting);
            appBody.RequireRender();
        }
        public static void NewVolume(Coocoo3DMain appBody)
        {
            GameObject volume = new GameObject();
            Components.VolumeComponent volumeComponent = new Components.VolumeComponent();
            volume.AddComponent(volumeComponent);
            volume.Name = "Volume";
            volume.Rotation = Quaternion.Identity;
            volume.Position = new Vector3(0, 25, 0);
            volumeComponent.Size = new Vector3(100, 50, 100);
            appBody.CurrentScene.AddGameObject(volume);
            appBody.RequireRender();
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

        public static List<string> GetTextureList(Coocoo3DMain appBody, string storageFolder, PMXFormat pmx)
        {
            List<string> paths = new List<string>();
            foreach (var vTex in pmx.Textures)
            {
                string relativePath = vTex.TexturePath.Replace("//", "\\").Replace('/', '\\');
                string texPath = Path.Combine(storageFolder, relativePath);
                paths.Add(texPath);
            }
            for (int i = 0; i < pmx.Textures.Count; i++)
            {
                appBody.mainCaches.Texture(paths[i]);
            }
            return paths;
        }

        public static void LoadTexture(Coocoo3DMain appBody, StorageFile storageFile, StorageFolder storageFolder)
        {
            appBody.mainCaches.Texture(storageFolder.Path);
        }

        public static async Task<BinaryReader> OpenReader(StorageFile file) => new BinaryReader((await file.OpenReadAsync()).AsStreamForRead());

        //public static async Task LoadPassSetting(Coocoo3DMain appBody, StorageFile file, StorageFolder storageFolder)
        //{
        //    var stream = (await file.OpenReadAsync()).AsStreamForRead();
        //    var passSetting = (RenderPipeline.PassSetting)appBody.RPContext.PassSettingSerializer.Deserialize(stream);
        //    var rpc = appBody.RPContext;
        //    if (passSetting.VertexShaders != null)
        //        foreach (var v1 in passSetting.VertexShaders)
        //        {
        //            VertexShader vertexShader = new VertexShader();
        //            if (!vertexShader.CompileInitialize1(await FileIO.ReadBufferAsync(await storageFolder.GetFileAsync(v1.Path)), v1.EntryPoint == null ? "main" : v1.EntryPoint, new MacroEntry[0])) throw new Exception("Compile vertex shader failed.");
        //            rpc.RPAssetsManager.VSAssets[v1.Name] = vertexShader;
        //        }
        //    if (passSetting.GeometryShaders != null)
        //        foreach (var g1 in passSetting.GeometryShaders)
        //        {
        //            GeometryShader geometryShader = new GeometryShader();
        //            if (!geometryShader.CompileInitialize1(await FileIO.ReadBufferAsync(await storageFolder.GetFileAsync(g1.Path)), g1.EntryPoint == null ? "main" : g1.EntryPoint, new MacroEntry[0])) throw new Exception("Compile gemoetry shader failed.");
        //            rpc.RPAssetsManager.GSAssets[g1.Name] = geometryShader;
        //        }
        //    if (passSetting.PixelShaders != null)
        //        foreach (var p1 in passSetting.PixelShaders)
        //        {
        //            PixelShader pixelShader = new PixelShader();
        //            if (!pixelShader.CompileInitialize1(await FileIO.ReadBufferAsync(await storageFolder.GetFileAsync(p1.Path)), p1.EntryPoint == null ? "main" : p1.EntryPoint, new MacroEntry[0])) throw new Exception("Compile pixel shader failed.");
        //            rpc.RPAssetsManager.PSAssets[p1.Name] = pixelShader;
        //        }
        //    //if (passSetting.Texture2Ds != null)
        //    //    foreach (var t1 in passSetting.Texture2Ds)
        //    //    {

        //    //        var file1 = await storageFolder.GetFileAsync(t1.Path);
        //    //        LoadTexture(appBody, file1, storageFolder);
        //    //    }
        //    //if (passSetting.TextureCubes != null)
        //    //    foreach (var t1 in passSetting.TextureCubes)
        //    //    {
        //    //        if (t1.Path == null || t1.Path.Length != 6) throw new Exception("TextureCubeError");
        //    //        TextureCube textureCube = null;
        //    //        rpc.RPAssetsManager.textureCubes.TryGetValue(t1.Name, out textureCube);
        //    //        if (textureCube == null)
        //    //        {
        //    //            textureCube = new TextureCube();
        //    //            rpc.RPAssetsManager.textureCubes[t1.Name] = textureCube;
        //    //        }
        //    //        Stream[] streams = new Stream[t1.Path.Length];

        //    //        for (int i = 0; i < t1.Path.Length; i++)
        //    //        {
        //    //            string path = t1.Path[i];
        //    //            streams[i] = await (await storageFolder.GetFileAsync(path)).OpenStreamForReadAsync();
        //    //        }
        //    //        appBody.ProcessingList.AddObject(TextureCubeUploadPack.FromFiles(textureCube, streams));
        //    //    }

        //    rpc.SetCurrentPassSetting(passSetting);
        //    rpc.customPassSetting = passSetting;

        //}
    }
}
