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
            MMD3DEntity entity = new MMD3DEntity();
            entity.Reload2(appBody.ProcessingList, pack, GetTextureList(appBody, storageFolder, pack.pmx), pmxPath);
            scene.AddSceneObject(entity);
            appBody.RequireRender();

            appBody.mainCaches.ReloadTextures(appBody.ProcessingList, appBody.RequireRender);
        }
        public static void NewLighting(Coocoo3DMain appBody)
        {
            Lighting lighting = new Lighting();
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            lighting.Name = resourceLoader.GetString("Object_Name_Lighting");
            lighting.Color = new Vector4(3, 3, 3, 1);
            lighting.Rotation = Quaternion.CreateFromYawPitchRoll(0, 1.570796326794f, 0);
            lighting.Position = new Vector3(0, 1, 0);
            lighting.Range = 10;
            if (appBody.CurrentScene.Lightings.Count > 0)
                lighting.LightingType = LightingType.Point;
            appBody.CurrentScene.AddSceneObject(lighting);
            appBody.RequireRender();
        }
        public static void RemoveSceneObject(Coocoo3DMain appBody, Scene scene, ISceneObject sceneObject)
        {
            if (scene.sceneObjects.Remove(sceneObject))
            {
                if (sceneObject is MMD3DEntity entity)
                    scene.RemoveSceneObject(entity);
                else if (sceneObject is Lighting lighting)
                    scene.RemoveSceneObject(lighting);
            }
            appBody.RequireRender();
        }
        public static async Task OpenResourceFolder(Coocoo3DMain appBody)
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
            if (folder == null) return;
            appBody.OpenedStorageFolderChange(folder);
        }

        public static async Task LoadVMD(Coocoo3DMain appBody, StorageFile storageFile, MMD3DEntity entity)
        {
            BinaryReader reader = new BinaryReader((await storageFile.OpenReadAsync()).AsStreamForRead());
            VMDFormat motionSet = VMDFormat.Load(reader);
            entity.motionComponent.Reload(motionSet);
            appBody.RequireRender(true);
        }

        public static void LoadShaderForEntities1(Coocoo3DMain appBody, StorageFile storageFile, StorageFolder storageFolder, IList<MMD3DEntity> entities)
        {
            RPShaderPack shaderPack;
            lock (appBody.mainCaches.RPShaderPackCaches)
            {
                shaderPack = appBody.mainCaches.RPShaderPackCaches.GetOrCreate(storageFile.Path);
                if (shaderPack.Status != GraphicsObjectStatus.loaded)
                {
                    if (shaderPack.loadLocker.GetLocker())
                    {
                        string relativePath = storageFile.Name;
                        _ = Task.Run(async () =>
                        {
                            var task1 = shaderPack.Reload1(storageFolder, relativePath, appBody.RPAssetsManager, appBody.ProcessingList);
                            appBody.RequireRender();
                            if (await task1)
                            {

                            }
                            appBody.RequireRender();
                            shaderPack.loadLocker.FreeLocker();
                        });
                    }
                }
            }
            foreach (var entity in entities)
            {
                entity.rendererComponent.PODraw = shaderPack.PODraw;
                entity.rendererComponent.POSkinning = shaderPack.POSkinning;
                entity.rendererComponent.POParticleDraw = shaderPack.POParticleDraw;
                entity.rendererComponent.ParticleCompute = shaderPack.CSParticle;
            }
            appBody.RequireRender();
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
    }
}
