using Coocoo3D.FileFormat;
using Coocoo3D.ResourceWarp;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches
    {
        public Dictionary<string, Texture2DPack> TextureCaches = new Dictionary<string, Texture2DPack>();
        public Dictionary<string, Texture2DPack> TextureOnDemand = new Dictionary<string, Texture2DPack>();

        public Dictionary<string, ModelPack> ModelPackCaches = new Dictionary<string, ModelPack>();

        public ProcessingList processingList;
        public Action _RequireRender;

        public bool ReloadTextures1 = false;

        public void Texture(string name, string relativePath, StorageFolder folder)
        {
            lock (TextureOnDemand)
            {
                if (!TextureOnDemand.ContainsKey(name))
                    TextureOnDemand[name] = new Texture2DPack() { folder = folder, relativePath = relativePath };
            }
        }

        public void OnFrame()
        {
            lock (TextureOnDemand)
            {
                ReloadTextures2();

                if (TextureOnDemand.Count == 0) return;
                foreach (var notLoad in TextureOnDemand.Where(u => { return u.Value.loadTask == null; }))
                {
                    var tex1 = TextureCaches.GetOrCreate(notLoad.Key);
                    tex1.Mark(GraphicsObjectStatus.loading);
                    notLoad.Value.loadTask = Task.Factory.StartNew(async (object a) =>
                        {
                            Texture2DPack texturePack1 = (Texture2DPack)a;
                            try
                            {
                                var file = await texturePack1.folder.GetFileAsync(texturePack1.relativePath);
                                var attr = await file.GetBasicPropertiesAsync();
                                if (texturePack1.lastModifiedTime != attr.DateModified)
                                {
                                    Uploader uploader = new Uploader();
                                    if (await texturePack1.ReloadTexture(file, uploader))
                                    {
                                        texturePack1.lastModifiedTime = attr.DateModified;
                                        texturePack1.Mark(GraphicsObjectStatus.loaded);
                                    }
                                    else
                                    {
                                        texturePack1.Mark(GraphicsObjectStatus.error);
                                    }
                                }
                                else
                                {
                                    texturePack1.doNothing = true;
                                    texturePack1.Mark(GraphicsObjectStatus.loaded);
                                }
                            }
                            catch
                            {
                                texturePack1.Mark(GraphicsObjectStatus.error);
                            }
                            finally
                            {
                                _RequireRender();
                            }
                        }, notLoad.Value);
                }
                foreach (var loadCompleted in TextureOnDemand.Where(u => { return u.Value.loadTask != null && u.Value.loadTask.IsCompleted; }).ToArray())
                {
                    var tex1 = TextureCaches.GetOrCreate(loadCompleted.Key);
                    if (loadCompleted.Value.loadTask.Status == TaskStatus.RanToCompletion &&
                        (loadCompleted.Value.Status == GraphicsObjectStatus.loaded ||
                        loadCompleted.Value.Status == GraphicsObjectStatus.error))
                    {
                        tex1.lastModifiedTime = loadCompleted.Value.lastModifiedTime;
                        tex1.folder = loadCompleted.Value.folder;
                        tex1.relativePath = loadCompleted.Value.relativePath;
                        tex1.Mark(loadCompleted.Value.Status);
                        if (loadCompleted.Value.Status == GraphicsObjectStatus.loaded && loadCompleted.Value.doNothing == false)
                            processingList.AddObject(new Texture2DUploadPack(tex1.texture2D, loadCompleted.Value.uploader1));
                        TextureOnDemand.Remove(loadCompleted.Key);
                    }
                }
            }
        }

        private void ReloadTextures2()
        {
            if (!ReloadTextures1) return;
            ReloadTextures1 = false;
            if (TextureCaches.Count > 0)
            {
                var packs = TextureCaches.ToList();
                foreach (var pair in packs)
                {
                    if (!TextureOnDemand.ContainsKey(pair.Key))
                        TextureOnDemand.Add(pair.Key, new Texture2DPack() { folder = pair.Value.folder, relativePath = pair.Value.relativePath, lastModifiedTime = pair.Value.lastModifiedTime });
                }
            }
        }
        public void ReloadTextures()
        {
            ReloadTextures1 = true;
        }
    }
}
