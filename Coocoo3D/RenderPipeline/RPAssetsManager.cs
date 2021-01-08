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
        public VertexShader VSMMDSkinning2 = new VertexShader();
        public VertexShader VSMMDTransform = new VertexShader();
        public VertexShader VSSkyBox = new VertexShader();
        public VertexShader VSPostProcess = new VertexShader();
        public VertexShader VSWidgetUI1 = new VertexShader();
        public VertexShader VSWidgetUI2 = new VertexShader();
        public VertexShader VSWidgetUILight = new VertexShader();
        public VertexShader VSDeferredRenderPointLight = new VertexShader();
        public PixelShader PSMMD = new PixelShader();
        public PixelShader PSMMDTransparent = new PixelShader();
        public PixelShader PSMMD_DisneyBrdf = new PixelShader();
        public PixelShader PSMMD_Toon1 = new PixelShader();
        public PixelShader PSMMDLoading = new PixelShader();
        public PixelShader PSMMDError = new PixelShader();
        public PixelShader PSMMDAlphaClip = new PixelShader();
        public PixelShader PSMMDAlphaClip1 = new PixelShader();
        public PixelShader PSDeferredRenderGBuffer = new PixelShader();
        public PixelShader PSDeferredRenderIBL = new PixelShader();
        public PixelShader PSDeferredRenderDirectLight = new PixelShader();
        public PixelShader PSDeferredRenderPointLight = new PixelShader();
        public PixelShader PSSkyBox = new PixelShader();
        public PixelShader PSPostProcess = new PixelShader();
        public PixelShader PSWidgetUI1 = new PixelShader();
        public PixelShader PSWidgetUI2 = new PixelShader();
        public PixelShader PSWidgetUILight = new PixelShader();
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
            await ReloadVertexShader(VSMMDSkinning2, "ms-appx:///Coocoo3DGraphics/VSMMDSkinning2.cso");
            await ReloadVertexShader(VSMMDTransform, "ms-appx:///Coocoo3DGraphics/VSMMDTransform.cso");
            await ReloadVertexShader(VSSkyBox, "ms-appx:///Coocoo3DGraphics/VSSkyBox.cso");
            await ReloadPixelShader(PSMMD, "ms-appx:///Coocoo3DGraphics/PSMMD.cso");
            await ReloadPixelShader(PSMMDTransparent, "ms-appx:///Coocoo3DGraphics/PSMMDTransparent.cso");
            await ReloadPixelShader(PSMMD_DisneyBrdf, "ms-appx:///Coocoo3DGraphics/PSMMD_DisneyBRDF.cso");
            await ReloadPixelShader(PSMMD_Toon1, "ms-appx:///Coocoo3DGraphics/PSMMD_Toon1.cso");
            await ReloadPixelShader(PSMMDLoading, "ms-appx:///Coocoo3DGraphics/PSMMDLoading.cso");
            await ReloadPixelShader(PSMMDError, "ms-appx:///Coocoo3DGraphics/PSMMDError.cso");
            await ReloadPixelShader(PSMMDAlphaClip, "ms-appx:///Coocoo3DGraphics/PSMMDAlphaClip.cso");
            await ReloadPixelShader(PSMMDAlphaClip1, "ms-appx:///Coocoo3DGraphics/PSMMDAlphaClip1.cso");
            await ReloadPixelShader(PSSkyBox, "ms-appx:///Coocoo3DGraphics/PSSkyBox.cso");

            await ReloadVertexShader(VSDeferredRenderPointLight, "ms-appx:///Coocoo3DGraphics/VSDeferredRenderPointLight.cso");
            await ReloadPixelShader(PSDeferredRenderGBuffer, "ms-appx:///Coocoo3DGraphics/PSDeferredRenderGBuffer.cso");
            await ReloadPixelShader(PSDeferredRenderIBL, "ms-appx:///Coocoo3DGraphics/PSDeferredRenderIBL.cso");
            await ReloadPixelShader(PSDeferredRenderDirectLight, "ms-appx:///Coocoo3DGraphics/PSDeferredRenderDirectLight.cso");
            await ReloadPixelShader(PSDeferredRenderPointLight, "ms-appx:///Coocoo3DGraphics/PSDeferredRenderPointLight.cso");

            await ReloadVertexShader(VSPostProcess, "ms-appx:///Coocoo3DGraphics/VSPostProcess.cso");
            await ReloadPixelShader(PSPostProcess, "ms-appx:///Coocoo3DGraphics/PSPostProcess.cso");

            await ReloadVertexShader(VSWidgetUI1, "ms-appx:///Coocoo3DGraphics/VSWidgetUI1.cso");
            await ReloadVertexShader(VSWidgetUI2, "ms-appx:///Coocoo3DGraphics/VSWidgetUI2.cso");
            await ReloadVertexShader(VSWidgetUILight, "ms-appx:///Coocoo3DGraphics/VSWidgetUILight.cso");
            await ReloadPixelShader(PSWidgetUI1, "ms-appx:///Coocoo3DGraphics/PSWidgetUI1.cso");
            await ReloadPixelShader(PSWidgetUI2, "ms-appx:///Coocoo3DGraphics/PSWidgetUI2.cso");
            await ReloadPixelShader(PSWidgetUILight, "ms-appx:///Coocoo3DGraphics/PSWidgetUILight.cso");
        }
        public void ChangeRenderTargetFormat(DeviceResources deviceResources, ProcessingList uploadProcess, DxgiFormat outputFormat, DxgiFormat middleFormat, DxgiFormat swapChainFormat, DxgiFormat depthFormat)
        {
            Ready = false;
            this.outputFormat = outputFormat;
            this.middleFormat = middleFormat;
            this.depthFormat = depthFormat;

            PObjectMMDSkinning.ReloadSkinning(VSMMDSkinning2, null);
            uploadProcess.UL(PObjectMMDSkinning, 1);

            PObjectMMD.ReloadDrawing(BlendState.alpha, VSMMDTransform, null, PSMMD, outputFormat, depthFormat);
            PObjectMMDTransparent.ReloadDrawing(BlendState.alpha, VSMMDTransform, null, PSMMDTransparent, outputFormat, depthFormat);
            PObjectMMD_DisneyBrdf.ReloadDrawing(BlendState.alpha, VSMMDTransform, null, PSMMD_DisneyBrdf, outputFormat, depthFormat);
            PObjectMMD_Toon1.ReloadDrawing(BlendState.alpha, VSMMDTransform, null, PSMMD_Toon1, outputFormat, depthFormat);
            PObjectMMDLoading.ReloadDrawing(BlendState.alpha, VSMMDTransform, null, PSMMDLoading, outputFormat, depthFormat);
            PObjectMMDError.ReloadDrawing(BlendState.alpha, VSMMDTransform, null, PSMMDError, outputFormat, depthFormat);
            uploadProcess.UL(PObjectMMD, 0);
            uploadProcess.UL(PObjectMMDTransparent, 0);
            uploadProcess.UL(PObjectMMD_DisneyBrdf, 0);
            uploadProcess.UL(PObjectMMD_Toon1, 0);
            uploadProcess.UL(PObjectMMDLoading, 0);
            uploadProcess.UL(PObjectMMDError, 0);

            PObjectDeferredRenderGBuffer.ReloadDrawing(BlendState.none, VSMMDTransform, null, PSDeferredRenderGBuffer, middleFormat, depthFormat, 3);
            PObjectDeferredRenderIBL.ReloadDrawing(BlendState.add, VSSkyBox, null, PSDeferredRenderIBL, outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectDeferredRenderDirectLight.ReloadDrawing(BlendState.add, VSSkyBox, null, PSDeferredRenderDirectLight, outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectDeferredRenderPointLight.ReloadDrawing(BlendState.add, VSDeferredRenderPointLight, null, PSDeferredRenderPointLight, outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            uploadProcess.UL(PObjectDeferredRenderGBuffer, 0);
            uploadProcess.UL(PObjectDeferredRenderIBL, 0);
            uploadProcess.UL(PObjectDeferredRenderDirectLight, 0);
            uploadProcess.UL(PObjectDeferredRenderPointLight, 0);

            //PObjectMMDShadowDepth.ReloadDepthOnly(VSMMDTransform, PSMMDAlphaClip, 2500);
            PObjectMMDShadowDepth.ReloadDepthOnly(VSMMDTransform, null, 2500, depthFormat);
            PObjectMMDDepth.ReloadDepthOnly(VSMMDTransform, PSMMDAlphaClip1, 0, depthFormat);
            uploadProcess.UL(PObjectMMDShadowDepth, 0);
            uploadProcess.UL(PObjectMMDDepth, 0);


            PObjectSkyBox.Reload(deviceResources, rootSignature, eInputLayout.postProcess, BlendState.none, VSSkyBox, null, PSSkyBox, outputFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectPostProcess.Reload(deviceResources, rootSignaturePostProcess, eInputLayout.postProcess, BlendState.none, VSPostProcess, null, PSPostProcess, swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectWidgetUI1.Reload(deviceResources, rootSignaturePostProcess, eInputLayout.postProcess, BlendState.alpha, VSWidgetUI1, null, PSWidgetUI1, swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectWidgetUI2.Reload(deviceResources, rootSignaturePostProcess, eInputLayout.postProcess, BlendState.alpha, VSWidgetUI2, null, PSWidgetUI2, swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN);
            PObjectWidgetUILight.Reload(deviceResources, rootSignaturePostProcess, eInputLayout.postProcess, BlendState.alpha, VSWidgetUILight, null, PSWidgetUILight, swapChainFormat, DxgiFormat.DXGI_FORMAT_UNKNOWN, D3D12PrimitiveTopologyType.LINE);
            Ready = true;
        }
        protected async Task ReloadPixelShader(PixelShader pixelShader, string uri)
        {
            pixelShader.Reload(await ReadFile(uri));
        }
        protected async Task ReloadVertexShader(VertexShader vertexShader, string uri)
        {
            vertexShader.Reload(await ReadFile(uri));
        }
        protected async Task ReloadGeometryShader(GeometryShader geometryShader, string uri)
        {
            geometryShader.Reload(await ReadFile(uri));
        }
        protected async Task ReloadComputeShader(ComputePO computeShader, string uri)
        {
            computeShader.Reload(await ReadFile(uri));
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
