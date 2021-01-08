using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Coocoo3D.ResourceWarp
{
    public class RPShaderPack
    {
        public VertexShader VS = new VertexShader();
        public GeometryShader GS = new GeometryShader();
        public VertexShader VS1 = new VertexShader();
        public GeometryShader GS1 = new GeometryShader();
        public PixelShader PS1 = new PixelShader();

        public VertexShader VSParticle = new VertexShader();
        public GeometryShader GSParticle = new GeometryShader();
        public PixelShader PSParticle = new PixelShader();

        public PObject POSkinning = new PObject();
        public PObject PODraw = new PObject();
        public PObject POParticleDraw = new PObject();
        public ComputePO CSParticle = new ComputePO();

        public DateTimeOffset lastModifiedTime;
        public StorageFolder folder;
        public string relativePath;
        public SingleLocker loadLocker;

        public GraphicsObjectStatus Status;

        public void Mark(GraphicsObjectStatus status)
        {
            Status = status;
            POSkinning.Status = status;
            PODraw.Status = status;
            POParticleDraw.Status = status;
            CSParticle.Status = status;
        }

        public async Task<bool> Reload1(StorageFolder folder, string relativePath, RPAssetsManager RPAssetsManager, ProcessingList processingList)
        {
            this.relativePath = relativePath;
            this.folder = folder;
            Mark(GraphicsObjectStatus.loading);
            IStorageItem storageItem = await folder.TryGetItemAsync(relativePath);
            try
            {
                var attr = await storageItem.GetBasicPropertiesAsync();
                lastModifiedTime = attr.DateModified;
            }
            catch
            {
                Mark(GraphicsObjectStatus.error);
                return false;
            }
            return await Reload(storageItem, RPAssetsManager, processingList);
        }

        public async Task<bool> Reload(IStorageItem storageItem, RPAssetsManager RPAssetsManager, ProcessingList processingList)
        {
            Mark(GraphicsObjectStatus.loading);

            if (!(storageItem is StorageFile file))
            {
                Mark(GraphicsObjectStatus.error);
                return false;
            }
            Windows.Storage.Streams.IBuffer datas;
            try
            {
                datas = await FileIO.ReadBufferAsync(file);
            }
            catch
            {
                Mark(GraphicsObjectStatus.error);
                return false;
            }
            VertexShader vs0 = VS;
            GeometryShader gs0 = GS;
            VertexShader vs1 = VS1;
            GeometryShader gs1 = GS1;
            PixelShader ps1 = PS1;

            VertexShader vs2 = VSParticle;
            GeometryShader gs2 = GSParticle;
            PixelShader ps2 = PSParticle;
            ComputePO cs1 = CSParticle;


            bool haveVS = vs0.CompileReload1(datas, "VS", ShaderMacro.DEFINE_COO_SURFACE);
            bool haveGS = gs0.CompileReload1(datas, "GS", ShaderMacro.DEFINE_COO_SURFACE);
            bool haveVS1 = vs1.CompileReload1(datas, "VS1", ShaderMacro.DEFINE_COO_SURFACE);
            bool haveGS1 = gs1.CompileReload1(datas, "GS1", ShaderMacro.DEFINE_COO_SURFACE);
            bool havePS1 = ps1.CompileReload1(datas, "PS1", ShaderMacro.DEFINE_COO_SURFACE);

            bool haveVSParticle = vs2.CompileReload1(datas, "VSParticle", ShaderMacro.DEFINE_COO_SURFACE);
            bool haveGSParticle = gs2.CompileReload1(datas, "GSParticle", ShaderMacro.DEFINE_COO_SURFACE);
            bool havePSParticle = ps2.CompileReload1(datas, "PSParticle", ShaderMacro.DEFINE_COO_SURFACE);


            bool haveCS1 = cs1.CompileReload1(datas, "CSParticle", ShaderMacro.DEFINE_COO_PARTICLE);
            if (haveVS || haveGS)
            {
                POSkinning.ReloadSkinning(
                    haveVS ? vs0 : RPAssetsManager.VSMMDSkinning2,
                    haveGS ? gs0 : null);
                processingList.UL(POSkinning, 1);
            }
            else
                POSkinning.Status = GraphicsObjectStatus.unload;
            if (haveVS1 || haveGS1 || havePS1)
            {
                PODraw.ReloadDrawing(BlendState.alpha,
                    haveVS1 ? vs1 : RPAssetsManager.VSMMDTransform,
                    haveGS1 ? gs1 : null,
                    havePS1 ? ps1 : RPAssetsManager.PSMMD, RPAssetsManager.outputFormat, RPAssetsManager.depthFormat);
                processingList.UL(PODraw, 0);
            }
            else
                PODraw.Status = GraphicsObjectStatus.unload;
            if (haveVSParticle || haveGSParticle || havePSParticle)
            {
                POParticleDraw.ReloadDrawing(BlendState.alpha,
                    haveVSParticle ? vs2 : RPAssetsManager.VSMMDTransform,
                    haveGSParticle ? gs2 : null,
                    havePSParticle ? ps2 : RPAssetsManager.PSMMD, RPAssetsManager.outputFormat,RPAssetsManager.depthFormat);
                processingList.UL(POParticleDraw, 0);
            }
            else
                POParticleDraw.Status = GraphicsObjectStatus.unload;
            if (haveCS1)
                processingList.UL(CSParticle, 0);
            else
                CSParticle.Status = GraphicsObjectStatus.unload;

            Status = GraphicsObjectStatus.loaded;
            return true;
        }
    }

}
