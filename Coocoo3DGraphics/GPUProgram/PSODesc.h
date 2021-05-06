#pragma once
#include "Interoperation/InteroperationTypes.h"
namespace Coocoo3DGraphics
{
	public enum struct ECullMode
	{
		notSpecial = 0,
		none = 1,
		front = 2,
		back = 3,
		NotSpecial = 0,
		None = 1,
		Front = 2,
		Back = 3,
	};
	public enum struct EBlendState
	{
		none = 0,
		alpha = 1,
		add = 2,
		None = 0,
		Alpha = 1,
		Add = 2,
	};
	public enum struct EInputLayout
	{
		mmd = 0,
		postProcess = 1,
		skinned = 2,
	};
	public enum struct ED3D12PrimitiveTopologyType
	{
		UNDEFINED = 0,
		POINT = 1,
		LINE = 2,
		TRIANGLE = 3,
		PATCH = 4
	};

	public value struct PSODesc sealed
	{
		EInputLayout inputLayout;
		EBlendState blendState;
		ECullMode cullMode;
		ED3D12PrimitiveTopologyType ptt;
		DxgiFormat rtvFormat;
		DxgiFormat dsvFormat;
		int renderTargetCount;
		int depthBias;
		float slopeScaledDepthBias;
		bool wireFrame;
		bool streamOutput;
	};
}
