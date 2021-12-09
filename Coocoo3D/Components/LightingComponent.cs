using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class LightingComponent : Component
    {
        public LightingType LightingType;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Color;
        public float Range;

        public LightingData GetLightingData()
        {
            return new LightingData
            {
                LightingType = LightingType,
                Position = Position,
                Rotation = Rotation,
                Color = Color,
                Range = Range,
            };
        }
    }
}
