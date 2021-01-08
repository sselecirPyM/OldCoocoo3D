using Coocoo3D.FileFormat;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWarp;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Coocoo3D.Core
{
    static class ModelReloader
    {
        public static void ReloadModels(Scene scene, MainCaches mainCaches, ProcessingList processingList, GameDriverContext gameDriverContext)
        {
            if (mainCaches.modelTaskLocker.GetLocker())
            {
                Task.Run(async () =>
                {
                    List<ModelPack> packs = new List<ModelPack>();
                    lock (mainCaches.ModelPackCaches)
                        foreach (var modelPack in mainCaches.ModelPackCaches.Values)
                            if (modelPack.Status == GraphicsObjectStatus.loaded || modelPack.Status == GraphicsObjectStatus.error)
                                packs.Add(modelPack);

                    List<ModelPack> updatePacks = new List<ModelPack>();
                    for (int i = 0; i < packs.Count; i++)
                    {
                        var pack = packs[i];
                        if (pack.LoadTask == null)
                        {
                            try
                            {
                                var file = await pack.folder.GetFileAsync(pack.relativePath);
                                var attr = await file.GetBasicPropertiesAsync();
                                if (attr.DateModified != pack.lastModifiedTime)
                                {
                                    updatePacks.Add(pack);
                                    pack.lastModifiedTime = attr.DateModified;
                                }
                            }
                            catch
                            {

                            }
                        }
                    }
                    List<ModelPack> newPacks = new List<ModelPack>();
                    for (int i = 0; i < updatePacks.Count; i++)
                    {
                        var file = await updatePacks[i].folder.GetFileAsync(updatePacks[i].relativePath);
                        ModelPack pack = new ModelPack();
                        pack.LoadTask = LoadPMX(file, updatePacks[i].folder, pack, processingList);
                        newPacks.Add(pack);
                    }
                    for (int i = 0; i < newPacks.Count; i++)
                    {
                        var pack = newPacks[i];
                        void fun1()
                        {
                            lock (mainCaches.ModelPackCaches)
                            {
                                mainCaches.ModelPackCaches[pack.fullPath] = pack;
                            }
                            for (int j = 0; j < scene.Entities.Count; j++)
                            {
                                if (scene.Entities[j].ModelPath == pack.fullPath)
                                {
                                    scene.Entities[j].ReloadModel(processingList, pack, GetTextureList(processingList, mainCaches, pack.folder, pack.pmx));
                                    scene.EntityRefreshList.Add(scene.Entities[j]);
                                    gameDriverContext.RequireResetPhysics = true;
                                    gameDriverContext.RequireRender(true);

                                    mainCaches.ReloadTextures(processingList, gameDriverContext.RequireRender);
                                }
                            }
                        }
                        if (pack.LoadTask == null && pack.Status == GraphicsObjectStatus.loaded)
                        {
                            fun1();
                        }
                        else
                        {
                            try
                            {
                                pack.LoadTask.Wait();
                                fun1();
                            }
                            catch
                            {
                            }
                        }
                    }
                    mainCaches.modelTaskLocker.FreeLocker();
                }).Wait();
            }
        }

        static async Task LoadPMX(StorageFile file, StorageFolder folder, ModelPack pack, ProcessingList processingList)
        {
            string path = file.Path;
            BinaryReader reader = new BinaryReader((await file.OpenReadAsync()).AsStreamForRead());
            pack.Reload2(reader);
            pack.fullPath = path;
            pack.folder = folder;
            pack.relativePath = file.Name;
            reader.Dispose();
            processingList.AddObject(pack.GetMesh());
            pack.Status = GraphicsObjectStatus.loaded;

            pack.LoadTask = null;
        }



        public static List<Texture2D> GetTextureList(ProcessingList processingList, MainCaches mainCaches, StorageFolder storageFolder, PMXFormat pmx)
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
            lock (mainCaches.TextureCaches)
            {
                for (int i = 0; i < pmx.Textures.Count; i++)
                {
                    Texture2DPack tex = mainCaches.TextureCaches.GetOrCreate(paths[i]);
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
