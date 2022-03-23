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

namespace Coocoo3D.Core
{
    ///<summary>是整个应用程序的上下文</summary>
    public class Coocoo3DMain : IDisposable
    {
        GraphicsDevice graphicsDevice { get => RPContext.graphicsDevice; }
        public MainCaches mainCaches { get => RPContext.mainCaches; }

        public Scene CurrentScene;

        public List<GameObject> SelectedGameObjects = new List<GameObject>();

        public GameDriver GameDriver;
        public GeneralGameDriver _GeneralGameDriver = new GeneralGameDriver();
        public RecorderGameDriver _RecorderGameDriver = new RecorderGameDriver();

        public bool RequireResize;
        public Vector2 NewSize;
        #region Time
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
            RPContext.Load();
            GameDriver = _GeneralGameDriver;
            mainCaches._RequireRender = RequireRender;


            CurrentScene = new Scene();
            CurrentScene.physics3DScene.Initialize();
            CurrentScene.physics3DScene.SetGravitation(new Vector3(0, -98.01f, 0));
            CurrentScene.mainCaches = mainCaches;

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

            RequireRender();
        }
        #region Rendering
        HybirdRenderPipeline hybridRenderPipeline = new HybirdRenderPipeline();

        WidgetRenderer widgetRenderer = new WidgetRenderer();
        public UI.ImguiInput imguiInput = new UI.ImguiInput();

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
            long stopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;
            deltaTime1 = deltaTime / (double)stopwatchFrequency;
            if (!GameDriver.Next(RPContext, now))
            {
                return false;
            }
            fpsRenderCount++;
            if (now - fpsPreviousUpdate > stopwatchFrequency)
            {
                framePerSecond = fpsRenderCount * (float)stopwatchFrequency / (float)(now - fpsPreviousUpdate);
                fpsRenderCount = 0;
                fpsPreviousUpdate = now;
            }
            LatestRenderTime = now;
            #region Scene Simulation
            var gdc = RPContext.gameDriverContext;

            RPContext.BeginDynamicContext(CurrentScene);
            RPContext.dynamicContextWrite.Time = gdc.PlayTime;
            RPContext.dynamicContextWrite.DeltaTime = gdc.Playing ? gdc.DeltaTime : 0;
            RPContext.dynamicContextWrite.RealDeltaTime = deltaTime1;

            CurrentScene.DealProcessList();

            if (CurrentScene.setTransform.Count != 0) gdc.RequireResetPhysics = true;
            if (gdc.Playing || gdc.RequireResetPhysics)
            {
                CurrentScene.Simulation(gdc.PlayTime, gdc.DeltaTime, gdc.RequireResetPhysics);
                gdc.RequireResetPhysics = false;
            }
            lock (CurrentScene)
            {
                RPContext.dynamicContextWrite.gameObjects.AddRange(CurrentScene.gameObjects);
            }
            RPContext.dynamicContextWrite.Preprocess();
            var rendererComponents = RPContext.dynamicContextWrite.renderers;
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].WriteMatriticesData();
            }

            #endregion
            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            (RPContext.dynamicContextRead, RPContext.dynamicContextWrite) = (RPContext.dynamicContextWrite, RPContext.dynamicContextRead);
            if (RequireResize.SetFalse())
            {
                graphicsDevice.SetLogicalSize(NewSize);
                graphicsDevice.WaitForGpu();
            }
            if (!Recording)
                mainCaches.OnFrame();
            RPContext.PreConfig();

            imguiInput.Update();
            UI.UIImGui.GUI(this);
            GraphicsContext.BeginAlloctor(graphicsDevice);
            graphicsContext.Begin();
            RPContext.UpdateGPUResource();
            hybridRenderPipeline.BeginFrame(RPContext);

            if (performanceSettings.MultiThreadRendering)
                RenderTask1 = Task.Run(RenderFunction);
            else
                RenderFunction();

            return true;
        }
        void RenderFunction()
        {
            foreach (var visualChannel in RPContext.visualChannels.Values)
            {
                hybridRenderPipeline.RenderCamera(RPContext, visualChannel);
            }
            hybridRenderPipeline.EndFrame(RPContext);

            GameDriver.AfterRender(RPContext, graphicsContext);
            widgetRenderer.Render(RPContext, graphicsContext);
            RPContext.AfterRender();
            graphicsContext.Present(performanceSettings.VSync);
            graphicsContext.EndCommand();
            graphicsContext.Execute();
        }

        public void Dispose()
        {
            cancelRenderThread.Cancel();
            graphicsDevice.WaitForGpu();
            RPContext.Dispose();
        }
        #endregion
        public bool Recording = false;

        public void Resize(int width,int height)
        {
            RequireResize = true;
            NewSize = new Vector2(width, height);
        }

        public void SetWindow(IntPtr hwnd, int width, int height)
        {
            graphicsDevice.SetSwapChainPanel(hwnd, width, height);
        }
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
