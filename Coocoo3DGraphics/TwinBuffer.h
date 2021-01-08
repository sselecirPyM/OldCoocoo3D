#pragma once
#include "DeviceResources.h"
namespace Coocoo3DGraphics
{
	public ref class TwinBuffer sealed
	{
	public:
		void Reload(int size);
		void Initialize(DeviceResources^ deviceResources);

		void Initialize(DeviceResources^ deviceResources, int size);
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource> m_buffer[2] = {};
		D3D12_RESOURCE_STATES m_prevResourceState[2];
		bool m_inverted = false;
		UINT m_size = 0;
	};
}
