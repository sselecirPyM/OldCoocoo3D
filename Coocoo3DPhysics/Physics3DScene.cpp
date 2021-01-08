#include "pch.h"
#include "Physics3DScene.h"
#include "MathUtility.h"
using namespace Coocoo3DPhysics;

Physics3DScene^ Physics3DScene::Load(Physics3D^ physics3D)
{
	Physics3DScene^ physics3DScene = ref new Physics3DScene();
	physics3DScene->Reload(physics3D);
	return physics3DScene;
}

void Physics3DScene::Reload(Physics3D^ physics3D)
{
	m_sdkRef = physics3D->m_sdkRef;
	physics3D->m_sdkRef->InitScene(m_sceneData);
}

void Physics3DScene::AddRigidBody(Physics3DRigidBody^ rigidBody, float3 position, quaternion rotation, float3 scale, float mass, float restitution, float friction, float movDampling, float rotDampling, UINT8 shape, UINT8 type, UINT8 collisionGroup, UINT collisionMask)
{
	m_sdkRef->SceneAddRigidBody(m_sceneData, rigidBody->m_rigidBodyData, position, rotation, scale, mass, restitution, friction, movDampling, rotDampling, shape, type, collisionGroup, collisionMask);
}

void Physics3DScene::AddJoint(Physics3DJoint^ joint, float3 position, quaternion rotation, Physics3DRigidBody^ r1, Physics3DRigidBody^ r2,
	float3 PositionMinimum,
	float3 PositionMaximum,
	float3 RotationMinimum,
	float3 RotationMaximum,
	float3 PositionSpring,
	float3 RotationSpring)
{
	m_sdkRef->SceneAddJoint(m_sceneData, joint->m_jointData, position, rotation, r1->m_rigidBodyData, r2->m_rigidBodyData, PositionMinimum, PositionMaximum, RotationMinimum, RotationMaximum, PositionSpring, RotationSpring);
}

void Physics3DScene::ResetRigidBody(Physics3DRigidBody^ rigidBody, float3 position, quaternion rotation)
{
	m_sdkRef->SceneResetRigidBody(m_sceneData, rigidBody->m_rigidBodyData, position, rotation);
}

void Physics3DScene::RemoveRigidBody(Physics3DRigidBody^ rigidBody)
{
	m_sdkRef->SceneRemoveRigidBody(m_sceneData, rigidBody->m_rigidBodyData);
}

void Physics3DScene::RemoveJoint(Physics3DJoint^ joint)
{
	m_sdkRef->SceneRemoveJoint(m_sceneData, joint->m_jointData);
}

void Physics3DScene::SetGravitation(float3 gravitation)
{
	m_sdkRef->SceneSetGravitation(m_sceneData, gravitation);
}

void Physics3DScene::MoveRigidBody(Physics3DRigidBody^ rigidBody, float3 position, quaternion rotation)
{
	m_sdkRef->SceneMoveRigidBody(m_sceneData, rigidBody->m_rigidBodyData, position, rotation);
}

void Physics3DScene::MoveRigidBody(Physics3DRigidBody^ rigidBody, float4x4 matrix)
{
	m_sdkRef->SceneMoveRigidBody(m_sceneData, rigidBody->m_rigidBodyData, matrix);
}

float3 Physics3DScene::GetRigidBodyPosition(Physics3DRigidBody^ rigidBody)
{
	return m_sdkRef->SceneGetRigidBodyPosition(m_sceneData, rigidBody->m_rigidBodyData);
}

quaternion Physics3DScene::GetRigidBodyRotation(Physics3DRigidBody^ rigidBody)
{
	return m_sdkRef->SceneGetRigidBodyRotation(m_sceneData, rigidBody->m_rigidBodyData);
}

float4x4 Physics3DScene::GetRigidBodyTransform(Physics3DRigidBody^ rigidBody)
{
	return m_sdkRef->SceneGetRigidBodyTransform(m_sceneData, rigidBody->m_rigidBodyData);
}

void Physics3DScene::Simulate(double time)
{
	m_sdkRef->SceneSimulate(m_sceneData, time);
}

void Physics3DScene::FetchResults()
{
	m_sdkRef->SceneFetchResults(m_sceneData);
}
