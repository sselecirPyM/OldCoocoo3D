#include "pch.h"
#include "Texture2D.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;

void Texture2D::ReloadAsDepthStencil(int width, int height, Format format)
{
	this->width = width;
	this->height = height;
	if ((DXGI_FORMAT)format == DXGI_FORMAT_D24_UNORM_S8_UINT)
		this->m_format = DXGI_FORMAT_R24_UNORM_X8_TYPELESS;
	else if ((DXGI_FORMAT)format == DXGI_FORMAT_D32_FLOAT)
		this->m_format = DXGI_FORMAT_R32_FLOAT;
	this->m_dsvFormat = (DXGI_FORMAT)format;
	this->m_rtvFormat = DXGI_FORMAT_UNKNOWN;
	this->mipLevels = 1;
}

void Texture2D::ReloadAsRenderTarget(int width, int height, Format format)
{
	this->width = width;
	this->height = height;
	this->m_format = (DXGI_FORMAT)format;
	this->m_dsvFormat = DXGI_FORMAT_UNKNOWN;
	this->m_rtvFormat = (DXGI_FORMAT)format;
	this->m_uavFormat = DXGI_FORMAT_UNKNOWN;
	this->mipLevels = 1;
}

void Texture2D::ReloadAsRTVUAV(int width, int height, Format format)
{
	this->width = width;
	this->height = height;
	this->m_format = (DXGI_FORMAT)format;
	this->m_dsvFormat = DXGI_FORMAT_UNKNOWN;
	this->m_rtvFormat = (DXGI_FORMAT)format;
	this->m_uavFormat = (DXGI_FORMAT)format;
	this->mipLevels = 1;
}

void Texture2D::Reload(Texture2D^ texture)
{
	this->width = texture->width;
	this->height = texture->height;
	this->resource = texture->resource;
	this->mipLevels = texture->mipLevels;
	this->Status = texture->Status;
}

void Texture2D::Unload()
{
	width = 0;
	height = 0;
	resource.Reset();
	Status = GraphicsObjectStatus::unload;
}

Format Texture2D::GetFormat()
{
	if (m_dsvFormat != DXGI_FORMAT_UNKNOWN)
		return (Format)m_dsvFormat;
	return (Format)m_format;
}

void Texture2D::StateTransition(ID3D12GraphicsCommandList* commandList, D3D12_RESOURCE_STATES state)
{
	if (prevResourceState != state)
		commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(resource.Get(), prevResourceState, state));
	prevResourceState = state;
}
