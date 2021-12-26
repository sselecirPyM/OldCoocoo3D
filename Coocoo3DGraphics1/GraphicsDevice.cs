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
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public class GraphicsDevice
    {
        public class d3d12RecycleResource
        {
            public ID3D12Object m_recycleResource;
            public UInt64 m_removeFrame;
            public HeapType heapType;
            public int length;
        };

        public const int c_frameCount = 3;
        public const int CBVSRVUAVDescriptorCount = 65536;
        public ID3D12Device5 device;
        public IDXGIAdapter adapter;

        public DescriptorHeapX cbvsrvuavHeap;
        public DescriptorHeapX rtvHeap;

        internal RingBuffer superRingBuffer = new RingBuffer();

        internal ID3D12Resource scratchResource;

        string m_deviceDescription;
        UInt64 m_deviceVideoMem;

        internal UInt64 m_currentFenceValue = 3;

        internal List<d3d12RecycleResource> m_recycleList = new List<d3d12RecycleResource>();
        List<ID3D12GraphicsCommandList4> m_commandLists = new List<ID3D12GraphicsCommandList4>();
        List<ID3D12GraphicsCommandList4> m_commandLists1 = new List<ID3D12GraphicsCommandList4>();

        IntPtr hwnd;
        IDXGIFactory4 m_dxgiFactory;
        IDXGISwapChain3 m_swapChain;
        ID3D12Resource[] m_renderTargets = new ID3D12Resource[c_frameCount];
        ResourceStates[] renderTargetResourceStates = new ResourceStates[c_frameCount];
        public ID3D12CommandQueue commandQueue;
        ID3D12CommandAllocator[] commandAllocators = new ID3D12CommandAllocator[c_frameCount];

        Format m_backBufferFormat;

        bool m_isRayTracingSupport;

        ID3D12Fence fence;
        EventWaitHandle fenceEvent;
        public uint executeIndex = 0;
        public ulong executeCount = 3;

        public Vector2 m_d3dRenderTargetSize;
        public Vector2 m_outputSize;
        public Vector2 m_logicalSize;
        public Vector2 m_nativeOrientation;
        public Vector2 m_currentOrientation;

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
                case Format.Opaque420:
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

        public Vector2 GetOutputSize() => m_outputSize;

        public GraphicsDevice()
        {
            m_backBufferFormat = Format.R8G8B8A8_UNorm;
            CreateDeviceResource();
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
                adapter?.Dispose();
                var hr = m_dxgiFactory.EnumAdapters(index1, out adapter);
                if (hr == SharpGen.Runtime.Result.Ok)
                {
                    break;
                }
                index1++;
            }
            m_deviceDescription = adapter.Description.Description;
            m_deviceVideoMem = (ulong)(long)adapter.Description.DedicatedVideoMemory;
            device?.Dispose();
            ThrowIfFailed(D3D12.D3D12CreateDevice(this.adapter, out device));
            m_isRayTracingSupport = CheckRayTracingSupport(device);
            CommandQueueDescription commandQueuDdescription;
            commandQueuDdescription.Flags = CommandQueueFlags.None;
            commandQueuDdescription.Type = CommandListType.Direct;
            commandQueuDdescription.NodeMask = 0;
            commandQueuDdescription.Priority = 0;
            ThrowIfFailed(device.CreateCommandQueue(commandQueuDdescription, out commandQueue));

            DescriptorHeapDescription descriptorHeapDescription;
            descriptorHeapDescription.DescriptorCount = CBVSRVUAVDescriptorCount;
            descriptorHeapDescription.Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.ShaderVisible;
            descriptorHeapDescription.NodeMask = 0;
            cbvsrvuavHeap = new DescriptorHeapX();
            cbvsrvuavHeap.Initialize(this, descriptorHeapDescription);

            descriptorHeapDescription.DescriptorCount = 16;
            descriptorHeapDescription.Type = DescriptorHeapType.RenderTargetView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.None;
            rtvHeap = new DescriptorHeapX();
            rtvHeap.Initialize(this, descriptorHeapDescription);
            fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);


            for (int i = 0; i < c_frameCount; i++)
            {
                ThrowIfFailed(device.CreateCommandAllocator(CommandListType.Direct, out ID3D12CommandAllocator commandAllocator));
                commandAllocators[i] = commandAllocator;
            }
            ThrowIfFailed(device.CreateFence(executeCount, FenceFlags.None, out fence));
            superRingBuffer.Init(this, 134217728);
            executeCount++;
        }

        internal ID3D12GraphicsCommandList4 GetCommandList()
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
                ThrowIfFailed(device.CreateCommandList(0, CommandListType.Direct, GetCommandAllocator(), null, out commandList));
                commandList.Close();
                return commandList;
            }
        }

        internal void ReturnCommandList(ID3D12GraphicsCommandList4 commandList)
        {
            m_commandLists1.Add(commandList);
        }

        public ID3D12Resource GetRenderTarget(ID3D12GraphicsCommandList graphicsCommandList)
        {
            int index = m_swapChain.GetCurrentBackBufferIndex();
            var state = renderTargetResourceStates[index];
            var stateAfter = ResourceStates.RenderTarget;
            if (state != stateAfter)
            {
                graphicsCommandList.ResourceBarrierTransition(m_renderTargets[index], state, stateAfter);
                renderTargetResourceStates[index] = stateAfter;
            }
            return m_renderTargets[index];
        }

        public void EndRenderTarget(ID3D12GraphicsCommandList graphicsCommandList)
        {
            int index = m_swapChain.GetCurrentBackBufferIndex();
            var state = renderTargetResourceStates[index];
            var stateAfter = ResourceStates.Present;
            if (state != stateAfter)
            {
                graphicsCommandList.ResourceBarrierTransition(m_renderTargets[index], state, stateAfter);
                renderTargetResourceStates[index] = stateAfter;
            }
        }

        public CpuDescriptorHandle GetRenderTargetView(ID3D12GraphicsCommandList graphicsCommandList)
        {
            var handle = rtvHeap.heap.GetCPUDescriptorHandleForHeapStart() + rtvHeap.IncrementSize * m_swapChain.GetCurrentBackBufferIndex();
            device.CreateRenderTargetView(GetRenderTarget(graphicsCommandList), null, handle);
            return handle;
        }

        internal void ResourceDelayRecycle(ID3D12Object res)
        {
            if (res != null)
                m_recycleList.Add(new d3d12RecycleResource { m_recycleResource = res, m_removeFrame = m_currentFenceValue });
        }

        public void CreateWindowSizeDependentResources()
        {
            // 等到以前的所有 GPU 工作完成。
            WaitForGpu();

            // 清除特定于先前窗口大小的内容。
            for (int n = 0; n < c_frameCount; n++)
            {
                m_renderTargets[n]?.Dispose();
                m_renderTargets[n] = null;
                renderTargetResourceStates[n] = ResourceStates.Common;
            }

            UpdateRenderTargetSize();

            int backBufferWidth = (int)Math.Round(m_d3dRenderTargetSize.X);
            int backBufferHeight = (int)Math.Round(m_d3dRenderTargetSize.Y);
            if (m_swapChain != null)
            {
                // 如果交换链已存在，请调整其大小。
                Result hr = m_swapChain.ResizeBuffers(c_frameCount, backBufferWidth, backBufferHeight, m_backBufferFormat, SwapChainFlags.AllowTearing);

                ThrowIfFailed(hr);
            }
            else
            {
                // 否则，使用与现有 Direct3D 设备相同的适配器新建一个。
                SwapChainDescription1 swapChainDescription1 = new SwapChainDescription1();

                swapChainDescription1.Width = backBufferWidth;                      // 匹配窗口的大小。
                swapChainDescription1.Height = backBufferHeight;
                swapChainDescription1.Format = m_backBufferFormat;
                swapChainDescription1.Stereo = false;
                swapChainDescription1.SampleDescription.Count = 1;                         // 请不要使用多采样。
                swapChainDescription1.SampleDescription.Quality = 0;
                swapChainDescription1.Usage = Usage.RenderTargetOutput;
                swapChainDescription1.BufferCount = c_frameCount;                   // 使用三重缓冲最大程度地减小延迟。
                swapChainDescription1.SwapEffect = SwapEffect.FlipSequential;
                swapChainDescription1.Flags = SwapChainFlags.AllowTearing;
                swapChainDescription1.Scaling = Scaling.Stretch;
                swapChainDescription1.AlphaMode = AlphaMode.Ignore;

                var swapChain = m_dxgiFactory.CreateSwapChainForHwnd(commandQueue, hwnd, swapChainDescription1);
                m_swapChain?.Dispose();
                m_swapChain = swapChain.QueryInterface<IDXGISwapChain3>();
                swapChain.Dispose();
            }


            // 创建交换链后台缓冲区的呈现目标视图。
            {
                rtvHeap.GetTempCpuHandle();
                CpuDescriptorHandle rtvDescriptor = rtvHeap.heap.GetCPUDescriptorHandleForHeapStart();
                for (int n = 0; n < c_frameCount; n++)
                {
                    ThrowIfFailed(m_swapChain.GetBuffer(n, out m_renderTargets[n]));
                    device.CreateRenderTargetView(m_renderTargets[n], null, rtvDescriptor);
                    rtvDescriptor.Ptr += rtvHeap.IncrementSize;
                    m_renderTargets[n].Name = "backbuffer";
                }
            }
        }

        public void SetLogicalSize(Vector2 logicalSize)
        {
            if (m_logicalSize != logicalSize)
            {
                m_logicalSize = logicalSize;
                CreateWindowSizeDependentResources();
            }
        }


        internal void Present(bool vsync)
        {
            // 第一个参数指示 DXGI 进行阻止直到 VSync，这使应用程序
            // 在下一个 VSync 前进入休眠。这将确保我们不会浪费任何周期渲染
            // 从不会在屏幕上显示的帧。
            Result hr;
            if (vsync)
            {
                hr = m_swapChain.Present(1, 0);
            }
            else
            {
                hr = m_swapChain.Present(0, PresentFlags.AllowTearing);
            }

            ThrowIfFailed(hr);
            RenderComplete();
        }

        public void RenderComplete()
        {
            commandQueue.Signal(fence, m_currentFenceValue);

            // 提高帧索引。
            executeIndex = (executeIndex < (c_frameCount - 1)) ? (executeIndex + 1) : 0;

            // 检查下一帧是否准备好启动。
            if (fence.CompletedValue < m_currentFenceValue - c_frameCount + 1)
            {
                fence.SetEventOnCompletion(m_currentFenceValue - c_frameCount + 1, fenceEvent);
                fenceEvent.WaitOne();
            }
            Recycle();

            // 为下一帧设置围栏值。
            m_currentFenceValue++;
        }

        public void WaitForGpu()
        {
            // 在队列中安排信号命令。
            commandQueue.Signal(fence, m_currentFenceValue);

            // 等待跨越围栏。
            fence.SetEventOnCompletion(m_currentFenceValue, fenceEvent);
            fenceEvent.WaitOne();

            Recycle();

            // 对当前帧递增围栏值。
            m_currentFenceValue++;
        }

        void Recycle()
        {
            List<d3d12RecycleResource> temp = new List<d3d12RecycleResource>();
            for (int i = 0; i < m_recycleList.Count; i++)
                if (m_recycleList[i].m_removeFrame > m_currentFenceValue - c_frameCount + 1)
                    temp.Add(m_recycleList[i]);
                else
                    m_recycleList[i].m_recycleResource.Release();
            m_recycleList = temp;
            for (int i = 0; i < m_commandLists1.Count; i++)
                m_commandLists.Add(m_commandLists1[i]);
            m_commandLists1.Clear();
        }


        // 确定呈现器目标的尺寸及其是否将缩小。
        void UpdateRenderTargetSize()
        {
            // 计算必要的呈现目标大小(以像素为单位)。
            m_outputSize.X = m_logicalSize.X;
            m_outputSize.Y = m_logicalSize.Y;

            // 防止创建大小为零的 DirectX 内容。
            m_outputSize.X = Math.Max(m_outputSize.X, 1);
            m_outputSize.Y = Math.Max(m_outputSize.Y, 1);

            m_d3dRenderTargetSize.X = m_outputSize.X;
            m_d3dRenderTargetSize.Y = m_outputSize.Y;
        }

        public bool IsRayTracingSupport()
        {
            return m_isRayTracingSupport;
        }

        public string GetDeviceDescription()
        {
            return m_deviceDescription;
        }

        public ulong GetDeviceVideoMemory()
        {
            return m_deviceVideoMem;
        }

        public void SetSwapChainPanel(IntPtr hwnd, float width, float height)
        {
            this.hwnd = hwnd;

            m_logicalSize = new Vector2(width, height);

            CreateWindowSizeDependentResources();
        }

        static bool CheckRayTracingSupport(ID3D12Device device)
        {
            FeatureDataD3D12Options5 featureDataD3D12Options5 = new FeatureDataD3D12Options5();
            var checkResult = device.CheckFeatureSupport(Vortice.Direct3D12.Feature.Options5, ref featureDataD3D12Options5);
            if (featureDataD3D12Options5.RaytracingTier == RaytracingTier.NotSupported)
                return false;
            else
                return true;
        }

        public ID3D12CommandAllocator GetCommandAllocator() { return commandAllocators[executeIndex]; }

        public void InitializeCBuffer(CBuffer cBuffer, int size)
        {
            cBuffer.size = (size + 255) & ~255;
            cBuffer.Mutable = true;
        }

        public void InitializeSBuffer(CBuffer sBuffer, int size)
        {
            sBuffer.size = (size + 255) & ~255;

            var d3dDevice = device;
            ResourceDelayRecycle(sBuffer.resource);
            ThrowIfFailed(d3dDevice.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)sBuffer.size),
                ResourceStates.GenericRead,
                null,
                out sBuffer.resource));
            sBuffer.resource.Name = "sbuffer";
            sBuffer.Mutable = false;
        }
    }
}
