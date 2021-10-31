#pragma once
#include "Interoperation/InteroperationTypes.h"
#include "GraphicsConstance.h"
#include "CBuffer.h"
#include "MeshBuffer.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Foundation;
	static const UINT c_graphicsPipelineHeapMaxCount = 65536;
	struct d3d12RecycleResource
	{
		Microsoft::WRL::ComPtr<ID3D12Object> m_recycleResource;
		UINT64 m_removeFrame;
	};
	// 控制所有 DirectX 设备资源。
	public ref class GraphicsDevice sealed
	{
	public:
		GraphicsDevice();
		void CreateDeviceResources();

		void SetSwapChainPanel(Windows::UI::Xaml::Controls::SwapChainPanel^ window, float width, float height, float scaleX, float scaleY, float dpi);
		void SetLogicalSize(Numerics::float2 logicalSize);
		// 呈现器目标的大小，以像素为单位。
		Numerics::float2 GetOutputSize() { return m_outputSize; }
		// 呈现器目标的大小，以 dip 为单位。
		Numerics::float2	GetLogicalSize() { return m_logicalSize; }
		//void SetDpi(float dpi);
		float GetDpi() { return m_dpi; }
		//void ValidateDevice();
		void Present(bool vsync);
		void RenderComplete();
		void WaitForGpu();
		bool IsRayTracingSupport();
		Format GetBackBufferFormat1();
		static UINT BitsPerPixel(Format format);
		Platform::String^ GetDeviceDescription();
		UINT64 GetDeviceVideoMemory();

		void InitializeCBuffer(CBuffer^ cBuffer, int size);
		void InitializeSBuffer(CBuffer^ sBuffer, int size);
		void InitializeMeshBuffer(MeshBuffer^ meshBuffer, int vertexCount);
	internal:
		bool						IsDeviceRemoved() const { return m_deviceRemoved; }

		// D3D 访问器。
		ID3D12Device2*				GetD3DDevice() const { return m_d3dDevice.Get(); }
		ID3D12Device5*				GetD3DDevice5() const { return m_d3dDevice5.Get(); }
		IDXGISwapChain3*			GetSwapChain() const { return m_swapChain.Get(); }
		ID3D12Resource*				GetRenderTarget() const { return m_renderTargets[m_swapChain->GetCurrentBackBufferIndex()].Get(); }
		ID3D12CommandQueue*			GetCommandQueue() const { return m_commandQueue.Get(); }
		ID3D12CommandAllocator*		GetCommandAllocator() const { return m_commandAllocators[m_executeIndex].Get(); }
		DXGI_FORMAT					GetBackBufferFormat() const { return m_backBufferFormat; }
		UINT						GetCurrentExecuteIndex() const { return m_executeIndex; }

		Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList4> GetCommandList();
		void ReturnCommandList(Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList4> commandList);

		CD3DX12_CPU_DESCRIPTOR_HANDLE GetRenderTargetView() const
		{
			return CD3DX12_CPU_DESCRIPTOR_HANDLE(m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), m_swapChain->GetCurrentBackBufferIndex(), m_rtvDescriptorSize);
		}

		static UINT BitsPerPixel(DXGI_FORMAT format);

		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_cbvSrvUavHeap;
		volatile UINT									m_cbvSrvUavHeapAllocCount;
		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_rtvHeap;
		volatile UINT									m_rtvHeapAllocCount;

		WCHAR m_deviceDescription[128];
		UINT64 m_deviceVideoMem;

		UINT64											m_currentFenceValue = 3;
		void ResourceDelayRecycle(Microsoft::WRL::ComPtr<ID3D12Object> res);
		std::vector<d3d12RecycleResource> m_recycleList;
		std::vector<Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList4>>	m_commandLists;
		std::vector<Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList4>>	m_commandLists1;

		// 缓存的设备属性。
		Numerics::float2						m_d3dRenderTargetSize;
		Numerics::float2						m_outputSize;
		Numerics::float2						m_logicalSize;
		float											m_dpi;
	private:
		void CreateWindowSizeDependentResources();
		void UpdateRenderTargetSize();
		void Recycle();


		// Direct3D 对象。
		Microsoft::WRL::ComPtr<ID3D12Device2>			m_d3dDevice;
		Microsoft::WRL::ComPtr<ID3D12Device5>			m_d3dDevice5;
		Microsoft::WRL::ComPtr<IDXGIFactory4>			m_dxgiFactory;
		Microsoft::WRL::ComPtr<IDXGISwapChain3>			m_swapChain;
		Microsoft::WRL::ComPtr<ID3D12Resource>			m_renderTargets[c_frameCount];
		Microsoft::WRL::ComPtr<ID3D12CommandQueue>		m_commandQueue;
		Microsoft::WRL::ComPtr<ID3D12CommandAllocator>	m_commandAllocators[c_frameCount];
		DXGI_FORMAT										m_backBufferFormat;
		UINT											m_rtvDescriptorSize;
		bool											m_deviceRemoved;

		bool											m_isRayTracingSupport;

		// CPU/GPU 同步。
		Microsoft::WRL::ComPtr<ID3D12Fence>				m_fence;
		HANDLE											m_fenceEvent;
		UINT											m_executeIndex;

		// 对窗口的缓存引用。
		Windows::UI::Xaml::Controls::SwapChainPanel^	m_swapChainPanel;


		float											m_compositionScaleX;
		float											m_compositionScaleY;
	};
}
