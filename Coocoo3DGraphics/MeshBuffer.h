#pragma once
namespace Coocoo3DGraphics
{
	public ref class MeshBuffer sealed
	{
	public:
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource> m_buffer;
		D3D12_RESOURCE_STATES m_prevState;
		int m_size;

		static const UINT c_vbvOffset = 64;
		static const UINT c_vbvStride = 64;
	};
}
