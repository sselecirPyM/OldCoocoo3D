#pragma once
#include "ITexture.h"
#include "DeviceResources.h"
namespace Coocoo3DGraphics
{
	public ref class RenderTexture2D sealed :public IRenderTexture
	{
	public:
		void ReloadAsDepthStencil(int width, int height, DxgiFormat format);
		void ReloadAsRenderTarget(int width, int height, DxgiFormat format);
		void ReloadAsRTVUAV(int width, int height, DxgiFormat format);
	internal:

		Microsoft::WRL::ComPtr<ID3D12Resource>				m_texture;
		UINT m_srvRefIndex;
		UINT m_uavRefIndex;
		UINT m_dsvHeapRefIndex;
		UINT m_rtvHeapRefIndex;
		UINT m_width;
		UINT m_height;
		DXGI_FORMAT m_format;
		DXGI_FORMAT m_dsvFormat;
		DXGI_FORMAT m_rtvFormat;
		DXGI_FORMAT m_uavFormat;
		D3D12_RESOURCE_FLAGS m_resourceFlags;
		D3D12_RESOURCE_STATES prevResourceState;

	};
}
