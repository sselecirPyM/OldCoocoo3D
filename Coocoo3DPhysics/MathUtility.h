#pragma once
#ifdef USE_PHYSX
#include "PxPhysicsAPI.h"
#endif
#include "btBulletDynamicsCommon.h"

namespace Util
{
#ifdef USE_PHYSX
	inline physx::PxQuat GetPxQuat(Windows::Foundation::Numerics::quaternion q)
	{
		return physx::PxQuat(q.x, q.y, q.z, q.w);
	}

	inline physx::PxVec3 GetPxVec3(Windows::Foundation::Numerics::float3 f)
	{
		return physx::PxVec3(f.x, f.y, f.z);
	}

	inline physx::PxTransform GetPxTransform(Windows::Foundation::Numerics::float3 f, Windows::Foundation::Numerics::quaternion q)
	{
		return physx::PxTransform(f.x, f.y, f.z, physx::PxQuat(q.x, q.y, q.z, q.w));
	}

	inline physx::PxTransform GetPxTransform(Windows::Foundation::Numerics::float4x4 mat)
	{
		return physx::PxTransform((const physx::PxMat44&)mat);
	}
#endif

	inline btQuaternion GetbtQuaternion(Windows::Foundation::Numerics::quaternion q)
	{
		return btQuaternion(q.x, q.y, q.z, q.w);
	}

	inline btVector3 GetbtVector3(Windows::Foundation::Numerics::float3 f)
	{
		return btVector3(f.x, f.y, f.z);
	}

	inline btTransform GetbtTransform(Windows::Foundation::Numerics::float4x4 mat)
	{
		btTransform mat1;
		mat1.setFromOpenGLMatrix((const btScalar*)&mat);
		return mat1;
	}

	inline btTransform GetbtTransform(Windows::Foundation::Numerics::float3 f, Windows::Foundation::Numerics::quaternion q)
	{
		return btTransform(btQuaternion(q.x, q.y, q.z, q.w), btVector3(f.x, f.y, f.z));
	}

	inline btTransform GetbtTransform2(Windows::Foundation::Numerics::float3 f, Windows::Foundation::Numerics::quaternion q, float worldScale)
	{
		return btTransform(btQuaternion(q.x, q.y, q.z, q.w), btVector3(f.x, f.y, f.z) * (1 / worldScale));
	}
}