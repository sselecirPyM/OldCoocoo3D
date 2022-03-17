﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Present
{
    public class RenderMaterial
    {
        public string Name;

        public int indexOffset;
        public int indexCount;
        public int vertexStart;
        public int vertexCount;
        public bool DrawDoubleFace;
        public bool Transparent;
        public Dictionary<string, string> textures = new Dictionary<string, string>();
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();

        public Vortice.Mathematics.BoundingBox boundingBox;

        public RenderMaterial GetClone()
        {
            var mat = (RenderMaterial)MemberwiseClone();
            mat.textures = new Dictionary<string, string>(textures);
            mat.Parameters = new Dictionary<string, object>(Parameters);
            return mat;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
