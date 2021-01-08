#include "pch.h"
#include "ConstantBuffer.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

ConstantBuffer ^ ConstantBuffer::Load(DeviceResources^ deviceResources, int size)
{
	ConstantBuffer^ constBuffer = ref new ConstantBuffer();
	constBuffer->Initialize(deviceResources, size);
	return constBuffer;
}

void ConstantBuffer::Reload(DeviceResources ^ deviceResources, int size)
{
	Initialize(deviceResources, size);
}

void ConstantBuffer::Unload()
{
	m_constantBuffer.Reset();
	lastUpdateIndex = 0;
}

void ConstantBuffer::Initialize(DeviceResources ^ deviceResources, int size)
{
	Size = (size + 255) & ~255;
	//D3D11_BUFFER_DESC bufferDesc1 = CD3D11_BUFFER_DESC(size, D3D11_BIND_CONSTANT_BUFFER);
	//DX::ThrowIfFailed(deviceResources->GetD3DDevice()->CreateBuffer(&bufferDesc1, nullptr, &m_buffer));


	auto d3dDevice = deviceResources->GetD3DDevice();
	CD3DX12_HEAP_PROPERTIES uploadHeapProperties(D3D12_HEAP_TYPE_UPLOAD);
	CD3DX12_RESOURCE_DESC constantBufferDesc = CD3DX12_RESOURCE_DESC::Buffer(Coocoo3DGraphics::c_frameCount * Size);
	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&uploadHeapProperties,
		D3D12_HEAP_FLAG_NONE,
		&constantBufferDesc,
		D3D12_RESOURCE_STATE_GENERIC_READ,
		nullptr,
		IID_PPV_ARGS(&m_constantBuffer)));

	NAME_D3D12_OBJECT(m_constantBuffer);

	// 映射常量缓冲区。
	CD3DX12_RANGE readRange(0, 0);		// 我们不打算从 CPU 上的此资源中进行读取。
	DX::ThrowIfFailed(m_constantBuffer->Map(0, &readRange, reinterpret_cast<void**>(&m_mappedConstantBuffer)));
	ZeroMemory(m_mappedConstantBuffer, Coocoo3DGraphics::c_frameCount * Size);
}

D3D12_GPU_VIRTUAL_ADDRESS ConstantBuffer::GetCurrentVirtualAddress()
{
	return m_constantBuffer->GetGPUVirtualAddress() + Size * lastUpdateIndex;
}
