using Coocoo3D.Components;
using Coocoo3D.FileFormat;
using Coocoo3D.ResourceWarp;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Vortice.Dxc;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches : IDisposable
    {
        public Dictionary<string, KnownFile> KnownFiles = new Dictionary<string, KnownFile>();
        public ConcurrentDictionary<string, DirectoryInfo> KnownFolders = new ConcurrentDictionary<string, DirectoryInfo>();

        public Dictionary<string, Texture2DPack> TextureCaches = new Dictionary<string, Texture2DPack>();
        public Dictionary<string, Texture2DPack> TextureOnDemand = new Dictionary<string, Texture2DPack>();

        public Dictionary<string, ModelPack> ModelPackCaches = new Dictionary<string, ModelPack>();
        public Dictionary<string, MMDMotion> Motions = new Dictionary<string, MMDMotion>();
        public Dictionary<string, VertexShader> VertexShaders = new Dictionary<string, VertexShader>();
        public Dictionary<string, PixelShader> PixelShaders = new Dictionary<string, PixelShader>();
        public Dictionary<string, GeometryShader> GeometryShaders = new Dictionary<string, GeometryShader>();
        public Dictionary<string, ComputeShader> ComputeShaders = new Dictionary<string, ComputeShader>();

        public DictionaryWithModifiyIndex<string, PassSetting> PassSettings = new DictionaryWithModifiyIndex<string, PassSetting>();
        public DictionaryWithModifiyIndex<string, PSO> PipelineStateObjects = new DictionaryWithModifiyIndex<string, PSO>();
        public DictionaryWithModifiyIndex<string, TextureCube> TextureCubes = new DictionaryWithModifiyIndex<string, TextureCube>();
        public DictionaryWithModifiyIndex<string, UnionShader> UnionShaders = new DictionaryWithModifiyIndex<string, UnionShader>();
        public DictionaryWithModifiyIndex<string, Assembly> Assemblies = new DictionaryWithModifiyIndex<string, Assembly>();
        public Dictionary<string, RootSignature> RootSignatures = new Dictionary<string, RootSignature>();

        public MainCaches()
        {
            KnownFolders.TryAdd(Environment.CurrentDirectory, new DirectoryInfo(Environment.CurrentDirectory));
            KnownFolders.TryAdd("Assets", new DirectoryInfo(Path.GetFullPath("Assets")));
        }

        public ProcessingList processingList;
        public Action _RequireRender;

        public bool ReloadTextures1 = false;
        public bool ReloadShaders = false;

        public void AddFolder(DirectoryInfo folder)
        {
            KnownFolders[folder.FullName] = folder;
        }

        public void Texture(string fullPath)
        {
            Texture(fullPath, true);
        }

        public void Texture(string fullPath, bool srgb)
        {
            if (!TextureOnDemand.ContainsKey(fullPath))
            {
                TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath, srgb = srgb };
            }
        }

        ConcurrentDictionary<Texture2DPack, Uploader> uploaders = new ConcurrentDictionary<Texture2DPack, Uploader>();
        public void OnFrame()
        {
            if (ReloadShaders.SetFalse())
            {
                Assemblies.Clear();
                UnionShaders.Clear();
                foreach (var pso in PipelineStateObjects)
                {
                    pso.Value?.Dispose();
                }
                PipelineStateObjects.Clear();
            }
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
                InitFolder(Path.GetDirectoryName(notLoad.Value.fullPath));
                Dictionary<string, object> taskParam = new Dictionary<string, object>();
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

        public T GetT<T>(IDictionary<string, T> caches, string path, Func<FileInfo, T> createFun) where T : class
        {
            return GetT(caches, path, path, createFun);
        }
        public T GetT<T>(IDictionary<string, T> caches, string path, string realPath, Func<FileInfo, T> createFun) where T : class
        {
            var knownFile = KnownFiles.GetOrCreate(realPath, () => new KnownFile()
            {
                fullPath = realPath,
                relativePath = Path.GetFileName(realPath)
            });
            if (!caches.TryGetValue(path, out var file) || knownFile.requireReload.SetFalse())
            {
                string folderPath = Path.GetDirectoryName(realPath);
                if (!InitFolder(folderPath) && !Path.IsPathRooted(folderPath))
                    return null;
                var folder = (Path.IsPathRooted(folderPath)) ? new DirectoryInfo(folderPath) : KnownFolders[folderPath];
                try
                {
                    if (caches is DictionaryWithModifiyIndex<string, T> a)
                    {
                        int modifyIndex = knownFile.GetModifyIndex(folder.GetFiles());
                        if (modifyIndex > a.GetModifyIndex(path))
                        {
                            a.SetModifyIndex(path, modifyIndex);
                            file = createFun(knownFile.file);
                            caches[path] = file;
                        }
                    }
                    else if (knownFile.IsModified(folder.GetFiles()))
                    {
                        file = createFun(knownFile.file);
                        caches[path] = file;
                    }
                }
                catch
                {
#if !DEBUG
                    file = null;
                    caches[path] = file;
#else
                    throw;
#endif
                }
            }
            return file;
        }

        public Texture2D GetTexture1(string path, GraphicsContext graphicsContext)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return GetT(TextureCaches, path, file =>
            {
                var texturePack1 = new Texture2DPack();
                texturePack1.fullPath = path;
                Uploader uploader = new Uploader();
                texturePack1.ReloadTexture(file, uploader);
                graphicsContext.UploadTexture(texturePack1.texture2D, uploader);
                texturePack1.Mark(GraphicsObjectStatus.loaded);
                return texturePack1;
            }).texture2D;
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
                BinaryReader reader = new BinaryReader(file.OpenRead());
                VMDFormat motionSet = VMDFormat.Load(reader);

                var motion = new MMDMotion();
                motion.Reload(motionSet);
                return motion;
            });
        }

        public PassSetting GetPassSetting(string path)
        {
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            var passSetting = GetT(PassSettings, path, file =>
            {
                var passes = ReadJsonStream<PassSetting>(file.OpenRead());
                foreach (var res in passes.Passes)
                {
                    if (res.Value.SRVs != null)
                        for (int i = 0; i < res.Value.SRVs.Count; i++)
                        {
                            SRVUAVSlotRes srv = res.Value.SRVs[i];
                            srv.Resource = srv.Resource.Replace("_BRDFLUT", "Assets/Textures/brdflut.png");
                            res.Value.SRVs[i] = srv;
                        }
                }
                return passes;
            });
            return passSetting;
        }

        public VertexShader GetVertexShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            return GetT(VertexShaders, path, file =>
            {
                VertexShader vertexShader = new VertexShader();
                if (Path.GetExtension(path) == ".hlsl")
                    vertexShader.Initialize(LoadShader(DxcShaderStage.Vertex, File.ReadAllText(path), "vsmain", path));
                else
                    vertexShader.Initialize(File.ReadAllBytes(path));
                return vertexShader;
            });
        }

        public PixelShader GetPixelShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            return GetT(PixelShaders, path, file =>
            {
                PixelShader pixelShader = new PixelShader();
                if (Path.GetExtension(path) == ".hlsl")
                    pixelShader.Initialize(LoadShader(DxcShaderStage.Pixel, File.ReadAllText(path), "psmain", path));
                else
                    pixelShader.Initialize(File.ReadAllBytes(path));
                return pixelShader;
            });
        }

        public GeometryShader GetGeometryShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            return GetT(GeometryShaders, path, file =>
            {
                GeometryShader geometryShader = new GeometryShader();
                if (Path.GetExtension(path) == ".hlsl")
                    geometryShader.Initialize(LoadShader(DxcShaderStage.Geometry, File.ReadAllText(path), "gsmain", path));
                else
                    geometryShader.Initialize(File.ReadAllBytes(path));
                return geometryShader;
            });
        }

        public UnionShader GetUnionShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);

            var assembly = GetAssembly(path);
            if (assembly == null) return null;
            if (!UnionShaders.TryGetValue(path, out UnionShader unionShader))
            {
                Type type = assembly.GetType(Path.GetFileNameWithoutExtension(path));
                var info = type.GetMethod("UnionShader");
                unionShader = (UnionShader)Delegate.CreateDelegate(typeof(UnionShader), info);
                UnionShaders[path] = unionShader;
            }
            return unionShader;
        }

        public Assembly GetAssembly(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            return GetT(Assemblies, path, file =>
            {
                byte[] datas = CompileScripts(path);
                if (datas != null && datas.Length > 0)
                {
                    return Assembly.Load(datas);
                }
                else
                    return null;
            });
        }

        public static byte[] CompileScripts(string path)
        {
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));

                MemoryStream memoryStream = new MemoryStream();
                List<MetadataReference> refs = new List<MetadataReference>() {
                    MetadataReference.CreateFromFile (typeof (object).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (List<int>).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (System.Text.ASCIIEncoding).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (JsonConvert).Assembly.Location),
                    MetadataReference.CreateFromFile (Assembly.GetExecutingAssembly().Location),
                    MetadataReference.CreateFromFile (typeof (SixLabors.ImageSharp.Image).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (GraphicsContext).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (Vortice.Dxc.Dxc).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (SharpGen.Runtime.CppObject).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (SharpGen.Runtime.ComObject).Assembly.Location),
                };
                refs.AddRange(AppDomain.CurrentDomain.GetAssemblies().Where(u => u.GetName().Name.Contains("netstandard") || u.GetName().Name.Contains("System")).Select(u => MetadataReference.CreateFromFile(u.Location)));
                var compilation = CSharpCompilation.Create(Path.GetFileName(path), new[] { syntaxTree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                var result = compilation.Emit(memoryStream);
                if (!result.Success)
                {
                    foreach (var diag in result.Diagnostics)
                        Console.WriteLine(diag.ToString());
                }
                return memoryStream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public ComputeShader GetComputeShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            return GetT(ComputeShaders, path, file =>
            {
                ComputeShader computeShader = new ComputeShader();
                if (Path.GetExtension(path) == ".hlsl")
                    computeShader.Initialize(LoadShader(DxcShaderStage.Compute, File.ReadAllText(path), "csmain", path));
                else
                    computeShader.Initialize(File.ReadAllBytes(path));
                return computeShader;
            });
        }

        public PSO GetPSOWithKeywords(List<string> keywords, string path, bool enableVS = true, bool enablePS = true)
        {
            string xPath;
            if (keywords != null)
            {
                keywords.Sort();
                xPath = path + string.Concat(keywords);
            }
            else
            {
                xPath = path;
            }
            return GetT(PipelineStateObjects, xPath, path, file =>
            {
                try
                {
                    string source = File.ReadAllText(file.FullName);
                    DxcDefine[] dxcDefines = null;
                    if (keywords != null)
                    {
                        dxcDefines = new DxcDefine[keywords.Count];
                        for (int i = 0; i < keywords.Count; i++)
                        {
                            dxcDefines[i] = new DxcDefine() { Name = keywords[i], Value = "1" };
                        }
                    }
                    byte[] vs = enableVS ? LoadShader(DxcShaderStage.Vertex, source, "vsmain", path, dxcDefines) : null;
                    byte[] ps = enablePS ? LoadShader(DxcShaderStage.Pixel, source, "psmain", path, dxcDefines) : null;
                    PSO pso = new PSO(vs, null, ps);
                    return pso;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }
            });
        }

        static byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint, string fileName, DxcDefine[] dxcDefines = null)
        {
            var result = DxcCompiler.Compile(shaderStage, shaderCode, entryPoint, new DxcCompilerOptions() { }, fileName, dxcDefines, null);
            if (result.GetStatus() != SharpGen.Runtime.Result.Ok)
            {
                string err = result.GetErrors();
                result.Dispose();
                throw new Exception(err);
            }
            byte[] resultData = result.GetResult().ToArray();
            result.Dispose();
            return resultData;
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

        public TextureCube GetTextureCube(string s)
        {
            if (TextureCubes.TryGetValue(s, out var tex))
            {
                return tex;
            }
            return null;
        }

        public void GetSkyBox(string s, GraphicsContext context, out TextureCube skyBox, out TextureCube reflect)
        {
            skyBox = GetTextureCube(s);
            reflect = GetTextureCube(s + "Reflect");
            if (skyBox == null)
            {
                skyBox = new TextureCube();
                reflect = new TextureCube();
                skyBox.ReloadAsRTVUAV(2048, 2048, 6, Vortice.DXGI.Format.R16G16B16A16_Float);
                reflect.ReloadAsRTVUAV(512, 512, 6, Vortice.DXGI.Format.R16G16B16A16_Float);

                context.UpdateRenderTexture(skyBox);
                context.UpdateRenderTexture(reflect);
                TextureCubes[s] = skyBox;
                TextureCubes[s + "Reflect"] = reflect;
            }
        }


        public RootSignature GetRootSignature(GraphicsDevice graphicsDevice, string s)
        {
            if (RootSignatures.TryGetValue(s, out RootSignature rs))
                return rs;
            rs = new RootSignature();
            rs.Reload(graphicsDevice, fromString(s));
            RootSignatures[s] = rs;
            return rs;
        }
        GraphicSignatureDesc[] fromString(string s)
        {
            GraphicSignatureDesc[] desc = new GraphicSignatureDesc[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case 'C':
                        desc[i] = GraphicSignatureDesc.CBV;
                        break;
                    case 'c':
                        desc[i] = GraphicSignatureDesc.CBVTable;
                        break;
                    case 'S':
                        desc[i] = GraphicSignatureDesc.SRV;
                        break;
                    case 's':
                        desc[i] = GraphicSignatureDesc.SRVTable;
                        break;
                    case 'U':
                        desc[i] = GraphicSignatureDesc.UAV;
                        break;
                    case 'u':
                        desc[i] = GraphicSignatureDesc.UAVTable;
                        break;
                    default:
                        throw new NotImplementedException("error root signature desc.");
                        break;
                }
            }
            return desc;
        }

        public bool TryGetTexture(string s, out Texture2D tex)
        {
            bool result = TextureCaches.TryGetValue(s, out var tex1);
            tex = tex1?.texture2D;
            return result;
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
