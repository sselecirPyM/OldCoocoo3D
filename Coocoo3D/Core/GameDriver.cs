using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;

namespace Coocoo3D.Core
{
    public abstract class GameDriver
    {
        public abstract bool Next(RenderPipelineContext context);

        public virtual void AfterRender(RenderPipelineContext  context)
        {

        }
    }
}
