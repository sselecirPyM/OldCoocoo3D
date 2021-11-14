using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Vortice.Dxc;

namespace Coocoo3D.RenderPipeline
{
    public class RPAssetsManager : IDisposable
    {
        public Dictionary<string, VertexShader> VSAssets = new Dictionary<string, VertexShader>();
        public Dictionary<string, PixelShader> PSAssets = new Dictionary<string, PixelShader>();
        public Dictionary<string, PSO> PSOs = new Dictionary<string, PSO>();
        public Dictionary<string, RootSignature> signaturePass = new Dictionary<string, RootSignature>();

        public DefaultResource defaultResource;

        public void LoadAssets()
        {
            defaultResource = ReadJsonStream<DefaultResource>(OpenReadStream("DefaultResources/DefaultResourceList.json"));
            ConcurrentDictionary<string, VertexShader> vss = new ConcurrentDictionary<string, VertexShader>();
            ConcurrentDictionary<string, PixelShader> pss = new ConcurrentDictionary<string, PixelShader>();

            Parallel.Invoke(() => Parallel.ForEach(defaultResource.vertexShaders, vertexShader => RegVSAssets1(vertexShader.Name, vertexShader.Path, vss)),
             () => Parallel.ForEach(defaultResource.pixelShaders, pixelShader => RegPSAssets1(pixelShader.Name, pixelShader.Path, pss)));

            foreach (var vs in vss)
                VSAssets.Add(vs.Key, vs.Value);
            foreach (var ps in pss)
                PSAssets.Add(ps.Key, ps.Value);


            foreach (var pipelineState in defaultResource.pipelineStates)
            {
                PSO pso = new PSO();
                VertexShader vs = null;
                GeometryShader gs = null;
                PixelShader ps = null;
                if (pipelineState.vertexShader != null)
                    vs = VSAssets[pipelineState.vertexShader];
                //if (pipelineState.geometryShader != null)
                //    gs = GSAssets[pipelineState.geometryShader];
                if (pipelineState.pixelShader != null)
                    ps = PSAssets[pipelineState.pixelShader];
                pso.Initialize(vs, gs, ps);
                PSOs.Add(pipelineState.name, pso);
            }
        }
        protected void RegVSAssets1(string name, string path, ConcurrentDictionary<string, VertexShader> assets)
        {
            VertexShader vertexShader = new VertexShader();
            if (Path.GetExtension(path) == ".hlsl")
                vertexShader.Initialize(LoadShader(DxcShaderStage.Vertex, File.ReadAllText(path), "main"));
            else
                vertexShader.Initialize(File.ReadAllBytes(path));
            assets.TryAdd(name, vertexShader);
        }
        protected void RegPSAssets1(string name, string path, ConcurrentDictionary<string, PixelShader> assets)
        {
            PixelShader pixelShader = new PixelShader();
            if (Path.GetExtension(path) == ".hlsl")
                pixelShader.Initialize(LoadShader(DxcShaderStage.Pixel, File.ReadAllText(path), "main"));
            else
                pixelShader.Initialize(File.ReadAllBytes(path));
            assets.TryAdd(name, pixelShader);
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

        protected Stream OpenReadStream(string uri)
        {
            FileInfo file = new FileInfo(uri);
            return file.OpenRead();
        }
        public RootSignature GetRootSignature(GraphicsDevice graphicsDevice, string s)
        {
            if (signaturePass.TryGetValue(s, out RootSignature rs))
                return rs;
            rs = new RootSignature();
            rs.Reload(graphicsDevice, fromString(s));
            signaturePass[s] = rs;
            return rs;
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

        public void Dispose()
        {
            foreach(var pso in PSOs)
            {
                pso.Value.Dispose();
            }
            foreach(var rs in signaturePass)
            {
                rs.Value.Dispose();
            }
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
