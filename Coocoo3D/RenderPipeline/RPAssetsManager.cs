using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using GSD = Coocoo3DGraphics.GraphicSignatureDesc;
using System.Collections.Concurrent;

namespace Coocoo3D.RenderPipeline
{
    public class RPAssetsManager
    {
        public Dictionary<string, VertexShader> VSAssets = new Dictionary<string, VertexShader>();
        public Dictionary<string, GeometryShader> GSAssets = new Dictionary<string, GeometryShader>();
        public Dictionary<string, PixelShader> PSAssets = new Dictionary<string, PixelShader>();
        public Dictionary<string, ComputeShader> CSAssets = new Dictionary<string, ComputeShader>();
        public Dictionary<string, PSO> PSOs = new Dictionary<string, PSO>();
        public Dictionary<string, Texture2D> texture2ds = new Dictionary<string, Texture2D>();
        public Dictionary<string, TextureCube> textureCubes = new Dictionary<string, TextureCube>();
        public Dictionary<string, GraphicsSignature> signaturePass = new Dictionary<string, GraphicsSignature>();
        public Dictionary<IntPtr, string> ptr2string = new Dictionary<IntPtr, string>();

        public GraphicsSignature rootSignatureSkinning = new GraphicsSignature();
        public GraphicsSignature rtLocal = new GraphicsSignature();
        public GraphicsSignature rtGlobal = new GraphicsSignature();

        public DefaultResource defaultResource;
        public bool Ready;
        public void InitializeRootSignature(DeviceResources deviceResources)
        {
            rootSignatureSkinning.ReloadSkinning(deviceResources);
            if (deviceResources.IsRayTracingSupport())
            {
                rtLocal.RayTracingLocal(deviceResources);
                rtGlobal.ReloadCompute(deviceResources, new GraphicSignatureDesc[] { GSD.UAVTable, GSD.SRV, GSD.CBV, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, });
            }
        }
        long count1 = 0;
        public IntPtr GetPtr(string s)
        {
            long i = System.Threading.Interlocked.Increment(ref count1);
            IntPtr ptr = new IntPtr(i);
            ptr2string[ptr] = s;
            return ptr;
        }
        public Texture2D GetTexture(IntPtr ptr)
        {
            if(ptr2string.TryGetValue(ptr, out string s) && texture2ds.TryGetValue(s, out var tex))
            {
                return tex;
            }
            return null;
        }
        public async Task LoadAssets()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(DefaultResource));
            defaultResource = (DefaultResource)xmlSerializer.Deserialize(await OpenReadStream("ms-appx:///DefaultResources/DefaultResourceList.xml"));
            ConcurrentDictionary<string, VertexShader> vss = new ConcurrentDictionary<string, VertexShader>();
            ConcurrentDictionary<string, PixelShader> pss = new ConcurrentDictionary<string, PixelShader>();
            ConcurrentDictionary<string, ComputeShader> css = new ConcurrentDictionary<string, ComputeShader>();

            Parallel.Invoke(() =>
            Parallel.ForEach(defaultResource.vertexShaders, async vertexShader => await RegVSAssets1(vertexShader.Name, vertexShader.Path, vss)),
            () => Parallel.ForEach(defaultResource.pixelShaders, async pixelShader => await RegPSAssets1(pixelShader.Name, pixelShader.Path, pss)),
            () => Parallel.ForEach(defaultResource.computeShaders, async computeShader => await RegCSAssets1(computeShader.Name, computeShader.Path, css)));
            
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
                if (pipelineState.VertexShader != null)
                    vs = VSAssets[pipelineState.VertexShader];
                if (pipelineState.GeometryShader != null)
                    gs = GSAssets[pipelineState.GeometryShader];
                if (pipelineState.PixelShader != null)
                    ps = PSAssets[pipelineState.PixelShader];
                pso.Initialize(vs, gs, ps);
                PSOs.Add(pipelineState.Name, pso);
            }
            Ready = true;
        }
        protected async Task RegVSAssets(string name, string path)
        {
            VertexShader vertexShader = new VertexShader();
            if (Path.GetExtension(path) == ".hlsl")
                vertexShader.CompileInitialize1(await ReadFile(path), "main", new MacroEntry[0]);
            else
                vertexShader.Initialize(await ReadFile(path));
            VSAssets.Add(name, vertexShader);
        }
        protected async Task RegVSAssets1(string name, string path, ConcurrentDictionary<string, VertexShader> assets)
        {
            VertexShader vertexShader = new VertexShader();
            if (Path.GetExtension(path) == ".hlsl")
                vertexShader.CompileInitialize1(await ReadFile(path), "main", new MacroEntry[0]);
            else
                vertexShader.Initialize(await ReadFile(path));
            assets.TryAdd(name, vertexShader);
        }
        protected async Task RegPSAssets(string name, string path)
        {
            PixelShader pixelShader = new PixelShader();
            if (Path.GetExtension(path) == ".hlsl")
                pixelShader.CompileInitialize1(await ReadFile(path), "main", new MacroEntry[0]);
            else
                pixelShader.Initialize(await ReadFile(path));
            PSAssets.Add(name, pixelShader);
        }
        protected async Task RegPSAssets1(string name, string path, ConcurrentDictionary<string, PixelShader> assets)
        {
            PixelShader pixelShader = new PixelShader();
            if (Path.GetExtension(path) == ".hlsl")
                pixelShader.CompileInitialize1(await ReadFile(path), "main", new MacroEntry[0]);
            else
                pixelShader.Initialize(await ReadFile(path));
            assets.TryAdd(name, pixelShader);
        }
        protected async Task RegCSAssets1(string name, string path, ConcurrentDictionary<string, ComputeShader> assets)
        {
            ComputeShader computeShader = new ComputeShader();
            if (Path.GetExtension(path) == ".hlsl")
                computeShader.CompileInitialize1(await ReadFile(path), "main", new MacroEntry[0]);
            else
                computeShader.Initialize(await ReadFile(path));
            assets.TryAdd(name, computeShader);
        }




        protected async Task<IBuffer> ReadFile(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            return await FileIO.ReadBufferAsync(file);
        }

        protected async Task<Stream> OpenReadStream(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            return (await file.OpenAsync(FileAccessMode.Read)).AsStreamForRead();
        }
        public GraphicsSignature GetRootSignature(DeviceResources deviceResources, string s)
        {
            if (signaturePass.TryGetValue(s, out GraphicsSignature g))
                return g;
            g = new GraphicsSignature();
            g.Reload(deviceResources, fromString(s));
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
        [XmlElement(ElementName = "VertexShader")]
        public List<_AssetDefine> vertexShaders;
        [XmlElement(ElementName = "GeometryShader")]
        public List<_AssetDefine> geometryShaders;
        [XmlElement(ElementName = "PixelShader")]
        public List<_AssetDefine> pixelShaders;
        [XmlElement(ElementName = "ComputeShader")]
        public List<_AssetDefine> computeShaders;
        [XmlElement(ElementName = "Texture2D")]
        public List<_AssetDefine> texture2Ds;
        [XmlElement(ElementName = "PipelineState")]
        public List<_ResourceStr3> pipelineStates;
    }
    public struct _ResourceStr3
    {
        public string Name;
        public string VertexShader;
        public string GeometryShader;
        public string PixelShader;
    }
}
