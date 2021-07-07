#include "pch.h"
#include "Uploader.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

void Uploader::Texture2DRaw(const Platform::Array<byte>^ rawData, DxgiFormat format, int width, int height)
{
	m_width = width;
	m_height = height;
	m_format = (DXGI_FORMAT)format;
	m_mipLevels = 1;
	m_data = std::vector<byte>();
	m_data.resize(rawData->Length);
	memcpy(m_data.data(), rawData->begin(), rawData->Length);
}

void Uploader::Texture2DRaw(const Platform::Array<byte>^ rawData, DxgiFormat format, int width, int height, int mipLevel)
{
	m_width = width;
	m_height = height;
	m_format = (DXGI_FORMAT)format;
	m_mipLevels = mipLevel;
	m_data = std::vector<byte>();
	m_data.resize(rawData->Length);
	memcpy(m_data.data(), rawData->begin(), rawData->Length);
}

void Uploader::Texture2DPure(int width, int height, Windows::Foundation::Numerics::float4 color)
{
	m_width = width;
	m_height = height;
	m_format = DXGI_FORMAT_R32G32B32A32_FLOAT;
	m_mipLevels = 1;
	int count = width * height;
	m_data.resize(count * 16);

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

void Uploader::TextureCubeRaw(const Platform::Array<byte>^ rawData, DxgiFormat format, int width, int height, int mipLevel)
{
	m_width = width;
	m_height = height;
	m_format = (DXGI_FORMAT)format;
	m_mipLevels = mipLevel;
	m_data = std::vector<byte>();
	m_data.resize(rawData->Length);
	memcpy(m_data.data(), rawData->begin(), rawData->Length);
}

void Uploader::TextureCubePure(int width, int height, const Platform::Array<Windows::Foundation::Numerics::float4>^ color)
{
	m_width = width;
	m_height = height;
	m_format = DXGI_FORMAT_R32G32B32A32_FLOAT;
	m_mipLevels = 1;
	int count = width * height;
	if (count < 256)throw ref new Platform::NotImplementedException("Texture too small");
	m_data.resize(count * 16 * 6);

	void* p = m_data.data();
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
