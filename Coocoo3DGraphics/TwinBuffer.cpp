#include "pch.h"
#include "TwinBuffer.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;

void TwinBuffer::Reload(int size)
{
	m_size = size;
}

void TwinBuffer::Initialize(DeviceResources^ deviceResources)
{
	Initialize(deviceResources, m_size);
}

void TwinBuffer::Initialize(DeviceResources^ deviceResources, int size)
{
	m_size = size;
	if (m_size < 64)
		m_size = 64;
	auto d3dDevice = deviceResources->GetD3DDevice();
	for (int i = 0; i < 2; i++)
	{
		DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
			&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
			D3D12_HEAP_FLAG_NONE,
			&CD3DX12_RESOURCE_DESC::Buffer(m_size, D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS),
			D3D12_RESOURCE_STATE_GENERIC_READ,
			nullptr,
			IID_PPV_ARGS(&m_buffer[i])));
		NAME_D3D12_OBJECT(m_buffer[i]);
		m_prevResourceState[i] = D3D12_RESOURCE_STATE_GENERIC_READ;
	}
}
