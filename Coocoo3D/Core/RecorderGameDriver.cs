using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Coocoo3D.Core
{
    public class RecorderGameDriver : GameDriver
    {
        const int c_frameCount = 3;
        public override bool Next(RenderPipelineContext rpContext)
        {
            ref GameDriverContext context = ref rpContext.gameDriverContext;

            context.NeedRender = 1;
            DateTime now = DateTime.Now;
            context.LatestRenderTime = now;

            ref RecordSettings recordSettings = ref context.recordSettings;
            if (switchEffect)
            {
                switchEffect = false;
                context.Playing = true;
                context.PlaySpeed = 2.0f;
                context.PlayTime = 0.0f;
                float logicSizeScale = rpContext.deviceResources.GetDpi() / 96.0f;
                var visualchannel = rpContext.visualChannels["main"];
                visualchannel.outputSize = new Numerics.Int2(recordSettings.Width, recordSettings.Height);
                visualchannel.camera.AspectRatio = (float)recordSettings.Width / (float)recordSettings.Height;
                //context.RequireResize = true;
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

            if (context.PlayTime >= StartTime || context.RequireResizeOuter)
                context.EnableDisplay = true;
            else
                context.EnableDisplay = false;

            return true;
        }
        class Pack1
        {
            public Task runningTask;
            public int renderIndex;
            public StorageFolder saveFolder;
            public byte[] imageData;
            public int width;
            public int height;
            public async Task task1()
            {
                Image<Rgba32> image = GetImage();

                StorageFile file = await saveFolder.CreateFileAsync(string.Format("{0}.png", renderIndex), CreationCollisionOption.ReplaceExisting);
                var stream = await file.OpenStreamForWriteAsync();
                image.SaveAsPng(stream);

                //await stream.FlushAsync();
                stream.Close();
            }
            Image<Rgba32> GetImage()
            {
                Image<Rgba32> image = new Image<Rgba32>(width, height);
                image.Frames[0].TryGetSinglePixelSpan(out var span1);
                imageData.CopyTo(MemoryMarshal.Cast<Rgba32, byte>(span1));
                return image;
            }
        }
        const int encFrameCount = 8;
        int exIndex = 0;
        Pack1[] packs = new Pack1[encFrameCount];
        public override void AfterRender(RenderPipelineContext rpContext, GraphicsContext graphicsContext)
        {
            ref GameDriverContext context = ref rpContext.gameDriverContext;

            if (context.PlayTime >= StartTime && (RenderCount - c_frameCount) * FrameIntervalF <= StopTime)
            {
                int index1 = RecordCount % c_frameCount;
                var visualchannel = rpContext.visualChannels["main"];
                if (rpContext.ReadBackTexture2D.GetWidth() != visualchannel.outputSize.X || rpContext.ReadBackTexture2D.GetHeight() != visualchannel.outputSize.Y)
                {
                    rpContext.ReadBackTexture2D.Reload(visualchannel.outputSize.X, visualchannel.outputSize.Y, 4);
                    graphicsContext.UpdateReadBackTexture(rpContext.ReadBackTexture2D);
                }

                graphicsContext.CopyTexture(rpContext.ReadBackTexture2D, visualchannel.FinalOutput, index1);
                if (RecordCount >= c_frameCount)
                {
                    rpContext.ReadBackTexture2D.GetDataTolocal(index1);
                    if (packs[exIndex] == null)
                    {
                        int width = rpContext.ReadBackTexture2D.GetWidth();
                        int height = rpContext.ReadBackTexture2D.GetHeight();
                        packs[exIndex] = new Pack1()
                        {
                            saveFolder = saveFolder,
                            width = width,
                            height = height,
                            imageData = new byte[width * height * 4],
                        };
                    }
                    else if (!packs[exIndex].runningTask.IsCompleted)
                    {
                        packs[exIndex].runningTask.Wait();
                    }

                    //packs[exIndex].imageData = rpContext.ReadBackTexture2D.GetRaw(index1);
                    rpContext.ReadBackTexture2D.GetRaw(index1, packs[exIndex].imageData);

                    packs[exIndex].renderIndex = RecordCount - c_frameCount;
                    packs[exIndex].runningTask = Task.Run(packs[exIndex].task1);
                    exIndex = (exIndex + 1) % packs.Length;
                }
                RecordCount++;
            }
            else
            {
                for (int i = 0; i < packs.Length; i++)
                {
                    if (packs[i] != null && !packs[i].runningTask.IsCompleted)
                    {
                        packs[i].runningTask.Wait();
                        packs[i] = null;
                    }
                }
            }
        }
        public float StartTime;
        public float StopTime;

        public float FrameIntervalF = 1 / 60.0f;
        public int RecordCount = 0;
        public int RenderCount = 0;
        bool switchEffect;
        public StorageFolder saveFolder;
        public void SwitchEffect()
        {
            switchEffect = true;
        }
    }
}
