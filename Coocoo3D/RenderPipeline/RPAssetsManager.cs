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

        public DefaultResource defaultResource;

        public void LoadAssets()
        {
            defaultResource = ReadJsonStream<DefaultResource>(OpenReadStream("DefaultResources/DefaultResourceList.json"));
            ConcurrentDictionary<string, VertexShader> vss = new ConcurrentDictionary<string, VertexShader>();
            ConcurrentDictionary<string, PixelShader> pss = new ConcurrentDictionary<string, PixelShader>();

            Parallel.Invoke(() => Parallel.ForEach(defaultResource.vertexShaders, vertexShader => RegVSAssets1(vertexShader, vss)),
             () => Parallel.ForEach(defaultResource.pixelShaders, pixelShader => RegPSAssets1(pixelShader, pss)));

            foreach (var vs in vss)
                VSAssets.Add(vs.Key, vs.Value);
            foreach (var ps in pss)
                PSAssets.Add(ps.Key, ps.Value);


            foreach (var pipelineState in defaultResource.pipelineStates)
            {
                PSO pso = new PSO();
                VertexShader vs = null;
                PixelShader ps = null;
                if (pipelineState.vertexShader != null)
                    vs = VSAssets[pipelineState.vertexShader];
                if (pipelineState.pixelShader != null)
                    ps = PSAssets[pipelineState.pixelShader];
                pso.Initialize(vs, null, ps);
                PSOs.Add(pipelineState.name, pso);
            }
        }
        protected void RegVSAssets1(_AssetDefine define, ConcurrentDictionary<string, VertexShader> assets)
        {
            var path = define.Path;
            VertexShader vertexShader = new VertexShader();
            if (Path.GetExtension(path) == ".hlsl")
                vertexShader.Initialize(LoadShader(DxcShaderStage.Vertex, File.ReadAllText(path), define.EntryPoint ?? "main"));
            else
                vertexShader.Initialize(File.ReadAllBytes(path));
            assets.TryAdd(define.Name, vertexShader);
        }
        protected void RegPSAssets1(_AssetDefine define, ConcurrentDictionary<string, PixelShader> assets)
        {
            var path = define.Path;
            PixelShader pixelShader = new PixelShader();
            if (Path.GetExtension(path) == ".hlsl")
                pixelShader.Initialize(LoadShader(DxcShaderStage.Pixel, File.ReadAllText(path), define.EntryPoint ?? "main"));
            else
                pixelShader.Initialize(File.ReadAllBytes(path));
            assets.TryAdd(define.Name, pixelShader);
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

        public void Dispose()
        {
            foreach (var pso in PSOs)
            {
                pso.Value.Dispose();
            }
        }
    }
    public class DefaultResource
    {
        public List<_AssetDefine> vertexShaders;
        public List<_AssetDefine> pixelShaders;
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
