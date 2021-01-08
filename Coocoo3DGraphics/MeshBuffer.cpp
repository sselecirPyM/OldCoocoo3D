#include "pch.h"
#include "MeshBuffer.h"
#include "DirectXHelper.h"

using namespace Coocoo3DGraphics;
void MeshBuffer::Reload(DeviceResources^ deviceResources, int vertexCount)
{
	m_size = vertexCount;
	auto d3dDevice = deviceResources->GetD3DDevice();
	CD3DX12_HEAP_PROPERTIES defaultHeapProperties(D3D12_HEAP_TYPE_DEFAULT);
	CD3DX12_RESOURCE_DESC vertexBufferDesc = CD3DX12_RESOURCE_DESC::Buffer(m_size * c_vbvStride + c_vbvOffset, D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS);
	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&defaultHeapProperties,
		D3D12_HEAP_FLAG_NONE,
		&vertexBufferDesc,
		D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER,
		nullptr,
		IID_PPV_ARGS(&m_buffer)));
	NAME_D3D12_OBJECT(m_buffer);

	m_prevState = D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER;
}
