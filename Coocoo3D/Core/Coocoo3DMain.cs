using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Coocoo3DGraphics;
using Coocoo3D.Present;
using Coocoo3D.Controls;
using Coocoo3D.Utility;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.Foundation;
using Coocoo3D.FileFormat;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Vortice.Direct3D12;

namespace Coocoo3D.Core
{
    ///<summary>是整个应用程序的上下文</summary>
    public class Coocoo3DMain
    {
        public GraphicsDevice graphicsDevice { get => RPContext.graphicsDevice; }
        public MainCaches mainCaches { get => RPContext.mainCaches; }

        public Scene CurrentScene;

        public List<GameObject> SelectedGameObjects = new List<GameObject>();

        public GameDriver GameDriver;
        public GeneralGameDriver _GeneralGameDriver = new GeneralGameDriver();
        public RecorderGameDriver _RecorderGameDriver = new RecorderGameDriver();
        #region Time
        ThreadPoolTimer threadPoolTimer;

        public DateTime LatestRenderTime = DateTime.Now;
        public CoreDispatcher Dispatcher;
        public event EventHandler FrameUpdated;

        public volatile int CompletedRenderCount = 0;
        public volatile int VirtualRenderCount = 0;
        private async void Tick(ThreadPoolTimer timer)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                FrameUpdated?.Invoke(this, null);
                await Coocoo3D.UI.UIHelper.OnFrame(this);
            });
        }
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
            //AutoReloadModels = true,
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
            RPContext.LoadTask = Task.Run(async () =>
            {
                await RPAssetsManager.LoadAssets();
                RPAssetsManager.InitializeRootSignature(graphicsDevice);
                await RPContext.ReloadDefalutResources();
                forwardRenderPipeline2.Reload();
                postProcess.Reload();
                widgetRenderer.Reload(RPContext);
                if (graphicsDevice.IsRayTracingSupport())
                {
                    //await rayTracingRenderPipeline1.ReloadAssets(RPContext);
                }

                miscProcess.ReloadAssets(graphicsDevice);
                RequireRender();
            });

            CurrentScene = new Scene();
            CurrentScene.physics3DScene.Initialize();
            CurrentScene.physics3DScene.SetGravitation(new Vector3(0, -98.01f, 0));
            Dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            threadPoolTimer = ThreadPoolTimer.CreatePeriodicTimer(Tick, TimeSpan.FromSeconds(1 / 30.0));

            canRenderThread = new CancellationTokenSource();
            renderWorkThread = new Thread(() =>
            {
                var token = canRenderThread.Token;
                while (!token.IsCancellationRequested)
                {
                    DateTime now = DateTime.Now;
                    if (now - LatestRenderTime < TimeSpan.FromSeconds(RPContext.gameDriverContext.FrameInterval)) continue;
                    bool actualRender = RenderFrame();
                    if ((performaceSettings.SaveCpuPower && !Recording) && (!performaceSettings.VSync || !actualRender))
                        System.Threading.Thread.Sleep(1);
                }
            });
            renderWorkThread.IsBackground = true;
            renderWorkThread.Start();

        }
        #region Rendering
        public RPAssetsManager RPAssetsManager { get => RPContext.RPAssetsManager; }
        ForwardRenderPipeline2 forwardRenderPipeline2 = new ForwardRenderPipeline2();
        //RayTracingRenderPipeline1 rayTracingRenderPipeline1 = new RayTracingRenderPipeline1();
        public PostProcess postProcess = new PostProcess();
        WidgetRenderer widgetRenderer = new WidgetRenderer();
        MiscProcess miscProcess = new MiscProcess();
        public RenderPipeline.RenderPipeline CurrentRenderPipeline { get => _currentRenderPipeline; }
        RenderPipeline.RenderPipeline _currentRenderPipeline;

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
        public RenderPipeline.RenderPipelineContext RPContext = new RenderPipeline.RenderPipelineContext();

        public bool swapChainReady;
        //public long[] StopwatchTimes = new long[8];
        //System.Diagnostics.Stopwatch stopwatch1 = new System.Diagnostics.Stopwatch();
        public GraphicsContext graphicsContext { get => RPContext.graphicsContext; }
        Task RenderTask1;
        private bool RenderFrame()
        {
            if (!GameDriver.Next(RPContext))
            {
                return false;
            }
            #region Scene Simulation

            RPContext.BeginDynamicContext(RPContext.gameDriverContext.EnableDisplay, settings);
            DateTime now = DateTime.Now;
            var deltaTime = now - LatestRenderTime;
            LatestRenderTime = now;
            RPContext.dynamicContextWrite.Time = RPContext.gameDriverContext.PlayTime;
            RPContext.dynamicContextWrite.RealDeltaTime = deltaTime.TotalSeconds;
            if (RPContext.gameDriverContext.Playing)
                RPContext.dynamicContextWrite.DeltaTime = RPContext.gameDriverContext.DeltaTime;
            else
                RPContext.dynamicContextWrite.DeltaTime = 0;


            CurrentScene.DealProcessList();
            lock (CurrentScene)
            {
                RPContext.dynamicContextWrite.gameObjects.AddRange(CurrentScene.gameObjects);
            }

            //lock (SelectedGameObjects)
            //{
            for (int i = 0; i < SelectedGameObjects.Count; i++)
            {
                LightingComponent lightingComponent = SelectedGameObjects[i].GetComponent<LightingComponent>();
                if (lightingComponent != null)
                    RPContext.dynamicContextWrite.selectedLightings.Add(lightingComponent.GetLightingData());
            }
            //}

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
                CurrentScene.Simulation(RPContext.gameDriverContext.PlayTime, RPContext.gameDriverContext.DeltaTime, rendererComponents,mainCaches, RPContext.gameDriverContext.RequireResetPhysics);
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

            if (RPContext.gameDriverContext.RequireResizeOuter)
            {
                RPContext.gameDriverContext.RequireResizeOuter = false;
                graphicsDevice.SetLogicalSize(RPContext.gameDriverContext.NewSize);
                graphicsDevice.WaitForGpu();
            }
            RPContext.ReloadScreenSizeResources();
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
                graphicsDevice.WaitForGpu();
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

                miscProcess.Process(RPContext);
                var currentRenderPipeline = _currentRenderPipeline;//避免在渲染时切换

                bool thisFrameReady = RPAssetsManager.Ready && currentRenderPipeline.Ready && postProcess.Ready && widgetRenderer.Ready;
                if (thisFrameReady)
                {
                    UI.UIImGui.GUI(this);
                    graphicsContext.Begin();
                    graphicsContext.SetDescriptorHeapDefault();
                    if (RPContext.dynamicContextRead.EnableDisplay)
                    {
                        foreach (var visualChannel in RPContext.visualChannels.Values)
                        {
                            currentRenderPipeline.PrepareRenderData(RPContext, visualChannel);
                            postProcess.PrepareRenderData(RPContext, visualChannel);
                        }
                    }
                    widgetRenderer.PrepareRenderData(RPContext, graphicsContext);
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
                    }
                    GameDriver.AfterRender(RPContext, graphicsContext);
                    graphicsContext.ResourceBarrierScreen(ResourceStates.Present, ResourceStates.RenderTarget);
                    widgetRenderer.RenderCamera(RPContext, graphicsContext);
                    graphicsContext.ResourceBarrierScreen(ResourceStates.RenderTarget, ResourceStates.Present);
                    graphicsContext.EndCommand();
                    graphicsContext.Execute();
                    graphicsDevice.Present(performaceSettings.VSync);
                    CompletedRenderCount++;
                }
            }
            return true;
        }
        #endregion
        public bool Recording = false;

        public int currentRenderPipelineIndex;
        public int renderPipelineIndex;
        public void SwitchToRenderPipeline(int index)
        {
            if (currentRenderPipelineIndex != index)
            {
                currentRenderPipelineIndex = index;
                if (currentRenderPipelineIndex == 0)
                {
                    _currentRenderPipeline = forwardRenderPipeline2;
                    RPContext.SetCurrentPassSetting(RPContext.defaultPassSetting);
                }
                if (currentRenderPipelineIndex == 1)
                {
                    _currentRenderPipeline = forwardRenderPipeline2;
                    RPContext.SetCurrentPassSetting(RPContext.deferredPassSetting);
                }
                if (currentRenderPipelineIndex == 2)
                {
                    //_currentRenderPipeline = rayTracingRenderPipeline1;
                }
                else if (currentRenderPipelineIndex == 3)
                //else if (currentRenderPipelineIndex == 2)
                {
                    if (RPContext.customPassSetting != null)
                    {
                        _currentRenderPipeline = forwardRenderPipeline2;
                        RPContext.SetCurrentPassSetting(RPContext.customPassSetting);
                    }
                }
            }
        }
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
