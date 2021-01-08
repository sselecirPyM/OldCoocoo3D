#pragma once
#include "UnionStructDefine.h"
namespace Coocoo3DPhysics
{
	public ref class Physics3DJoint sealed
	{
	public:
	internal:
		byte m_jointData[MAX_UNION_JOINT_STRUCTURE_SIZE];
		//physx::PxJoint* m_joint;
	};
}

