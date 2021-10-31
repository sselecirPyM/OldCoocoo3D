#pragma once
#include "Interoperation/InteroperationTypes.h"
namespace Coocoo3DGraphics
{
	public ref class TextureCube sealed
	{
	public:
		void ReloadAsRTVUAV(int width, int height, int mipLevels, Format format);
		void ReloadAsDSV(int width, int height, Format format);
		property GraphicsObjectStatus Status;
		property UINT width;
		property UINT height;
		property UINT mipLevels;
	internal:
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
