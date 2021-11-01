using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using System.IO;
using GSD = Coocoo3DGraphics.GraphicSignatureDesc;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Vortice.Dxc;

namespace Coocoo3D.RenderPipeline
{
    public class RPAssetsManager
    {
        public Dictionary<string, VertexShader> VSAssets = new Dictionary<string, VertexShader>();
        public Dictionary<string, GeometryShader> GSAssets = new Dictionary<string, GeometryShader>();
        public Dictionary<string, PixelShader> PSAssets = new Dictionary<string, PixelShader>();
        public Dictionary<string, ComputeShader> CSAssets = new Dictionary<string, ComputeShader>();
        public Dictionary<string, PSO> PSOs = new Dictionary<string, PSO>();
        public Dictionary<string, RootSignature> signaturePass = new Dictionary<string, RootSignature>();


        public RootSignature rootSignatureSkinning = new RootSignature();
        //public RootSignature rtLocal = new RootSignature();
        //public RootSignature rtGlobal = new RootSignature();

        public DefaultResource defaultResource;
        public bool Ready;
        public void InitializeRootSignature(GraphicsDevice graphicsDevice)
        {
            rootSignatureSkinning.ReloadSkinning(graphicsDevice);
            if (graphicsDevice.IsRayTracingSupport())
            {
                //rtLocal.RayTracingLocal(graphicsDevice);
                //rtGlobal.ReloadCompute(graphicsDevice, new GraphicSignatureDesc[] { GSD.UAVTable, GSD.SRV, GSD.CBV, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, });
            }
        }

        public async Task LoadAssets()
        {
            defaultResource = ReadJsonStream<DefaultResource>(OpenReadStream("DefaultResources/DefaultResourceList.json").Result);
            ConcurrentDictionary<string, VertexShader> vss = new ConcurrentDictionary<string, VertexShader>();
            ConcurrentDictionary<string, PixelShader> pss = new ConcurrentDictionary<string, PixelShader>();
            ConcurrentDictionary<string, ComputeShader> css = new ConcurrentDictionary<string, ComputeShader>();


            Parallel.Invoke(() => Parallel.ForEach(defaultResource.vertexShaders, vertexShader => RegVSAssets1(vertexShader.Name, vertexShader.Path, vss)),
             () => Parallel.ForEach(defaultResource.pixelShaders, pixelShader => RegPSAssets1(pixelShader.Name, pixelShader.Path, pss)),
             () => Parallel.ForEach(defaultResource.computeShaders, computeShader => RegCSAssets1(computeShader.Name, computeShader.Path, css)));

            foreach (var vs in vss)
                VSAssets.Add(vs.Key, vs.Value);
            foreach (var ps in pss)
                PSAssets.Add(ps.Key, ps.Value);
            foreach (var cs in css)
                CSAssets.Add(cs.Key, cs.Value);


            foreach (var pipelineState in defaultResource.pipelineStates)
            {
                PSO pso = new PSO();
                VertexShader vs = null;
                GeometryShader gs = null;
                PixelShader ps = null;
                if (pipelineState.vertexShader != null)
                    vs = VSAssets[pipelineState.vertexShader];
                if (pipelineState.geometryShader != null)
                    gs = GSAssets[pipelineState.geometryShader];
                if (pipelineState.pixelShader != null)
                    ps = PSAssets[pipelineState.pixelShader];
                pso.Initialize(vs, gs, ps);
                PSOs.Add(pipelineState.name, pso);
            }
            Ready = true;
        }
        //protected void RegVSAssets(string name, string path)
        //{
        //    VertexShader vertexShader = new VertexShader();
        //    if (Path.GetExtension(path) == ".hlsl")
        //        vertexShader.CompileInitialize1(ReadFile(path).Result, "main", new MacroEntry[0]);
        //    else
        //        vertexShader.Initialize(ReadFile(path).Result);
        //    VSAssets.Add(name, vertexShader);
        //}
        protected void RegVSAssets1(string name, string path, ConcurrentDictionary<string, VertexShader> assets)
        {
            VertexShader vertexShader = new VertexShader();
            if (Path.GetExtension(path) == ".hlsl")
                vertexShader.Initialize(LoadShader(DxcShaderStage.Vertex, ReadAllText(path), "main"));
            else
                vertexShader.Initialize(ReadFile1(path).Result);
            assets.TryAdd(name, vertexShader);
        }
        //protected void RegPSAssets(string name, string path)
        //{
        //    PixelShader pixelShader = new PixelShader();
        //    if (Path.GetExtension(path) == ".hlsl")
        //        pixelShader.CompileInitialize1(ReadFile(path).Result, "main", new MacroEntry[0]);
        //    else
        //        pixelShader.Initialize(ReadFile(path).Result);
        //    PSAssets.Add(name, pixelShader);
        //}
        protected void RegPSAssets1(string name, string path, ConcurrentDictionary<string, PixelShader> assets)
        {
            PixelShader pixelShader = new PixelShader();
            if (Path.GetExtension(path) == ".hlsl")
                pixelShader.Initialize(LoadShader(DxcShaderStage.Pixel, ReadAllText(path), "main"));
            else
                pixelShader.Initialize(ReadFile1(path).Result);
            assets.TryAdd(name, pixelShader);
        }
        protected void RegCSAssets1(string name, string path, ConcurrentDictionary<string, ComputeShader> assets)
        {
            ComputeShader computeShader = new ComputeShader();
            if (Path.GetExtension(path) == ".hlsl")
                computeShader.Initialize(LoadShader(DxcShaderStage.Compute, ReadAllText(path), "main"));
            else
                computeShader.Initialize(ReadFile1(path).Result);
            assets.TryAdd(name, computeShader);
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

        byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint)
        {
            var result = DxcCompiler.Compile(shaderStage, shaderCode, entryPoint, new DxcCompilerOptions() { });
            if (result.GetStatus() != SharpGen.Runtime.Result.Ok)
                throw new Exception(result.GetErrors());
            return result.GetResult().ToArray();
        }

        string ReadAllText(string uri)
        {
            var streamReader = new StreamReader(OpenReadStream(uri).Result);
            string result = streamReader.ReadToEnd();
            streamReader.Close();
            return result;
        }

        protected async Task<IBuffer> ReadFile(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///" + uri));
            return await FileIO.ReadBufferAsync(file);
        }

        protected async Task<byte[]> ReadFile1(string uri)
        {
            var binaryReader = new BinaryReader(OpenReadStream(uri).Result);
            byte[] result = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
            binaryReader.Close();
            return result;
        }

        protected async Task<Stream> OpenReadStream(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///" + uri));
            return (await file.OpenAsync(FileAccessMode.Read)).AsStreamForRead();
        }
        public RootSignature GetRootSignature(GraphicsDevice graphicsDevice, string s)
        {
            if (signaturePass.TryGetValue(s, out RootSignature g))
                return g;
            g = new RootSignature();
            g.Reload(graphicsDevice, fromString(s));
            signaturePass[s] = g;
            return g;
        }
        public GraphicSignatureDesc[] fromString(string s)
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
    }
    public class DefaultResource
    {
        public List<_AssetDefine> vertexShaders;
        public List<_AssetDefine> geometryShaders;
        public List<_AssetDefine> pixelShaders;
        public List<_AssetDefine> computeShaders;
        public List<_AssetDefine> texture2Ds;
        public List<_ResourceStr3> pipelineStates;
    }
    public struct _ResourceStr3
    {
        public string name;
        public string vertexShader;
        public string geometryShader;
        public string pixelShader;
    }
}
