#pragma once
#include "Interoperation/InteroperationTypes.h"
namespace Coocoo3DGraphics
{
	public ref class Texture2D sealed
	{
	public:
		property GraphicsObjectStatus Status;
		property UINT m_width;
		property UINT m_height;
		property UINT m_mipLevels;
		void ReloadAsDepthStencil(int width, int height, DxgiFormat format);
		void ReloadAsRenderTarget(int width, int height, DxgiFormat format);
		void ReloadAsRTVUAV(int width, int height, DxgiFormat format);
		int GetWidth() { return m_width; }
		int GetHeight() { return m_height; }
		DxgiFormat GetFormat();
		void Reload(Texture2D^ texture);
		void Unload();
		virtual Platform::String^ ToString() override;
	internal:
		void StateTransition(ID3D12GraphicsCommandList* commandList, D3D12_RESOURCE_STATES state);
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
