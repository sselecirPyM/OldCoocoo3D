#include "pch.h"
#include "Uploader.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

void Uploader::Texture2DRaw(const Platform::Array<byte>^ rawData, Format format, int width, int height)
{
	Texture2DRaw(rawData, format, width, height, 1);
}

void Uploader::Texture2DRaw(const Platform::Array<byte>^ rawData, Format format, int width, int height, int mipLevel)
{
	this->width = width;
	this->height = height;
	this->m_format = (DXGI_FORMAT)format;
	this->mipLevels = mipLevel;
	this->m_data = std::vector<byte>();
	this->m_data.resize(rawData->Length);
	memcpy(m_data.data(), rawData->begin(), rawData->Length);
}

void Uploader::Texture2DPure(int width, int height, Windows::Foundation::Numerics::float4 color)
{
	this->width = width;
	this->height = height;
	this->m_format = DXGI_FORMAT_R32G32B32A32_FLOAT;
	this->mipLevels = 1;
	int count = width * height;
	this->m_data.resize(count * 16);

	void* p = m_data.data();
	float* p1 = (float*)p;
	for (int i = 0; i < count; i++) {
		*p1 = color.x;
		*(p1 + 1) = color.y;
		*(p1 + 2) = color.z;
		*(p1 + 3) = color.w;
		p1 += 4;
	}
}

void Uploader::TextureCubeRaw(const Platform::Array<byte>^ rawData, Format format, int width, int height, int mipLevel)
{
	this->width = width;
	this->height = height;
	this->m_format = (DXGI_FORMAT)format;
	this->mipLevels = mipLevel;
	this->m_data = std::vector<byte>();
	this->m_data.resize(rawData->Length);
	memcpy(this->m_data.data(), rawData->begin(), rawData->Length);
}

void Uploader::TextureCubePure(int width, int height, const Platform::Array<Windows::Foundation::Numerics::float4>^ color)
{
	this->width = width;
	this->height = height;
	this->m_format = DXGI_FORMAT_R32G32B32A32_FLOAT;
	this->mipLevels = 1;
	int count = width * height;
	if (count < 256)throw ref new Platform::NotImplementedException("Texture too small");
	this->m_data.resize(count * 16 * 6);

	void* p = this->m_data.data();
	float* p1 = (float*)p;
	for (int c = 0; c < 6; c++)
	{
		for (int i = 0; i < count; i++) {
			*p1 = color[c].x;
			*(p1 + 1) = color[c].y;
			*(p1 + 2) = color[c].z;
			*(p1 + 3) = color[c].w;
			p1 += 4;
		}
	}
}
