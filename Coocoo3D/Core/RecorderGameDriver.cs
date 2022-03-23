using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Coocoo3D.ResourceWarp;

namespace Coocoo3D.Core
{
    public class RecorderGameDriver : GameDriver
    {
        const int c_frameCount = 3;
        public override bool Next(RenderPipelineContext rpContext, long now)
        {
            ref GameDriverContext context = ref rpContext.gameDriverContext;

            context.NeedRender = 1;
            context.LatestRenderTime = now;

            ref RecordSettings recordSettings = ref context.recordSettings;
            if (switchEffect)
            {
                switchEffect = false;
                context.Playing = true;
                context.PlaySpeed = 2.0f;
                context.PlayTime = 0.0f;
                var visualchannel = rpContext.visualChannels["main"];
                visualchannel.outputSize = new Numerics.Int2(recordSettings.Width, recordSettings.Height);
                visualchannel.camera.AspectRatio = (float)recordSettings.Width / (float)recordSettings.Height;
                context.RequireResetPhysics = true;
                StartTime = recordSettings.StartTime;
                StopTime = recordSettings.StopTime;
                RenderCount = 0;
                RecordCount = 0;
                FrameIntervalF = 1 / MathF.Max(context.recordSettings.FPS, 1e-3f);
            }
            else
            {
            }

            context.DeltaTime = FrameIntervalF;
            context.PlayTime = FrameIntervalF * RenderCount;
            RenderCount++;

            return true;
        }

        public ReadBackTexture2D ReadBackTexture2D = new ReadBackTexture2D();

        public override void AfterRender(RenderPipelineContext rpContext, GraphicsContext graphicsContext)
        {
            rpContext.recording = false;
            ref GameDriverContext context = ref rpContext.gameDriverContext;
            if (context.PlayTime >= StartTime && (RenderCount - c_frameCount) * FrameIntervalF <= StopTime)
            {
                rpContext.recording = true;
                int index1 = RecordCount % c_frameCount;
                var visualchannel = rpContext.visualChannels["main"];

                int width = visualchannel.outputSize.X;
                int height = visualchannel.outputSize.Y;

                if (ReadBackTexture2D.GetWidth() != width || ReadBackTexture2D.GetHeight() != height)
                {
                    ReadBackTexture2D.Reload(width, height, 4);
                    graphicsContext.UpdateReadBackTexture(ReadBackTexture2D);
                }

                graphicsContext.CopyTexture(ReadBackTexture2D, visualchannel.OutputRTV, index1);

                if (RecordCount >= c_frameCount)
                {
                    int renderIndex = RecordCount - c_frameCount;
                    var data = ReadBackTexture2D.StartRead<byte>(index1);
                    TextureHelper.SaveToFile(data, width, height, Path.GetFullPath(string.Format("{0}.png", renderIndex), saveFolder.FullName));
                    ReadBackTexture2D.StopRead(index1);
                }
                RecordCount++;
            }
        }
        public float StartTime;
        public float StopTime;

        public float FrameIntervalF = 1 / 60.0f;
        public int RecordCount = 0;
        public int RenderCount = 0;
        bool switchEffect;
        public DirectoryInfo saveFolder;
        public void SwitchEffect()
        {
            switchEffect = true;
        }
    }
}
