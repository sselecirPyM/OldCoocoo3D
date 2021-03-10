using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;

namespace Coocoo3D.Controls
{
    public static class C2Control
    {
        public static Dictionary<Type, Type> componentToControl = new Dictionary<Type, Type>()
        {
            {typeof(LightingComponent),typeof(CLightingComponent) }
        };
    }
}
