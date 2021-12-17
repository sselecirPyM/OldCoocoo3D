using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class DefaultPassDispatcher : IPassDispatcher
    {
        public void FrameBegin(RenderPipelineContext context)
        {

        }

        public void FrameEnd(RenderPipelineContext context)
        {

        }
        public void Dispatch(UnionShaderParam unionShaderParam)
        {
            var passSetting = unionShaderParam.passSetting;
            foreach (var renderSequence in passSetting.RenderSequence)
            {
                unionShaderParam.renderSequence = renderSequence;
                HybirdRenderPipeline.DispatchPass(unionShaderParam);
            }
        }
    }
}
