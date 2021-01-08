using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public struct InShaderSettings
    {
        //public Vector4 backgroundColor;
        public float SkyBoxLightMultiple;
        public uint _EnableAO;
        public uint _EnableShadow;
        public uint Quality;

        public bool EnableAO { get => _EnableAO != 0; set => _EnableAO = Convert.ToUInt32(value); }
        public bool EnableShadow { get => _EnableShadow != 0; set => _EnableShadow = Convert.ToUInt32(value); }
    }
}
