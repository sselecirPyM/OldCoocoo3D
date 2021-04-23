#include "pch.h"
#include "DirectXHelper.h"
#include "GraphicsContext.h"
using namespace Coocoo3DGraphics;

inline void DX12UAVResourceBarrier(ID3D12GraphicsCommandList* commandList, ID3D12Resource* resource, D3D12_RESOURCE_STATES& stateRef)
{
	if (stateRef != D3D12_RESOURCE_STATE_UNORDERED_ACCESS)
		commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(resource, stateRef, D3D12_RESOURCE_STATE_UNORDERED_ACCESS));
	else
		commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::UAV(resource));
	stateRef = D3D12_RESOURCE_STATE_UNORDERED_ACCESS;
}

GraphicsContext^ GraphicsContext::Load(DeviceResources^ deviceResources)
{
	GraphicsContext^ graphicsContext = ref new GraphicsContext();
	graphicsContext->m_deviceResources = deviceResources;
	return graphicsContext;
}

void GraphicsContext::Reload(DeviceResources^ deviceResources)
{
	m_deviceResources = deviceResources;
	auto d3dDevice = deviceResources->GetD3DDevice();
	DX::ThrowIfFailed(d3dDevice->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, m_deviceResources->GetCommandAllocator(), nullptr, IID_PPV_ARGS(&m_commandList)));
	NAME_D3D12_OBJECT(m_commandList);
	DX::ThrowIfFailed(m_commandList->Close());
}

void GraphicsContext::ClearTextureRTV(RenderTextureCube^ texture)
{
	if (texture->prevResourceState != D3D12_RESOURCE_STATE_RENDER_TARGET)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_RENDER_TARGET));
	texture->prevResourceState = D3D12_RESOURCE_STATE_RENDER_TARGET;
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
	for (int i = 0; i < texture->m_mipLevels; i++)
	{
		CD3DX12_CPU_DESCRIPTOR_HANDLE cpuHandle(m_deviceResources->m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), texture->m_rtvHeapRefIndex + i, incrementSize);
		float clearColor[4] = {};
		m_commandList->ClearRenderTargetView(cpuHandle, clearColor, 0, nullptr);
	}
}

void GraphicsContext::SetPObject(ComputePO^ pObject)
{
	m_commandList->SetPipelineState(pObject->m_pipelineState.Get());
}

void GraphicsContext::SetPObject1(PObject^ pObject, int variantIndex)
{
	m_commandList->SetPipelineState(pObject->m_pipelineStates[variantIndex].Get());
}

void GraphicsContext::UpdateResource(CBuffer^ buffer, const Platform::Array<byte>^ data, UINT sizeInByte, int dataOffset)
{
	buffer->lastUpdateIndex = (buffer->lastUpdateIndex < (c_frameCount - 1)) ? (buffer->lastUpdateIndex + 1) : 0;
	memcpy(buffer->m_mappedConstantBuffer + buffer->lastUpdateIndex * buffer->m_size, data->begin() + dataOffset, sizeInByte);
}

void GraphicsContext::UpdateResource(CBuffer^ buffer, const Platform::Array<Windows::Foundation::Numerics::float4x4>^ data, UINT sizeInByte, int dataOffset)
{
	buffer->lastUpdateIndex = (buffer->lastUpdateIndex < (c_frameCount - 1)) ? (buffer->lastUpdateIndex + 1) : 0;
	memcpy(buffer->m_mappedConstantBuffer + buffer->lastUpdateIndex * buffer->m_size, data->begin() + dataOffset, sizeInByte);
}

inline void UpdateCBStaticResource(SBuffer^ buffer, ID3D12GraphicsCommandList* commandList, void* data, UINT sizeInByte, int dataOffset)
{
	buffer->lastUpdateIndex = (buffer->lastUpdateIndex < (c_frameCount - 1)) ? (buffer->lastUpdateIndex + 1) : 0;
	int lastUpdateIndex = buffer->lastUpdateIndex;

	CD3DX12_RANGE readRange(0, 0);
	void* mapped = nullptr;
	buffer->m_constantBufferUploads->Map(0, &readRange, &mapped);
	memcpy((byte*)mapped + buffer->m_size * lastUpdateIndex, (byte*)data + dataOffset, sizeInByte);
	buffer->m_constantBufferUploads->Unmap(0, nullptr);
	commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(buffer->m_constantBuffer.Get(), D3D12_RESOURCE_STATE_GENERIC_READ, D3D12_RESOURCE_STATE_COPY_DEST));
	commandList->CopyBufferRegion(buffer->m_constantBuffer.Get(), 0, buffer->m_constantBufferUploads.Get(), buffer->m_size * lastUpdateIndex, sizeInByte);
	commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(buffer->m_constantBuffer.Get(), D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_GENERIC_READ));
}

void GraphicsContext::UpdateResource(SBuffer^ buffer, const Platform::Array<byte>^ data, UINT sizeInByte, int dataOffset)
{
	UpdateCBStaticResource(buffer, m_commandList.Get(), data->begin(), sizeInByte, dataOffset);
}

void GraphicsContext::UpdateResource(SBuffer^ buffer, const Platform::Array<Windows::Foundation::Numerics::float4x4>^ data, UINT sizeInByte, int dataOffset)
{
	UpdateCBStaticResource(buffer, m_commandList.Get(), data->begin(), sizeInByte, dataOffset);
}

void GraphicsContext::UpdateResourceRegion(CBuffer^ buffer, UINT bufferDataOffset, const Platform::Array<byte>^ data, UINT sizeInByte, int dataOffset)
{
	memcpy(buffer->m_mappedConstantBuffer + buffer->lastUpdateIndex * buffer->m_size + bufferDataOffset, data->begin() + dataOffset, sizeInByte);
}

void GraphicsContext::UpdateResourceRegion(CBuffer^ buffer, UINT bufferDataOffset, const Platform::Array<Windows::Foundation::Numerics::float4x4>^ data, UINT sizeInByte, int dataOffset)
{
	memcpy(buffer->m_mappedConstantBuffer + buffer->lastUpdateIndex * buffer->m_size + bufferDataOffset, data->begin() + dataOffset, sizeInByte);
}

inline void _UpdateVerticesPos(ID3D12GraphicsCommandList* commandList, ID3D12Resource* resource, ID3D12Resource* uploaderResource, void* dataBegin, UINT dataLength, int offset)
{
	CD3DX12_RANGE readRange(0, 0);
	CD3DX12_RANGE writeRange(offset, offset + dataLength);
	void* pMapped = nullptr;
	DX::ThrowIfFailed(uploaderResource->Map(0, &readRange, &pMapped));
	memcpy((char*)pMapped + offset, dataBegin, dataLength);
	uploaderResource->Unmap(0, &writeRange);
	commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(resource, D3D12_RESOURCE_STATE_GENERIC_READ, D3D12_RESOURCE_STATE_COPY_DEST));
	commandList->CopyBufferRegion(resource, 0, uploaderResource, offset, dataLength);
	commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(resource, D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_GENERIC_READ));
}

void GraphicsContext::UpdateVerticesPos(MMDMeshAppend^ mesh, const Platform::Array<Windows::Foundation::Numerics::float3>^ verticeData, int index)
{
	mesh->lastUpdateIndexs[index]++;
	mesh->lastUpdateIndexs[index] = (mesh->lastUpdateIndexs[index] < c_frameCount) ? mesh->lastUpdateIndexs[index] : 0;
	_UpdateVerticesPos(m_commandList.Get(), mesh->m_vertexBufferPos[index].Get(), mesh->m_vertexBufferPosUpload[index].Get(), verticeData->begin(), verticeData->Length * 12, mesh->lastUpdateIndexs[index] * mesh->m_bufferSize);
}

void GraphicsContext::SetSRVT(ITexture2D^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	Texture2D^ tex2d = dynamic_cast<Texture2D^>(texture);
	RenderTexture2D^ rtex2d = dynamic_cast<RenderTexture2D^>(texture);
	if (texture != nullptr)
	{
		if (tex2d != nullptr)
		{
			CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), tex2d->m_heapRefIndex, incrementSize);
			m_commandList->SetGraphicsRootDescriptorTable(index, gpuHandle);
			tex2d->m_lastRefFrame = m_deviceResources->m_currentFenceValue;
		}
		else if (rtex2d != nullptr)
		{
			if (rtex2d->prevResourceState != D3D12_RESOURCE_STATE_GENERIC_READ)
				m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(rtex2d->m_texture.Get(), rtex2d->prevResourceState, D3D12_RESOURCE_STATE_GENERIC_READ));
			rtex2d->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;

			CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), rtex2d->m_srvRefIndex, incrementSize);
			m_commandList->SetGraphicsRootDescriptorTable(index, gpuHandle);
		}
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetSRVT(ITextureCube^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	TextureCube^ texc = dynamic_cast<TextureCube^>(texture);
	RenderTextureCube^ rtexc = dynamic_cast<RenderTextureCube^>(texture);
	if (texture != nullptr)
	{
		if (texc != nullptr)
		{
			CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texc->m_heapRefIndex, incrementSize);
			m_commandList->SetGraphicsRootDescriptorTable(index, gpuHandle);
		}
		else if (rtexc != nullptr)
		{
			if (rtexc->prevResourceState != D3D12_RESOURCE_STATE_GENERIC_READ)
				m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(rtexc->m_texture.Get(), rtexc->prevResourceState, D3D12_RESOURCE_STATE_GENERIC_READ));
			rtexc->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;

			CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), rtexc->m_srvRefIndex, incrementSize);
			m_commandList->SetGraphicsRootDescriptorTable(index, gpuHandle);
		}
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetSRVTFace(RenderTextureCube^ texture, int face, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		if (texture->prevResourceState != D3D12_RESOURCE_STATE_GENERIC_READ)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_GENERIC_READ));
		texture->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;

		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_srvRefIndex + face + 2, incrementSize);
		m_commandList->SetGraphicsRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetSRVTArray(RenderTextureCube^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		if (texture->prevResourceState != D3D12_RESOURCE_STATE_GENERIC_READ)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_GENERIC_READ));
		texture->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;

		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_srvRefIndex + 1, incrementSize);
		m_commandList->SetGraphicsRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetCBVR(CBuffer^ buffer, int index)
{
	m_commandList->SetGraphicsRootConstantBufferView(index, buffer->GetCurrentVirtualAddress());
}

void GraphicsContext::SetCBVR(SBuffer^ buffer, int index)
{
	m_commandList->SetGraphicsRootConstantBufferView(index, buffer->GetCurrentVirtualAddress());
}

void GraphicsContext::SetCBVR(CBuffer^ buffer, int offset256, int size256, int index)
{
	m_commandList->SetGraphicsRootConstantBufferView(index, buffer->GetCurrentVirtualAddress() + offset256 * 256);
}

void GraphicsContext::SetCBVR(SBuffer^ buffer, int offset256, int size256, int index)
{
	m_commandList->SetGraphicsRootConstantBufferView(index, buffer->GetCurrentVirtualAddress() + offset256 * 256);
}

void GraphicsContext::SetUAVT(RenderTexture2D^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		if (texture->prevResourceState != D3D12_RESOURCE_STATE_UNORDERED_ACCESS)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_UNORDERED_ACCESS));
		texture->prevResourceState = D3D12_RESOURCE_STATE_UNORDERED_ACCESS;

		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_uavRefIndex, incrementSize);
		m_commandList->SetGraphicsRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetComputeSRVT(Texture2D^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_heapRefIndex, incrementSize);
		m_commandList->SetComputeRootDescriptorTable(index, gpuHandle);
		texture->m_lastRefFrame = m_deviceResources->m_currentFenceValue;
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetComputeSRVT(TextureCube^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_heapRefIndex, incrementSize);
		m_commandList->SetComputeRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetComputeSRVT(RenderTexture2D^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		if (texture->prevResourceState != D3D12_RESOURCE_STATE_GENERIC_READ)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_GENERIC_READ));
		texture->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;

		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_srvRefIndex, incrementSize);
		m_commandList->SetComputeRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetComputeSRVT(RenderTextureCube^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		if (texture->prevResourceState != D3D12_RESOURCE_STATE_GENERIC_READ)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_GENERIC_READ));
		texture->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;

		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_srvRefIndex, incrementSize);
		m_commandList->SetComputeRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetComputeSRVTFace(RenderTextureCube^ texture, int face, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		if (texture->prevResourceState != D3D12_RESOURCE_STATE_GENERIC_READ)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_GENERIC_READ));
		texture->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;

		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_srvRefIndex + face + 2, incrementSize);
		m_commandList->SetComputeRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetComputeSRVR(TwinBuffer^ buffer, int bufIndex, int index)
{
	if (buffer->m_prevResourceState[bufIndex] != D3D12_RESOURCE_STATE_GENERIC_READ)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(buffer->m_buffer[bufIndex].Get(), buffer->m_prevResourceState[bufIndex], D3D12_RESOURCE_STATE_GENERIC_READ));
	buffer->m_prevResourceState[bufIndex] = D3D12_RESOURCE_STATE_GENERIC_READ;
	m_commandList->SetComputeRootShaderResourceView(index, buffer->m_buffer[bufIndex]->GetGPUVirtualAddress());
}

void GraphicsContext::SetComputeSRVR(MeshBuffer^ mesh, int startLocation, int index)
{
	if (mesh->m_prevState != D3D12_RESOURCE_STATE_GENERIC_READ)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_buffer.Get(), mesh->m_prevState, D3D12_RESOURCE_STATE_GENERIC_READ));
	mesh->m_prevState = D3D12_RESOURCE_STATE_GENERIC_READ;
	m_commandList->SetComputeRootShaderResourceView(index, mesh->m_buffer->GetGPUVirtualAddress() + startLocation * mesh->c_vbvStride);
}

void GraphicsContext::SetComputeSRVRIndex(MMDMesh^ mesh, int startLocation, int index)
{
	m_commandList->SetComputeRootShaderResourceView(index, mesh->m_indexBuffer->GetGPUVirtualAddress() + startLocation * sizeof(UINT));
}

void GraphicsContext::SetComputeCBVR(CBuffer^ buffer, int index)
{
	m_commandList->SetComputeRootConstantBufferView(index, buffer->GetCurrentVirtualAddress());
}

void GraphicsContext::SetComputeCBVR(SBuffer^ buffer, int index)
{
	m_commandList->SetComputeRootConstantBufferView(index, buffer->GetCurrentVirtualAddress());
}

void GraphicsContext::SetComputeCBVR(CBuffer^ buffer, int offset256, int size256, int index)
{
	m_commandList->SetComputeRootConstantBufferView(index, buffer->GetCurrentVirtualAddress() + offset256 * 256);
}

void GraphicsContext::SetComputeCBVR(SBuffer^ buffer, int offset256, int size256, int index)
{
	m_commandList->SetComputeRootConstantBufferView(index, buffer->GetCurrentVirtualAddress() + offset256 * 256);
}

void GraphicsContext::SetComputeUAVR(MeshBuffer^ mesh, int startLocation, int index)
{
	DX12UAVResourceBarrier(m_commandList.Get(), mesh->m_buffer.Get(), mesh->m_prevState);
	m_commandList->SetComputeRootUnorderedAccessView(index, mesh->m_buffer->GetGPUVirtualAddress() + startLocation * mesh->c_vbvStride);
}

void GraphicsContext::SetComputeUAVR(TwinBuffer^ buffer, int bufIndex, int index)
{
	DX12UAVResourceBarrier(m_commandList.Get(), buffer->m_buffer[bufIndex].Get(), buffer->m_prevResourceState[bufIndex]);
	m_commandList->SetComputeRootUnorderedAccessView(index, buffer->m_buffer[bufIndex]->GetGPUVirtualAddress());
}

void GraphicsContext::SetComputeUAVT(RenderTexture2D^ texture, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		DX12UAVResourceBarrier(m_commandList.Get(), texture->m_texture.Get(), texture->prevResourceState);

		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_uavRefIndex, incrementSize);
		m_commandList->SetComputeRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetComputeUAVT(RenderTextureCube^ texture, int mipIndex, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	if (texture != nullptr)
	{
		DX12UAVResourceBarrier(m_commandList.Get(), texture->m_texture.Get(), texture->prevResourceState);
		DX::ThrowIfFalse(mipIndex < texture->m_mipLevels);
		CD3DX12_GPU_DESCRIPTOR_HANDLE gpuHandle(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), texture->m_uavRefIndex + mipIndex, incrementSize);
		m_commandList->SetComputeRootDescriptorTable(index, gpuHandle);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetSOMesh(MeshBuffer^ mesh)
{
	if (mesh != nullptr)
	{
		if (mesh->m_prevState != D3D12_RESOURCE_STATE_COPY_DEST)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_buffer.Get(), mesh->m_prevState, D3D12_RESOURCE_STATE_COPY_DEST));
		mesh->m_prevState = D3D12_RESOURCE_STATE_COPY_DEST;
		D3D12_WRITEBUFFERIMMEDIATE_PARAMETER parameter = { mesh->m_buffer->GetGPUVirtualAddress() + mesh->m_size * mesh->c_vbvStride,0 };
		D3D12_WRITEBUFFERIMMEDIATE_MODE modes[] = { D3D12_WRITEBUFFERIMMEDIATE_MODE_MARKER_IN };
		m_commandList->WriteBufferImmediate(1, &parameter, modes);
		if (mesh->m_prevState != D3D12_RESOURCE_STATE_STREAM_OUT)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_buffer.Get(), mesh->m_prevState, D3D12_RESOURCE_STATE_STREAM_OUT));
		mesh->m_prevState = D3D12_RESOURCE_STATE_STREAM_OUT;

		D3D12_STREAM_OUTPUT_BUFFER_VIEW temp = {};
		temp.BufferLocation = mesh->m_buffer->GetGPUVirtualAddress();
		temp.BufferFilledSizeLocation = mesh->m_buffer->GetGPUVirtualAddress() + mesh->m_size * mesh->c_vbvStride;
		temp.SizeInBytes = mesh->m_size * mesh->c_vbvStride;

		m_commandList->SOSetTargets(0, 1, &temp);
	}
	else
	{
		throw ref new Platform::NotImplementedException();
	}
}

void GraphicsContext::SetSOMeshNone()
{
	D3D12_STREAM_OUTPUT_BUFFER_VIEW bufferView = {};
	m_commandList->SOSetTargets(0, 1, &bufferView);
}

void GraphicsContext::Draw(int vertexCount, int startVertexLocation)
{
	m_commandList->DrawInstanced(vertexCount, 1, startVertexLocation, 0);
}

void GraphicsContext::DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
{
	m_commandList->DrawIndexedInstanced(indexCount, 1, startIndexLocation, baseVertexLocation, 0);
}

void GraphicsContext::DrawIndexedInstanced(int indexCount, int startIndexLocation, int baseVertexLocation, int instanceCount, int startInstanceLocation)
{
	m_commandList->DrawIndexedInstanced(indexCount, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
}

void GraphicsContext::Dispatch(int x, int y, int z)
{
	m_commandList->Dispatch(x, y, z);
}

void GraphicsContext::DoRayTracing(RayTracingScene^ rayTracingScene, int width, int height, int raygenIndex)
{
	m_commandList->SetComputeRootShaderResourceView(1, rayTracingScene->m_topLevelAccelerationStructure[rayTracingScene->asLastUpdateIndex]->GetGPUVirtualAddress());

	int lastUpdateIndexRtpso = rayTracingScene->stLastUpdateIndex;
	D3D12_DISPATCH_RAYS_DESC dispatchDesc = {};
	dispatchDesc.HitGroupTable.StartAddress = rayTracingScene->m_hitGroupShaderTable[lastUpdateIndexRtpso]->GetGPUVirtualAddress();
	dispatchDesc.HitGroupTable.SizeInBytes = rayTracingScene->m_hitGroupShaderTable[lastUpdateIndexRtpso]->GetDesc().Width;
	dispatchDesc.HitGroupTable.StrideInBytes = rayTracingScene->m_hitGroupShaderTableStrideInBytes;
	dispatchDesc.MissShaderTable.StartAddress = rayTracingScene->m_missShaderTable[lastUpdateIndexRtpso]->GetGPUVirtualAddress();
	dispatchDesc.MissShaderTable.SizeInBytes = rayTracingScene->m_missShaderTable[lastUpdateIndexRtpso]->GetDesc().Width;
	dispatchDesc.MissShaderTable.StrideInBytes = rayTracingScene->m_missShaderTableStrideInBytes;
	dispatchDesc.RayGenerationShaderRecord.StartAddress = rayTracingScene->m_rayGenShaderTable[lastUpdateIndexRtpso]->GetGPUVirtualAddress() + raygenIndex * rayTracingScene->m_rayGenerateShaderTableStrideInBytes;
	dispatchDesc.RayGenerationShaderRecord.SizeInBytes = rayTracingScene->m_rayGenerateShaderTableStrideInBytes;
	dispatchDesc.Width = width;
	dispatchDesc.Height = height;
	dispatchDesc.Depth = 1;
	m_commandList->SetPipelineState1(rayTracingScene->m_dxrStateObject.Get());
	m_commandList->DispatchRays(&dispatchDesc);
}

void GraphicsContext::UploadMesh(MMDMesh^ mesh)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();

	CD3DX12_HEAP_PROPERTIES defaultHeapProperties(D3D12_HEAP_TYPE_DEFAULT);
	CD3DX12_HEAP_PROPERTIES uploadHeapProperties(D3D12_HEAP_TYPE_UPLOAD);
	CD3DX12_RESOURCE_DESC vertexBufferDesc = CD3DX12_RESOURCE_DESC::Buffer(mesh->m_verticeData->Length);
	CD3DX12_RESOURCE_DESC indexBufferDesc = CD3DX12_RESOURCE_DESC::Buffer(mesh->m_indexCount * mesh->c_indexStride);
	CD3DX12_RESOURCE_DESC uploaderBufferDesc = CD3DX12_RESOURCE_DESC::Buffer(mesh->m_verticeData->Length + mesh->m_indexCount * mesh->c_indexStride);

	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&uploadHeapProperties,
		D3D12_HEAP_FLAG_NONE,
		&uploaderBufferDesc,
		D3D12_RESOURCE_STATE_GENERIC_READ,
		nullptr,
		IID_PPV_ARGS(&mesh->m_bufferUpload)));
	NAME_D3D12_OBJECT(mesh->m_bufferUpload);
	UINT offset = 0;
	D3D12_RANGE readRange = CD3DX12_RANGE(0, 0);
	void* mapped = nullptr;
	DX::ThrowIfFailed(mesh->m_bufferUpload->Map(0, &readRange, &mapped));
	if (mesh->m_verticeData->Length > 0)
	{
		DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
			&defaultHeapProperties,
			D3D12_HEAP_FLAG_NONE,
			&vertexBufferDesc,
			D3D12_RESOURCE_STATE_COPY_DEST,
			nullptr,
			IID_PPV_ARGS(&mesh->m_vertexBuffer)));
		NAME_D3D12_OBJECT(mesh->m_vertexBuffer);

		memcpy(static_cast<byte*>(mapped) + offset, mesh->m_verticeData->begin(), mesh->m_verticeData->Length);
		m_commandList->CopyBufferRegion(mesh->m_vertexBuffer.Get(), 0, mesh->m_bufferUpload.Get(), offset, mesh->m_verticeData->Length);
		offset += mesh->m_verticeData->Length;

		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_vertexBuffer.Get(), D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_GENERIC_READ));
	}
	if (mesh->m_indexCount > 0)
	{
		DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
			&defaultHeapProperties,
			D3D12_HEAP_FLAG_NONE,
			&indexBufferDesc,
			D3D12_RESOURCE_STATE_COPY_DEST,
			nullptr,
			IID_PPV_ARGS(&mesh->m_indexBuffer)));
		NAME_D3D12_OBJECT(mesh->m_indexBuffer);

		memcpy(static_cast<byte*>(mapped) + offset, mesh->m_indexData->GetBufferPointer(), mesh->m_indexData->GetBufferSize());
		m_commandList->CopyBufferRegion(mesh->m_indexBuffer.Get(), 0, mesh->m_bufferUpload.Get(), offset, mesh->m_indexData->GetBufferSize());
		offset += mesh->m_indexData->GetBufferSize();

		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_indexBuffer.Get(), D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_INDEX_BUFFER));
	}
	mesh->m_bufferUpload->Unmap(0, nullptr);

	// 创建顶点/索引缓冲区视图。
	if (mesh->m_verticeData->Length > 0)
	{
		mesh->m_vertexBufferView.BufferLocation = mesh->m_vertexBuffer->GetGPUVirtualAddress();
		mesh->m_vertexBufferView.StrideInBytes = mesh->m_vertexStride;
		mesh->m_vertexBufferView.SizeInBytes = mesh->m_vertexStride * mesh->m_vertexCount;
	}
	if (mesh->m_indexCount > 0)
	{
		mesh->m_indexBufferView.BufferLocation = mesh->m_indexBuffer->GetGPUVirtualAddress();
		mesh->m_indexBufferView.SizeInBytes = mesh->m_indexCount * mesh->c_indexStride;
		mesh->m_indexBufferView.Format = DXGI_FORMAT_R32_UINT;
	}
}

void GraphicsContext::UploadMesh(MMDMeshAppend^ mesh, const Platform::Array<byte>^ data)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	CD3DX12_RESOURCE_DESC vertexBufferDesc = CD3DX12_RESOURCE_DESC::Buffer(mesh->m_bufferSize);
	CD3DX12_HEAP_PROPERTIES defaultHeapProperties(D3D12_HEAP_TYPE_DEFAULT);
	CD3DX12_HEAP_PROPERTIES uploadHeapProperties(D3D12_HEAP_TYPE_UPLOAD);
	for (int i = 0; i < mesh->c_bufferCount; i++)
	{
		DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
			&defaultHeapProperties,
			D3D12_HEAP_FLAG_NONE,
			&vertexBufferDesc,
			D3D12_RESOURCE_STATE_COPY_DEST,
			nullptr,
			IID_PPV_ARGS(&mesh->m_vertexBufferPos[i])));
		NAME_D3D12_OBJECT(mesh->m_vertexBufferPos[i]);
	}
	CD3DX12_RESOURCE_DESC uploadBufferDesc = CD3DX12_RESOURCE_DESC::Buffer(mesh->m_bufferSize * c_frameCount);
	for (int i = 0; i < mesh->c_bufferCount; i++)
	{
		DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
			&uploadHeapProperties,
			D3D12_HEAP_FLAG_NONE,
			&uploadBufferDesc,
			D3D12_RESOURCE_STATE_GENERIC_READ,
			nullptr,
			IID_PPV_ARGS(&mesh->m_vertexBufferPosUpload[i])));
		NAME_D3D12_OBJECT(mesh->m_vertexBufferPosUpload[i]);
	}
	for (int i = 0; i < mesh->c_bufferCount; i++)
	{
		D3D12_SUBRESOURCE_DATA vertexData = {};
		vertexData.pData = data->begin();
		vertexData.RowPitch = data->Length;
		vertexData.SlicePitch = vertexData.RowPitch;

		UpdateSubresources(m_commandList.Get(), mesh->m_vertexBufferPos[i].Get(), mesh->m_vertexBufferPosUpload[i].Get(), 0, 0, 1, &vertexData);
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_vertexBufferPos[i].Get(), D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_GENERIC_READ));
	}
	for (int i = 0; i < mesh->c_bufferCount; i++)
	{
		mesh->m_vertexBufferPosViews[i].BufferLocation = mesh->m_vertexBufferPos[i]->GetGPUVirtualAddress();
		mesh->m_vertexBufferPosViews[i].StrideInBytes = mesh->c_vertexStride;
		mesh->m_vertexBufferPosViews[i].SizeInBytes = mesh->c_vertexStride * mesh->m_posCount;
	}
}

void GraphicsContext::UploadTexture(TextureCube^ texture, Uploader^ uploader)
{
	texture->m_width = uploader->m_width;
	texture->m_height = uploader->m_height;
	texture->m_mipLevels = uploader->m_mipLevels;
	texture->m_format = uploader->m_format;

	auto d3dDevice = m_deviceResources->GetD3DDevice();
	D3D12_RESOURCE_DESC textureDesc = {};
	textureDesc.MipLevels = uploader->m_mipLevels;
	textureDesc.Format = uploader->m_format;
	textureDesc.Width = uploader->m_width;
	textureDesc.Height = uploader->m_height;
	textureDesc.Flags = D3D12_RESOURCE_FLAG_NONE;
	textureDesc.DepthOrArraySize = 6;
	textureDesc.SampleDesc.Count = 1;
	textureDesc.SampleDesc.Quality = 0;
	textureDesc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;

	int bitsPerPixel = DeviceResources::BitsPerPixel(textureDesc.Format);
	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
		D3D12_HEAP_FLAG_NONE,
		&textureDesc,
		D3D12_RESOURCE_STATE_COPY_DEST,
		nullptr,
		IID_PPV_ARGS(&texture->m_texture)));
	NAME_D3D12_OBJECT(texture->m_texture);

	Microsoft::WRL::ComPtr<ID3D12Resource> uploadBuffer;
	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_UPLOAD),
		D3D12_HEAP_FLAG_NONE,
		&CD3DX12_RESOURCE_DESC::Buffer(uploader->m_data.size()),
		D3D12_RESOURCE_STATE_GENERIC_READ,
		nullptr,
		IID_PPV_ARGS(&uploadBuffer)));
	m_deviceResources->m_recycleList.push_back(d3d12RecycleResource{ uploadBuffer,m_deviceResources->m_currentFenceValue });

	std::vector<D3D12_SUBRESOURCE_DATA>subresources;

	subresources.reserve(textureDesc.MipLevels * 6);

	D3D12_SUBRESOURCE_DATA textureDatas[6] = {};
	for (int i = 0; i < 6; i++)
	{
		UINT width = textureDesc.Width;
		UINT height = textureDesc.Height;
		byte* pdata = uploader->m_data.data() + (uploader->m_data.size() / 6) * i;
		for (int j = 0; j < textureDesc.MipLevels; j++)
		{
			D3D12_SUBRESOURCE_DATA subresourcedata = {};
			subresourcedata.pData = pdata;
			subresourcedata.RowPitch = width * bitsPerPixel / 8;
			subresourcedata.SlicePitch = width * height * bitsPerPixel / 8;
			pdata += width * height * bitsPerPixel / 8;

			subresources.push_back(subresourcedata);
			width /= 2;
			height /= 2;
		}
	}

	UpdateSubresources(m_commandList.Get(), texture->m_texture.Get(), uploadBuffer.Get(), 0, 0, textureDesc.MipLevels * 6, subresources.data());

	m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_GENERIC_READ));

	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	texture->m_heapRefIndex = _InterlockedIncrement(&m_deviceResources->m_cbvSrvUavHeapAllocCount) - 1;

	D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
	srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
	srvDesc.Format = textureDesc.Format;
	srvDesc.ViewDimension = D3D12_SRV_DIMENSION_TEXTURECUBE;
	srvDesc.TextureCube.MipLevels = textureDesc.MipLevels;
	CD3DX12_CPU_DESCRIPTOR_HANDLE handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), incrementSize * texture->m_heapRefIndex);
	d3dDevice->CreateShaderResourceView(texture->m_texture.Get(), &srvDesc, handle);
	texture->Status = GraphicsObjectStatus::loaded;
}

void GraphicsContext::UploadTexture(Texture2D^ texture, Uploader^ uploader)
{
	texture->m_width = uploader->m_width;
	texture->m_height = uploader->m_height;
	texture->m_mipLevels = uploader->m_mipLevels;
	texture->m_format = uploader->m_format;

	auto d3dDevice = m_deviceResources->GetD3DDevice();

	D3D12_RESOURCE_DESC textureDesc = {};
	textureDesc.MipLevels = uploader->m_mipLevels;
	textureDesc.Format = uploader->m_format;
	textureDesc.Width = uploader->m_width;
	textureDesc.Height = uploader->m_height;
	textureDesc.Flags = D3D12_RESOURCE_FLAG_NONE;
	textureDesc.DepthOrArraySize = 1;
	textureDesc.SampleDesc.Count = 1;
	textureDesc.SampleDesc.Quality = 0;
	textureDesc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;

	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
		D3D12_HEAP_FLAG_NONE,
		&textureDesc,
		D3D12_RESOURCE_STATE_COPY_DEST,
		nullptr,
		IID_PPV_ARGS(&texture->m_texture)));
	NAME_D3D12_OBJECT(texture->m_texture);

	Microsoft::WRL::ComPtr<ID3D12Resource> uploadBuffer;
	DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
		&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_UPLOAD),
		D3D12_HEAP_FLAG_NONE,
		&CD3DX12_RESOURCE_DESC::Buffer(uploader->m_data.size()),
		D3D12_RESOURCE_STATE_GENERIC_READ,
		nullptr,
		IID_PPV_ARGS(&uploadBuffer)));

	m_deviceResources->m_recycleList.push_back(d3d12RecycleResource{ uploadBuffer,m_deviceResources->m_currentFenceValue });

	std::vector<D3D12_SUBRESOURCE_DATA>subresources;
	subresources.reserve(textureDesc.MipLevels);

	byte* pdata = uploader->m_data.data();
	int bitsPerPixel = DeviceResources::BitsPerPixel(textureDesc.Format);
	UINT width = textureDesc.Width;
	UINT height = textureDesc.Height;
	for (int i = 0; i < textureDesc.MipLevels; i++)
	{
		D3D12_SUBRESOURCE_DATA subresourcedata = {};
		subresourcedata.pData = pdata;
		subresourcedata.RowPitch = width * bitsPerPixel / 8;
		subresourcedata.SlicePitch = width * height * bitsPerPixel / 8;
		pdata += width * height * bitsPerPixel / 8;

		subresources.push_back(subresourcedata);
		width /= 2;
		height /= 2;
	}

	UpdateSubresources(m_commandList.Get(), texture->m_texture.Get(), uploadBuffer.Get(), 0, 0, textureDesc.MipLevels, subresources.data());

	m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_GENERIC_READ));
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	texture->m_heapRefIndex = _InterlockedIncrement(&m_deviceResources->m_cbvSrvUavHeapAllocCount) - 1;
	D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
	srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
	srvDesc.Format = textureDesc.Format;
	srvDesc.ViewDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
	srvDesc.Texture2D.MipLevels = textureDesc.MipLevels;
	d3dDevice->CreateShaderResourceView(texture->m_texture.Get(), &srvDesc, CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), incrementSize * texture->m_heapRefIndex));
	texture->m_lastRefFrame = m_deviceResources->m_currentFenceValue;
	texture->Status = GraphicsObjectStatus::loaded;
}

void GraphicsContext::UpdateRenderTexture(IRenderTexture^ texture)
{
	RenderTexture2D^ tex2D = dynamic_cast<RenderTexture2D^>(texture);
	RenderTextureCube^ texCube = dynamic_cast<RenderTextureCube^>(texture);
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	if (tex2D != nullptr)
	{
		if (tex2D->m_texture == nullptr)
		{
			tex2D->m_srvRefIndex = _InterlockedIncrement(&m_deviceResources->m_cbvSrvUavHeapAllocCount) - 1;
			if (tex2D->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
			{
				tex2D->m_dsvHeapRefIndex = _InterlockedIncrement(&m_deviceResources->m_dsvHeapAllocCount) - 1;
			}
			if (tex2D->m_rtvFormat != DXGI_FORMAT_UNKNOWN)
			{
				tex2D->m_rtvHeapRefIndex = _InterlockedIncrement(&m_deviceResources->m_rtvHeapAllocCount) - 1;
			}
			if (tex2D->m_uavFormat != DXGI_FORMAT_UNKNOWN)
			{
				tex2D->m_uavRefIndex = _InterlockedIncrement(&m_deviceResources->m_cbvSrvUavHeapAllocCount) - 1;
			}
		}

		{
			D3D12_RESOURCE_DESC textureDesc = {};
			textureDesc.MipLevels = 1;
			if (tex2D->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
				textureDesc.Format = tex2D->m_dsvFormat;
			else
				textureDesc.Format = tex2D->m_format;
			textureDesc.Width = tex2D->m_width;
			textureDesc.Height = tex2D->m_height;
			textureDesc.Flags = tex2D->m_resourceFlags;
			textureDesc.DepthOrArraySize = 1;
			textureDesc.SampleDesc.Count = 1;
			textureDesc.SampleDesc.Quality = 0;
			textureDesc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;

			if (tex2D->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
			{
				CD3DX12_CLEAR_VALUE clearValue(tex2D->m_dsvFormat, 1.0f, 0);
				DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
					&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
					D3D12_HEAP_FLAG_NONE,
					&textureDesc,
					D3D12_RESOURCE_STATE_GENERIC_READ,
					&clearValue,
					IID_PPV_ARGS(&tex2D->m_texture)));
			}
			else
			{
				float color[] = { 0.0f,0.0f,0.0f,0.0f };
				CD3DX12_CLEAR_VALUE clearValue(tex2D->m_format, color);
				DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
					&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
					D3D12_HEAP_FLAG_NONE,
					&textureDesc,
					D3D12_RESOURCE_STATE_GENERIC_READ,
					&clearValue,
					IID_PPV_ARGS(&tex2D->m_texture)));
			}
			tex2D->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;
			NAME_D3D12_OBJECT(tex2D->m_texture);
		}
		D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
		srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
		srvDesc.Format = tex2D->m_format;
		srvDesc.ViewDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
		srvDesc.Texture2D.MipLevels = 1;

		UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
		CD3DX12_CPU_DESCRIPTOR_HANDLE handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), tex2D->m_srvRefIndex, incrementSize);
		d3dDevice->CreateShaderResourceView(tex2D->m_texture.Get(), &srvDesc, handle);
		if (tex2D->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
		{
			incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_DSV);
			handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_dsvHeap->GetCPUDescriptorHandleForHeapStart(), tex2D->m_dsvHeapRefIndex, incrementSize);
			d3dDevice->CreateDepthStencilView(tex2D->m_texture.Get(), nullptr, handle);
		}
		if (tex2D->m_rtvFormat != DXGI_FORMAT_UNKNOWN)
		{
			incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
			handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), tex2D->m_rtvHeapRefIndex, incrementSize);
			d3dDevice->CreateRenderTargetView(tex2D->m_texture.Get(), nullptr, handle);
		}
		if (tex2D->m_uavFormat != DXGI_FORMAT_UNKNOWN)
		{
			D3D12_UNORDERED_ACCESS_VIEW_DESC uavDesc = {};
			uavDesc.ViewDimension = D3D12_UAV_DIMENSION_TEXTURE2D;
			uavDesc.Format = tex2D->m_uavFormat;

			incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
			handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), tex2D->m_uavRefIndex, incrementSize);
			d3dDevice->CreateUnorderedAccessView(tex2D->m_texture.Get(), nullptr, &uavDesc, handle);
		}
	}
	else if (texCube != nullptr)
	{
		if (texCube->m_texture == nullptr)
		{
			texCube->m_srvRefIndex = InterlockedAdd((volatile LONG*)&m_deviceResources->m_cbvSrvUavHeapAllocCount, 8) - 8;
			if (texCube->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
			{
				texCube->m_dsvHeapRefIndex = InterlockedAdd((volatile LONG*)&m_deviceResources->m_dsvHeapAllocCount, 6) - 6;
			}
			if (texCube->m_rtvFormat != DXGI_FORMAT_UNKNOWN)
			{
				texCube->m_rtvHeapRefIndex = InterlockedAdd((volatile LONG*)&m_deviceResources->m_rtvHeapAllocCount, 6) - 6;
			}
			if (texCube->m_uavFormat != DXGI_FORMAT_UNKNOWN)
			{
				texCube->m_uavRefIndex = InterlockedAdd((volatile LONG*)&m_deviceResources->m_cbvSrvUavHeapAllocCount, texCube->m_mipLevels) - texCube->m_mipLevels;
			}
		}

		{
			D3D12_RESOURCE_DESC textureDesc = {};
			textureDesc.MipLevels = texCube->m_mipLevels;
			if (texCube->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
				textureDesc.Format = texCube->m_dsvFormat;
			else
				textureDesc.Format = texCube->m_format;
			textureDesc.Width = texCube->m_width;
			textureDesc.Height = texCube->m_height;
			textureDesc.Flags = texCube->m_resourceFlags;
			textureDesc.DepthOrArraySize = 6;
			textureDesc.SampleDesc.Count = 1;
			textureDesc.SampleDesc.Quality = 0;
			textureDesc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;

			if (texCube->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
			{
				CD3DX12_CLEAR_VALUE clearValue(texCube->m_dsvFormat, 1.0f, 0);
				DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
					&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
					D3D12_HEAP_FLAG_NONE,
					&textureDesc,
					D3D12_RESOURCE_STATE_GENERIC_READ,
					&clearValue,
					IID_PPV_ARGS(&texCube->m_texture)));
			}
			else
			{
				float color[] = { 0.0f,0.0f,0.0f,0.0f };
				CD3DX12_CLEAR_VALUE clearValue(texCube->m_format, color);
				DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
					&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_DEFAULT),
					D3D12_HEAP_FLAG_NONE,
					&textureDesc,
					D3D12_RESOURCE_STATE_GENERIC_READ,
					&clearValue,
					IID_PPV_ARGS(&texCube->m_texture)));
			}
			texCube->prevResourceState = D3D12_RESOURCE_STATE_GENERIC_READ;
			NAME_D3D12_OBJECT(texCube->m_texture);
		}
		UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
		{
			D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
			srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
			srvDesc.Format = texCube->m_format;
			srvDesc.ViewDimension = D3D12_SRV_DIMENSION_TEXTURECUBE;
			srvDesc.TextureCube.MipLevels = texCube->m_mipLevels;
			CD3DX12_CPU_DESCRIPTOR_HANDLE handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), texCube->m_srvRefIndex, incrementSize);
			d3dDevice->CreateShaderResourceView(texCube->m_texture.Get(), &srvDesc, handle);
		}
		{
			D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
			srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
			srvDesc.Format = texCube->m_format;
			srvDesc.ViewDimension = D3D12_SRV_DIMENSION_TEXTURE2DARRAY;
			srvDesc.Texture2DArray.ArraySize = 6;
			srvDesc.Texture2DArray.FirstArraySlice = 0;
			srvDesc.Texture2DArray.MipLevels = texCube->m_mipLevels;
			CD3DX12_CPU_DESCRIPTOR_HANDLE handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), texCube->m_srvRefIndex + 1, incrementSize);
			d3dDevice->CreateShaderResourceView(texCube->m_texture.Get(), &srvDesc, handle);
		}
		for (int i = 0; i < 6; i++)
		{
			D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
			srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
			srvDesc.Format = texCube->m_format;
			srvDesc.ViewDimension = D3D12_SRV_DIMENSION_TEXTURE2DARRAY;
			srvDesc.Texture2DArray.ArraySize = 1;
			srvDesc.Texture2DArray.FirstArraySlice = i;
			srvDesc.Texture2DArray.MipLevels = texCube->m_mipLevels;
			CD3DX12_CPU_DESCRIPTOR_HANDLE handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), texCube->m_srvRefIndex + i + 2, incrementSize);
			d3dDevice->CreateShaderResourceView(texCube->m_texture.Get(), &srvDesc, handle);
		}
		if (texCube->m_dsvFormat != DXGI_FORMAT_UNKNOWN)
		{
			for (int i = 0; i < 6; i++)
			{
				D3D12_DEPTH_STENCIL_VIEW_DESC dsvDesc = {};
				dsvDesc.ViewDimension = D3D12_DSV_DIMENSION_TEXTURE2DARRAY;
				dsvDesc.Texture2DArray.ArraySize = 1;
				dsvDesc.Texture2DArray.FirstArraySlice = i;
				incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_DSV);
				CD3DX12_CPU_DESCRIPTOR_HANDLE handle = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_dsvHeap->GetCPUDescriptorHandleForHeapStart(), texCube->m_dsvHeapRefIndex + i, incrementSize);
				d3dDevice->CreateDepthStencilView(texCube->m_texture.Get(), &dsvDesc, handle);
			}
		}
		if (texCube->m_rtvFormat != DXGI_FORMAT_UNKNOWN)
		{
			for (int i = 0; i < 6; i++)
			{
				D3D12_RENDER_TARGET_VIEW_DESC rtvDesc = {};
				rtvDesc.ViewDimension = D3D12_RTV_DIMENSION_TEXTURE2DARRAY;
				rtvDesc.Format = texCube->m_rtvFormat;
				rtvDesc.Texture2DArray.ArraySize = 1;
				rtvDesc.Texture2DArray.FirstArraySlice = i;

				incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
				CD3DX12_CPU_DESCRIPTOR_HANDLE handle2(m_deviceResources->m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), texCube->m_rtvHeapRefIndex + i, incrementSize);
				d3dDevice->CreateRenderTargetView(texCube->m_texture.Get(), &rtvDesc, handle2);
			}
		}
		if (texCube->m_uavFormat != DXGI_FORMAT_UNKNOWN)
		{
			for (int i = 0; i < texCube->m_mipLevels; i++)
			{
				D3D12_UNORDERED_ACCESS_VIEW_DESC uavDesc = {};
				uavDesc.ViewDimension = D3D12_UAV_DIMENSION_TEXTURE2DARRAY;
				uavDesc.Format = texCube->m_uavFormat;
				uavDesc.Texture2DArray.ArraySize = 6;
				uavDesc.Texture2DArray.MipSlice = i;

				incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
				CD3DX12_CPU_DESCRIPTOR_HANDLE handle3(m_deviceResources->m_cbvSrvUavHeap->GetCPUDescriptorHandleForHeapStart(), texCube->m_uavRefIndex + i, incrementSize);
				d3dDevice->CreateUnorderedAccessView(texCube->m_texture.Get(), nullptr, &uavDesc, handle3);
			}
		}
	}
}

void GraphicsContext::UpdateReadBackTexture(ReadBackTexture2D^ texture)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	for (int i = 0; i < c_frameCount; i++)
	{
		DX::ThrowIfFailed(d3dDevice->CreateCommittedResource(
			&CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_READBACK),
			D3D12_HEAP_FLAG_NONE,
			&CD3DX12_RESOURCE_DESC::Buffer(((texture->m_width + 63) & ~63) * texture->m_height * texture->m_bytesPerPixel),
			D3D12_RESOURCE_STATE_COPY_DEST,
			nullptr,
			IID_PPV_ARGS(&texture->m_textureReadBack[i])));
	}
}

void GraphicsContext::Copy(TextureCube^ source, RenderTextureCube^ dest)
{
	m_commandList->CopyResource(dest->m_texture.Get(), source->m_texture.Get());
}

void GraphicsContext::CopyBackBuffer(ReadBackTexture2D^ target, int index)
{
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	auto backBuffer = m_deviceResources->GetRenderTarget();
	m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(backBuffer, D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATE_COPY_SOURCE));

	D3D12_PLACED_SUBRESOURCE_FOOTPRINT footPrint = {};
	footPrint.Footprint.Width = target->m_width;
	footPrint.Footprint.Height = target->m_height;
	footPrint.Footprint.Depth = 1;
	footPrint.Footprint.RowPitch = (target->m_width * 4 + 255) & ~255;
	footPrint.Footprint.Format = m_deviceResources->GetBackBufferFormat();
	CD3DX12_TEXTURE_COPY_LOCATION Dst(target->m_textureReadBack[index].Get(), footPrint);
	CD3DX12_TEXTURE_COPY_LOCATION Src(backBuffer, 0);
	m_commandList->CopyTextureRegion(&Dst, 0, 0, 0, &Src, nullptr);

	m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(backBuffer, D3D12_RESOURCE_STATE_COPY_SOURCE, D3D12_RESOURCE_STATE_RENDER_TARGET));
}

void GraphicsContext::BuildBottomAccelerationStructures(RayTracingScene^ rayTracingAccelerationStructure, MeshBuffer^ mesh, MMDMesh^ indexBuffer, int vertexBegin, int indexBegin, int indexCount)
{
	int lastUpdateIndex = rayTracingAccelerationStructure->asLastUpdateIndex;

	auto m_dxrDevice = m_deviceResources->GetD3DDevice5();
	CD3DX12_HEAP_PROPERTIES defaultHeapProperties(D3D12_HEAP_TYPE_DEFAULT);

	D3D12_RAYTRACING_GEOMETRY_DESC geometryDesc = {};
	geometryDesc.Type = D3D12_RAYTRACING_GEOMETRY_TYPE_TRIANGLES;
	geometryDesc.Flags = D3D12_RAYTRACING_GEOMETRY_FLAG_NONE;
	geometryDesc.Triangles.VertexFormat = DXGI_FORMAT_R32G32B32_FLOAT;
	geometryDesc.Triangles.VertexBuffer.StartAddress = mesh->m_buffer->GetGPUVirtualAddress() + vertexBegin * mesh->c_vbvStride;
	geometryDesc.Triangles.VertexBuffer.StrideInBytes = mesh->c_vbvStride;
	geometryDesc.Triangles.VertexCount = indexBuffer->m_vertexCount;
	geometryDesc.Triangles.IndexFormat = DXGI_FORMAT_R32_UINT;
	geometryDesc.Triangles.IndexBuffer = indexBuffer->m_indexBuffer->GetGPUVirtualAddress() + indexBegin * sizeof(UINT);
	geometryDesc.Triangles.IndexCount = indexCount;

	D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS bottomLevelInputs = {};
	bottomLevelInputs.DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY;
	bottomLevelInputs.Flags = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_TRACE;
	bottomLevelInputs.NumDescs = 1;
	bottomLevelInputs.Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL;
	bottomLevelInputs.pGeometryDescs = &geometryDesc;
	D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO bottomLevelPrebuildInfo = {};
	m_dxrDevice->GetRaytracingAccelerationStructurePrebuildInfo(&bottomLevelInputs, &bottomLevelPrebuildInfo);
	DX::ThrowIfFalse(bottomLevelPrebuildInfo.ResultDataMaxSizeInBytes > 0);
	Microsoft::WRL::ComPtr<ID3D12Resource> asStruct;
	DX::ThrowIfFailed(m_dxrDevice->CreateCommittedResource(
		&defaultHeapProperties,
		D3D12_HEAP_FLAG_NONE,
		&CD3DX12_RESOURCE_DESC::Buffer(bottomLevelPrebuildInfo.ResultDataMaxSizeInBytes, D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS),
		D3D12_RESOURCE_STATE_RAYTRACING_ACCELERATION_STRUCTURE,
		nullptr,
		IID_PPV_ARGS(&asStruct)));
	NAME_D3D12_OBJECT(asStruct);

	D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC bottomLevelBuildDesc = {};
	bottomLevelBuildDesc.Inputs = bottomLevelInputs;
	bottomLevelBuildDesc.ScratchAccelerationStructureData = rayTracingAccelerationStructure->m_scratchResource[lastUpdateIndex]->GetGPUVirtualAddress();
	bottomLevelBuildDesc.DestAccelerationStructureData = asStruct->GetGPUVirtualAddress();
	rayTracingAccelerationStructure->m_bottomLevelASs[lastUpdateIndex].push_back(asStruct);

	if (mesh->m_prevState != D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_buffer.Get(), mesh->m_prevState, D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE));
	mesh->m_prevState = D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE;
	m_commandList->BuildRaytracingAccelerationStructure(&bottomLevelBuildDesc, 0, nullptr);
	m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::UAV(asStruct.Get()));
}

void GraphicsContext::BuildBASAndParam(RayTracingScene^ rayTracingAccelerationStructure, MeshBuffer^ mesh, MMDMesh^ indexBuffer, UINT instanceMask, int vertexBegin, int indexBegin, int indexCount, Texture2D^ diff, SBuffer^ mat, int offset256)
{
	BuildBottomAccelerationStructures(rayTracingAccelerationStructure, mesh, indexBuffer, vertexBegin, indexBegin, indexCount);
	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	void* pBase = (byte*)rayTracingAccelerationStructure->pArgumentCache + (RayTracingScene::c_argumentCacheStride * rayTracingAccelerationStructure->m_instanceDescs.size());

	CooRayTracingParamLocal1 params = {};
	params.cbv3 = mat->GetCurrentVirtualAddress() + 256 * offset256;
	params.srv0_1 = mesh->m_buffer.Get()->GetGPUVirtualAddress() + mesh->c_vbvStride * vertexBegin;
	params.srv1_1 = indexBuffer->m_indexBuffer->GetGPUVirtualAddress() + indexBegin * sizeof(UINT);
	params.srv2_1 = CD3DX12_GPU_DESCRIPTOR_HANDLE(m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart(), diff->m_heapRefIndex, incrementSize);
	*(static_cast<CooRayTracingParamLocal1*>(pBase)) = params;

	int index1 = rayTracingAccelerationStructure->m_instanceDescs.size();
	D3D12_RAYTRACING_INSTANCE_DESC instanceDesc = {};
	instanceDesc.Transform[0][0] = instanceDesc.Transform[1][1] = instanceDesc.Transform[2][2] = 1;
	instanceDesc.InstanceMask = instanceMask;
	instanceDesc.InstanceID = index1;
	instanceDesc.InstanceContributionToHitGroupIndex = index1 * rayTracingAccelerationStructure->m_rayTypeCount;
	instanceDesc.AccelerationStructure = rayTracingAccelerationStructure->m_bottomLevelASs[rayTracingAccelerationStructure->asLastUpdateIndex][index1]->GetGPUVirtualAddress();
	rayTracingAccelerationStructure->m_instanceDescs.push_back(instanceDesc);
	diff->m_lastRefFrame = m_deviceResources->m_currentFenceValue;
}

void GraphicsContext::BuildTopAccelerationStructures(RayTracingScene^ rayTracingAccelerationStructure)
{
	int lastUpdateIndex = rayTracingAccelerationStructure->asLastUpdateIndex;
	auto m_dxrDevice = m_deviceResources->GetD3DDevice5();
	CD3DX12_HEAP_PROPERTIES defaultHeapProperties(D3D12_HEAP_TYPE_DEFAULT);
	int meshCount = rayTracingAccelerationStructure->m_instanceDescs.size();

	D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_INPUTS topLevelInputs = {};
	topLevelInputs.Type = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL;
	topLevelInputs.DescsLayout = D3D12_ELEMENTS_LAYOUT_ARRAY;
	topLevelInputs.Flags = D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAG_PREFER_FAST_BUILD;
	topLevelInputs.NumDescs = meshCount;

	D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO topLevelPrebuildInfo = {};
	m_dxrDevice->GetRaytracingAccelerationStructurePrebuildInfo(&topLevelInputs, &topLevelPrebuildInfo);
	DX::ThrowIfFalse(topLevelPrebuildInfo.ResultDataMaxSizeInBytes > 0);

	DX::ThrowIfFailed(m_dxrDevice->CreateCommittedResource(
		&defaultHeapProperties,
		D3D12_HEAP_FLAG_NONE,
		&CD3DX12_RESOURCE_DESC::Buffer(topLevelPrebuildInfo.ResultDataMaxSizeInBytes, D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS),
		D3D12_RESOURCE_STATE_RAYTRACING_ACCELERATION_STRUCTURE,
		nullptr,
		IID_PPV_ARGS(&rayTracingAccelerationStructure->m_topLevelAccelerationStructure[lastUpdateIndex])));
	NAME_D3D12_OBJECT(rayTracingAccelerationStructure->m_topLevelAccelerationStructure[lastUpdateIndex]);
	rayTracingAccelerationStructure->m_topLevelAccelerationStructureSize[lastUpdateIndex] = topLevelPrebuildInfo.ResultDataMaxSizeInBytes;
	CD3DX12_RANGE readRange(0, 0);
	void* pMappedData;
	rayTracingAccelerationStructure->instanceDescs[lastUpdateIndex]->Map(0, &readRange, &pMappedData);
	memcpy(pMappedData, rayTracingAccelerationStructure->m_instanceDescs.data(), rayTracingAccelerationStructure->m_instanceDescs.size() * sizeof(D3D12_RAYTRACING_INSTANCE_DESC));
	rayTracingAccelerationStructure->instanceDescs[lastUpdateIndex]->Unmap(0, nullptr);

	topLevelInputs.InstanceDescs = rayTracingAccelerationStructure->instanceDescs[lastUpdateIndex]->GetGPUVirtualAddress();
	D3D12_BUILD_RAYTRACING_ACCELERATION_STRUCTURE_DESC topLevelBuildDesc = {};
	topLevelBuildDesc.Inputs = topLevelInputs;
	topLevelBuildDesc.DestAccelerationStructureData = rayTracingAccelerationStructure->m_topLevelAccelerationStructure[lastUpdateIndex]->GetGPUVirtualAddress();
	topLevelBuildDesc.ScratchAccelerationStructureData = rayTracingAccelerationStructure->m_scratchResource[lastUpdateIndex]->GetGPUVirtualAddress();

	m_commandList->BuildRaytracingAccelerationStructure(&topLevelBuildDesc, 0, nullptr);
}

void GraphicsContext::SetMesh(MMDMesh^ mesh)
{
	m_commandList->IASetPrimitiveTopology(mesh->m_primitiveTopology);
	m_commandList->IASetVertexBuffers(0, 1, &mesh->m_vertexBufferView);
	m_commandList->IASetIndexBuffer(&mesh->m_indexBufferView);
}

void GraphicsContext::SetMeshVertex(MMDMesh^ mesh)
{
	m_commandList->IASetPrimitiveTopology(mesh->m_primitiveTopology);
	m_commandList->IASetVertexBuffers(0, 1, &mesh->m_vertexBufferView);
}

void GraphicsContext::SetMeshVertex1(MMDMesh^ mesh)
{
	m_commandList->IASetPrimitiveTopology(mesh->m_primitiveTopology);
	m_commandList->IASetVertexBuffers(0, 1, &mesh->m_vertexBufferView);
}

void GraphicsContext::SetMeshVertex(MMDMeshAppend^ mesh)
{
	m_commandList->IASetVertexBuffers(1, 1, &mesh->m_vertexBufferPosViews[0]);
	m_commandList->IASetVertexBuffers(2, 1, &mesh->m_vertexBufferPosViews[1]);
}

void GraphicsContext::SetMeshIndex(MMDMesh^ mesh)
{
	m_commandList->IASetIndexBuffer(&mesh->m_indexBufferView);
}

void GraphicsContext::SetMesh(MeshBuffer^ mesh)
{
	D3D12_VERTEX_BUFFER_VIEW vbv = {};
	vbv.BufferLocation = mesh->m_buffer->GetGPUVirtualAddress();
	vbv.StrideInBytes = mesh->c_vbvStride;
	vbv.SizeInBytes = mesh->c_vbvStride * mesh->m_size;

	if (mesh->m_prevState != D3D12_RESOURCE_STATE_GENERIC_READ)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(mesh->m_buffer.Get(), mesh->m_prevState, D3D12_RESOURCE_STATE_GENERIC_READ));
	mesh->m_prevState = D3D12_RESOURCE_STATE_GENERIC_READ;
	m_commandList->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
	m_commandList->IASetVertexBuffers(0, 1, &vbv);
}

void GraphicsContext::SetDSV(RenderTexture2D^ texture, bool clear)
{
	D3D12_VIEWPORT viewport = CD3DX12_VIEWPORT(
		0.0f,
		0.0f,
		texture->m_width,
		texture->m_height
	);
	D3D12_RECT scissorRect = { 0, 0, static_cast<LONG>(viewport.Width), static_cast<LONG>(viewport.Height) };
	m_commandList->RSSetViewports(1, &viewport);
	m_commandList->RSSetScissorRects(1, &scissorRect);
	if (texture->prevResourceState != D3D12_RESOURCE_STATE_DEPTH_WRITE)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_DEPTH_WRITE));
	texture->prevResourceState = D3D12_RESOURCE_STATE_DEPTH_WRITE;

	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_DSV);

	D3D12_CPU_DESCRIPTOR_HANDLE depthStencilView = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_dsvHeap->GetCPUDescriptorHandleForHeapStart(), texture->m_dsvHeapRefIndex, incrementSize);
	if (clear)
		m_commandList->ClearDepthStencilView(depthStencilView, D3D12_CLEAR_FLAG_DEPTH | D3D12_CLEAR_FLAG_STENCIL, 1.0f, 0, 0, nullptr);
	m_commandList->OMSetRenderTargets(0, nullptr, false, &depthStencilView);
}

void GraphicsContext::SetDSV(RenderTextureCube^ texture, int face, bool clear)
{
	D3D12_VIEWPORT viewport = CD3DX12_VIEWPORT(
		0.0f,
		0.0f,
		texture->m_width,
		texture->m_height
	);
	D3D12_RECT scissorRect = { 0, 0, static_cast<LONG>(viewport.Width), static_cast<LONG>(viewport.Height) };
	m_commandList->RSSetViewports(1, &viewport);
	m_commandList->RSSetScissorRects(1, &scissorRect);
	if (texture->prevResourceState != D3D12_RESOURCE_STATE_DEPTH_WRITE)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(texture->m_texture.Get(), texture->prevResourceState, D3D12_RESOURCE_STATE_DEPTH_WRITE));
	texture->prevResourceState = D3D12_RESOURCE_STATE_DEPTH_WRITE;

	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_DSV);

	D3D12_CPU_DESCRIPTOR_HANDLE depthStencilView = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_dsvHeap->GetCPUDescriptorHandleForHeapStart(), texture->m_dsvHeapRefIndex + face, incrementSize);
	if (clear)
		m_commandList->ClearDepthStencilView(depthStencilView, D3D12_CLEAR_FLAG_DEPTH | D3D12_CLEAR_FLAG_STENCIL, 1.0f, 0, 0, nullptr);
	m_commandList->OMSetRenderTargets(0, nullptr, false, &depthStencilView);
}

void GraphicsContext::SetRTV(RenderTexture2D^ RTV, Windows::Foundation::Numerics::float4 color, bool clear)
{
	// 设置视区和剪刀矩形。
	D3D12_VIEWPORT viewport = CD3DX12_VIEWPORT(
		0.0f,
		0.0f,
		RTV->m_width,
		RTV->m_height
	);
	D3D12_RECT scissorRect = { 0, 0, static_cast<LONG>(viewport.Width), static_cast<LONG>(viewport.Height) };
	m_commandList->RSSetViewports(1, &viewport);
	m_commandList->RSSetScissorRects(1, &scissorRect);


	if (RTV->prevResourceState != D3D12_RESOURCE_STATE_RENDER_TARGET)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(RTV->m_texture.Get(), RTV->prevResourceState, D3D12_RESOURCE_STATE_RENDER_TARGET));
	RTV->prevResourceState = D3D12_RESOURCE_STATE_RENDER_TARGET;

	auto d3dDevice = m_deviceResources->GetD3DDevice();

	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
	D3D12_CPU_DESCRIPTOR_HANDLE renderTargetView = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), RTV->m_rtvHeapRefIndex, incrementSize);

	float _color[4] = { color.x,color.y,color.z,color.w };
	if (clear)
		m_commandList->ClearRenderTargetView(renderTargetView, _color, 0, nullptr);
	m_commandList->OMSetRenderTargets(1, &renderTargetView, false, nullptr);
}

void GraphicsContext::SetRTVDSV(RenderTexture2D^ RTV, RenderTexture2D^ DSV, Windows::Foundation::Numerics::float4 color, bool clearRTV, bool clearDSV)
{
	if ((RTV->m_width > DSV->m_width) || (RTV->m_height > DSV->m_height))
	{
		throw ref new Platform::NotImplementedException();
	}
	// 设置视区和剪刀矩形。
	D3D12_VIEWPORT viewport = CD3DX12_VIEWPORT(
		0.0f,
		0.0f,
		RTV->m_width,
		RTV->m_height
	);
	D3D12_RECT scissorRect = { 0, 0, static_cast<LONG>(viewport.Width), static_cast<LONG>(viewport.Height) };
	m_commandList->RSSetViewports(1, &viewport);
	m_commandList->RSSetScissorRects(1, &scissorRect);


	if (RTV->prevResourceState != D3D12_RESOURCE_STATE_RENDER_TARGET)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(RTV->m_texture.Get(), RTV->prevResourceState, D3D12_RESOURCE_STATE_RENDER_TARGET));
	RTV->prevResourceState = D3D12_RESOURCE_STATE_RENDER_TARGET;

	if (DSV->prevResourceState != D3D12_RESOURCE_STATE_DEPTH_WRITE)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(DSV->m_texture.Get(), DSV->prevResourceState, D3D12_RESOURCE_STATE_DEPTH_WRITE));
	DSV->prevResourceState = D3D12_RESOURCE_STATE_DEPTH_WRITE;

	auto d3dDevice = m_deviceResources->GetD3DDevice();

	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
	D3D12_CPU_DESCRIPTOR_HANDLE renderTargetView = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), RTV->m_rtvHeapRefIndex, incrementSize);
	incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_DSV);
	D3D12_CPU_DESCRIPTOR_HANDLE depthStencilView = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_dsvHeap->GetCPUDescriptorHandleForHeapStart(), DSV->m_dsvHeapRefIndex, incrementSize);

	float _color[4] = { color.x,color.y,color.z,color.w };
	if (clearRTV)
		m_commandList->ClearRenderTargetView(renderTargetView, _color, 0, nullptr);
	if (clearDSV)
		m_commandList->ClearDepthStencilView(depthStencilView, D3D12_CLEAR_FLAG_DEPTH | D3D12_CLEAR_FLAG_STENCIL, 1.0f, 0, 0, nullptr);
	m_commandList->OMSetRenderTargets(1, &renderTargetView, false, &depthStencilView);
}

void GraphicsContext::SetRTVDSV(const Platform::Array<RenderTexture2D^>^ RTVs, RenderTexture2D^ DSV, Windows::Foundation::Numerics::float4 color, bool clearRTV, bool clearDSV)
{
	if ((RTVs[0]->m_width > DSV->m_width) || (RTVs[0]->m_height > DSV->m_height))
	{
		throw ref new Platform::NotImplementedException();
	}
	// 设置视区和剪刀矩形。
	D3D12_VIEWPORT viewport = CD3DX12_VIEWPORT(
		0.0f,
		0.0f,
		RTVs[0]->m_width,
		RTVs[0]->m_height
	);
	D3D12_RECT scissorRect = { 0, 0, static_cast<LONG>(viewport.Width), static_cast<LONG>(viewport.Height) };
	m_commandList->RSSetViewports(1, &viewport);
	m_commandList->RSSetScissorRects(1, &scissorRect);

	for (int i = 0; i < RTVs->Length; i++)
	{
		auto RTV = RTVs[i];
		if (RTV->prevResourceState != D3D12_RESOURCE_STATE_RENDER_TARGET)
			m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(RTV->m_texture.Get(), RTV->prevResourceState, D3D12_RESOURCE_STATE_RENDER_TARGET));
		RTV->prevResourceState = D3D12_RESOURCE_STATE_RENDER_TARGET;
	}
	if (DSV->prevResourceState != D3D12_RESOURCE_STATE_DEPTH_WRITE)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(DSV->m_texture.Get(), DSV->prevResourceState, D3D12_RESOURCE_STATE_DEPTH_WRITE));
	DSV->prevResourceState = D3D12_RESOURCE_STATE_DEPTH_WRITE;

	auto d3dDevice = m_deviceResources->GetD3DDevice();

	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_DSV);
	D3D12_CPU_DESCRIPTOR_HANDLE depthStencilView = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_dsvHeap->GetCPUDescriptorHandleForHeapStart(), DSV->m_dsvHeapRefIndex, incrementSize);

	D3D12_CPU_DESCRIPTOR_HANDLE* rtvs1 = (D3D12_CPU_DESCRIPTOR_HANDLE*)malloc(sizeof(D3D12_CPU_DESCRIPTOR_HANDLE) * RTVs->Length);
	for (int i = 0; i < RTVs->Length; i++)
	{
		UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
		rtvs1[i] = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_rtvHeap->GetCPUDescriptorHandleForHeapStart(), RTVs[i]->m_rtvHeapRefIndex, incrementSize);
	}
	float _color[4] = { color.x,color.y,color.z,color.w };
	if (clearRTV)
		for (int i = 0; i < RTVs->Length; i++)
			m_commandList->ClearRenderTargetView(rtvs1[i], _color, 0, nullptr);
	if (clearDSV)
		m_commandList->ClearDepthStencilView(depthStencilView, D3D12_CLEAR_FLAG_DEPTH | D3D12_CLEAR_FLAG_STENCIL, 1.0f, 0, 0, nullptr);
	m_commandList->OMSetRenderTargets(RTVs->Length, rtvs1, false, &depthStencilView);
	free(rtvs1);
}

void GraphicsContext::SetRootSignature(GraphicsSignature^ rootSignature)
{
	m_commandList->SetGraphicsRootSignature(rootSignature->m_rootSignature.Get());
}

void GraphicsContext::SetRootSignatureCompute(GraphicsSignature^ rootSignature)
{
	m_commandList->SetComputeRootSignature(rootSignature->m_rootSignature.Get());
}

void GraphicsContext::SetRootSignatureRayTracing(RayTracingScene^ rayTracingScene)
{
	m_commandList->SetComputeRootSignature(rayTracingScene->m_rootSignatures[0].Get());
}

void GraphicsContext::ResourceBarrierScreen(D3D12ResourceStates before, D3D12ResourceStates after)
{
	CD3DX12_RESOURCE_BARRIER resourceBarrier =
		CD3DX12_RESOURCE_BARRIER::Transition(m_deviceResources->GetRenderTarget(), (D3D12_RESOURCE_STATES)before, (D3D12_RESOURCE_STATES)after);
	m_commandList->ResourceBarrier(1, &resourceBarrier);
}

void GraphicsContext::SetRenderTargetScreen(Windows::Foundation::Numerics::float4 color, RenderTexture2D^ DSV, bool clearScreen, bool clearDSV)
{
	if ((m_deviceResources->GetOutputSize().Width > DSV->m_width) || (m_deviceResources->GetOutputSize().Height > DSV->m_height))
	{
		throw ref new Platform::NotImplementedException();
	}
	if (DSV->prevResourceState != D3D12_RESOURCE_STATE_DEPTH_WRITE)
		m_commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(DSV->m_texture.Get(), DSV->prevResourceState, D3D12_RESOURCE_STATE_DEPTH_WRITE));
	DSV->prevResourceState = D3D12_RESOURCE_STATE_DEPTH_WRITE;

	auto d3dDevice = m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_DSV);
	D3D12_CPU_DESCRIPTOR_HANDLE depthStencilView = CD3DX12_CPU_DESCRIPTOR_HANDLE(m_deviceResources->m_dsvHeap->GetCPUDescriptorHandleForHeapStart(), DSV->m_dsvHeapRefIndex, incrementSize);

	// 设置视区和剪刀矩形。
	D3D12_VIEWPORT viewport = m_deviceResources->GetScreenViewport();
	D3D12_RECT scissorRect = { 0, 0, static_cast<LONG>(viewport.Width), static_cast<LONG>(viewport.Height) };
	m_commandList->RSSetViewports(1, &viewport);
	m_commandList->RSSetScissorRects(1, &scissorRect);


	float _color[4] = { color.x,color.y,color.z,color.w };
	D3D12_CPU_DESCRIPTOR_HANDLE renderTargetView = m_deviceResources->GetRenderTargetView();
	if (clearScreen)
		m_commandList->ClearRenderTargetView(renderTargetView, _color, 0, nullptr);
	if (clearDSV)
		m_commandList->ClearDepthStencilView(depthStencilView, D3D12_CLEAR_FLAG_DEPTH, 1.0f, 0, 0, nullptr);
	m_commandList->OMSetRenderTargets(1, &renderTargetView, false, &depthStencilView);
}

void GraphicsContext::BeginAlloctor(DeviceResources^ deviceResources)
{
	DX::ThrowIfFailed(deviceResources->GetCommandAllocator()->Reset());
}

void GraphicsContext::SetDescriptorHeapDefault()
{
	ID3D12DescriptorHeap* heaps[] = { m_deviceResources->m_cbvSrvUavHeap.Get() };
	m_commandList->SetDescriptorHeaps(_countof(heaps), heaps);
}

void GraphicsContext::BeginCommand()
{
	DX::ThrowIfFailed(m_commandList->Reset(m_deviceResources->GetCommandAllocator(), nullptr));
}

void GraphicsContext::EndCommand()
{
	DX::ThrowIfFailed(m_commandList->Close());
}

void GraphicsContext::BeginEvent()
{
	PIXBeginEvent(m_commandList.Get(), 0, L"Draw");
}

void GraphicsContext::EndEvent()
{
	PIXEndEvent(m_commandList.Get());
}

void GraphicsContext::Execute()
{
	ID3D12CommandList* ppCommandLists[] = { m_commandList.Get() };
	m_deviceResources->GetCommandQueue()->ExecuteCommandLists(_countof(ppCommandLists), ppCommandLists);
}
