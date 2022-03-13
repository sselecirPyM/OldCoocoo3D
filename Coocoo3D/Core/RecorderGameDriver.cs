﻿using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
        class Pack1
        {
            public Task runningTask;
            public int renderIndex;
            public DirectoryInfo saveFolder;
            public byte[] imageData;
            public int width;
            public int height;
            public void EncodeTask()
            {
                Image<Rgba32> image = Image.WrapMemory<Rgba32>(imageData, width, height);

                FileInfo file = new FileInfo(Path.GetFullPath(string.Format("{0}.png", renderIndex), saveFolder.FullName));
                var stream = file.Open(FileMode.Create);
                image.SaveAsPng(stream);

                stream.Close();
            }
        }
        const int encFrameCount = 8;
        int exIndex = 0;
        Pack1[] packs = new Pack1[encFrameCount];

        public ReadBackTexture2D ReadBackTexture2D = new ReadBackTexture2D();

        public override void AfterRender(RenderPipelineContext rpContext, GraphicsContext graphicsContext)
        {
            ref GameDriverContext context = ref rpContext.gameDriverContext;

            if (context.PlayTime >= StartTime && (RenderCount - c_frameCount) * FrameIntervalF <= StopTime)
            {
                int index1 = RecordCount % c_frameCount;
                var visualchannel = rpContext.visualChannels["main"];

                if (ReadBackTexture2D.GetWidth() != visualchannel.outputSize.X || ReadBackTexture2D.GetHeight() != visualchannel.outputSize.Y)
                {
                    ReadBackTexture2D.Reload(visualchannel.outputSize.X, visualchannel.outputSize.Y, 4);
                    graphicsContext.UpdateReadBackTexture(ReadBackTexture2D);
                }

                graphicsContext.CopyTexture(ReadBackTexture2D, visualchannel.OutputRTV, index1);
                if (RecordCount >= c_frameCount)
                {
                    int width = ReadBackTexture2D.GetWidth();
                    int height = ReadBackTexture2D.GetHeight();
                    if (packs[exIndex] == null)
                        packs[exIndex] = new Pack1();
                    else if (!packs[exIndex].runningTask.IsCompleted)
                    {
                        packs[exIndex].runningTask.Wait();
                    }
                    var pack = packs[exIndex];
                    pack.width = width;
                    pack.height = height;
                    pack.saveFolder = saveFolder;
                    if (pack.imageData == null || pack.imageData.Length != width * height * 4)
                        pack.imageData = new byte[width * height * 4];

                    ReadBackTexture2D.GetRaw<byte>(index1, pack.imageData);

                    pack.renderIndex = RecordCount - c_frameCount;
                    pack.runningTask = Task.Run(pack.EncodeTask);
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
        public DirectoryInfo saveFolder;
        public void SwitchEffect()
        {
            switchEffect = true;
        }
    }
}
