#include "pch.h"
#include "GeometryShader.h"
#include "DirectXHelper.h"
#include "TextUtil.h"
using namespace Coocoo3DGraphics;

bool GeometryShader::CompileReload1(IBuffer^ file1, Platform::String^ entryPoint, ShaderMacro macro)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(file1)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* sourceCode = nullptr;
	if (FAILED(bufferByteAccess->Buffer(&sourceCode)))return false;

	const D3D_SHADER_MACRO* macros = nullptr;
	if (macro == ShaderMacro::DEFINE_COO_SURFACE)macros = MACROS_DEFINE_COO_SURFACE;
	else if (macro == ShaderMacro::DEFINE_COO_PARTICLE)macros = MACROS_DEFINE_COO_PARTICLE;


	int bomOffset = CooBomTest(sourceCode);
	char* entryPointStr = CooGetMStr(entryPoint->Begin());
	HRESULT hr = D3DCompile(
		sourceCode + bomOffset,
		file1->Length - bomOffset,
		nullptr,
		macros,
		D3D_COMPILE_STANDARD_FILE_INCLUDE,
		entryPointStr,
		"gs_5_0",
		0,
		0,
		&byteCode,
		nullptr
	);
	CooFreeMStr(entryPointStr);
	if (FAILED(hr))
		return false;
	else
		return true;
}

void GeometryShader::Reload(IBuffer^ data)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(data)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* pData = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&pData));

	DX::ThrowIfFailed(D3DCreateBlob(data->Length, &byteCode));
	memcpy(byteCode->GetBufferPointer(), pData, data->Length);
}
