#pragma once
#include "Interoperation/InteroperationTypes.h"
#include "ITexture.h"
namespace Coocoo3DGraphics
{
	public ref class TextureCube sealed : public ITextureCube
	{
	public:
		property GraphicsObjectStatus Status;
		property UINT m_width;
		property UINT m_height;
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_texture;
		DXGI_FORMAT m_format;
		UINT m_mipLevels;
	};
}
