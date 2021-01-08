#pragma once
#include "DeviceResources.h"
namespace Coocoo3DGraphics
{
	public ref class MMDMeshAppend sealed
	{
	public:
		void Reload(int count);
	internal:
		static const UINT c_vertexStride = 12;
		static const UINT c_bufferCount = 2;

		Microsoft::WRL::ComPtr<ID3D12Resource> m_vertexBufferPos[c_bufferCount];
		Microsoft::WRL::ComPtr<ID3D12Resource> m_vertexBufferPosUpload[c_bufferCount][c_frameCount];
		D3D12_VERTEX_BUFFER_VIEW m_vertexBufferPosViews[c_bufferCount];
		int lastUpdateIndexs[c_bufferCount] = {};
		int m_posCount;
	};
}
