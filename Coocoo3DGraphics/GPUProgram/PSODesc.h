#pragma once
#include "Interoperation/InteroperationTypes.h"
namespace Coocoo3DGraphics
{
	public enum struct CullMode
	{
		notSpecific = 0,
		none = 1,
		front = 2,
		back = 3,
		NotSpecific = 0,
		None = 1,
		Front = 2,
		Back = 3,
	};
	public enum struct BlendState
	{
		none = 0,
		alpha = 1,
		add = 2,
		None = 0,
		Alpha = 1,
		Add = 2,
	};
	public enum struct InputLayout
	{
		mmd = 0,
		postProcess = 1,
		skinned = 2,
		imgui = 3,
	};
	public enum struct PrimitiveTopologyType
	{
		Undefined = 0,
		Point = 1,
		Line = 2,
		Triangle = 3,
		Patch = 4
	};

	public value struct PSODesc sealed
	{
		InputLayout inputLayout;
		BlendState blendState;
		CullMode cullMode;
		PrimitiveTopologyType ptt;
		Format rtvFormat;
		Format dsvFormat;
		int renderTargetCount;
		int depthBias;
		float slopeScaledDepthBias;
		bool wireFrame;
		bool streamOutput;
	};
}
