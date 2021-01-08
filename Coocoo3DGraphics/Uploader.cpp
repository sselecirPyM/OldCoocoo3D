#include "pch.h"
#include "Uploader.h"
#include "DirectXTex/DirectXTex.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

inline UINT CountMips(_In_ UINT width, _In_ UINT height) noexcept
{
	UINT mipLevels = 1;

	while (height > 1 || width > 1)
	{
		if (height > 1)
			height >>= 1;

		if (width > 1)
			width >>= 1;

		++mipLevels;
	}

	return mipLevels;
}

inline void _Fun4(Uploader^ tex, DirectX::TexMetadata& metaData, DirectX::ScratchImage& scratchImage, bool srgb, bool generateMips)
{
	DirectX::ScratchImage generatedMips;
	if (srgb)
		tex->m_format = DirectX::MakeSRGB(metaData.format);
	else
		tex->m_format = metaData.format;

	tex->m_width = metaData.width;
	tex->m_height = metaData.height;
	if (generateMips)
		tex->m_mipLevels = max(CountMips(tex->m_width, tex->m_height) - 6, 1);
	else
		tex->m_mipLevels = 1;

	if (tex->m_mipLevels > 1)
	{
		DX::ThrowIfFailed(DirectX::GenerateMipMaps(*scratchImage.GetImage(0, 0, 0), DirectX::TEX_FILTER_LINEAR | DirectX::TEX_FILTER_MIRROR | DirectX::TEX_FILTER_SEPARATE_ALPHA, tex->m_mipLevels, generatedMips));

		tex->m_data.resize(generatedMips.GetPixelsSize());
		memcpy(tex->m_data.data(), generatedMips.GetPixels(), generatedMips.GetPixelsSize());
	}
	else
	{
		tex->m_data.resize(scratchImage.GetPixelsSize());
		memcpy(tex->m_data.data(), scratchImage.GetPixels(), scratchImage.GetPixelsSize());
	}
}

inline void _Fun1(Uploader^ tex, DirectX::TexMetadata& metaData, DirectX::ScratchImage& scratchImage)
{
	DirectX::ScratchImage generatedMips;
	tex->m_format = DirectX::MakeSRGB(metaData.format);

	tex->m_mipLevels = max(CountMips(tex->m_width, tex->m_height) - 6, 1);

	DX::ThrowIfFailed(DirectX::GenerateMipMaps(*scratchImage.GetImage(0, 0, 0), DirectX::TEX_FILTER_LINEAR | DirectX::TEX_FILTER_MIRROR, tex->m_mipLevels, generatedMips));
	tex->m_data.resize(generatedMips.GetPixelsSize() * 6);

	//tex->m_mipLevels = max(generatedMips.GetImageCount() - 6, 1);
	//tex->m_mipLevels = 2;
	memcpy(tex->m_data.data(), generatedMips.GetPixels(), generatedMips.GetPixelsSize());
}

inline void _Fun2(Uploader^ tex, DirectX::TexMetadata& metaData, DirectX::ScratchImage& scratchImage, int index)
{
	DirectX::ScratchImage generatedMips;
	DX::ThrowIfFailed(DirectX::GenerateMipMaps(*scratchImage.GetImage(0, 0, 0), DirectX::TEX_FILTER_LINEAR | DirectX::TEX_FILTER_MIRROR, tex->m_mipLevels, generatedMips));

	DX::ThrowIfFalse((6 * generatedMips.GetPixelsSize()) == tex->m_data.size());
	memcpy(tex->m_data.data() + index * generatedMips.GetPixelsSize(), generatedMips.GetPixels(), generatedMips.GetPixelsSize());
}

void Uploader::Texture2D(IBuffer^ file1, bool srgb, bool generateMips)
{
	ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(file1)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* pixels = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&pixels));

	DirectX::TexMetadata metaData;
	DirectX::ScratchImage scratchImage;

	HRESULT hr1 = DirectX::GetMetadataFromTGAMemory(pixels, file1->Length, metaData);
	if (SUCCEEDED(hr1))
	{
		DX::ThrowIfFailed(DirectX::LoadFromTGAMemory(pixels, file1->Length, &metaData, scratchImage));
		_Fun4(this, metaData, scratchImage, srgb, generateMips);
		return;
	}

	HRESULT hr2 = DirectX::GetMetadataFromHDRMemory(pixels, file1->Length, metaData);
	if (SUCCEEDED(hr2))
	{
		DX::ThrowIfFailed(DirectX::LoadFromHDRMemory(pixels, file1->Length, &metaData, scratchImage));
		_Fun4(this, metaData, scratchImage, srgb, generateMips);
		return;
	}

	HRESULT hr3 = DirectX::GetMetadataFromWICMemory(pixels, file1->Length, DirectX::WIC_FLAGS_NONE, metaData);
	if (SUCCEEDED(hr3))
	{
		DX::ThrowIfFailed(DirectX::LoadFromWICMemory(pixels, file1->Length, DirectX::WIC_FLAGS_NONE, &metaData, scratchImage));
		_Fun4(this, metaData, scratchImage, srgb, generateMips);
		return;
	}
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

void Uploader::TextureCube(int width, int height, const Platform::Array<IBuffer^>^ files)
{
	m_width = width;
	m_height = height;

	{
		ComPtr<IBufferByteAccess> bufferByteAccess;
		reinterpret_cast<IInspectable*>(files[0])->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
		byte* pixels = nullptr;
		DX::ThrowIfFailed(bufferByteAccess->Buffer(&pixels));
		DirectX::TexMetadata metaData;
		DirectX::ScratchImage scratchImage;
		HRESULT hr1 = DirectX::GetMetadataFromTGAMemory(pixels, files[0]->Length, metaData);
		if (SUCCEEDED(hr1))
		{
			DX::ThrowIfFailed(DirectX::LoadFromTGAMemory(pixels, files[0]->Length, &metaData, scratchImage));
			_Fun1(this, metaData, scratchImage);
			goto SomeWhere1;
		}

		HRESULT hr2 = DirectX::GetMetadataFromHDRMemory(pixels, files[0]->Length, metaData);
		if (SUCCEEDED(hr2))
		{
			DX::ThrowIfFailed(DirectX::LoadFromHDRMemory(pixels, files[0]->Length, &metaData, scratchImage));
			_Fun1(this, metaData, scratchImage);
			goto SomeWhere1;
		}

		HRESULT hr3 = DirectX::GetMetadataFromWICMemory(pixels, files[0]->Length, DirectX::WIC_FLAGS_NONE, metaData);
		if (SUCCEEDED(hr3))
		{
			DX::ThrowIfFailed(DirectX::LoadFromWICMemory(pixels, files[0]->Length, DirectX::WIC_FLAGS_NONE, &metaData, scratchImage));
			_Fun1(this, metaData, scratchImage);
			goto SomeWhere1;
		}
	}
SomeWhere1:
	for (int i = 1; i < 6; i++)
	{
		IBuffer^ data;
		data = files[i];

		ComPtr<IBufferByteAccess> bufferByteAccess;
		reinterpret_cast<IInspectable*>(data)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
		byte* pixels = nullptr;
		DX::ThrowIfFailed(bufferByteAccess->Buffer(&pixels));

		DirectX::TexMetadata metaData;
		DirectX::ScratchImage scratchImage;
		HRESULT hr1 = DirectX::GetMetadataFromTGAMemory(pixels, data->Length, metaData);
		if (SUCCEEDED(hr1))
		{
			DX::ThrowIfFailed(DirectX::LoadFromTGAMemory(pixels, data->Length, &metaData, scratchImage));
			_Fun2(this, metaData, scratchImage, i);
			continue;
		}

		HRESULT hr2 = DirectX::GetMetadataFromHDRMemory(pixels, data->Length, metaData);
		if (SUCCEEDED(hr2))
		{
			DX::ThrowIfFailed(DirectX::LoadFromHDRMemory(pixels, data->Length, &metaData, scratchImage));
			_Fun2(this, metaData, scratchImage, i);
			continue;
		}

		HRESULT hr3 = DirectX::GetMetadataFromWICMemory(pixels, data->Length, DirectX::WIC_FLAGS_NONE, metaData);
		if (SUCCEEDED(hr3))
		{
			DX::ThrowIfFailed(DirectX::LoadFromWICMemory(pixels, data->Length, DirectX::WIC_FLAGS_NONE, &metaData, scratchImage));
			_Fun2(this, metaData, scratchImage, i);
			continue;
		}
	}
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
