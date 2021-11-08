﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3DGraphics;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class PassSetting
    {
        public string Name;
        public List<RenderTarget> RenderTargets;
        public List<PassMatch1> RenderSequence;
        public List<Pass> Passes;
        public List<PSPS> PipelineStates;
        public List<_AssetDefine> VertexShaders;
        public List<_AssetDefine> GeometryShaders;
        public List<_AssetDefine> PixelShaders;
        public List<_AssetDefine> ComputeShaders;
        public List<_AssetDefine> Texture2Ds;
        public List<_AssetDefine2> TextureCubes;

        [NonSerialized]
        public Dictionary<string, string> aliases = new Dictionary<string, string>();

        [NonSerialized]
        public bool configured;
        [NonSerialized]
        public HashSet<string> renderTargets;

        public bool Verify()
        {
            if (RenderTargets == null || RenderTargets.Count == 0)
                return false;
            if (RenderSequence == null || RenderSequence.Count == 0)
                return false;
            if (Passes == null || Passes.Count == 0)
                return false;
            if (PipelineStates == null)
                PipelineStates = new List<PSPS>();
            foreach (var passMatch in RenderSequence)
            {
                if (passMatch.Name != null)
                {
                    if (passMatch.Type == null)
                        passMatch.DrawObjects = true;
                    foreach (var pass in Passes)
                    {
                        if (passMatch.Name == pass.Name)
                            passMatch.Pass = pass;
                    }
                    if (passMatch.Pass == null)
                        return false;
                }
                else
                    return false;
            }

            return true;
        }
    }
    public class PassMatch1
    {
        public string Name;
        public int DepthBias;
        public float SlopeScaledDepthBias;
        public string Type;
        public List<string> RenderTargets;

        public List<PassParameter> passParameters;
        public string DepthStencil;
        public BlendState BlendMode;
        public bool ClearDepth;
        public bool ClearRenderTarget;
        public CullMode CullMode;
        public string Filter;

        [NonSerialized]
        public PSO PSODefault;
        [NonSerialized]
        public bool DrawObjects;
        [NonSerialized]
        public Dictionary<string, float> passParameters1;
        [NonSerialized]
        public Pass Pass;
        [NonSerialized]
        public string rootSignatureKey;
    }
    public class Pass
    {
        public string Name;
        public string Camera;
        public string VertexShader;
        public string GeometryShader;
        public string PixelShader;
        public string ComputeShader;
        public List<SRVUAVSlotRes> SRVs;
        public List<CBVSlotRes> CBVs;
        public List<SRVUAVSlotRes> UAVs;
    }
    public class PSPS
    {
        public string Name;
        public string VertexShader;
        public string GeometryShader;
        public string PixelShader;
    }

    public struct SRVUAVSlotRes
    {
        public int Index;
        public string ResourceType;
        public string Resource;
    }
    public struct CBVSlotRes
    {
        public int Index;
        public List<string> Datas;
    }
    public struct RenderTarget
    {
        public string Name;
        public VarSize Size;
        public Format Format;
    }
    public struct PParameter
    {
        public string Name;
        public float Value;
    }
    public class PTopAccelerateStructure
    {
        public string Name;
        public string Filter;
        public List<SRVUAVSlotRes> SRVs;
        public List<CBVSlotRes> CBVs;
    }
    public class VarSize
    {
        public string Source;

        [DefaultValue(1.0f)]
        public float Multiplier = 1.0f;
        [DefaultValue(1)]
        public int x = 1;
        [DefaultValue(1)]
        public int y = 1;
        [DefaultValue(1)]
        public int z = 1;
    }
    public struct _AssetDefine
    {
        public string Name;
        public string Path;
        public string EntryPoint;
    }
    public class _AssetDefine2
    {
        public string Name;
        public string[] Path;
    }
    public struct PassParameter
    {
        public string Name;
        public float Value;
    }
}
