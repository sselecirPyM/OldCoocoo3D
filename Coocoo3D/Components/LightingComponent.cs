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

        public DirectionalLightData GetDirectionalLightData()
        {
            return new DirectionalLightData
            {
                Rotation = Rotation,
                Direction = Vector3.Transform(-Vector3.UnitZ, Rotation),
                Color = Color,
            };
        }

        public PointLightData GetPointLightData()
        {
            return new PointLightData
            {
                Position = Position,
                Color = Color,
                Range = Math.Max(Range, 1e-4f),
            };
        }
    }
}
