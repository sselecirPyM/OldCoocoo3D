﻿using Coocoo3D.Components;
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
using System.Text;
using System.Threading.Tasks;
using Vortice.Dxc;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches : IDisposable
    {
        public Dictionary<string, KnownFile> KnownFiles = new();
        public ConcurrentDictionary<string, DirectoryInfo> KnownFolders = new();

        public DictionaryWithModifiyIndex<string, Texture2DPack> TextureCaches = new();
        public DictionaryWithModifiyIndex<string, Texture2DPack> TextureOnDemand = new();

        public DictionaryWithModifiyIndex<string, ModelPack> ModelPackCaches = new();
        public DictionaryWithModifiyIndex<string, MMDMotion> Motions = new();
        public DictionaryWithModifiyIndex<string, ComputeShader> ComputeShaders = new();

        public DictionaryWithModifiyIndex<string, PassSetting> PassSettings = new();
        public DictionaryWithModifiyIndex<string, RayTracingShader> RayTracingShaders = new();
        public DictionaryWithModifiyIndex<string, PSO> PipelineStateObjects = new();
        public DictionaryWithModifiyIndex<string, RTPSO> RTPSOs = new();
        public DictionaryWithModifiyIndex<string, TextureCube> TextureCubes = new();
        public DictionaryWithModifiyIndex<string, UnionShader> UnionShaders = new();
        public DictionaryWithModifiyIndex<string, IPassDispatcher> PassDispatchers = new();
        public DictionaryWithModifiyIndex<string, Assembly> Assemblies = new();
        public DictionaryWithModifiyIndex<string, RootSignature> RootSignatures = new();

        public ConcurrentQueue<ValueTuple<Texture2D, Uploader>> TextureReadyToUpload = new();
        public ConcurrentQueue<Mesh> MeshReadyToUpload = new();

        public MainCaches()
        {
            KnownFolders.TryAdd(Environment.CurrentDirectory, new DirectoryInfo(Environment.CurrentDirectory));
            KnownFolders.TryAdd("Assets", new DirectoryInfo(Path.GetFullPath("Assets")));
        }

        public Action _RequireRender;

        public bool ReloadTextures1 = false;
        public bool ReloadShaders = false;

        public void AddFolder(DirectoryInfo folder)
        {
            KnownFolders[folder.FullName] = folder;
        }

        public void Texture(string fullPath, bool srgb = true)
        {
            if (!TextureOnDemand.ContainsKey(fullPath))
            {
                AddFolder(new DirectoryInfo(Path.GetDirectoryName(fullPath)));
                TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath, srgb = srgb };
            }
        }

        ConcurrentDictionary<Texture2DPack, Uploader> uploaders = new();
        public void OnFrame()
        {
            if (ReloadShaders.SetFalse())
            {
                foreach (var knownFile in KnownFiles)
                    knownFile.Value.requireReload = true;

                Console.Clear();
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
                Dictionary<string, object> taskParam = new();
                taskParam["pack"] = notLoad.Value;
                taskParam["knownFile"] = KnownFiles.GetOrCreate(notLoad.Value.fullPath, (string path) => new KnownFile()
                {
                    fullPath = path,
                });

                notLoad.Value.loadTask = Task.Factory.StartNew((object a) =>
                {
                    var taskParam1 = (Dictionary<string, object>)a;
                    Texture2DPack texturePack1 = (Texture2DPack)taskParam1["pack"];
                    var knownFile = (KnownFile)taskParam1["knownFile"];

                    var folder = KnownFolders[Path.GetDirectoryName(knownFile.fullPath)];

                    if (LoadTexture(folder, texturePack1, knownFile))
                        texturePack1.Mark(GraphicsObjectStatus.loaded);
                    else
                        texturePack1.Mark(GraphicsObjectStatus.error);
                    _RequireRender();
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
                        tex1.texture2D.Name = tex1.fullPath;
                        TextureReadyToUpload.Enqueue(new(tex1.texture2D, uploader));
                    }
                    TextureOnDemand.Remove(loadCompleted.Key);
                }
            }
        }

        bool LoadTexture(DirectoryInfo folder, Texture2DPack texturePack1, KnownFile knownFile)
        {
            try
            {
                if (!knownFile.IsModified(folder.GetFiles())) return true;
                Uploader uploader = new Uploader();
                if (texturePack1.ReloadTexture(knownFile.file, uploader))
                {
                    uploaders[texturePack1] = uploader;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public T GetT<T>(DictionaryWithModifiyIndex<string, T> caches, string path, Func<FileInfo, T> createFun) where T : class
        {
            return GetT(caches, path, path, createFun);
        }
        public T GetT<T>(DictionaryWithModifiyIndex<string, T> caches, string path, string realPath, Func<FileInfo, T> createFun) where T : class
        {
            var knownFile = KnownFiles.GetOrCreate(realPath, () => new KnownFile()
            {
                fullPath = realPath,
            });
            int modifyIndex = knownFile.modifiyIndex;
            if (knownFile.requireReload.SetFalse() || knownFile.file == null)
            {
                string folderPath = Path.GetDirectoryName(realPath);
                if (!InitFolder(folderPath) && !Path.IsPathRooted(folderPath))
                    return null;
                var folder = (Path.IsPathRooted(folderPath)) ? new DirectoryInfo(folderPath) : KnownFolders[folderPath];
                try
                {
                    modifyIndex = knownFile.GetModifyIndex(folder.GetFiles());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            if (!caches.TryGetValue(path, out var file) || modifyIndex > caches.GetModifyIndex(path))
            {
                try
                {
                    caches.SetModifyIndex(path, modifyIndex);
                    var file1 = createFun(knownFile.file);
                    caches[path] = file1;
                    if (file is IDisposable disposable)
                        disposable?.Dispose();
                    file = file1;
                }
                catch (Exception e)
                {
                    if (file is IDisposable disposable)
                        disposable?.Dispose();
                    file = null;
                    caches[path] = file;
                    Console.WriteLine(e.Message);
                }
            }
            return file;
        }

        public Texture2D GetTextureLoaded(string path, GraphicsContext graphicsContext)
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

                    if (".pmx".Equals(file.Extension, StringComparison.CurrentCultureIgnoreCase))
                    {
                        modelPack.LoadPMX(path);
                    }
                    else
                    {
                        modelPack.LoadModel(path);
                    }
                    MeshReadyToUpload.Enqueue(modelPack.GetMesh());
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
                            SlotRes srv = res.Value.SRVs[i];
                            res.Value.SRVs[i] = srv;
                        }
                }
                return passes;
            });
            passSetting.path = path;
            return passSetting;
        }

        public UnionShader GetUnionShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);

            var assembly = GetAssembly(path);
            if (assembly == null) return null;

            return GetT(UnionShaders, path, file =>
            {
                Type type = assembly.GetType(Path.GetFileNameWithoutExtension(path));
                var info = type.GetMethod("UnionShader");
                var unionShader = (UnionShader)Delegate.CreateDelegate(typeof(UnionShader), info);
                return unionShader;
            });
        }

        public IPassDispatcher GetPassDispatcher(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);

            var assembly = GetAssembly(path);
            if (assembly == null) return null;

            return GetT(PassDispatchers, path, file =>
            {
                Type type = assembly.GetType(Path.GetFileNameWithoutExtension(path));
                var inst = Activator.CreateInstance(type);
                var dispatcher = (IPassDispatcher)inst;
                return dispatcher;
            });
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
                refs.AddRange(AppDomain.CurrentDomain.GetAssemblies().Where(u => u.GetName().Name.Contains("netstandard") ||
                    u.GetName().Name.Contains("System")).Select(u => MetadataReference.CreateFromFile(u.Location)));
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
                computeShader.Initialize(LoadShader(DxcShaderStage.Compute, File.ReadAllText(path), "csmain", path));
                return computeShader;
            });
        }

        public ComputeShader GetComputeShaderWithKeywords(List<ValueTuple<string, string>> keywords, string path)
        {
            string xPath;
            if (keywords != null)
            {
                keywords.Sort((x, y) => x.CompareTo(y));
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(path);
                foreach (var keyword in keywords)
                {
                    stringBuilder.Append(keyword.Item1);
                    stringBuilder.Append(keyword.Item2);
                }
                xPath = stringBuilder.ToString();
            }
            else
            {
                xPath = path;
            }
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            return GetT(ComputeShaders, xPath, path, file =>
            {
                DxcDefine[] dxcDefines = null;
                if (keywords != null)
                {
                    dxcDefines = new DxcDefine[keywords.Count];
                    for (int i = 0; i < keywords.Count; i++)
                    {
                        dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                    }
                }
                ComputeShader computeShader = new ComputeShader();
                computeShader.Initialize(LoadShader(DxcShaderStage.Compute, File.ReadAllText(path), "csmain", path, dxcDefines));
                return computeShader;
            });
        }

        public RayTracingShader GetRayTracingShader(string path)
        {
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            var rayTracingShader = GetT(RayTracingShaders, path, file =>
            {
                return ReadJsonStream<RayTracingShader>(file.OpenRead());
            });
            return rayTracingShader;
        }

        public RTPSO GetRTPSO(List<ValueTuple<string, string>> keywords, RayTracingShader shader, string path)
        {
            string xPath;
            if (keywords != null)
            {
                keywords.Sort((x, y) => x.CompareTo(y));
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(path);
                foreach (var keyword in keywords)
                {
                    stringBuilder.Append(keyword.Item1);
                    stringBuilder.Append(keyword.Item2);
                }
                xPath = stringBuilder.ToString();
            }
            else
            {
                xPath = path;
            }
            return GetT(RTPSOs, xPath, path, file =>
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
                            dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                        }
                    }
                    byte[] result = LoadShader(DxcShaderStage.Library, source, "", path, dxcDefines);

                    if (shader.hitGroups != null)
                    {
                        foreach (var pair in shader.hitGroups)
                            pair.Value.name = pair.Key;
                    }

                    RTPSO rtpso = new RTPSO();
                    rtpso.datas = result;
                    if (shader.rayGenShaders != null)
                        rtpso.rayGenShaders = shader.rayGenShaders.Values.ToArray();
                    else
                        rtpso.rayGenShaders = new RayTracingShaderDescription[0];
                    if (shader.hitGroups != null)
                        rtpso.hitGroups = shader.hitGroups.Values.ToArray();
                    else
                        rtpso.hitGroups = new RayTracingShaderDescription[0];

                    if (shader.missShaders != null)
                        rtpso.missShaders = shader.missShaders.Values.ToArray();
                    else
                        rtpso.missShaders = new RayTracingShaderDescription[0];

                    rtpso.exports = shader.GetExports();
                    List<ResourceAccessType> ShaderAccessTypes = new();
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    if (shader.CBVs != null)
                        for (int i = 0; i < shader.CBVs.Count; i++)
                            ShaderAccessTypes.Add(ResourceAccessType.CBV);
                    if (shader.SRVs != null)
                        for (int i = 0; i < shader.SRVs.Count; i++)
                            ShaderAccessTypes.Add(ResourceAccessType.SRVTable);
                    if (shader.UAVs != null)
                        for (int i = 0; i < shader.UAVs.Count; i++)
                            ShaderAccessTypes.Add(ResourceAccessType.UAVTable);
                    rtpso.shaderAccessTypes = ShaderAccessTypes.ToArray();
                    ShaderAccessTypes.Clear();
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    if (shader.localCBVs != null)
                        foreach (var cbv in shader.localCBVs)
                            ShaderAccessTypes.Add(ResourceAccessType.CBV);
                    if (shader.localSRVs != null)
                        foreach (var srv in shader.localSRVs)
                            ShaderAccessTypes.Add(ResourceAccessType.SRVTable);
                    rtpso.localShaderAccessTypes = ShaderAccessTypes.ToArray();
                    return rtpso;
                }
                catch (Exception e)
                {
                    Console.WriteLine(path);
                    Console.WriteLine(e);
                    return null;
                }
            });
        }

        public PSO GetPSOWithKeywords(List<ValueTuple<string, string>> keywords, string path, bool enableVS = true, bool enablePS = true, bool enableGS = false)
        {
            string xPath;
            if (keywords != null)
            {
                keywords.Sort((x, y) => x.CompareTo(y));
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(path);
                foreach (var keyword in keywords)
                {
                    stringBuilder.Append(keyword.Item1);
                    stringBuilder.Append(keyword.Item2);
                }
                xPath = stringBuilder.ToString();
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
                            dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                        }
                    }
                    byte[] vs = enableVS ? LoadShader(DxcShaderStage.Vertex, source, "vsmain", path, dxcDefines) : null;
                    byte[] gs = enableGS ? LoadShader(DxcShaderStage.Geometry, source, "gsmain", path, dxcDefines) : null;
                    byte[] ps = enablePS ? LoadShader(DxcShaderStage.Pixel, source, "psmain", path, dxcDefines) : null;
                    PSO pso = new PSO(vs, gs, ps);
                    return pso;
                }
                catch (Exception e)
                {
                    Console.WriteLine(path);
                    Console.WriteLine(e);
                    return null;
                }
            });
        }

        static byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint, string fileName, DxcDefine[] dxcDefines = null)
        {
            var result = DxcCompiler.Compile(shaderStage, shaderCode, entryPoint, new DxcCompilerOptions() { ShaderModel = shaderStage == DxcShaderStage.Library ? DxcShaderModel.Model6_3 : DxcShaderModel.Model6_0 }, fileName, dxcDefines, null);
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

        public Dictionary<IntPtr, string> ptr2string = new();
        public Dictionary<string, IntPtr> string2Ptr = new();
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

        public RootSignature GetRootSignature(string s)
        {
            if (RootSignatures.TryGetValue(s, out RootSignature rs))
                return rs;
            rs = new RootSignature();
            rs.Reload(fromString(s));
            RootSignatures[s] = rs;
            return rs;
        }
        ResourceAccessType[] fromString(string s)
        {
            ResourceAccessType[] desc = new ResourceAccessType[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case 'C':
                        desc[i] = ResourceAccessType.CBV;
                        break;
                    case 'c':
                        desc[i] = ResourceAccessType.CBVTable;
                        break;
                    case 'S':
                        desc[i] = ResourceAccessType.SRV;
                        break;
                    case 's':
                        desc[i] = ResourceAccessType.SRVTable;
                        break;
                    case 'U':
                        desc[i] = ResourceAccessType.UAV;
                        break;
                    case 'u':
                        desc[i] = ResourceAccessType.UAVTable;
                        break;
                    default:
                        throw new NotImplementedException("error root signature desc.");
                }
            }
            return desc;
        }

        public bool TryGetTexture(string s, out Texture2D tex)
        {
            bool result = TextureCaches.TryGetValue(s, out var tex1);
            tex = tex1?.texture2D;
            if (!result)
            {
                if (Path.IsPathFullyQualified(s))
                    Texture(s);
                else
                    Console.WriteLine(s);
            }
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
            using StreamReader reader1 = new StreamReader(stream);
            return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
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
