using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using GSD = Coocoo3DGraphics.GraphicSignatureDesc;

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
        public PixelShader PSMMD_DisneyBrdf = new PixelShader();
        public PixelShader PSMMD_Toon1 = new PixelShader();
        public PixelShader PSMMDAlphaClip = new PixelShader();
        public PixelShader PSMMDAlphaClip1 = new PixelShader();

        public Dictionary<string, VertexShader> VSAssets = new Dictionary<string, VertexShader>();
        public Dictionary<string, GeometryShader> GSAssets = new Dictionary<string, GeometryShader>();
        public Dictionary<string, PixelShader> PSAssets = new Dictionary<string, PixelShader>();

        public PObject PObjectMMDSkinning = new PObject();
        public PObject PObjectMMD = new PObject();
        public PObject PObjectMMDTransparent = new PObject();
        public PObject PObjectMMD_DisneyBrdf = new PObject();
        public PObject PObjectMMD_Toon1 = new PObject();
        public PObject PObjectMMDShadowDepth = new PObject();
        public PObject PObjectMMDDepth = new PObject();
        public PObject PObjectMMDLoading = new PObject();
        public PObject PObjectMMDError = new PObject();
        public PObject PObjectDeferredRenderGBuffer = new PObject();
        public PObject PObjectDeferredRenderIBL = new PObject();
        public PObject PObjectDeferredRenderDirectLight = new PObject();
        public PObject PObjectDeferredRenderPointLight = new PObject();
        public PObject PObjectSkyBox = new PObject();
        public PObject PObjectPostProcess = new PObject();
        public PObject PObjectWidgetUI1 = new PObject();
        public PObject PObjectWidgetUI2 = new PObject();
        public PObject PObjectWidgetUILight = new PObject();
        public DxgiFormat outputFormat;
        public DxgiFormat middleFormat;
        public DxgiFormat depthFormat;
        public bool Ready;
        public void Reload(DeviceResources deviceResources)
        {
            rootSignature.ReloadMMD(deviceResources);
            rootSignatureSkinning.ReloadSkinning(deviceResources);
            rootSignaturePostProcess.Reload(deviceResources, new GraphicSignatureDesc[] { GSD.CBV, GSD.SRVTable, GSD.SRVTable, GSD.CBV });
            rootSignatureCompute.ReloadCompute(deviceResources, new GraphicSignatureDesc[] { GSD.CBV, GSD.CBV, GSD.CBV, GSD.SRV, GSD.UAV, GSD.UAV });
        }
        public async Task ReloadAssets()
        {
            await ReloadVertexShader(VSMMDTransform, "ms-appx:///Coocoo3DGraphics/VSMMDTransform.cso");
            await ReloadPixelShader(PSMMD, "ms-appx:///Coocoo3DGraphics/PSMMD.cso");
            await ReloadPixelShader(PSMMDTransparent, "ms-appx:///Coocoo3DGraphics/PSMMDTransparent.cso");
            await ReloadPixelShader(PSMMD_DisneyBrdf, "ms-appx:///Coocoo3DGraphics/PSMMD_DisneyBRDF.cso");
            await ReloadPixelShader(PSMMD_Toon1, "ms-appx:///Coocoo3DGraphics/PSMMD_Toon1.cso");
            await ReloadPixelShader(PSMMDAlphaClip, "ms-appx:///Coocoo3DGraphics/PSMMDAlphaClip.cso");
            await ReloadPixelShader(PSMMDAlphaClip1, "ms-appx:///Coocoo3DGraphics/PSMMDAlphaClip1.cso");

            await RegVSAssets("VSMMDSkinning.cso");
            await RegVSAssets("VSSkyBox.cso");
            await RegVSAssets("VSDeferredRenderPointLight.cso");
            await RegVSAssets("VSPostProcess.cso");

            await RegVSAssets("VSWidgetUI1.cso");
            await RegVSAssets("VSWidgetUI2.cso");
            await RegVSAssets("VSWidgetUILight.cso");

            await RegPSAssets("PSDeferredRenderGBuffer.cso");
            await RegPSAssets("PSDeferredRenderIBL.cso");
            await RegPSAssets("PSDeferredRenderDirectLight.cso");
            await RegPSAssets("PSDeferredRenderPointLight.cso");

            await RegPSAssets("PSSkyBox.cso");
            await RegPSAssets("PSPostProcess.cso");
            await RegPSAssets("PSWidgetUI1.cso");
            await RegPSAssets("PSWidgetUI2.cso");
            await RegPSAssets("PSWidgetUILight.cso");

            await RegPSAssets("PSLoading.cso");
            await RegPSAssets("PSError.cso");
        }
        public void ChangeRenderTargetFormat(DeviceResources deviceResources, ProcessingList uploadProcess, DxgiFormat outputFormat, DxgiFormat middleFormat, DxgiFormat swapChainFormat, DxgiFormat depthFormat)
        {
            Ready = false;
            this.outputFormat = outputFormat;
            this.middleFormat = middleFormat;
            this.depthFormat = depthFormat;

            PObjectMMDSkinning.InitializeSkinning(VSAssets["VSMMDSkinning.cso"], null);
            uploadProcess.UL(PObjectMMDSkinning, 1);

            PObjectMMD.InitializeDrawing(EBlendState.alpha, VSMMDTransform, null, PSMMD, outputFormat, depthFormat);
            PObjectMMDTransparent.InitializeDrawing(EBlendState.alpha, VSMMDTransform, null, PSMMDTransparent, outputFormat, depthFormat);
            PObjectMMD_DisneyBrdf.InitializeDrawing(EBlendState.alpha, VSMMDTransform, null, PSMMD_DisneyBrdf, outputFormat, depthFormat);
            PObjectMMD_Toon1.InitializeDrawing(EBlendState.alpha, VSMMDTransform, null, PSMMD_Toon1, outputFormat, depthFormat);
            PObjectMMDLoading.InitializeDrawing(EBlendState.alpha, VSMMDTransform, null, PSAssets["PSLoading.cso"], outputFormat, depthFormat);
            PObjectMMDError.InitializeDrawing(EBlendState.alpha, VSMMDTransform, null, PSAssets["PSError.cso"], outputFormat, depthFormat);
            uploadProcess.UL(PObjectMMD, 0);
            uploadProcess.UL(PObjectMMDTransparent, 0);
            uploadProcess.UL(PObjectMMD_DisneyBrdf, 0);
            uploadProcess.UL(PObjectMMD_Toon1, 0);
            uploadProcess.UL(PObjectMMDLoading, 0);
            uploadProcess.UL(PObjectMMDError, 0);

            PObjectDeferredRenderGBuffer.InitializeDrawing(EBlendState.none, VSMMDTransform, null, PSAssets["PSDeferredRenderGBuffer.cso"], middleFormat, depthFormat, 3);
            PObjectDeferredRenderIBL.InitializeDrawing(EBlendState.add,VSAssets["VSSkyBox.cso"], null, PSAssets["PSDeferredRenderIBL.cso"], outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectDeferredRenderDirectLight.InitializeDrawing(EBlendState.add, VSAssets["VSSkyBox.cso"], null, PSAssets["PSDeferredRenderDirectLight.cso"], outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectDeferredRenderPointLight.InitializeDrawing(EBlendState.add, VSAssets["VSDeferredRenderPointLight.cso"], null, PSAssets["PSDeferredRenderPointLight.cso"], outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            uploadProcess.UL(PObjectDeferredRenderGBuffer, 0);
            uploadProcess.UL(PObjectDeferredRenderIBL, 0);
            uploadProcess.UL(PObjectDeferredRenderDirectLight, 0);
            uploadProcess.UL(PObjectDeferredRenderPointLight, 0);

            PObjectMMDShadowDepth.InitializeDepthOnly(VSMMDTransform, null, 2500, depthFormat);
            PObjectMMDDepth.InitializeDepthOnly(VSMMDTransform, PSMMDAlphaClip1, 0, depthFormat);
            uploadProcess.UL(PObjectMMDShadowDepth, 0);
            uploadProcess.UL(PObjectMMDDepth, 0);


            PObjectSkyBox.Initialize(deviceResources, rootSignature, EInputLayout.postProcess, EBlendState.none, VSAssets["VSSkyBox.cso"], null, PSAssets["PSSkyBox.cso"], outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectPostProcess.Initialize(deviceResources, rootSignaturePostProcess, EInputLayout.postProcess, EBlendState.none,VSAssets["VSPostProcess.cso"], null, PSAssets["PSPostProcess.cso"], swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectWidgetUI1.Initialize(deviceResources, rootSignaturePostProcess, EInputLayout.postProcess, EBlendState.alpha, VSAssets["VSWidgetUI1.cso"], null, PSAssets["PSWidgetUI1.cso"], swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectWidgetUI2.Initialize(deviceResources, rootSignaturePostProcess, EInputLayout.postProcess, EBlendState.alpha,VSAssets["VSWidgetUI2.cso"], null, PSAssets["PSWidgetUI2.cso"], swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectWidgetUILight.Initialize(deviceResources, rootSignaturePostProcess, EInputLayout.postProcess, EBlendState.alpha, VSAssets["VSWidgetUILight.cso"], null, PSAssets["PSWidgetUILight.cso"], swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN, ED3D12PrimitiveTopologyType.LINE);
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
        protected async Task ReloadComputeShader(ComputePO computeShader, string uri)
        {
            computeShader.Initialize(await ReadFile(uri));
        }
        static string assetsUri = "ms-appx:///Coocoo3DGraphics/";
        protected async Task RegVSAssets(string name)
        {
            VertexShader vertexShader = new VertexShader();
            vertexShader.Initialize(await ReadFile(assetsUri + name));
            VSAssets.Add(name, vertexShader);
        }
        protected async Task RegGSAssets(string name)
        {
            GeometryShader geometryShader = new GeometryShader();
            geometryShader.Initialize(await ReadFile(assetsUri + name));
            GSAssets.Add(name, geometryShader);
        }
        protected async Task RegPSAssets(string name)
        {
            PixelShader pixelShader = new PixelShader();
            pixelShader.Initialize(await ReadFile(assetsUri + name));
            PSAssets.Add(name, pixelShader);
        }
        protected async Task<IBuffer> ReadFile(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            return await FileIO.ReadBufferAsync(file);
        }

        #region UploadProceess
        public void _DealStep3(DeviceResources deviceResources, ProcessingList uploadProcess)
        {
            foreach (var a in uploadProcess.pobjectLists[0])
            {
                a.Upload(deviceResources, rootSignature);
            }
            foreach (var a in uploadProcess.pobjectLists[1])
            {
                a.Upload(deviceResources, rootSignatureSkinning);
            }
            foreach (var a in uploadProcess.pobjectLists[2])
            {
                a.Upload(deviceResources, rootSignaturePostProcess);
            }
            foreach (var a in uploadProcess.computePObjectLists[0])
            {
                a.Upload(deviceResources, rootSignatureCompute);
            }
        }
        #endregion
    }
}
