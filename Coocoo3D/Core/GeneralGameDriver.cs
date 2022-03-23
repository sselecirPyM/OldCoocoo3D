using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class GeneralGameDriver : GameDriver
    {
        public override bool Next(RenderPipelineContext rpContext, long now)
        {
            rpContext.recording = false;
            ref GameDriverContext context = ref rpContext.gameDriverContext;
            if (!(context.NeedRender > 0 || context.Playing))
            {
                return false;
            }
            if (now - context.LatestRenderTime < context.FrameInterval * 1e7f)
            {
                context.NeedRender -= 1;
                return false;
            }
            context.NeedRender -= 1;
            foreach (var visualChannel in rpContext.visualChannels.Values)
            {
                visualChannel.outputSize = visualChannel.sceneViewSize;
                visualChannel.camera.AspectRatio = (float)visualChannel.outputSize.X / (float)visualChannel.outputSize.Y;
            }

            context.DeltaTime = Math.Clamp((now - context.LatestRenderTime) / 1e7f * context.PlaySpeed, -0.17f, 0.17f);
            context.LatestRenderTime = now;
            if (context.Playing)
                context.PlayTime += context.DeltaTime;
            return true;
        }
    }
}
