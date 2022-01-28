using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Coocoo3DGraphics;
using Coocoo3D.Present;
using Coocoo3D.Utility;
using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;

namespace Coocoo3D.Core
{
    ///<summary>是整个应用程序的上下文</summary>
    public class Coocoo3DMain : IDisposable
    {
        public GraphicsDevice graphicsDevice { get => RPContext.graphicsDevice; }
        public MainCaches mainCaches { get => RPContext.mainCaches; }

        public Scene CurrentScene;

        public List<GameObject> SelectedGameObjects = new List<GameObject>();

        public GameDriver GameDriver;
        public GeneralGameDriver _GeneralGameDriver = new GeneralGameDriver();
        public RecorderGameDriver _RecorderGameDriver = new RecorderGameDriver();
        #region Time
        public int CompletedRenderCount = 0;
        public long LatestRenderTime = 0;

        public double deltaTime1;
        public float framePerSecond;
        public long fpsPreviousUpdate;
        public int fpsRenderCount;
        #endregion
        public PerformanceSettings performanceSettings = new PerformanceSettings()
        {
            MultiThreadRendering = true,
            SaveCpuPower = true,
            AutoReloadShaders = true,
            AutoReloadTextures = true,
            VSync = false,
        };

        Thread renderWorkThread;
        CancellationTokenSource cancelRenderThread;
        public GameDriverContext GameDriverContext { get => RPContext.gameDriverContext; }
        public Coocoo3DMain()
        {
            RPContext.Reload();
            GameDriver = _GeneralGameDriver;
            mainCaches._RequireRender = RequireRender;


            CurrentScene = new Scene();
            CurrentScene.physics3DScene.Initialize();
            CurrentScene.physics3DScene.SetGravitation(new Vector3(0, -98.01f, 0));

            cancelRenderThread = new CancellationTokenSource();
            renderWorkThread = new Thread(() =>
            {
                var token = cancelRenderThread.Token;
                while (!token.IsCancellationRequested)
                {
                    long now = stopwatch1.ElapsedTicks;
                    if ((now - LatestRenderTime) / 1e7f < RPContext.gameDriverContext.FrameInterval) continue;
                    bool actualRender = RenderFrame();
                    if ((performanceSettings.SaveCpuPower && !Recording) && (!performanceSettings.VSync || !actualRender))
                        System.Threading.Thread.Sleep(1);
                }
            });
            renderWorkThread.IsBackground = true;
            renderWorkThread.Start();

            widgetRenderer.Reload(RPContext);

            RequireRender();
        }
        #region Rendering
        HybirdRenderPipeline hybridRenderPipeline = new HybirdRenderPipeline();

        WidgetRenderer widgetRenderer = new WidgetRenderer();
        public ImguiInput imguiInput = new ImguiInput();

        public void RequireRender(bool updateEntities)
        {
            GameDriverContext.RequireRender(updateEntities);
        }
        public void RequireRender()
        {
            GameDriverContext.RequireRender();
        }

        public RenderPipelineContext RPContext = new RenderPipelineContext();

        public System.Diagnostics.Stopwatch stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        public GraphicsContext graphicsContext { get => RPContext.graphicsContext; }
        Task RenderTask1;

        private bool RenderFrame()
        {
            long now = stopwatch1.ElapsedTicks;
            var deltaTime = now - LatestRenderTime;
            deltaTime1 = deltaTime / (double)System.Diagnostics.Stopwatch.Frequency;
            if (!GameDriver.Next(RPContext, now))
            {
                return false;
            }
            fpsRenderCount++;
            if (now - fpsPreviousUpdate > System.Diagnostics.Stopwatch.Frequency)
            {
                framePerSecond = fpsRenderCount * (float)System.Diagnostics.Stopwatch.Frequency / (float)(now - fpsPreviousUpdate);
                fpsRenderCount = 0;
                fpsPreviousUpdate = now;
            }
            #region Scene Simulation
            var gdc = RPContext.gameDriverContext;

            RPContext.BeginDynamicContext(gdc.EnableDisplay, CurrentScene);
            LatestRenderTime = now;
            RPContext.dynamicContextWrite.Time = gdc.PlayTime;
            RPContext.dynamicContextWrite.DeltaTime = gdc.Playing ? gdc.DeltaTime : 0;
            RPContext.dynamicContextWrite.RealDeltaTime = deltaTime1;

            CurrentScene.DealProcessList();
            lock (CurrentScene)
            {
                RPContext.dynamicContextWrite.gameObjects.AddRange(CurrentScene.gameObjects);
            }

            RPContext.dynamicContextWrite.Preprocess();
            var gameObjects = RPContext.dynamicContextWrite.gameObjects;
            var rendererComponents = RPContext.dynamicContextWrite.renderers;

            for (int i = 0; i < gameObjects.Count; i++)
            {
                var gameObject = gameObjects[i];
                if (gameObject.Position != gameObject.PositionNextFrame || gameObject.Rotation != gameObject.RotationNextFrame)
                    gdc.RequireResetPhysics = true;
            }
            if (gdc.Playing || gdc.RequireResetPhysics)
            {
                CurrentScene.Simulation(gdc.PlayTime, gdc.DeltaTime, rendererComponents, mainCaches, gdc.RequireResetPhysics);
                gdc.RequireResetPhysics = false;
            }
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].WriteMatriticesData();
            }

            #endregion
            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            var temp1 = RPContext.dynamicContextWrite;
            RPContext.dynamicContextWrite = RPContext.dynamicContextRead;
            RPContext.dynamicContextRead = temp1;

            if (RPContext.RequireResize.SetFalse())
            {
                graphicsDevice.SetLogicalSize(RPContext.NewSize);
                graphicsDevice.WaitForGpu();
            }
            if (!Recording)
                mainCaches.OnFrame();
            RPContext.PreConfig();

            foreach (var visualChannel in RPContext.visualChannels.Values)
            {
                visualChannel.Onframe(RPContext);
            }
            imguiInput.Update();
            UI.UIImGui.GUI(this);
            GraphicsContext.BeginAlloctor(graphicsDevice);
            graphicsContext.Begin();
            if (RPContext.dynamicContextRead.EnableDisplay)
            {
                hybridRenderPipeline.BeginFrame(RPContext);
            }
            RPContext.UpdateGPUResource();

            if (performanceSettings.MultiThreadRendering)
                RenderTask1 = Task.Run(_RenderFunction);
            else
                _RenderFunction();

            void _RenderFunction()
            {
                if (RPContext.dynamicContextRead.EnableDisplay)
                {
                    foreach (var visualChannel in RPContext.visualChannels.Values)
                    {
                        hybridRenderPipeline.RenderCamera(RPContext, visualChannel);
                    }
                    hybridRenderPipeline.EndFrame(RPContext);
                }
                GameDriver.AfterRender(RPContext, graphicsContext);
                widgetRenderer.Render(RPContext, graphicsContext);
                graphicsContext.Present(performanceSettings.VSync);
                graphicsContext.EndCommand();
                graphicsContext.Execute();
                CompletedRenderCount++;
            }
            return true;
        }

        public void Dispose()
        {
            cancelRenderThread.Cancel();
            graphicsDevice.WaitForGpu();
            RPContext.Dispose();
        }
        #endregion
        public bool Recording = false;
    }

    public struct PerformanceSettings
    {
        public bool MultiThreadRendering;
        public bool SaveCpuPower;
        public bool AutoReloadShaders;
        public bool AutoReloadTextures;
        public bool VSync;
    }
}
