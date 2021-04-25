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
        public VertexShader VSMMDTransform = new VertexShader();
        public PixelShader PSMMD = new PixelShader();
        public PixelShader PSMMDTransparent = new PixelShader();
        public PixelShader PSMMDAlphaClip = new PixelShader();
        public PixelShader PSMMDAlphaClip1 = new PixelShader();

        public Dictionary<string, VertexShader> VSAssets = new Dictionary<string, VertexShader>();
        public Dictionary<string, GeometryShader> GSAssets = new Dictionary<string, GeometryShader>();
        public Dictionary<string, PixelShader> PSAssets = new Dictionary<string, PixelShader>();
        public Dictionary<string, PSO> PSOs = new Dictionary<string, PSO>();

        public PSO PSOMMDSkinning = new PSO();
        public PSO PSOMMD = new PSO();
        public PSO PSOMMDTransparent = new PSO();
        public PSO PSOMMDShadowDepth = new PSO();
        public PSO PObjectMMDDepth = new PSO();
        public PSO PSOMMDLoading = new PSO();
        public PSO PSOMMDError = new PSO();
        public PSO PObjectDeferredRenderGBuffer = new PSO();
        public PSO PObjectDeferredRenderIBL = new PSO();
        public PSO PObjectDeferredRenderDirectLight = new PSO();
        public PSO PObjectDeferredRenderPointLight = new PSO();
        public PSO PSOSkyBox = new PSO();
        public PSO PObjectPostProcess = new PSO();
        public PSO PObjectWidgetUI1 = new PSO();
        public PSO PObjectWidgetUI2 = new PSO();
        public PSO PObjectWidgetUILight = new PSO();

        public bool Ready;
        public void InitializeRootSignature(DeviceResources deviceResources)
        {
            rootSignature.ReloadMMD(deviceResources);
            rootSignatureSkinning.ReloadSkinning(deviceResources);
            rootSignaturePostProcess.Reload(deviceResources, new GraphicSignatureDesc[] { GSD.CBV, GSD.SRVTable, GSD.SRVTable, GSD.CBV });
            rootSignatureCompute.ReloadCompute(deviceResources, new GraphicSignatureDesc[] { GSD.CBV, GSD.CBV, GSD.CBV, GSD.SRV, GSD.UAV, GSD.UAV });
        }
        public async Task LoadAssets()
        {
            await ReloadVertexShader(VSMMDTransform, "ms-appx:///Coocoo3DGraphics/VSMMDTransform.cso");
            await ReloadPixelShader(PSMMD, "ms-appx:///Coocoo3DGraphics/PSMMD.cso");
            await ReloadPixelShader(PSMMDTransparent, "ms-appx:///Coocoo3DGraphics/PSMMDTransparent.cso");
            await ReloadPixelShader(PSMMDAlphaClip, "ms-appx:///Coocoo3DGraphics/PSMMDAlphaClip.cso");
            await ReloadPixelShader(PSMMDAlphaClip1, "ms-appx:///Coocoo3DGraphics/PSMMDAlphaClip1.cso");

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(DefaultResource));
            DefaultResource d = (DefaultResource)xmlSerializer.Deserialize(await OpenReadStream("ms-appx:///DefaultResources/DefaultResourceList.xml"));
            foreach(var vertexShader in d.vertexShaders)
            {
                RegVSAssets(vertexShader.Name, vertexShader.Path);
            }
            foreach(var pixelShader in d.pixelShaders)
            {
                RegPSAssets(pixelShader.Name, pixelShader.Path);
            }
            foreach(var pipelineState in d.pipelineStates)
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

            PSOMMD.Initialize(VSMMDTransform, null, PSMMD);

            PSOMMDTransparent.Initialize(VSMMDTransform, null, PSMMDTransparent);
            PSOMMDLoading.Initialize(VSMMDTransform, null, PSAssets["PSLoading.cso"]);
            PSOMMDError.Initialize(VSMMDTransform, null, PSAssets["PSError.cso"]);

            PObjectDeferredRenderGBuffer.Initialize(VSMMDTransform, null, PSAssets["PSDeferredRenderGBuffer.cso"]);
            PObjectDeferredRenderIBL.Initialize(VSAssets["VS_SkyBox"], null, PSAssets["PSDeferredRenderIBL.cso"]);
            PObjectDeferredRenderDirectLight.Initialize(VSAssets["VS_SkyBox"], null, PSAssets["PSDeferredRenderDirectLight.cso"]);
            PObjectDeferredRenderPointLight.Initialize(VSAssets["VSDeferredRenderPointLight.cso"], null, PSAssets["PSDeferredRenderPointLight.cso"]);

            PSOMMDShadowDepth.Initialize(VSMMDTransform, null, null);
            PObjectMMDDepth.Initialize(VSMMDTransform, null, PSMMDAlphaClip1);

            PSOSkyBox.Initialize(VSAssets["VS_SkyBox"], null, PSAssets["PS_SkyBox"]);
            PObjectPostProcess.Initialize(VSAssets["VSPostProcess.cso"], null, PSAssets["PSPostProcess.cso"]);
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
        protected async Task RegPSAssets(string name,string path)
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
    [Serializable]
    public class DefaultResource
    {
        [XmlElement(ElementName = "VertexShader")]
        public List<_ResourceStr2> vertexShaders;
        [XmlElement(ElementName = "GeometryShader")]
        public List<_ResourceStr2> geometryShaders;
        [XmlElement(ElementName = "PixelShader")]
        public List<_ResourceStr2> pixelShaders;
        [XmlElement(ElementName = "PipelineState")]
        public List<_ResourceStr3> pipelineStates;
    }
    public struct _ResourceStr2
    {
        public string Name;
        public string Path;
    }
    public struct _ResourceStr3
    {
        public string Name;
        public string VertexShader;
        public string GeometryShader;
        public string PixelShader;
    }
}
