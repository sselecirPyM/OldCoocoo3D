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
using Coocoo3D.Components;
using System.IO;
using Newtonsoft.Json;
using Vortice.Dxc;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches : IDisposable
    {
        public Dictionary<string, KnownFile> KnownFiles = new Dictionary<string, KnownFile>();
        public Dictionary<string, DirectoryInfo> KnownFolders = new Dictionary<string, DirectoryInfo>();

        public Dictionary<string, Texture2DPack> TextureCaches = new Dictionary<string, Texture2DPack>();
        public Dictionary<string, Texture2DPack> TextureOnDemand = new Dictionary<string, Texture2DPack>();

        public Dictionary<string, ModelPack> ModelPackCaches = new Dictionary<string, ModelPack>();
        public Dictionary<string, MMDMotion> Motions = new Dictionary<string, MMDMotion>();
        public Dictionary<string, PassSetting> PassSettings = new Dictionary<string, PassSetting>();
        public Dictionary<string, VertexShader> VertexShaders = new Dictionary<string, VertexShader>();
        public Dictionary<string, PixelShader> PixelShaders = new Dictionary<string, PixelShader>();
        public Dictionary<string, GeometryShader> GeometryShaders = new Dictionary<string, GeometryShader>();
        public Dictionary<string, ComputeShader> ComputeShaders = new Dictionary<string, ComputeShader>();

        public MainCaches()
        {
            KnownFolders.Add(Environment.CurrentDirectory, new DirectoryInfo(Environment.CurrentDirectory));
            KnownFolders.Add("Assets", new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "Assets")));
        }

        public ProcessingList processingList;
        public Action _RequireRender;

        public bool ReloadTextures1 = false;

        public void AddFolder(DirectoryInfo folder)
        {
            lock (TextureOnDemand)
            {
                KnownFolders[folder.FullName] = folder;
            }
        }

        public void Texture(string fullPath)
        {
            lock (TextureOnDemand)
            {
                if (!TextureOnDemand.ContainsKey(fullPath))
                {
                    TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath };

                }
            }
        }

        public void Texture(string fullPath, bool srgb)
        {
            lock (TextureOnDemand)
            {
                if (!TextureOnDemand.ContainsKey(fullPath))
                {
                    TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath, srgb = srgb };

                }
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
                            TextureOnDemand.Add(pair.Key, new Texture2DPack() { fullPath = pair.Value.fullPath, srgb = pair.Value.srgb });
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

                                if (knownFile.IsModified(folder.GetFiles()))
                                {
                                    Uploader uploader = new Uploader();
                                    if (texturePack1.ReloadTexture(knownFile.file, uploader))
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
                    tex1.srgb = loadCompleted.Value.srgb;
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

        T GetT<T>(IDictionary<string, T> caches, string path, Func<FileInfo, T> createFun) where T : class
        {
            var knownFile = KnownFiles.GetOrCreate(path, () => new KnownFile()
            {
                fullPath = path,
                relativePath = Path.GetFileName(path)
            });
            if (!caches.TryGetValue(path, out var file) || knownFile.requireReload.SetFalse())
            {
                string folderPath = Path.GetDirectoryName(path);
                if (!InitFolder(folderPath))
                    return null;
                var folder = KnownFolders[folderPath];
#if !DEBUG
                try
                {
                    if (knownFile.IsModified(folder.GetFiles()))
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
#else
                if (knownFile.IsModified(folder.GetFiles()))
                {
                    file = createFun(knownFile.file);
                    caches[path] = file;
                }
#endif
            }
            return file;
        }

        public ModelPack GetModel(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            lock (ModelPackCaches)
                return GetT(ModelPackCaches, path, file =>
                {
                    var modelPack = new ModelPack();
                    modelPack.fullPath = path;

                    BinaryReader reader = new BinaryReader(file.OpenRead());
                    modelPack.Reload(reader, Path.GetDirectoryName(path));
                    reader.Dispose();
                    processingList.AddObject(modelPack.GetMesh());
                    return modelPack;
                });
        }

        public MMDMotion GetMotion(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return GetT(Motions, path, file =>
            {
                BinaryReader reader = new BinaryReader(OpenReadStream(file));
                VMDFormat motionSet = VMDFormat.Load(reader);

                var motion = new MMDMotion();
                motion.Reload(motionSet);
                return motion;
            });
        }

        public PassSetting GetPassSetting(string path)
        {
            if (!Path.IsPathRooted(path)) path = Path.Combine(Environment.CurrentDirectory, path);
            var passSetting = GetT(PassSettings, path, file =>
            {
                var passes = ReadJsonStream<PassSetting>(OpenReadStream(file));
                foreach (var res in passes.Passes)
                {
                    if (res.SRVs != null)
                        for (int i = 0; i < res.SRVs.Count; i++)
                        {
                            SRVUAVSlotRes srv = res.SRVs[i];
                            srv.Resource = srv.Resource.Replace("_BRDFLUT", "Assets/Textures/brdflut.png");
                            res.SRVs[i] = srv;
                        }
                }
                return passes;
            });
            return passSetting;
        }

        public VertexShader GetVertexShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.Combine(Environment.CurrentDirectory, path);
            return GetT(VertexShaders, path, file =>
            {
                VertexShader vertexShader = new VertexShader();
                if (Path.GetExtension(path) == ".hlsl")
                    vertexShader.Initialize(LoadShader(DxcShaderStage.Vertex, File.ReadAllText(path), "main"));
                else
                    vertexShader.Initialize(File.ReadAllBytes(path));
                return vertexShader;
            });
        }

        public PixelShader GetPixelShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.Combine(Environment.CurrentDirectory, path);
            return GetT(PixelShaders, path, file =>
            {
                PixelShader pixelShader = new PixelShader();
                if (Path.GetExtension(path) == ".hlsl")
                    pixelShader.Initialize(LoadShader(DxcShaderStage.Pixel, File.ReadAllText(path), "main"));
                else
                    pixelShader.Initialize(File.ReadAllBytes(path));
                return pixelShader;
            });
        }

        public GeometryShader GetGeometryShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.Combine(Environment.CurrentDirectory, path);
            return GetT(GeometryShaders, path, file =>
            {
                GeometryShader geometryShader = new GeometryShader();
                if (Path.GetExtension(path) == ".hlsl")
                    geometryShader.Initialize(LoadShader(DxcShaderStage.Geometry, File.ReadAllText(path), "main"));
                else
                    geometryShader.Initialize(File.ReadAllBytes(path));
                return geometryShader;
            });
        }

        public ComputeShader GetComputeShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.Combine(Environment.CurrentDirectory, path);
            return GetT(ComputeShaders, path, file =>
            {
                ComputeShader geometryShader = new ComputeShader();
                if (Path.GetExtension(path) == ".hlsl")
                    geometryShader.Initialize(LoadShader(DxcShaderStage.Compute, File.ReadAllText(path), "main"));
                else
                    geometryShader.Initialize(File.ReadAllBytes(path));
                return geometryShader;
            });
        }

        byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint)
        {
            var result = DxcCompiler.Compile(shaderStage, shaderCode, entryPoint, new DxcCompilerOptions() { });
            if (result.GetStatus() != SharpGen.Runtime.Result.Ok)
                throw new Exception(result.GetErrors());
            return result.GetResult().ToArray();
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
                if (AddChildFolder(KnownFolders[path1], path) != null)
                    return true;
                return false;
            }
            else
                return false;
        }

        public DirectoryInfo AddChildFolder(DirectoryInfo folder, string path)
        {
            try
            {
                var path1 = path.Substring(0, path.LastIndexOf('\\'));
                var folder1 = new DirectoryInfo(path);
                if (folder1 != null)
                    KnownFolders[path] = folder1;
                return folder1;
            }
            catch
            {
                return null;
            }
        }

        public static T ReadJsonStream<T>(Stream stream)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamReader reader1 = new StreamReader(stream))
            {
                return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
            }
        }

        Stream OpenReadStream(FileInfo file)
        {
            return file.OpenRead();
        }

        public void ReloadTextures()
        {
            ReloadTextures1 = true;
        }

        public void Dispose()
        {
            foreach (var t in TextureCaches)
            {
                t.Value.texture2D.Dispose();
            }
            TextureCaches.Clear();
        }
    }
}
