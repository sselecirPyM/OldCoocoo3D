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

        private List<MMD3DEntity> Entities { get => CurrentScene.Entities; }
        private ObservableCollection<ISceneObject> sceneObjects { get => CurrentScene.sceneObjects; }
        public object selectedObjcetLock = new object();
        public List<MMD3DEntity> SelectedEntities = new List<MMD3DEntity>();
        public List<Lighting> SelectedLighting = new List<Lighting>();


        public Camera camera = new Camera();
        //public WidgetRenderer widgetRenderer = new WidgetRenderer();
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
            _currentRenderPipeline = forwardRenderPipeline1;
            RPContext.LoadTask = Task.Run(async () =>
            {
                await RPAssetsManager.ReloadAssets();
                await RPContext.ReloadDefalutResources(ProcessingList, miscProcessContext);
                RPAssetsManager.Reload(deviceResources);
                forwardRenderPipeline1.Reload(deviceResources);
                deferredRenderPipeline1.Reload(deviceResources);
                postProcess.Reload(deviceResources);
                widgetRenderer.Reload(deviceResources);
                RPContext.SkinningMeshBuffer.Reload(deviceResources, 0);
                if (deviceResources.IsRayTracingSupport())
                    rayTracingRenderPipeline1.Reload(deviceResources);

                RPAssetsManager.ChangeRenderTargetFormat(deviceResources, ProcessingList, RPContext.outputFormat, RPContext.middleFormat, RPContext.swapChainFormat, RPContext.depthFormat);

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
        ForwardRenderPipeline1 forwardRenderPipeline1 = new ForwardRenderPipeline1();
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
        Task[] poolTasks1;
        private void UpdateEntities(float playTime)
        {
            int threshold = 1;
            if (Entities.Count > threshold)
            {
                if (poolTasks1 != null && poolTasks1.Length == Entities.Count - threshold)
                {
                    for (int i = 0; i < Entities.Count - threshold; i++)
                    {
                        poolTasks1[i] = null;
                    }
                }
                else
                    poolTasks1 = new Task[Entities.Count - threshold];

                for (int i = threshold; i < Entities.Count; i++)
                {
                    MMD3DEntity entity = Entities[i];
                    poolTasks1[i - threshold] = Task.Run(() => entity.SetMotionTime(playTime));
                }
                for (int i = 0; i < threshold; i++)
                    Entities[i].SetMotionTime(playTime);
                Task.WaitAll(poolTasks1);
            }
            else for (int i = 0; i < Entities.Count; i++)
                {
                    Entities[i].SetMotionTime(playTime);
                }
        }
        public void RequireRender(bool updateEntities)
        {
            RPContext.gameDriverContext.NeedUpdateEntities |= updateEntities;
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
            lock (deviceResources)
            {
                #region Render Preparing

                bool needUpdateEntities = RPContext.gameDriverContext.NeedUpdateEntities;
                RPContext.gameDriverContext.NeedUpdateEntities = false;

                RPContext.BeginDynamicContext(RPContext.gameDriverContext.EnableDisplay, settings, inShaderSettings);
                RPContext.dynamicContextWrite.Time = RPContext.gameDriverContext.PlayTime;
                if (RPContext.gameDriverContext.Playing || needUpdateEntities)
                    RPContext.dynamicContextWrite.DeltaTime = RPContext.gameDriverContext.DeltaTime;
                else
                    RPContext.dynamicContextWrite.DeltaTime = 0;


                CurrentScene.DealProcessList(physics3DScene);
                lock (CurrentScene)
                {
                    RPContext.dynamicContextWrite.entities.AddRange(CurrentScene.Entities);
                    for (int i = 0; i < CurrentScene.Lightings.Count; i++)
                    {
                        RPContext.dynamicContextWrite.lightings.Add(CurrentScene.Lightings[i].GetLightingData());
                    }
                }
                RPContext.dynamicContextWrite.selectedEntity = SelectedEntities.FirstOrDefault();

                lock (selectedObjcetLock)
                {
                    for (int i = 0; i < SelectedLighting.Count; i++)
                    {
                        RPContext.dynamicContextWrite.selectedLightings.Add(SelectedLighting[i].GetLightingData());
                    }
                }

                var entities = RPContext.dynamicContextWrite.entities;
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (entity.NeedTransform)
                    {
                        entity.NeedTransform = false;
                        entity.Position = entity.PositionNextFrame;
                        entity.Rotation = entity.RotationNextFrame;
                        entity.boneComponent.TransformToNew(physics3DScene, entity.Position, entity.Rotation);

                        RPContext.gameDriverContext.RequireResetPhysics = true;
                    }
                }
                if (camera.CameraMotionOn) camera.SetCameraMotion((float)RPContext.gameDriverContext.PlayTime);
                camera.AspectRatio = RPContext.gameDriverContext.AspectRatio;
                RPContext.dynamicContextWrite.cameras.Add(camera.GetCameraData());
                RPContext.dynamicContextWrite.Preprocess();



                void _ResetPhysics()
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        entities[i].boneComponent.ResetPhysics(physics3DScene);
                    }
                    physics3DScene.Simulate(1 / 60.0);
                    physics3DScene.FetchResults();
                }

                double t1 = RPContext.gameDriverContext.DeltaTime;
                void _BoneUpdate()
                {
                    UpdateEntities((float)RPContext.gameDriverContext.PlayTime);

                    for (int i = 0; i < entities.Count; i++)
                    {
                        entities[i].boneComponent.SetPhysicsPose(physics3DScene);
                    }
                    physics3DScene.Simulate(t1 >= 0 ? t1 : -t1);

                    physics3DScene.FetchResults();
                    for (int i = 0; i < entities.Count; i++)
                    {
                        entities[i].boneComponent.SetPoseAfterPhysics(physics3DScene);
                    }
                }
                if (RPContext.gameDriverContext.RequireResetPhysics)
                {
                    RPContext.gameDriverContext.RequireResetPhysics = false;
                    _ResetPhysics();
                    _BoneUpdate();
                    _ResetPhysics();
                }
                if (RPContext.gameDriverContext.Playing || needUpdateEntities)
                {
                    _BoneUpdate();
                }
                for (int i = 0; i < entities.Count; i++)
                {
                    entities[i].boneComponent.WriteMatriticesData();
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
                    RPAssetsManager._DealStep3(deviceResources, _processingList);
                    _processingList.Clear();
                    if (SceneObjectVertexCount > RPContext.SkinningMeshBufferSize)
                    {
                        RPContext.SkinningMeshBuffer.Reload(deviceResources, SceneObjectVertexCount);
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

                GraphicsContext.BeginAlloctor(deviceResources);

                miscProcessContext.MoveToAnother(_miscProcessContext);
                miscProcess.Process(RPContext.graphicsContext1, _miscProcessContext);

                if (swapChainReady)
                {
                    graphicsContext.BeginCommand();
                    graphicsContext.SetDescriptorHeapDefault();

                    var currentRenderPipeline = _currentRenderPipeline;//避免在渲染时切换

                    bool thisFrameReady = RPAssetsManager.Ready && currentRenderPipeline.Ready && postProcess.Ready;
                    if (thisFrameReady)
                    {
                        currentRenderPipeline.PrepareRenderData(RPContext);
                        postProcess.PrepareRenderData(RPContext);
                        widgetRenderer.PrepareRenderData(RPContext);
                        RPContext.UpdateGPUResource();
                    }
                    #endregion

                    void _RenderFunction()
                    {
                        graphicsContext.ResourceBarrierScreen(D3D12ResourceStates._PRESENT, D3D12ResourceStates._RENDER_TARGET);
                        currentRenderPipeline.RenderCamera(RPContext);
                        postProcess.RenderCamera(RPContext);
                        GameDriver.AfterRender(RPContext);
                        widgetRenderer.RenderCamera(RPContext);
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
                    _currentRenderPipeline = forwardRenderPipeline1;
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
    }
}
