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
    public class ShaderWarp1
    {
        public PObject pipelineState;
        public VertexShader vs;
        public GeometryShader gs;
        public PixelShader ps;
    }
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
        //public ComputePO CSParticle = new ComputePO();

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
            //CSParticle.Status = status;
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
            //ComputePO cs1 = CSParticle;


            bool haveVS = vs0.CompileInitialize1(datas, "VS", macroEntryVS);
            bool haveGS = gs0.CompileInitialize1(datas, "GS", macroEntryGS);
            bool haveVS1 = vs1.CompileInitialize1(datas, "VS1", macroEntryVS);
            bool haveGS1 = gs1.CompileInitialize1(datas, "GS1", macroEntryGS);
            bool havePS1 = ps1.CompileInitialize1(datas, "PS1", macroEntryPS);

            bool haveVSParticle = vs2.CompileInitialize1(datas, "VSParticle", macroEntryVS);
            bool haveGSParticle = gs2.CompileInitialize1(datas, "GSParticle", macroEntryGS);
            bool havePSParticle = ps2.CompileInitialize1(datas, "PSParticle", macroEntryPS);


            //bool haveCS1 = cs1.CompileInitialize1(datas, "CSParticle", macroEntryCS1);
            if (haveVS || haveGS)
            {
                processingList.UL(new ShaderWarp1() { pipelineState = POSkinning, vs = haveVS ? vs0 : RPAssetsManager.VSAssets["VSMMDSkinning2.cso"], gs = haveGS ? gs0 : null, ps = null });
            }
            else
                POSkinning.Status = GraphicsObjectStatus.unload;
            if (haveVS1 || haveGS1 || havePS1)
            {
                processingList.UL(new ShaderWarp1() { pipelineState = PODraw, vs = haveVS1 ? vs1 : RPAssetsManager.VSMMDTransform, gs = haveGS1 ? gs1 : null, ps = havePS1 ? ps1 : RPAssetsManager.PSMMD });
            }
            else
                PODraw.Status = GraphicsObjectStatus.unload;
            if (haveVSParticle || haveGSParticle || havePSParticle)
            {
                processingList.UL(new ShaderWarp1() { pipelineState = POParticleDraw, vs = haveVSParticle ? vs2 : RPAssetsManager.VSMMDTransform, gs = haveGSParticle ? gs2 : null, ps = havePSParticle ? ps2 : RPAssetsManager.PSMMD });
            }
            else
                POParticleDraw.Status = GraphicsObjectStatus.unload;
            //if (haveCS1)
            //    processingList.UL(CSParticle, 0);
            //else
            //    CSParticle.Status = GraphicsObjectStatus.unload;

            Status = GraphicsObjectStatus.loaded;
            return true;
        }
        static MacroEntry[] macroEntryVS = new MacroEntry[] { new MacroEntry("COO_SURFACE","1") };
        static MacroEntry[] macroEntryGS = new MacroEntry[] { new MacroEntry("COO_SURFACE","1") };
        static MacroEntry[] macroEntryPS = new MacroEntry[] { new MacroEntry("COO_SURFACE","1") };
        static MacroEntry[] macroEntryCS1 = new MacroEntry[] { new MacroEntry("COO_PARTICLE", "1") };
    }

}
