using Coocoo3D.FileFormat;
using Coocoo3D.ResourceWarp;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Coocoo3D.Components;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches
    {
        public Dictionary<string, KnownFile> KnownFiles = new Dictionary<string, KnownFile>();

        public Dictionary<string, Texture2DPack> TextureCaches = new Dictionary<string, Texture2DPack>();
        public Dictionary<string, Texture2DPack> TextureOnDemand = new Dictionary<string, Texture2DPack>();
        public ConcurrentDictionary<string, MMDMotion> motions = new ConcurrentDictionary<string, MMDMotion>();

        public Dictionary<string, ModelPack> ModelPackCaches = new Dictionary<string, ModelPack>();

        public ProcessingList processingList;
        public Action _RequireRender;

        public bool ReloadTextures1 = false;

        public void Texture(string fullPath, string relativePath, StorageFolder folder)
        {
            lock (TextureOnDemand)
            {
                if (!TextureOnDemand.ContainsKey(fullPath))
                    TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath };
                KnownFiles[fullPath] = new KnownFile { fullPath = fullPath, folder = folder, relativePath = relativePath };
            }
        }

        ConcurrentDictionary<Texture2DPack, Uploader> uploaders = new ConcurrentDictionary<Texture2DPack, Uploader>();
        public void OnFrame()
        {
            lock (TextureOnDemand)
            {
                if (ReloadTextures1.SetFalse() && TextureCaches.Count > 0)
                {
                    var packs = TextureCaches.ToList();
                    foreach (var pair in packs)
                    {
                        if (!TextureOnDemand.ContainsKey(pair.Key) && pair.Value.canReload)
                            TextureOnDemand.Add(pair.Key, new Texture2DPack() { fullPath = pair.Value.fullPath, });
                    }
                }

                if (TextureOnDemand.Count == 0) return;

                foreach (var notLoad in TextureOnDemand.Where(u => { return u.Value.loadTask == null; }))
                {
                    var tex1 = TextureCaches.GetOrCreate(notLoad.Key);
                    tex1.Mark(GraphicsObjectStatus.loading);
                    notLoad.Value.loadTask = Task.Factory.StartNew((object a) =>
                        {
                            Texture2DPack texturePack1 = (Texture2DPack)a;
                            try
                            {
                                var knownFile = KnownFiles[texturePack1.fullPath];
                                if (knownFile.IsModified().Result)
                                {
                                    Uploader uploader = new Uploader();
                                    if (texturePack1.ReloadTexture(knownFile.file, uploader).Result)
                                    {
                                        texturePack1.Mark(GraphicsObjectStatus.loaded);
                                        uploaders[texturePack1] = uploader;
                                    }
                                    else
                                    {
                                        texturePack1.Mark(GraphicsObjectStatus.error);
                                    }
                                }
                                else
                                {
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
                        tex1.fullPath = loadCompleted.Value.fullPath;
                        tex1.Mark(loadCompleted.Value.Status);
                        if (uploaders.TryRemove(loadCompleted.Value, out Uploader uploader))
                        {
                            processingList.AddObject(tex1.texture2D, uploader);
                        }
                        TextureOnDemand.Remove(loadCompleted.Key);
                    }
                }
            }
        }

        public Dictionary<IntPtr, string> ptr2string = new Dictionary<IntPtr, string>();
        public Dictionary<string, IntPtr> string2Ptr = new Dictionary<string, IntPtr>();
        long ptrCount = 0;
        public IntPtr GetPtr(string s)
        {
            if (string2Ptr.TryGetValue(s,out IntPtr ptr))
            {
                return ptr;
            }
            long i = System.Threading.Interlocked.Increment(ref ptrCount);
            ptr = new IntPtr(i);
            ptr2string[ptr] = s;
            string2Ptr[s] = ptr;
            return ptr;
        }
        public Texture2D GetTexture(IntPtr ptr)
        {
            if (ptr2string.TryGetValue(ptr, out string s) && TextureCaches.TryGetValue(s, out var tex))
            {
                return tex.texture2D;
            }
            return null;
        }
        public Texture2D GetTexture(string s)
        {
            if (TextureCaches.TryGetValue(s, out var tex))
            {
                return tex.texture2D;
            }
            return null;
        }
        public void SetTexture(Texture2D tex, IntPtr ptr)
        {
            string name = ptr2string[ptr];
            TextureCaches[name] = new Texture2DPack() { canReload = false, fullPath = name, texture2D = tex };
        }
        public void SetTexture(string name, Texture2D tex)
        {
            TextureCaches[name] = new Texture2DPack() { canReload = false, fullPath = name, texture2D = tex };
        }

        public void ReloadTextures()
        {
            ReloadTextures1 = true;
        }
    }
}
