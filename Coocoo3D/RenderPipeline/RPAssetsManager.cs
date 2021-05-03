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
using PSO = Coocoo3DGraphics.PObject;

namespace Coocoo3D.RenderPipeline
{
    public class RPAssetsManager
    {
        public GraphicsSignature rootSignature = new GraphicsSignature();
        public GraphicsSignature rootSignatureSkinning = new GraphicsSignature();
        public GraphicsSignature rootSignaturePostProcess = new GraphicsSignature();
        public GraphicsSignature rootSignatureCompute = new GraphicsSignature();
        public GraphicsSignature rtLocal = new GraphicsSignature();
        public GraphicsSignature rtGlobal = new GraphicsSignature();


        public Dictionary<string, VertexShader> VSAssets = new Dictionary<string, VertexShader>();
        public Dictionary<string, GeometryShader> GSAssets = new Dictionary<string, GeometryShader>();
        public Dictionary<string, PixelShader> PSAssets = new Dictionary<string, PixelShader>();
        public Dictionary<string, PSO> PSOs = new Dictionary<string, PSO>();
        public Dictionary<string, Texture2D> texture2ds = new Dictionary<string, Texture2D>();


        public PSO PSOMMDSkinning = new PSO();
        public PSO PObjectDeferredRenderGBuffer = new PSO();
        public PSO PObjectDeferredRenderIBL = new PSO();
        public PSO PObjectDeferredRenderDirectLight = new PSO();
        public PSO PObjectDeferredRenderPointLight = new PSO();
        public PSO PObjectWidgetUI1 = new PSO();
        public PSO PObjectWidgetUI2 = new PSO();
        public PSO PObjectWidgetUILight = new PSO();
        public DefaultResource defaultResource;
        public bool Ready;
        public void InitializeRootSignature(DeviceResources deviceResources)
        {
            rootSignature.Reload(deviceResources, new GraphicSignatureDesc[] { GSD.CBV,GSD.CBV,GSD.CBV, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable });
            rootSignatureSkinning.ReloadSkinning(deviceResources);
            rootSignaturePostProcess.Reload(deviceResources, new GraphicSignatureDesc[] { GSD.CBV, GSD.SRVTable, GSD.SRVTable, GSD.CBV });
            rootSignatureCompute.ReloadCompute(deviceResources, new GraphicSignatureDesc[] { GSD.CBV, GSD.CBV, GSD.CBV, GSD.SRV, GSD.UAV, GSD.UAV });
            if (deviceResources.IsRayTracingSupport())
            {
                rtLocal.RayTracingLocal(deviceResources);
                rtGlobal.ReloadCompute(deviceResources, new GraphicSignatureDesc[] { GSD.UAVTable, GSD.SRV, GSD.CBV, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, GSD.SRVTable, });
            }
        }
        public async Task LoadAssets()
        {

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(DefaultResource));
            defaultResource = (DefaultResource)xmlSerializer.Deserialize(await OpenReadStream("ms-appx:///DefaultResources/DefaultResourceList.xml"));
            foreach (var vertexShader in defaultResource.vertexShaders)
            {
                RegVSAssets(vertexShader.Name, vertexShader.Path);
            }
            foreach (var pixelShader in defaultResource.pixelShaders)
            {
                RegPSAssets(pixelShader.Name, pixelShader.Path);
            }
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
        }
        public void InitializePipelineState()
        {
            Ready = false;

            PSOMMDSkinning.Initialize(VSAssets["VSMMDSkinning.cso"], null, null);

            PObjectDeferredRenderGBuffer.Initialize(VSAssets["VSMMDTransform"], null, PSAssets["PSDeferredRenderGBuffer.cso"]);
            PObjectDeferredRenderIBL.Initialize(VSAssets["VS_SkyBox"], null, PSAssets["PSDeferredRenderIBL.cso"]);
            PObjectDeferredRenderDirectLight.Initialize(VSAssets["VS_SkyBox"], null, PSAssets["PSDeferredRenderDirectLight.cso"]);
            PObjectDeferredRenderPointLight.Initialize(VSAssets["VSDeferredRenderPointLight.cso"], null, PSAssets["PSDeferredRenderPointLight.cso"]);


            PObjectWidgetUI1.Initialize(VSAssets["VSWidgetUI1.cso"], null, PSAssets["PSWidgetUI1.cso"]);
            PObjectWidgetUI2.Initialize(VSAssets["VSWidgetUI2.cso"], null, PSAssets["PSWidgetUI2.cso"]);
            PObjectWidgetUILight.Initialize(VSAssets["VSWidgetUILight.cso"], null, PSAssets["PSWidgetUILight.cso"]);

            Ready = true;
        }
        protected async Task ReloadVertexShader(VertexShader vertexShader, string uri)
        {
            vertexShader.Initialize(await ReadFile(uri));
        }
        protected async Task ReloadPixelShader(PixelShader pixelShader, string uri)
        {
            pixelShader.Initialize(await ReadFile(uri));
        }
        protected async Task RegVSAssets(string name, string path)
        {
            VertexShader vertexShader = new VertexShader();
            vertexShader.Initialize(await ReadFile(path));
            VSAssets.Add(name, vertexShader);
        }
        protected async Task RegPSAssets(string name, string path)
        {
            PixelShader pixelShader = new PixelShader();
            pixelShader.Initialize(await ReadFile(path));
            PSAssets.Add(name, pixelShader);
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

    }
    public class DefaultResource
    {
        [XmlElement(ElementName = "VertexShader")]
        public List<_AssetDefine> vertexShaders;
        [XmlElement(ElementName = "GeometryShader")]
        public List<_AssetDefine> geometryShaders;
        [XmlElement(ElementName = "PixelShader")]
        public List<_AssetDefine> pixelShaders;
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
