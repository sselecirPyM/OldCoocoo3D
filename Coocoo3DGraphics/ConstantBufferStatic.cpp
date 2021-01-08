#include "pch.h"
#include "ConstantBufferStatic.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

ConstantBufferStatic^ ConstantBufferStatic::Load(DeviceResources^ deviceResources, int size)
{
	ConstantBufferStatic^ constBuffer = ref new ConstantBufferStatic();
	constBuffer->Initialize(deviceResources, size);
	return constBuffer;
}

void ConstantBufferStatic::Reload(DeviceResources^ deviceResources, int size)
{
	Initialize(deviceResources, size);
}

void ConstantBufferStatic::Unload()
{

	lastUpdateIndex = 0;
}

void ConstantBufferStatic::Initialize(DeviceResources^ deviceResources, int size)
{
	Size = (size + 255) & ~255;

	auto d3dDevice = deviceResources->GetD3DDevice();

	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
		D3D12_HEAP_FLAG_NONE,
		&CD3DX12_RESOURCE_DESC::Buffer(Size),
		D3D12_RESOURCE_STATE_GENERIC_READ,
		nullptr,
		IID_PPV_ARGS(&m_constantBuffer)));
	NAME_D3D12_OBJECT(m_constantBuffer);
	for (int i = 0; i < c_frameCount; i++)
	{
		DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
			&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_UPLOAD),
			D3D12_HEAP_FLAG_NONE,
			&CD3DX12_RESOURCE_DESC::Buffer(/*c_frameCount **/ Size),
			D3D12_RESOURCE_STATE_GENERIC_READ,
			nullptr,
			IID_PPV_ARGS(&m_constantBufferUploads[i])));
		NAME_D3D12_OBJECT(m_constantBufferUploads[i]);
	}
}

D3D12_GPU_VIRTUAL_ADDRESS ConstantBufferStatic::GetCurrentVirtualAddress()
{
	return m_constantBuffer->GetGPUVirtualAddress();
}
