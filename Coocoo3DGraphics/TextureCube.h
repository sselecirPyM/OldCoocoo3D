#pragma once
#include "Interoperation/InteroperationTypes.h"
namespace Coocoo3DGraphics
{
	public ref class TextureCube sealed
	{
	public:
		void ReloadAsRTVUAV(int width, int height, int mipLevels, DxgiFormat format);
		void ReloadAsDSV(int width, int height, DxgiFormat format);
		property GraphicsObjectStatus Status;
		property UINT m_width;
		property UINT m_height;
		property UINT m_mipLevels;
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_texture;
		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_dsvHeap;
		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_rtvHeap;
		DXGI_FORMAT m_format;
		DXGI_FORMAT m_dsvFormat;
		DXGI_FORMAT m_rtvFormat;
		DXGI_FORMAT m_uavFormat;
		D3D12_RESOURCE_FLAGS m_resourceFlags;
		D3D12_RESOURCE_STATES prevResourceState;
	};
}
