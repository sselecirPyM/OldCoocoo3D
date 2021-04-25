#pragma once
#include "ITexture.h"
#include "Interoperation/InteroperationTypes.h"
namespace Coocoo3DGraphics
{
	public ref class RenderTexture2D sealed :public IRenderTexture, public ITexture2D
	{
	public:
		void ReloadAsDepthStencil(int width, int height, DxgiFormat format);
		void ReloadAsRenderTarget(int width, int height, DxgiFormat format);
		void ReloadAsRTVUAV(int width, int height, DxgiFormat format);
		DxgiFormat GetFormat();
		int GetWidth() { return m_width; }
		int GetHeight() { return m_height; }
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
