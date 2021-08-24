using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using System.Numerics;
using SharpGen.Runtime;
using Vortice.DXGI;
using System.Threading;
using Vortice.Mathematics;
using Vortice.Direct3D12.Debug;

namespace Coocoo3DGraphics1
{
    public class GraphicsDevice
    {
        public class d3d12RecycleResource
        {
            public ID3D12Object m_recycleResource;
            public UInt64 m_removeFrame;
        };

        public const int c_frameCount = 3;
        public const int CBVSRVUAVDescriptorCount = 65536;
        public ID3D12Device5 device;
        public IDXGIAdapter adapter;

        public DescriptorHeapX cbvsrvuavHeap;
        public DescriptorHeapX rtvHeap;
        public DescriptorHeapX dsvHeap;

        string m_deviceDescription;
        UInt64 m_deviceVideoMem;

        UInt64[] m_fenceValues = new UInt64[c_frameCount];
        UInt64 m_currentFenceValue;

        List<d3d12RecycleResource> m_recycleList;
        List<ID3D12GraphicsCommandList4> m_commandLists;
        List<ID3D12GraphicsCommandList4> m_commandLists1;

        IDXGIFactory4 m_dxgiFactory;
        IDXGISwapChain3 m_swapChain;
        ID3D12Resource[] m_renderTargets = new ID3D12Resource[c_frameCount];
        public ID3D12CommandQueue commandQueue;
        ID3D12CommandAllocator[] commandAllocators = new ID3D12CommandAllocator[c_frameCount];

        Format m_backBufferFormat;
        Viewport m_screenViewport;
        uint m_rtvDescriptorSize;
        bool m_deviceRemoved;

        bool m_isRayTracingSupport;

        ID3D12Fence fence;
        EventWaitHandle fenceEvent;
        public uint executeIndex = 0;
        public ulong executeCount = 3;

        public ISwapChainPanelNative m_swapChainPanel;

        public Vector2 m_d3dRenderTargetSize;
        public Vector2 m_outputSize;
        public Vector2 m_logicalSize;
        public Vector2 m_nativeOrientation;
        public Vector2 m_currentOrientation;
        public float m_dpi;

        public float m_compositionScaleX;
        public float m_compositionScaleY;

        public float m_effectiveDpi;

        public static uint BitsPerPixel(Format format)
        {
            switch (format)
            {
                case Format.R32G32B32A32_Typeless:
                case Format.R32G32B32A32_Float:
                case Format.R32G32B32A32_UInt:
                case Format.R32G32B32A32_SInt:
                    return 128;

                case Format.R32G32B32_Typeless:
                case Format.R32G32B32_Float:
                case Format.R32G32B32_UInt:
                case Format.R32G32B32_SInt:
                    return 96;

                case Format.R16G16B16A16_Typeless:
                case Format.R16G16B16A16_Float:
                case Format.R16G16B16A16_UNorm:
                case Format.R16G16B16A16_UInt:
                case Format.R16G16B16A16_SNorm:
                case Format.R16G16B16A16_SInt:
                case Format.R32G32_Typeless:
                case Format.R32G32_Float:
                case Format.R32G32_UInt:
                case Format.R32G32_SInt:
                case Format.R32G8X24_Typeless:
                case Format.D32_Float_S8X24_UInt:
                case Format.R32_Float_X8X24_Typeless:
                case Format.X32_Typeless_G8X24_UInt:
                case Format.Y416:
                case Format.Y210:
                case Format.Y216:
                    return 64;

                case Format.R10G10B10A2_Typeless:
                case Format.R10G10B10A2_UNorm:
                case Format.R10G10B10A2_UInt:
                case Format.R11G11B10_Float:
                case Format.R8G8B8A8_Typeless:
                case Format.R8G8B8A8_UNorm:
                case Format.R8G8B8A8_UNorm_SRgb:
                case Format.R8G8B8A8_UInt:
                case Format.R8G8B8A8_SNorm:
                case Format.R8G8B8A8_SInt:
                case Format.R16G16_Typeless:
                case Format.R16G16_Float:
                case Format.R16G16_UNorm:
                case Format.R16G16_UInt:
                case Format.R16G16_SNorm:
                case Format.R16G16_SInt:
                case Format.R32_Typeless:
                case Format.D32_Float:
                case Format.R32_Float:
                case Format.R32_UInt:
                case Format.R32_SInt:
                case Format.R24G8_Typeless:
                case Format.D24_UNorm_S8_UInt:
                case Format.R24_UNorm_X8_Typeless:
                case Format.X24_Typeless_G8_UInt:
                case Format.R9G9B9E5_SharedExp:
                case Format.R8G8_B8G8_UNorm:
                case Format.G8R8_G8B8_UNorm:
                case Format.B8G8R8A8_UNorm:
                case Format.B8G8R8X8_UNorm:
                case Format.R10G10B10_Xr_Bias_A2_UNorm:
                case Format.B8G8R8A8_Typeless:
                case Format.B8G8R8A8_UNorm_SRgb:
                case Format.B8G8R8X8_Typeless:
                case Format.B8G8R8X8_UNorm_SRgb:
                case Format.AYUV:
                case Format.Y410:
                case Format.YUY2:
                    return 32;

                case Format.P010:
                case Format.P016:
                    return 24;

                case Format.R8G8_Typeless:
                case Format.R8G8_UNorm:
                case Format.R8G8_UInt:
                case Format.R8G8_SNorm:
                case Format.R8G8_SInt:
                case Format.R16_Typeless:
                case Format.R16_Float:
                case Format.D16_UNorm:
                case Format.R16_UNorm:
                case Format.R16_UInt:
                case Format.R16_SNorm:
                case Format.R16_SInt:
                case Format.B5G6R5_UNorm:
                case Format.B5G5R5A1_UNorm:
                case Format.A8P8:
                case Format.B4G4R4A4_UNorm:
                    return 16;

                case Format.NV12:
                //case Format.420_OPAQUE:
                case Format.NV11:
                    return 12;

                case Format.R8_Typeless:
                case Format.R8_UNorm:
                case Format.R8_UInt:
                case Format.R8_SNorm:
                case Format.R8_SInt:
                case Format.A8_UNorm:
                case Format.AI44:
                case Format.IA44:
                case Format.P8:
                    return 8;

                case Format.R1_UNorm:
                    return 1;

                case Format.BC1_Typeless:
                case Format.BC1_UNorm:
                case Format.BC1_UNorm_SRgb:
                case Format.BC4_Typeless:
                case Format.BC4_UNorm:
                case Format.BC4_SNorm:
                    return 4;

                case Format.BC2_Typeless:
                case Format.BC2_UNorm:
                case Format.BC2_UNorm_SRgb:
                case Format.BC3_Typeless:
                case Format.BC3_UNorm:
                case Format.BC3_UNorm_SRgb:
                case Format.BC5_Typeless:
                case Format.BC5_UNorm:
                case Format.BC5_SNorm:
                case Format.BC6H_Typeless:
                case Format.BC6H_Uf16:
                case Format.BC6H_Sf16:
                case Format.BC7_Typeless:
                case Format.BC7_UNorm:
                case Format.BC7_UNorm_SRgb:
                    return 8;

                default:
                    return 0;
            }
        }

        public void CreateDeviceResource()
        {
#if DEBUG
            if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out var pDx12Debug).Success)
                pDx12Debug.EnableDebugLayer();
#endif

            ThrowIfFailed(DXGI.CreateDXGIFactory1(out m_dxgiFactory));

            int index1 = 0;
            while (true)
            {
                var hr = m_dxgiFactory.EnumAdapters(index1, out adapter);
                if (hr == SharpGen.Runtime.Result.Ok)
                {
                    break;
                }
                index1++;
            }
            ThrowIfFailed(D3D12.D3D12CreateDevice(this.adapter, out device));
            CommandQueueDescription description;
            description.Flags = CommandQueueFlags.None;
            description.Type = CommandListType.Direct;
            description.NodeMask = 0;
            description.Priority = 0;
            ThrowIfFailed(device.CreateCommandQueue(description, out commandQueue));

            DescriptorHeapDescription descriptorHeapDescription;
            descriptorHeapDescription.DescriptorCount = CBVSRVUAVDescriptorCount;
            descriptorHeapDescription.Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.ShaderVisible;
            descriptorHeapDescription.NodeMask = 0;
            cbvsrvuavHeap.Initialize(this, descriptorHeapDescription);

            descriptorHeapDescription.DescriptorCount = 16;
            descriptorHeapDescription.Type = DescriptorHeapType.DepthStencilView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.None;
            dsvHeap.Initialize(this, descriptorHeapDescription);

            descriptorHeapDescription.DescriptorCount = 16;
            descriptorHeapDescription.Type = DescriptorHeapType.RenderTargetView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.None;
            rtvHeap.Initialize(this, descriptorHeapDescription);
            fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);


            for (int i = 0; i < c_frameCount; i++)
            {
                ThrowIfFailed(device.CreateCommandAllocator(CommandListType.Direct, out ID3D12CommandAllocator commandAllocator));
                commandAllocators[i] = commandAllocator;
            }
            ThrowIfFailed(device.CreateFence(executeCount, FenceFlags.None, out fence));
            executeCount++;
        }

        public ID3D12GraphicsCommandList4 GetCommandList()
        {
            if (m_commandLists.Count > 0)
            {
                var commandList = m_commandLists[m_commandLists.Count - 1];
                m_commandLists.RemoveAt(m_commandLists.Count - 1);
                return commandList;
            }
            else
            {
                ID3D12GraphicsCommandList4 commandList;
                //ThrowIfFailed(GetD3DDevice()->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, GetCommandAllocator(), nullptr, IID_PPV_ARGS(&commandList)));
                ThrowIfFailed(device.CreateCommandList(0, CommandListType.Direct, GetCommandAllocator(), null, out commandList));
                NAME_D3D12_OBJECT(commandList);
                commandList.Close();
                return commandList;
            }
        }

        public void ReturnCommandList(ID3D12GraphicsCommandList4 commandList)
        {
            m_commandLists1.Add(commandList);
        }

        public ID3D12Resource GetRenderTarget()
        {
            return m_renderTargets[m_swapChain.GetCurrentBackBufferIndex()];
        }

        public void ResourceDelayRecycle(ID3D12Resource res)
        {
            if (res != null)
                m_recycleList.Add(new d3d12RecycleResource { m_recycleResource = res, m_removeFrame = m_currentFenceValue });
        }
        public void ResourceDelayRecycle(ID3D12PipelineState res2)
        {
            if (res2 != null)
                m_recycleList.Add(new d3d12RecycleResource { m_recycleResource = res2, m_removeFrame = m_currentFenceValue });
        }

        public void CreateWindowSizeDependentResources()
        {
            WaitForGpu();
            for (int n = 0; n < c_frameCount; n++)
            {
                m_renderTargets[n].Dispose();
            }
        }

        public void WaitForGpu()
        {
            // 在队列中安排信号命令。
            commandQueue.Signal(fence, m_currentFenceValue);

            // 等待跨越围栏。
            fence.SetEventOnCompletion(m_currentFenceValue, fenceEvent);
            fenceEvent.WaitOne();
            //WaitForSingleObjectEx(mfenceEvent, INFINITE, FALSE);

            Recycle();

            // 对当前帧递增围栏值。
            m_currentFenceValue++;
            m_fenceValues[executeIndex] = m_currentFenceValue;
        }

        void Recycle()
        {
            List<d3d12RecycleResource> temp = new List<d3d12RecycleResource>();
            for (int i = 0; i < m_recycleList.Count; i++)
                if (m_recycleList[i].m_removeFrame > m_fenceValues[executeIndex])
                    temp.Add(m_recycleList[i]);
                else
                    m_recycleList[i].m_recycleResource.Dispose();
            m_recycleList = temp;
            for (int i = 0; i < m_commandLists1.Count; i++)
                m_commandLists.Add(m_commandLists1[i]);
            m_commandLists1.Clear();
        }

        public void SetupSwapChain(object panel)
        {
            ComObject comObject = new ComObject(panel);
            ISwapChainPanelNative swapChainPanelNative = comObject.QueryInterface<ISwapChainPanelNative>();
            swapChainPanelNative.SetSwapChain(m_swapChain);

        }

        void NAME_D3D12_OBJECT(ID3D12Object d3D12Object)
        {

        }

        static bool CheckRayTracingSupport(ID3D12Device device)
        {
            FeatureDataD3D12Options5 featureDataD3D12Options5 = new FeatureDataD3D12Options5();
            return device.CheckFeatureSupport(Vortice.Direct3D12.Feature.Options5, ref featureDataD3D12Options5);
        }

        ID3D12CommandAllocator GetCommandAllocator() { return commandAllocators[executeIndex]; }

        public static void ThrowIfFailed(SharpGen.Runtime.Result hr)
        {
            if (hr != SharpGen.Runtime.Result.Ok)
                throw new NotImplementedException(hr.ToString());
        }
    }
}
