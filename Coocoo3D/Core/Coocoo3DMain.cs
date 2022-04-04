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

        public float framePerSecond;
        public long fpsPreviousUpdate;
        public int fpsRenderCount;
        #endregion
        public PerformanceSettings performanceSettings = new PerformanceSettings()
        {
            MultiThreadRendering = true,
            SaveCpuPower = true,
            VSync = false,
        };

        Thread renderWorkThread;
        CancellationTokenSource cancelRenderThread;
        public GameDriverContext GameDriverContext { get => RPContext.gameDriverContext; }
        public Coocoo3DMain()
        {
            RPContext.Load();
            GameDriver = _GeneralGameDriver;
            mainCaches._RequireRender = () => RequireRender(false);

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
                    if (performanceSettings.SaveCpuPower && !Recording && (!performanceSettings.VSync || !actualRender))
                        System.Threading.Thread.Sleep(1);
                }
            });
            renderWorkThread.IsBackground = true;
            renderWorkThread.Start();

            RequireRender();
        }
        #region Rendering

        WidgetRenderer widgetRenderer = new WidgetRenderer();
        public UI.ImguiInput imguiInput = new UI.ImguiInput();

        public void RequireRender(bool updateEntities = false)
        {
            GameDriverContext.RequireRender(updateEntities);
        }

        public RenderPipelineContext RPContext = new RenderPipelineContext();

        public System.Diagnostics.Stopwatch stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        GraphicsContext graphicsContext { get => RPContext.graphicsContext; }
        Task RenderTask1;

        private bool RenderFrame()
        {
            long now = stopwatch1.ElapsedTicks;
            long stopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;
            double deltaTime = (now - LatestRenderTime) / (double)stopwatchFrequency;
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
            RPContext.dynamicContextWrite.RealDeltaTime = deltaTime;

            CurrentScene.DealProcessList();

            if (CurrentScene.setTransform.Count != 0) gdc.RequireResetPhysics = true;
            if (gdc.Playing || gdc.RequireResetPhysics)
            {
                CurrentScene.Simulation(gdc.PlayTime, gdc.DeltaTime, gdc.RequireResetPhysics);
                gdc.RequireResetPhysics = false;
            }

            RPContext.dynamicContextWrite.Preprocess(CurrentScene.gameObjects);

            #endregion
            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            (RPContext.dynamicContextRead, RPContext.dynamicContextWrite) = (RPContext.dynamicContextWrite, RPContext.dynamicContextRead);
            if (RequireResize.SetFalse())
            {
                RPContext.swapChain.Resize(NewSize.X, NewSize.Y);
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
            HybirdRenderPipeline.BeginFrame(RPContext);

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
                HybirdRenderPipeline.RenderCamera(RPContext, visualChannel);
            }
            HybirdRenderPipeline.EndFrame(RPContext);

            GameDriver.AfterRender(RPContext, graphicsContext);
            widgetRenderer.Render(RPContext, graphicsContext);
            RPContext.AfterRender();
            graphicsContext.Present(RPContext.swapChain, performanceSettings.VSync);
            graphicsContext.EndCommand();
            graphicsContext.Execute();
            graphicsDevice.RenderComplete();
        }

        public void Dispose()
        {
            cancelRenderThread.Cancel();
            graphicsDevice.WaitForGpu();
            RPContext.Dispose();
        }
        #endregion
        public bool Recording = false;

        public void ToPlayMode()
        {
            if (Recording)
            {
                GameDriver = _GeneralGameDriver;
                Recording = false;
            }
        }

        public void ToRecordMode(System.IO.DirectoryInfo saveDir)
        {
            _RecorderGameDriver.saveFolder = saveDir;
            _RecorderGameDriver.SwitchEffect();
            GameDriver = _RecorderGameDriver;
            Recording = true;
        }

        public void Resize(int width, int height)
        {
            RequireResize = true;
            NewSize = new Vector2(width, height);
        }

        public void SetWindow(IntPtr hwnd, int width, int height)
        {
            RPContext.swapChain.Initialize(graphicsDevice, hwnd, width, height);
        }
    }

    public struct PerformanceSettings
    {
        public bool MultiThreadRendering;
        public bool SaveCpuPower;
        public bool VSync;
    }
}
