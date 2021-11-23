﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public delegate bool UnionShader(UnionShaderParam param);
    public class UnionShaderParam
    {
        public RenderPipelineContext rp;
        public RuntimeMaterial material;
        public MMDRendererComponent renderer;
        public PassSetting passSetting;
        public GraphicsContext graphicsContext;
        public VisualChannel visualChannel;
        public RootSignature rootSignature;
        public PSODesc PSODesc;
        public PSO PSO;
        public string passName;
    }
}
