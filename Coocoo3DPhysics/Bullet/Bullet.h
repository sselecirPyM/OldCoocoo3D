#pragma once
#include "btBulletDynamicsCommon.h"
#include "../IPhysicsAPI.h"
namespace Coocoo3DPhysics
{
	using namespace Windows::Foundation::Numerics;
	class Bullet :public IPhysicsAPI
	{
	public:
		virtual void Init();
		virtual void InitScene(void* _scene);
		virtual void SceneAddRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation, float3 scale, float mass, float restitution, float friction, float movDampling, float rotDampling, UINT8 shape, UINT8 type, UINT8 collisionGroup, UINT collisionMask);
		virtual void SceneAddJoint(void* _scene, void* _joint, float3 position, quaternion rotation, void* _rigidBody1, void* _rigidBody2,
			float3 PositionMinimum,
			float3 PositionMaximum,
			float3 RotationMinimum,
			float3 RotationMaximum,
			float3 PositionSpring,
			float3 RotationSpring);
		virtual void SceneResetRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation);
		virtual void SceneSimulate(void* _scene, double time);
		virtual void SceneFetchResults(void* _scene);
		virtual void SceneMoveRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation);
		virtual void SceneMoveRigidBody(void* _scene, void* _rigidBody, float4x4 matrix);
		virtual float3 SceneGetRigidBodyPosition(void* _scene, void* _rigidBody);
		virtual quaternion SceneGetRigidBodyRotation(void* _scene, void* _rigidBody);
		virtual float4x4 SceneGetRigidBodyTransform(void* _scene, void* _rigidBody);
		virtual void SceneSetGravitation(void* _scene, float3 gravitation);
		virtual void SceneRemoveRigidBody(void* _scene, void* _rigidBody);
		virtual void SceneRemoveJoint(void* _scene, void* _joint);

		btDefaultCollisionConfiguration* m_collisionConfiguration = nullptr;
		btCollisionDispatcher* m_dispatcher = nullptr;
		btBroadphaseInterface* m_broadphase = nullptr;
		btSequentialImpulseConstraintSolver* m_solver = nullptr;
	};
}
