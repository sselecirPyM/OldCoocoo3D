using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float scale;
        public Vector3 color;
        public float life;
        public float rotation;
    }
}
