using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public abstract class RenderPipeline
    {
        public abstract void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel);

        public virtual void EndFrame() { }

        public virtual void BeginFrame() { }

        protected Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
        {
            if (texture == null) return error;
            if (texture.Status == GraphicsObjectStatus.loaded)
                return texture;
            else if (texture.Status == GraphicsObjectStatus.loading)
                return loading;
            else if (texture.Status == GraphicsObjectStatus.unload)
                return unload;
            else
                return error;
        }

        protected void SetPipelineStateVariant(GraphicsDevice graphicsDevice, GraphicsContext graphicsContext, RootSignature graphicsSignature, in PSODesc desc, PSO pso)
        {
            int variant = pso.GetVariantIndex(graphicsDevice, graphicsSignature, desc);
            graphicsContext.SetPSO(pso, variant);
        }
    }
}
