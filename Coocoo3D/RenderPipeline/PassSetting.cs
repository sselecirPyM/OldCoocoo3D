using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Coocoo3DGraphics;

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

        [XmlIgnore]
        public List<CombinedPass> CombinedPasses;

        public bool Verify()
        {
            if (RenderTargets == null || RenderTargets.Count == 0)
                return false;
            if (RenderSequence == null || RenderSequence.Count == 0)
                return false;
            if (Passes == null || Passes.Count == 0)
                return false;
            CombinedPasses = new List<CombinedPass>();
            for (int i = 0; i < RenderSequence.Count; i++)
            {
                var combined = new CombinedPass();
                combined.PassMatch1 = RenderSequence[i];
                foreach (var pass in Passes)
                {
                    if (RenderSequence[i].Name == pass.Name)
                        combined.Pass = pass;
                }
                if (combined.Pass == null)
                    return false;
                CombinedPasses.Add(combined);
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
        public string RenderTarget;
        public string DepthStencil;
    }
    public class Pass
    {
        public string Name;
        public string Camera;
        public string VertexShader;
        public string GeometryShader;
        public string PixelShader;
        [XmlElement(ElementName = "SRV")]
        public List<ShaderSlotRes> SRVs;
    }
    public class CombinedPass
    {
        public PassMatch1 PassMatch1;
        public Pass Pass;
        public RenderTexture2D renderTarget;
        public RenderTexture2D depthSencil;
        public PObject PSODefault;
        public bool DrawObjects;
    }
    public struct ShaderSlotRes
    {
        public int Index;
        public string ResourceType;
        public string Resource;
    }
    public struct RenderTarget
    {
        public string Name;
        public VarSize Size;
        public DxgiFormat Format;
    }
    public struct VarSize
    {
        public string Source;
        public int x;
        public int y;
        public int z;
    }
}
