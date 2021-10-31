#pragma once
#include "Interoperation/InteroperationTypes.h"
namespace Coocoo3DGraphics
{
	public ref class Texture2D sealed
	{
	public:
		property GraphicsObjectStatus Status;
		property UINT width;
		property UINT height;
		property UINT mipLevels;
		void ReloadAsDepthStencil(int width, int height, Format format);
		void ReloadAsRenderTarget(int width, int height, Format format);
		void ReloadAsRTVUAV(int width, int height, Format format);
		int GetWidth() { return width; }
		int GetHeight() { return height; }
		Format GetFormat();
		void Reload(Texture2D^ texture);
		void Unload();
	internal:
		void StateTransition(ID3D12GraphicsCommandList* commandList, D3D12_RESOURCE_STATES state);
		Microsoft::WRL::ComPtr<ID3D12Resource>			resource;
		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_dsvHeap;
		Microsoft::WRL::ComPtr<ID3D12DescriptorHeap>	m_rtvHeap;

		DXGI_FORMAT m_format;
		DXGI_FORMAT m_dsvFormat;
		DXGI_FORMAT m_rtvFormat;
		DXGI_FORMAT m_uavFormat;
		D3D12_RESOURCE_STATES prevResourceState;
	};
}
