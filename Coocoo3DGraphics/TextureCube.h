#pragma once
#include "GraphicsObjectStatus.h"
namespace Coocoo3DGraphics
{
	public ref class TextureCube sealed
	{
	public:
		property GraphicsObjectStatus Status;
		property UINT m_width;
		property UINT m_height;
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_texture;
		UINT m_heapRefIndex;
		DXGI_FORMAT m_format;
		UINT m_mipLevels;
	};
}
