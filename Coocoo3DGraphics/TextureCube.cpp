#include "pch.h"
#include "TextureCube.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Concurrency;


void TextureCube::ReloadAsRTVUAV(int width, int height, int mipLevels, Format format)
{
	this->width = width;
	this->height = height;
	this->mipLevels = mipLevels;
	this->m_format = (DXGI_FORMAT)format;
	this->m_dsvFormat = DXGI_FORMAT_UNKNOWN;
	this->m_rtvFormat = (DXGI_FORMAT)format;
	this->m_uavFormat = (DXGI_FORMAT)format;
}

void TextureCube::ReloadAsDSV(int width, int height, Format format)
{
	this->width = width;
	this->height = height;
	this->mipLevels = 1;
	if ((DXGI_FORMAT)format == DXGI_FORMAT_D24_UNORM_S8_UINT)
		this->m_format = DXGI_FORMAT_R24_UNORM_X8_TYPELESS;
	else if ((DXGI_FORMAT)format == DXGI_FORMAT_D32_FLOAT)
		this->m_format = DXGI_FORMAT_R32_FLOAT;
	this->m_dsvFormat = (DXGI_FORMAT)format;
	this->m_rtvFormat = DXGI_FORMAT_UNKNOWN;
	this->m_uavFormat = DXGI_FORMAT_UNKNOWN;
}