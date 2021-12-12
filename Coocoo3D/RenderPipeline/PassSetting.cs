using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3DGraphics;
using Vortice.Direct3D12;
using Vortice.DXGI;
using System.Numerics;

namespace Coocoo3D.RenderPipeline
{
    public class PassSetting
    {
        public string Name;
        public List<PassMatch1> RenderSequence;
        public Dictionary<string, RenderTarget> RenderTargets;
        public Dictionary<string, Pass> Passes;
        public List<_AssetDefine> VertexShaders;
        public List<_AssetDefine> GeometryShaders;
        public List<_AssetDefine> PixelShaders;
        public List<_AssetDefine> ComputeShaders;
        public List<_AssetDefine> Texture2Ds;
        public Dictionary<string, string> UnionShaders;
        public Dictionary<string, string> ShowTextures;
        public Dictionary<string, PassParameter> ShowParameters;
        public Dictionary<string, string> ShowSettingTextures;
        public Dictionary<string, PassParameter> ShowSettingParameters;

        [NonSerialized]
        public string path;

        [NonSerialized]
        public Dictionary<string, string> aliases = new Dictionary<string, string>();

        public string GetAliases(string input)
        {
            if (input == null)
                return null;
            if (aliases.TryGetValue(input, out string s))
                return s;
            return input;
        }

        [NonSerialized]
        public bool configured;

        public bool Verify()
        {
            if (RenderTargets == null || RenderTargets.Count == 0)
                return false;
            if (RenderSequence == null || RenderSequence.Count == 0)
                return false;
            if (Passes == null || Passes.Count == 0)
                return false;
            foreach (var pass in Passes)
            {
                pass.Value.Name = pass.Key;
            }
            foreach (var renderTarget in RenderTargets)
            {
                renderTarget.Value.Name = renderTarget.Key;
            }
            if (ShowParameters != null)
                foreach (var parameter in ShowParameters)
                {
                    parameter.Value.Name ??= parameter.Key;
                    parameter.Value.GenerateRuntimeValue();
                }
            if (ShowSettingParameters != null)
                foreach (var parameter in ShowSettingParameters)
                {
                    parameter.Value.Name ??= parameter.Key;
                    parameter.Value.GenerateRuntimeValue();
                }
            foreach (var passMatch in RenderSequence)
            {
                if (passMatch.Name != null)
                {
                    if (passMatch.Type == null)
                        passMatch.DrawObjects = true;
                    foreach (var pass in Passes)
                    {
                        if (passMatch.Name == pass.Key)
                            passMatch.Pass = pass.Value;
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

        public string DepthStencil;
        public bool ClearDepth;
        public bool ClearRenderTarget;
        public CullMode CullMode;
        public string Filter;

        [NonSerialized]
        public PSO PSODefault;
        [NonSerialized]
        public bool DrawObjects;
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
        public string UnionShader;
        public BlendState BlendMode;
        public List<CBVSlotRes> CBVs;
        public List<SRVUAVSlotRes> SRVs;
        public List<SRVUAVSlotRes> UAVs;
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
    public class RenderTarget
    {
        public string Name;
        public VarSize Size;
        public Format Format;
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
    public class PassParameter
    {
        public string Name;
        public string Type;
        public string Default;
        public string Min;
        public string Max;
        public string Step;
        public string Format;
        public bool IsHidden;
        [NonSerialized]
        public object defaultValue;
        [NonSerialized]
        public object minValue;
        [NonSerialized]
        public object maxValue;
        [NonSerialized]
        public object step;

        public void GenerateRuntimeValue()
        {
            if (Type == "float" || Type == "sliderFloat")
            {
                float f1;
                if (float.TryParse(Default, out f1))
                    defaultValue = f1;
                else
                    defaultValue = default(float);

                if (float.TryParse(Min, out f1))
                    minValue = f1;
                else
                    minValue = float.MinValue;

                if (float.TryParse(Max, out f1))
                    maxValue = f1;
                else
                    maxValue = float.MaxValue;

                if (float.TryParse(Step, out f1))
                    step = f1;
                else
                    step = 1.0f;

                Format ??= "%.3f";
            }
            else if (Type == "float2")
            {
                defaultValue = Utility.StringConvert.GetFloat2(Default);
            }
            else if (Type == "float3" || Type == "color3")
            {
                defaultValue = Utility.StringConvert.GetFloat3(Default);
            }
            else if (Type == "float4" || Type == "color4")
            {
                defaultValue = Utility.StringConvert.GetFloat4(Default);
            }
            else if (Type == "int" || Type == "sliderInt")
            {
                int f1;
                if (int.TryParse(Default, out f1))
                    defaultValue = f1;
                else
                    defaultValue = default(int);

                if (int.TryParse(Min, out f1))
                    minValue = f1;
                else
                    minValue = int.MinValue;

                if (int.TryParse(Max, out f1))
                    maxValue = f1;
                else
                    maxValue = int.MaxValue;

                if (int.TryParse(Step, out f1))
                    step = f1;
                else
                    step = 1;
            }
            else if (Type == "bool")
            {
                if (bool.TryParse(Default, out bool f1))
                    defaultValue = f1;
                else defaultValue = default(bool);
            }
        }
    }
}
