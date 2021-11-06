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
using System.IO;
using System.Xml.Serialization;
using Windows.ApplicationModel;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches
    {
        public Dictionary<string, KnownFile> KnownFiles = new Dictionary<string, KnownFile>();
        public Dictionary<string, StorageFolder> KnownFolders = new Dictionary<string, StorageFolder>();

        public Dictionary<string, Texture2DPack> TextureCaches = new Dictionary<string, Texture2DPack>();
        public Dictionary<string, Texture2DPack> TextureOnDemand = new Dictionary<string, Texture2DPack>();

        public Dictionary<string, ModelPack> ModelPackCaches = new Dictionary<string, ModelPack>();
        public ConcurrentDictionary<string, MMDMotion> Motions = new ConcurrentDictionary<string, MMDMotion>();
        public Dictionary<string, PassSetting> PassSettings = new Dictionary<string, PassSetting>();

        public MainCaches()
        {
            KnownFolders.Add("ms-appx:///", Package.Current.InstalledLocation);
        }

        public XmlSerializer PassSettingSerializer = new XmlSerializer(typeof(PassSetting));

        public ProcessingList processingList;
        public Action _RequireRender;

        public bool ReloadTextures1 = false;

        public void AddFolder(StorageFolder folder)
        {
            lock (TextureOnDemand)
            {
                KnownFolders[folder.Path] = folder;
            }
        }

        public void Texture(string fullPath)
        {
            lock (TextureOnDemand)
            {
                if (!TextureOnDemand.ContainsKey(fullPath))
                    TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath };
            }
        }

        public void Texture(string fullPath, bool srgb)
        {
            lock (TextureOnDemand)
            {
                if (!TextureOnDemand.ContainsKey(fullPath))
                    TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath, srgb = srgb };
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
                    foreach (var pair in KnownFiles)
                    {
                        pair.Value.requireReload = true;
                    }

                }

                if (TextureOnDemand.Count == 0) return;

                foreach (var notLoad in TextureOnDemand.Where(u => { return u.Value.loadTask == null; }))
                {
                    var tex1 = TextureCaches.GetOrCreate(notLoad.Key);
                    tex1.Mark(GraphicsObjectStatus.loading);
                    Dictionary<string, object> taskParam = new Dictionary<string, object>();
                    InitFolder(Path.GetDirectoryName(notLoad.Value.fullPath));
                    taskParam["pack"] = notLoad.Value;
                    taskParam["knownFile"] = KnownFiles.GetOrCreate(notLoad.Value.fullPath, (string path) => new KnownFile()
                    {
                        fullPath = path,
                        relativePath = Path.GetFileName(path)
                    });

                    notLoad.Value.loadTask = Task.Factory.StartNew((object a) =>
                        {
                            var taskParam1 = (Dictionary<string, object>)a;
                            Texture2DPack texturePack1 = (Texture2DPack)taskParam1["pack"];
                            var knownFile = (KnownFile)taskParam1["knownFile"];

                            try
                            {
                                var folder = KnownFolders[Path.GetDirectoryName(knownFile.fullPath)];
                                if (knownFile.IsModified(folder).Result)
                                {
                                    Uploader uploader = new Uploader();
                                    texturePack1.Mark(GraphicsObjectStatus.loading);
                                    if (texturePack1.ReloadTexture(knownFile.file, uploader).Result)
                                    {
                                        texturePack1.Mark(GraphicsObjectStatus.loaded);
                                        uploaders[texturePack1] = uploader;
                                    }
                                    else
                                        texturePack1.Mark(GraphicsObjectStatus.error);
                                }
                                else
                                    texturePack1.Mark(GraphicsObjectStatus.loaded);
                            }
                            catch
                            {
                                texturePack1.Mark(GraphicsObjectStatus.error);
                            }
                            finally
                            {
                                _RequireRender();
                            }
                        }, taskParam);
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

        T GetT<T>(IDictionary<string, T> caches, string path, Func<StorageFile, T> createFun) where T : class
        {
            var knownFile = KnownFiles.GetOrCreate(path, () => new KnownFile()
            {
                fullPath = path,
                relativePath = Path.GetFileName(path)
            });
            if (!caches.TryGetValue(path, out var file) || knownFile.requireReload.SetFalse())
            {
                string folderPath = GetDirectoryName(path);
                if (!InitFolder(folderPath))
                    return null;
                var folder = KnownFolders[folderPath];
                try
                {
                    if (knownFile.IsModified(folder).Result)
                    {
                        file = createFun(knownFile.file);
                        caches[path] = file;
                    }
                }
                catch
                {
                    file = null;
                    caches[path] = file;
                }
            }
            return file;
        }

        public MMDMotion GetMotion(string path)
        {
            return GetT(Motions, path, file =>
            {
                BinaryReader reader = new BinaryReader(OpenReadStream(file).Result);
                VMDFormat motionSet = VMDFormat.Load(reader);

                var motion = new Components.MMDMotion();
                motion.Reload(motionSet);
                return motion;
            });
        }

        public PassSetting GetPassSetting(string path)
        {
            var passSetting= GetT(PassSettings, path, file => (PassSetting)PassSettingSerializer.Deserialize(OpenReadStream(file).Result));
            foreach(var res in passSetting.Passes)
            {
                if (res.SRVs != null)
                    foreach (var srv in res.SRVs)
                        srv.Resource?.Replace("_BRDFLUT", "ms-appx:///Assets/Textures/brdflut.png");
            }
            return passSetting;
        }

        public string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path).Replace("ms-appx:\\", "ms-appx:///\\");
        }

        public Dictionary<IntPtr, string> ptr2string = new Dictionary<IntPtr, string>();
        public Dictionary<string, IntPtr> string2Ptr = new Dictionary<string, IntPtr>();
        long ptrCount = 0;
        public IntPtr GetPtr(string s)
        {
            if (string2Ptr.TryGetValue(s, out IntPtr ptr))
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

        bool InitFolder(string path)
        {
            if (path == null) return false;
            if (KnownFolders.ContainsKey(path)) return true;
            if (!path.Contains('\\')) return false;

            var path1 = path.Substring(0, path.LastIndexOf('\\'));
            if (InitFolder(path1))
            {
                if (AddChildFolder(KnownFolders[path1], path).Result != null)
                    return true;
                return false;
            }
            else
                return false;
        }

        public async Task<StorageFolder> AddChildFolder(StorageFolder folder, string path)
        {
            try
            {
                var path1 = path.Substring(0, path.LastIndexOf('\\'));
                var folder1 = await folder.GetFolderAsync(Path.GetRelativePath(path1, path));
                if (folder1 != null)
                    KnownFolders[path] = folder1;
                return folder1;
            }
            catch
            {
                return null;
            }
        }


        async Task<Stream> OpenReadStream(StorageFile file)
        {
            return (await file.OpenAsync(FileAccessMode.Read)).AsStreamForRead();
        }

        public void ReloadTextures()
        {
            ReloadTextures1 = true;
        }
    }
}
