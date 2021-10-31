using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Coocoo3DGraphics;
//using Vortice.Direct3D12;
//using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class PassSetting
    {
        [XmlArrayItem("RenderTarget")]
        public List<RenderTarget> RenderTargets;
        [XmlArrayItem("Pass")]
        public List<PassMatch1> RenderSequence;
        [XmlArrayItem("Pass")]
        public List<Pass> Passes;
        [XmlArrayItem("PipelineState")]
        public List<PSPS> PipelineStates;
        [XmlArrayItem("VertexShader")]
        public List<_AssetDefine> VertexShaders;
        [XmlArrayItem("GeometryShader")]
        public List<_AssetDefine> GeometryShaders;
        [XmlArrayItem("PixelShader")]
        public List<_AssetDefine> PixelShaders;
        [XmlArrayItem("ComputeShader")]
        public List<_AssetDefine> ComputeShaders;
        [XmlArrayItem("Texture2D")]
        public List<_AssetDefine> Texture2Ds;
        [XmlArrayItem("TextureCube")]
        public List<_AssetDefine2> TextureCubes;

        //[XmlArrayItem("RayTracingStateObject")]
        public PSRTSO RayTracingStateObject;

        [XmlIgnore]
        public bool configured;
        [XmlIgnore]
        public HashSet<string> renderTargets;
        //[XmlIgnore]
        //public RayTracingStateObject RTSO;

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
                //else if (passMatch.Foreach != null)
                //{
                //}
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
        [XmlElement("RenderTarget")]
        public List<string> RenderTargets;

        [XmlElement("Parameter")]
        public List<PassParameter> passParameters;
        public string DepthStencil;
        public BlendState BlendMode;
        public bool ClearDepth;
        public bool ClearRenderTarget;
        public CullMode CullMode;
        public string Filter;

        //public string Foreach;
        //[XmlElement("Pass")]
        //public List<PassMatch1> passes;

        [XmlIgnore]
        public string[] RayGenShaders;
        [XmlIgnore]
        public string[] MissShaders;
        [XmlIgnore]
        public PSO PSODefault;
        [XmlIgnore]
        public bool DrawObjects;
        [XmlIgnore]
        public Dictionary<string, float> passParameters1;
        [XmlIgnore]
        public Pass Pass;
        [XmlIgnore]
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
        [XmlElement(ElementName = "SRV")]
        public List<SRVUAVSlotRes> SRVs;
        [XmlElement(ElementName = "CBV")]
        public List<CBVSlotRes> CBVs;
        [XmlElement(ElementName = "UAV")]
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
        [XmlArrayItem(ElementName = "Data")]
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
        [XmlElement(ElementName = "SRV")]
        public List<SRVUAVSlotRes> SRVs;
        [XmlElement(ElementName = "CBV")]
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
    public class PSRTSO
    {
        public string Name;
        public string Path;
        public int MaxPayloadSize;
        public int MaxAttributeSize;
        public int MaxRecursionDepth;
        [XmlElement(ElementName = "RayGenShader")]
        public RTShader[] rayGenShaders;
        [XmlElement(ElementName = "MissShader")]
        public List<RTShader> missShaders;
        [XmlElement("HitGroupSubobject")]
        public List<RTHitGroup> hitGroups;

    }
    public class RTShader
    {
        public string Name;
    }
    public class RTHitGroup
    {
        public string Name;
        public string AnyHitShader;
        public string ClosestHitShader;
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
        [XmlElement(ElementName = "Path")]
        public string[] Path;
    }
    public struct PassParameter
    {
        public string Name;
        public float Value;
    }
}
