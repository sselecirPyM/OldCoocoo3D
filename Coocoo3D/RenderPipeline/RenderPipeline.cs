﻿using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public abstract class RenderPipeline
    {
        public abstract void PrepareRenderData(RenderPipelineContext context, VisualChannel visualChannel);

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

        protected PSO PSOSelect(GraphicsDevice graphicsDevice, RootSignature graphicsSignature, in PSODesc desc, PSO pso, PSO loading, PSO unload, PSO error)
        {
            if (pso == null) return unload;
            if (pso.Status == GraphicsObjectStatus.unload)
                return unload;
            else if (pso.Status == GraphicsObjectStatus.loaded)
            {
                if (pso.GetVariantIndex(graphicsDevice, graphicsSignature, desc) != -1)
                    return pso;
                else
                    return error;
            }
            else if (pso.Status == GraphicsObjectStatus.loading)
                return loading;
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
