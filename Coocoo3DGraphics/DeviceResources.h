#pragma once
#include "Interoperation/InteroperationTypes.h"
#include "GraphicsConstance.h"
#include "CBuffer.h"
#include "SBuffer.h"
#include "MeshBuffer.h"
namespace Coocoo3DGraphics
{
	static const UINT c_graphicsPipelineHeapMaxCount = 65536;
	struct d3d12RecycleResource
	{
		Microsoft::WRL::ComPtr<ID3D12Resource> m_recycleResource;
		UINT64 m_removeFrame;
	};
	// 控制所有 DirectX 设备资源。
	public ref class DeviceResources sealed
	{
	public:
		DeviceResources();
		void CreateDeviceResources();

		void SetSwapChainPanel(Windows::UI::Xaml::Controls::SwapChainPanel^ window);
		void SetLogicalSize(Windows::Foundation::Size logicalSize);
		// 呈现器目标的大小，以像素为单位。
		Windows::Foundation::Size	GetOutputSize() { return m_outputSize; }
		// 呈现器目标的大小，以 dip 为单位。
		Windows::Foundation::Size	GetLogicalSize() { return m_logicalSize; }
		void SetDpi(float dpi);
		float GetDpi() { return m_effectiveDpi; }
		void ValidateDevice();
		void Present(bool vsync);
		void WaitForGpu();
		bool IsRayTracingSupport();
		DxgiFormat GetBackBufferFormat1();
		static UINT BitsPerPixel(DxgiFormat format);
		Platform::String^ GetDeviceDescription();
		UINT64 GetDeviceVideoMemory();

		void InitializeCBuffer(CBuffer^ cBuffer, int size);
		void InitializeSBuffer(SBuffer^ sBuffer, int size);
		void InitializeMeshBuffer(MeshBuffer^ meshBuffer, int vertexCount);
	internal:
		bool						IsDeviceRemoved() const { return m_deviceRemoved; }

		// D3D 访问器。
		ID3D12Device2*				GetD3DDevice() const { return m_d3dDevice.Get(); }
		ID3D12Device5*				GetD3DDevice5() const { return m_d3dDevice5.Get(); }
		IDXGISwapChain3*			GetSwapChain() const { return m_swapChain.Get(); }
		ID3D12Resource*				GetRenderTarget() const { return m_renderTargets[m_frameIndex].Get(); }
		ID3D12CommandQueue*			GetCommandQueue() const { return m_commandQueue.Get(); }
		ID3D12CommandAllocator*		GetCommandAllocator() const { return m_commandAllocators[m_executeIndex].Get(); }
		DXGI_FORMAT					GetBackBufferFormat() const { return m_backBufferFormat; }
		D3D12_VIEWPORT				GetScreenViewport() const { return m_screenViewport; }
		UINT						GetCurrentFrameIndex() const { return m_frameIndex; }
		UINT						GetCurrentExecuteIndex() const { return m_executeIndex; }

		CD3DX12_CPU_DESCRIPTOR_HANDLE GetRenderTargetView() const
		{
			return CD3DX12_CPU_DESCRIPTOR_HANDLE(m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), m_frameIndex, m_rtvDescriptorSize);
		}

		static UINT BitsPerPixel(DXGI_FORMAT format);

		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_cbvSrvUavHeap;
		volatile UINT									m_cbvSrvUavHeapAllocCount;
		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_rtvHeap;
		volatile UINT									m_rtvHeapAllocCount;
		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_dsvHeap;
		volatile UINT									m_dsvHeapAllocCount;

		WCHAR m_deviceDescription[128];
		UINT64 m_deviceVideoMem;

		UINT64											m_fenceValues[c_frameCount];
		UINT64											m_currentFenceValue;
		void ResourceDelayRecycle(Microsoft::WRL::ComPtr<ID3D12Resource> res);
		std::vector<d3d12RecycleResource> m_recycleList;
	private:
		void CreateWindowSizeDependentResources();
		void UpdateRenderTargetSize();


		// Direct3D 对象。
		Microsoft::WRL::ComPtr<ID3D12Device2>			m_d3dDevice;
		Microsoft::WRL::ComPtr<ID3D12Device5>			m_d3dDevice5;
		Microsoft::WRL::ComPtr<IDXGIFactory4>			m_dxgiFactory;
		Microsoft::WRL::ComPtr<IDXGISwapChain3>			m_swapChain;
		Microsoft::WRL::ComPtr<ID3D12Resource>			m_renderTargets[c_frameCount];
		Microsoft::WRL::ComPtr<ID3D12CommandQueue>		m_commandQueue;
		Microsoft::WRL::ComPtr<ID3D12CommandAllocator>	m_commandAllocators[c_frameCount];
		DXGI_FORMAT										m_backBufferFormat;
		D3D12_VIEWPORT									m_screenViewport;
		UINT											m_rtvDescriptorSize;
		bool											m_deviceRemoved;

		bool											m_isRayTracingSupport;

		// CPU/GPU 同步。
		Microsoft::WRL::ComPtr<ID3D12Fence>				m_fence;
		HANDLE											m_fenceEvent;
		UINT											m_frameIndex;
		UINT											m_executeIndex;

		// 对窗口的缓存引用。
		Windows::UI::Xaml::Controls::SwapChainPanel^	m_swapChainPanel;

		// 缓存的设备属性。
		Windows::Foundation::Size						m_d3dRenderTargetSize;
		Windows::Foundation::Size						m_outputSize;
		Windows::Foundation::Size						m_logicalSize;
		Windows::Graphics::Display::DisplayOrientations	m_nativeOrientation;
		Windows::Graphics::Display::DisplayOrientations	m_currentOrientation;
		float											m_dpi;

		float											m_compositionScaleX;
		float											m_compositionScaleY;

		// 这是将向应用传回的 DPI。它考虑了应用是否支持高分辨率屏幕。
		float											m_effectiveDpi;
	};
}
