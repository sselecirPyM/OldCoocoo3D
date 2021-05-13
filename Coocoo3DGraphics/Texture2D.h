#pragma once
#include "Interoperation/InteroperationTypes.h"
#include "ITexture.h"
namespace Coocoo3DGraphics
{
	public ref class Texture2D sealed :public ITexture2D
	{
	public:
		property GraphicsObjectStatus Status;
		property UINT m_width;
		property UINT m_height;
		property UINT m_mipLevels;

		void Reload(Texture2D^ texture);
		void Unload();
		virtual Platform::String^ ToString() override;
	internal:
		DXGI_FORMAT m_format;
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_texture;
	};
}
