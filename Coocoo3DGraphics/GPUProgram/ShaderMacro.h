#pragma once
namespace Coocoo3DGraphics
{
	public enum struct ShaderMacro
	{
		DEFINE_NONE,
		DEFINE_COO_SURFACE,
		DEFINE_COO_PARTICLE,
	};

	const D3D_SHADER_MACRO MACROS_DEFINE_COO_SURFACE[]
	{
		"COO_SURFACE","1",
		nullptr,nullptr,
	};

	const D3D_SHADER_MACRO MACROS_DEFINE_COO_PARTICLE[]
	{
		"COO_PARTICLE","1",
		nullptr,nullptr,
	};
}