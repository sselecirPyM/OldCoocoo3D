using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3DGraphics;
using Coocoo3D.Present;
using Coocoo3D.Controls;
using Coocoo3D.Utility;
using Windows.Storage;
using System.Collections.ObjectModel;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.Foundation;
using Coocoo3DPhysics;
using System.Globalization;
using Coocoo3D.FileFormat;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;

namespace Coocoo3D.Core
{
    ///<summary>是整个应用程序的上下文</summary>
    public class Coocoo3DMain
    {
        public DeviceResources deviceResources { get => RPContext.deviceResources; }
        public WICFactory wicFactory = new WICFactory();
        public MainCaches mainCaches = new MainCaches();

        public Scene CurrentScene;

        public object selectedObjcetLock = new object();
        public List<MMD3DEntity> SelectedEntities = new List<MMD3DEntity>();
        public List<GameObject> SelectedGameObjects = new List<GameObject>();

        public Camera camera = new Camera();
        public GameDriver GameDriver;
        public GeneralGameDriver _GeneralGameDriver = new GeneralGameDriver();
        public RecorderGameDriver _RecorderGameDriver = new RecorderGameDriver();
        #region Time
        ThreadPoolTimer threadPoolTimer;

        public DateTime LatestRenderTime = DateTime.Now;
        public float Fps = 240;
        public CoreDispatcher Dispatcher;
        public event EventHandler FrameUpdated;

        public volatile int CompletedRenderCount = 0;
        public volatile int VirtualRenderCount = 0;
        private async void Tick(ThreadPoolTimer timer)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                FrameUpdated?.Invoke(this, null);
            });
        }
        #endregion
        public Settings settings = new Settings()
        {
            viewSelectedEntityBone = true,
            backgroundColor = new Vector4(0, 0.3f, 0.3f, 0.0f),
            ExtendShadowMapRange = 64,
            ZPrepass = false,
            ViewerUI = true,
            Wireframe = false,
        };
        public InShaderSettings inShaderSettings = new InShaderSettings()
        {
            //backgroundColor = new Vector4(0, 0.3f, 0.3f, 0.0f),
            SkyBoxLightMultiple = 1.0f,
            EnableAO = true,
            EnableShadow = true,
            Quality = 0,
        };
        public PerformaceSettings performaceSettings = new PerformaceSettings()
        {
            MultiThreadRendering = true,
            SaveCpuPower = true,
            HighResolutionShadow = false,
            AutoReloadShaders = true,
            AutoReloadTextures = true,
            AutoReloadModels = true,
            VSync = false,
        };

        public Physics3D physics3D = new Physics3D();
        public Physics3DScene physics3DScene = new Physics3DScene();
        IAsyncAction RenderLoop;
        public GameDriverContext GameDriverContext { get => RPContext.gameDriverContext; }
        public Coocoo3DMain()
        {
            RPContext.Reload();
            GameDriver = _GeneralGameDriver;
            RPContext.gameDriverContext.DeviceResources = deviceResources;
            RPContext.gameDriverContext.ProcessingList = ProcessingList;
            RPContext.gameDriverContext.WICFactory = wicFactory;
            _currentRenderPipeline = forwardRenderPipeline2;
            RPContext.LoadTask = Task.Run(async () =>
            {
                await RPAssetsManager.LoadAssets();
                RPAssetsManager.InitializeRootSignature(deviceResources);
                RPAssetsManager.InitializePipelineState();
                await RPContext.ReloadDefalutResources(ProcessingList, miscProcessContext);
                //forwardRenderPipeline1.Reload(deviceResources);
                forwardRenderPipeline2.Reload(deviceResources);
                deferredRenderPipeline1.Reload(deviceResources);
                postProcess.Reload(deviceResources);
                widgetRenderer.Reload(deviceResources);
                deviceResources.InitializeMeshBuffer(RPContext.SkinningMeshBuffer, 0);
                if (deviceResources.IsRayTracingSupport())
                    rayTracingRenderPipeline1.Reload(deviceResources);


                await miscProcess.ReloadAssets(deviceResources);
                if (deviceResources.IsRayTracingSupport())
                    await rayTracingRenderPipeline1.ReloadAssets(deviceResources);
                RequireRender();
            });
            //PhysXAPI.SetAPIUsed(physics3D);
            BulletAPI.SetAPIUsed(physics3D);
            physics3D.Init();
            physics3DScene.Reload(physics3D);
            physics3DScene.SetGravitation(new Vector3(0, -98.01f, 0));

            CurrentScene = new Scene();
            Dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            threadPoolTimer = ThreadPoolTimer.CreatePeriodicTimer(Tick, TimeSpan.FromSeconds(1 / 30.0));
            RenderLoop = ThreadPool.RunAsync((IAsyncAction action) =>
              {
                  while (action.Status == AsyncStatus.Started)
                  {
                      DateTime now = DateTime.Now;
                      if (now - LatestRenderTime < RPContext.gameDriverContext.FrameInterval) continue;
                      bool actualRender = RenderFrame();
                      if (performaceSettings.SaveCpuPower && (!performaceSettings.VSync || !actualRender))//开启VSync下不需要sleep，以免帧生成不均匀
                          System.Threading.Thread.Sleep(1);
                  }
              }, WorkItemPriority.Low, WorkItemOptions.TimeSliced);
        }
        #region Rendering
        public RPAssetsManager RPAssetsManager { get => RPContext.RPAssetsManager; }
        //ForwardRenderPipeline1 forwardRenderPipeline1 = new ForwardRenderPipeline1();
        ForwardRenderPipeline2 forwardRenderPipeline2 = new ForwardRenderPipeline2();
        DeferredRenderPipeline1 deferredRenderPipeline1 = new DeferredRenderPipeline1();
        RayTracingRenderPipeline1 rayTracingRenderPipeline1 = new RayTracingRenderPipeline1();
        public PostProcess postProcess = new PostProcess();
        WidgetRenderer widgetRenderer = new WidgetRenderer();
        MiscProcess miscProcess = new MiscProcess();
        public MiscProcessContext miscProcessContext = new MiscProcessContext();
        MiscProcessContext _miscProcessContext = new MiscProcessContext();
        public RenderPipeline.RenderPipeline CurrentRenderPipeline { get => _currentRenderPipeline; }
        RenderPipeline.RenderPipeline _currentRenderPipeline;

        public bool UseNewFun;
        public void RequireRender(bool updateEntities)
        {
            RPContext.gameDriverContext.notPaused |= updateEntities;
            RPContext.gameDriverContext.NeedRender = true;
        }
        public void RequireRender()
        {
            RPContext.gameDriverContext.NeedRender = true;
        }

        public ProcessingList ProcessingList = new ProcessingList();
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
            #region Render Preparing

            bool notPaused = RPContext.gameDriverContext.notPaused;
            RPContext.gameDriverContext.notPaused = false;

            RPContext.BeginDynamicContext(RPContext.gameDriverContext.EnableDisplay, settings, inShaderSettings);
            RPContext.dynamicContextWrite.Time = RPContext.gameDriverContext.PlayTime;
            if (RPContext.gameDriverContext.Playing || notPaused)
                RPContext.dynamicContextWrite.DeltaTime = RPContext.gameDriverContext.DeltaTime;
            else
                RPContext.dynamicContextWrite.DeltaTime = 0;


            CurrentScene.DealProcessList(physics3DScene);
            lock (CurrentScene)
            {
                RPContext.dynamicContextWrite.entities.AddRange(CurrentScene.Entities);
                RPContext.dynamicContextWrite.gameObjects.AddRange(CurrentScene.gameObjects);
            }
            RPContext.dynamicContextWrite.selectedEntity = SelectedEntities.FirstOrDefault();

            lock (selectedObjcetLock)
            {
                for (int i = 0; i < SelectedGameObjects.Count; i++)
                {
                    LightingComponent lightingComponent = SelectedGameObjects[i].GetComponent<LightingComponent>();
                    if (lightingComponent != null)
                        RPContext.dynamicContextWrite.selectedLightings.Add(lightingComponent.GetLightingData());
                }
            }

            var entities = RPContext.dynamicContextWrite.entities;
            var rendererComponents = RPContext.dynamicContextWrite.rendererComponents;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.NeedTransform)
                {
                    entity.NeedTransform = false;
                    entity.Position = entity.PositionNextFrame;
                    entity.Rotation = entity.RotationNextFrame;
                    entity.rendererComponent.TransformToNew(physics3DScene, entity.Position, entity.Rotation);

                    RPContext.gameDriverContext.RequireResetPhysics = true;
                }
            }
            if (camera.CameraMotionOn) camera.SetCameraMotion((float)RPContext.gameDriverContext.PlayTime);
            camera.AspectRatio = RPContext.gameDriverContext.AspectRatio;
            RPContext.dynamicContextWrite.cameras.Add(camera.GetCameraData());
            RPContext.dynamicContextWrite.Preprocess();



            void _ResetPhysics()
            {
                for (int i = 0; i < rendererComponents.Count; i++)
                {
                    rendererComponents[i].ResetPhysics(physics3DScene);
                }
                physics3DScene.Simulate(1 / 60.0);
                physics3DScene.FetchResults();
            }

            double t1 = RPContext.gameDriverContext.DeltaTime;
            void _BoneUpdate()
            {
                void UpdateEntities(float playTime)
                {
                    int threshold = 1;
                    if (entities.Count > threshold)
                    {
                        Parallel.ForEach(entities, (MMD3DEntity e) => { e.SetMotionTime(playTime); });
                    }
                    else for (int i = 0; i < entities.Count; i++)
                        {
                            entities[i].SetMotionTime(playTime);
                        }
                }
                UpdateEntities((float)RPContext.gameDriverContext.PlayTime);

                for (int i = 0; i < rendererComponents.Count; i++)
                {
                    rendererComponents[i].SetPhysicsPose(physics3DScene);
                }
                physics3DScene.Simulate(t1 >= 0 ? t1 : -t1);

                physics3DScene.FetchResults();
                for (int i = 0; i < rendererComponents.Count; i++)
                {
                    rendererComponents[i].SetPoseAfterPhysics(physics3DScene);
                }
            }
            if (RPContext.gameDriverContext.RequireResetPhysics)
            {
                RPContext.gameDriverContext.RequireResetPhysics = false;
                _ResetPhysics();
                _BoneUpdate();
                _ResetPhysics();
            }
            if (RPContext.gameDriverContext.Playing || notPaused)
            {
                _BoneUpdate();
            }
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                rendererComponents[i].WriteMatriticesData();
            }

            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            #region Render preparing
            var temp1 = RPContext.dynamicContextWrite;
            RPContext.dynamicContextWrite = RPContext.dynamicContextRead;
            RPContext.dynamicContextRead = temp1;

            ProcessingList.MoveToAnother(_processingList);
            int SceneObjectVertexCount = RPContext.dynamicContextRead.GetSceneObjectVertexCount();
            if (!_processingList.IsEmpty() || RPContext.gameDriverContext.RequireInterruptRender || SceneObjectVertexCount > RPContext.SkinningMeshBufferSize)
            {
                RPContext.gameDriverContext.RequireInterruptRender = false;
                deviceResources.WaitForGpu();
                if (RPContext.gameDriverContext.NeedReloadModel)
                {
                    RPContext.gameDriverContext.NeedReloadModel = false;
                    ModelReloader.ReloadModels(CurrentScene, mainCaches, _processingList, RPContext.gameDriverContext);
                }
                RPContext.ChangeShadowMapsQuality(_processingList, performaceSettings.HighResolutionShadow);
                GraphicsContext.BeginAlloctor(deviceResources);
                graphicsContext.BeginCommand();
                _processingList._DealStep1(graphicsContext);
                graphicsContext.EndCommand();
                graphicsContext.Execute();
                deviceResources.WaitForGpu();
                if (RPContext.gameDriverContext.RequireResize)
                {
                    RPContext.gameDriverContext.RequireResize = false;
                    deviceResources.SetLogicalSize(RPContext.gameDriverContext.NewSize);
                    RPContext.ReloadTextureSizeResources(_processingList);
                }

                _processingList._DealStep2(graphicsContext, deviceResources);
                _processingList.Clear();
                if (SceneObjectVertexCount > RPContext.SkinningMeshBufferSize)
                {
                    deviceResources.InitializeMeshBuffer(RPContext.SkinningMeshBuffer, SceneObjectVertexCount);
                    RPContext.SkinningMeshBufferSize = SceneObjectVertexCount;
                    RPContext.LightCacheBuffer.Initialize(deviceResources, SceneObjectVertexCount * 16);
                }
            }
            #endregion
            if (!RPContext.dynamicContextRead.EnableDisplay)
            {
                VirtualRenderCount++;
                return true;
            }
            #endregion

            GraphicsContext.BeginAlloctor(deviceResources);

            miscProcessContext.MoveToAnother(_miscProcessContext);
            miscProcess.Process(RPContext, _miscProcessContext);

            if (swapChainReady)
            {
                graphicsContext.BeginCommand();
                graphicsContext.SetDescriptorHeapDefault();

                var currentRenderPipeline = _currentRenderPipeline;//避免在渲染时切换

                bool thisFrameReady = RPAssetsManager.Ready && currentRenderPipeline.Ready && postProcess.Ready;
                if (thisFrameReady)
                {
                    currentRenderPipeline.PrepareRenderData(RPContext, graphicsContext);
                    postProcess.PrepareRenderData(RPContext, graphicsContext);
                    widgetRenderer.PrepareRenderData(RPContext, graphicsContext);
                    RPContext.UpdateGPUResource();
                }

                void _RenderFunction()
                {
                    graphicsContext.ResourceBarrierScreen(D3D12ResourceStates._PRESENT, D3D12ResourceStates._RENDER_TARGET);
                    currentRenderPipeline.RenderCamera(RPContext, graphicsContext);
                    postProcess.RenderCamera(RPContext, graphicsContext);
                    GameDriver.AfterRender(RPContext, graphicsContext);
                    widgetRenderer.RenderCamera(RPContext, graphicsContext);
                    graphicsContext.ResourceBarrierScreen(D3D12ResourceStates._RENDER_TARGET, D3D12ResourceStates._PRESENT);
                    graphicsContext.EndCommand();
                    graphicsContext.Execute();
                    deviceResources.Present(performaceSettings.VSync);
                    CompletedRenderCount++;
                }
                if (thisFrameReady)
                {
                    if (performaceSettings.MultiThreadRendering)
                        RenderTask1 = Task.Run(_RenderFunction);
                    else
                        _RenderFunction();
                }
                else
                {
                    graphicsContext.EndCommand();
                    graphicsContext.Execute();
                    deviceResources.Present(performaceSettings.VSync);
                }
            }
            return true;
        }
        #endregion
        public bool Recording = false;

        int currentRenderPipelineIndex;
        public void SwitchToRenderPipeline(int index)
        {
            if (currentRenderPipelineIndex != index)
            {
                currentRenderPipelineIndex = index;
                if (currentRenderPipelineIndex == 0)
                {
                    _currentRenderPipeline = forwardRenderPipeline2;
                }
                if (currentRenderPipelineIndex == 1)
                {
                    _currentRenderPipeline = deferredRenderPipeline1;
                }
                if (currentRenderPipelineIndex == 2)
                {
                    _currentRenderPipeline = rayTracingRenderPipeline1;
                }
            }
        }
        #region UI
        public StorageFolder openedStorageFolder;
        public event EventHandler OpenedStorageFolderChanged;
        public void OpenedStorageFolderChange(StorageFolder storageFolder)
        {
            openedStorageFolder = storageFolder;
            OpenedStorageFolderChanged?.Invoke(this, null);
        }
        public Frame frameViewProperties;
        public void ShowDetailPage(Type page, object parameter)
        {
            frameViewProperties.Navigate(page, parameter);
        }
        #endregion
    }

    public struct PerformaceSettings
    {
        public bool MultiThreadRendering;
        public bool SaveCpuPower;
        public bool HighResolutionShadow;
        public bool AutoReloadShaders;
        public bool AutoReloadTextures;
        public bool AutoReloadModels;
        public bool VSync;
    }

    public struct Settings
    {
        public bool viewSelectedEntityBone;
        public Vector4 backgroundColor;
        public float ExtendShadowMapRange;
        public bool ZPrepass;
        public uint RenderStyle;
        public bool ViewerUI;
        public bool Wireframe;
        //public float SkyBoxLightMultiple;
    }
}
