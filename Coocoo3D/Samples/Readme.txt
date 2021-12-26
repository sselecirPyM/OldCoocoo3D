类型名需要与脚本文件名一致。脚本之间不可互相引用。
在设置中点击“重新加载Shader”来重新加载。
请复制samples文件夹然后修改，但是不要更改程序目录下的文件。coocoox文件可以被程序打开。
在github上提issue。https://github.com/sselecirPyM/Coocoo3D

一些帮助(2021/12/19)：


    public class UnionShaderParam
    {
        public RenderPipelineContext rp;
        public RuntimeMaterial material;
        public MMDRendererComponent renderer;
        public PassSetting passSetting;

        public List<MMDRendererComponent> renderers;

        public List<DirectionalLightData> directionalLights;
        public List<PointLightData> pointLights;

        public RenderSequence renderSequence;
        public Pass pass;

        public GraphicsContext graphicsContext;
        public VisualChannel visualChannel;
        public PSODesc PSODesc;
        public string passName;
        public string relativePath;
        public GPUWriter GPUWriter;
        public Settings settings;
        public Texture2D[] renderTargets;
        public Texture2D depthStencil;
        public MainCaches mainCaches;

        public Texture2D texLoading;
        public Texture2D texError;

        public RayTracingShader rayTracingShader;

        public GraphicsDevice graphicsDevice { get; }

        public Dictionary<string, object> customValue;
        public MMDMesh mesh { get; }
        public MMDMesh meshOverride { get; }

        public object GetSettingsValue(string name);
        public object GetSettingsValue(RuntimeMaterial material, string name);
        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent);
        public Texture2D GetTex2D(string name, RuntimeMaterial material = null);
        public void WriteCBV(SlotRes cbv);
        public void WriteGPU(List<string> datas, GPUWriter writer);
        
        public void SetSRVs(List<SlotRes> SRVs, RuntimeMaterial material = null);
        public void SetComputeSRVs(List<SlotRes> SRVs, RuntimeMaterial material = null);
        public void SRVUAVs(List<SlotRes> SRVUAV, Dictionary<int, object> dict, Dictionary<int, int> flags = null, RuntimeMaterial material = null);
        public Texture2D TextureFallBack(Texture2D _tex) => TextureStatusSelect(_tex, texLoading, texError, texError);
        public static Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error);
    }
    
    public interface IPassDispatcher
    {
        public void FrameBegin(RenderPipelineContext context);
        public void FrameEnd(RenderPipelineContext context);
        public void Dispatch(UnionShaderParam unionShaderParam);
    }
