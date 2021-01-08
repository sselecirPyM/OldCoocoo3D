#pragma once
#include "DeviceResources.h"
namespace Coocoo3DGraphics
{
	public ref class ConstantBuffer sealed
	{
	public:
		static ConstantBuffer^ Load(DeviceResources^ deviceResources, int size);
		void Reload(DeviceResources^ deviceResources, int size);
		void Unload();
		property int Size;
	internal:
		void Initialize(DeviceResources^ deviceResources, int size);
		D3D12_GPU_VIRTUAL_ADDRESS GetCurrentVirtualAddress();
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_constantBuffer;
		int lastUpdateIndex = 0;
		byte* m_mappedConstantBuffer=nullptr;
		//Microsoft::WRL::ComPtr<ID3D11Buffer> m_buffer;
	};
}