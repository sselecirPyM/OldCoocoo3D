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
using Coocoo3D.FileFormat;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Vortice.Direct3D12;
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

        public volatile int CompletedRenderCount = 0;
        public volatile int VirtualRenderCount = 0;
        public long LatestRenderTime = 0;
        #endregion
        public Settings settings = new Settings()
        {
            viewSelectedEntityBone = true,
            backgroundColor = new Vector4(0, 0.3f, 0.3f, 0.0f),
            Wireframe = false,
            SkyBoxLightMultiplier = 1.0f,
            ShadowMapResolution = 2048,
            EnableAO = true,
            EnableShadow = true,
            Quality = 0,
        };
        public PerformaceSettings performaceSettings = new PerformaceSettings()
        {
            MultiThreadRendering = true,
            SaveCpuPower = true,
            AutoReloadShaders = true,
            AutoReloadTextures = true,
            VSync = false,
        };

        Thread renderWorkThread;
        CancellationTokenSource canRenderThread;
        public GameDriverContext GameDriverContext { get => RPContext.gameDriverContext; }
        public Coocoo3DMain()
        {
            RPContext.Reload();
            GameDriver = _GeneralGameDriver;
            _currentRenderPipeline = forwardRenderPipeline2;
            mainCaches.processingList = ProcessingList;
            mainCaches._RequireRender = RequireRender;


            CurrentScene = new Scene();
            CurrentScene.physics3DScene.Initialize();
            CurrentScene.physics3DScene.SetGravitation(new Vector3(0, -98.01f, 0));

            canRenderThread = new CancellationTokenSource();
            renderWorkThread = new Thread(() =>
            {
                var token = canRenderThread.Token;
                while (!token.IsCancellationRequested)
                {
                    long now = stopwatch1.ElapsedTicks;
                    if ((now - LatestRenderTime) / 1e7f < RPContext.gameDriverContext.FrameInterval) continue;
                    bool actualRender = RenderFrame();
                    if ((performaceSettings.SaveCpuPower && !Recording) && (!performaceSettings.VSync || !actualRender))
                        System.Threading.Thread.Sleep(1);
                }
            });
            renderWorkThread.IsBackground = true;
            renderWorkThread.Start();

            //RPContext.LoadTask = Task.Run(() =>
            //{
            RPContext.ReloadDefalutResources();
            widgetRenderer.Reload(RPContext);
            if (graphicsDevice.IsRayTracingSupport())
            {
                //await rayTracingRenderPipeline1.ReloadAssets(RPContext);
            }
            RequireRender();
            //});
        }
        #region Rendering
        ForwardRenderPipeline2 forwardRenderPipeline2 = new ForwardRenderPipeline2();
        //RayTracingRenderPipeline1 rayTracingRenderPipeline1 = new RayTracingRenderPipeline1();
        public PostProcess postProcess = new PostProcess();
        WidgetRenderer widgetRenderer = new WidgetRenderer();
        RenderPipeline.RenderPipeline _currentRenderPipeline;
        public ImguiInput imguiInput = new ImguiInput();

        public void RequireRender(bool updateEntities)
        {
            GameDriverContext.RequireRender(updateEntities);
        }
        public void RequireRender()
        {
            GameDriverContext.RequireRender();
        }

        public ProcessingList ProcessingList { get => RPContext.processingList; }
        ProcessingList _processingList = new ProcessingList();
        public RenderPipelineContext RPContext = new RenderPipelineContext();

        public bool swapChainReady;
        public System.Diagnostics.Stopwatch stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        public GraphicsContext graphicsContext { get => RPContext.graphicsContext; }
        Task RenderTask1;

        public double deltaTime1;
        public float framePerSecond;
        public long fpsPreviousUpdate;
        public int fpsRenderCount;
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

            RPContext.BeginDynamicContext(RPContext.gameDriverContext.EnableDisplay, settings);
            LatestRenderTime = now;
            RPContext.dynamicContextWrite.Time = RPContext.gameDriverContext.PlayTime;
            RPContext.dynamicContextWrite.RealDeltaTime = deltaTime1;
            if (RPContext.gameDriverContext.Playing)
                RPContext.dynamicContextWrite.DeltaTime = RPContext.gameDriverContext.DeltaTime;
            else
                RPContext.dynamicContextWrite.DeltaTime = 0;


            CurrentScene.DealProcessList();
            lock (CurrentScene)
            {
                RPContext.dynamicContextWrite.gameObjects.AddRange(CurrentScene.gameObjects);
            }

            for (int i = 0; i < SelectedGameObjects.Count; i++)
            {
                LightingComponent lightingComponent = SelectedGameObjects[i].GetComponent<LightingComponent>();
            }

            var gameObjects = RPContext.dynamicContextWrite.gameObjects;
            var rendererComponents = RPContext.dynamicContextWrite.renderers;
            RPContext.dynamicContextWrite.Preprocess();

            for (int i = 0; i < gameObjects.Count; i++)
            {
                var gameObject = gameObjects[i];
                if (gameObject.Position != gameObject.PositionNextFrame || gameObject.Rotation != gameObject.RotationNextFrame)
                    RPContext.gameDriverContext.RequireResetPhysics = true;
            }

            if (RPContext.gameDriverContext.Playing || RPContext.gameDriverContext.RequireResetPhysics)
            {
                CurrentScene.Simulation(RPContext.gameDriverContext.PlayTime, RPContext.gameDriverContext.DeltaTime, rendererComponents, mainCaches, RPContext.gameDriverContext.RequireResetPhysics);
                RPContext.gameDriverContext.RequireResetPhysics = false;
            }
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].WriteMatriticesData();
            }

            #endregion
            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            #region Render preparing
            var temp1 = RPContext.dynamicContextWrite;
            RPContext.dynamicContextWrite = RPContext.dynamicContextRead;
            RPContext.dynamicContextRead = temp1;

            if (RPContext.RequireResize.SetFalse())
            {
                graphicsDevice.SetLogicalSize(RPContext.NewSize);
                graphicsDevice.WaitForGpu();
            }
            RPContext.PreConfig();

            if (!Recording)
                mainCaches.OnFrame();
            ProcessingList.MoveToAnother(_processingList);
            if (!_processingList.IsEmpty())
            {
                GraphicsContext.BeginAlloctor(graphicsDevice);
                graphicsContext.Begin();
                _processingList._DealStep1(graphicsContext);
                graphicsContext.EndCommand();
                graphicsContext.Execute();
                graphicsDevice.RenderComplete();
            }
            #endregion
            if (!RPContext.dynamicContextRead.EnableDisplay)
            {
                VirtualRenderCount++;
            }

            foreach (var visualChannel in RPContext.visualChannels.Values)
            {
                visualChannel.Onframe(RPContext);
            }
            if (swapChainReady)
            {
                GraphicsContext.BeginAlloctor(graphicsDevice);

                var currentRenderPipeline = _currentRenderPipeline;//避免在渲染时切换

                bool thisFrameReady = widgetRenderer.Ready;
                if (thisFrameReady)
                {
                    imguiInput.Update();
                    UI.UIImGui.GUI(this);
                    graphicsContext.Begin();
                    if (RPContext.dynamicContextRead.EnableDisplay)
                    {
                        currentRenderPipeline.BeginFrame();
                        foreach (var visualChannel in RPContext.visualChannels.Values)
                        {
                            currentRenderPipeline.PrepareRenderData(RPContext, visualChannel);
                            postProcess.PrepareRenderData(RPContext, visualChannel);
                        }
                    }
                    RPContext.UpdateGPUResource();

                    if (performaceSettings.MultiThreadRendering)
                        RenderTask1 = Task.Run(_RenderFunction);
                    else
                        _RenderFunction();
                }
                else
                {
                    graphicsDevice.RenderComplete();
                }

                void _RenderFunction()
                {
                    if (RPContext.dynamicContextRead.EnableDisplay)
                    {
                        SkinningCompute.Process(RPContext);
                        foreach (var visualChannel in RPContext.visualChannels.Values)
                        {
                            currentRenderPipeline.RenderCamera(RPContext, visualChannel);
                            postProcess.RenderCamera(RPContext, visualChannel);
                        }
                        currentRenderPipeline.EndFrame();
                    }
                    GameDriver.AfterRender(RPContext, graphicsContext);
                    graphicsContext.ResourceBarrierScreen(ResourceStates.Present, ResourceStates.RenderTarget);
                    widgetRenderer.Render(RPContext, graphicsContext);
                    graphicsContext.ResourceBarrierScreen(ResourceStates.RenderTarget, ResourceStates.Present);
                    graphicsContext.EndCommand();
                    graphicsContext.Execute();
                    graphicsDevice.Present(performaceSettings.VSync);
                    CompletedRenderCount++;
                }
            }
            return true;
        }

        public void Dispose()
        {
            canRenderThread.Cancel();
            graphicsDevice.WaitForGpu();
            RPContext.Dispose();
        }
        #endregion
        public bool Recording = false;
    }

    public struct PerformaceSettings
    {
        public bool MultiThreadRendering;
        public bool SaveCpuPower;
        public bool AutoReloadShaders;
        public bool AutoReloadTextures;
        public bool VSync;
    }

    public struct Settings
    {
        public bool viewSelectedEntityBone;
        public Vector4 backgroundColor;
        public bool Wireframe;

        public float SkyBoxLightMultiplier;
        public int ShadowMapResolution;

        public uint Quality;

        public bool EnableAO;
        public bool EnableShadow;
    }
}
