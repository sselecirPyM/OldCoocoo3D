
#define SH_PI 3.14159265358979
static float SH_RCP_PI = 1 / SH_PI;

//l = 0,m = 0
float GetY00(float3 p) {
	return 0.5 * sqrt(SH_RCP_PI);
}

//l = 1,m = 0
float GetY10(float3 p) {
	return 0.5 * sqrt(3 * SH_RCP_PI) * p.z;
}

//l = 1,m = 1
float GetY1p1(float3 p) {
	return 0.5 * sqrt(3 * SH_RCP_PI) * p.x;
}

//l = 1,m = -1
float GetY1n1(float3 p) {
	return 0.5 * sqrt(3 * SH_RCP_PI) * p.y;
}

//l = 2, m = 0
float GetY20(float3 p) {
	return 0.25 * sqrt(5 * SH_RCP_PI) * (2 * p.z * p.z - p.x * p.x - p.y * p.y);
}

//l = 2, m = 1
float GetY2p1(float3 p) {
	return 0.5 * sqrt(15 * SH_RCP_PI) * p.z * p.x;
}

//l = 2, m = -1
float GetY2n1(float3 p) {
	return 0.5 * sqrt(15 * SH_RCP_PI) * p.z * p.y;
}

//l = 2, m = 2
float GetY2p2(float3 p) {
	return 0.25 * sqrt(15 * SH_RCP_PI) * (p.x * p.x - p.y * p.y);
}

//l = 2, m = -2
float GetY2n2(float3 p) {
	return 0.5 * sqrt(15 * SH_RCP_PI) * p.x * p.y;
}

struct SH9C
{
	float4 values[9];
};

float4 GetSH9Color(in SH9C sh9, in float3 dir)
{
	float4 shColor =
		sh9.values[0] * GetY00(dir) +
		sh9.values[1] * GetY1n1(dir) +
		sh9.values[2] * GetY10(dir) +
		sh9.values[3] * GetY1p1(dir) +
		sh9.values[4] * GetY2n2(dir) +
		sh9.values[5] * GetY2n1(dir) +
		sh9.values[6] * GetY20(dir) +
		sh9.values[7] * GetY2p1(dir) +
		sh9.values[8] * GetY2p2(dir);
	return shColor;
}

void AddSH9Color(inout SH9C sh9, in float3 dir, in float4 color)
{
	sh9.values[0] += color * GetY00(dir);
	sh9.values[1] += color * GetY1n1(dir);
	sh9.values[2] += color * GetY10(dir);
	sh9.values[3] += color * GetY1p1(dir);
	sh9.values[4] += color * GetY2n2(dir);
	sh9.values[5] += color * GetY2n1(dir);
	sh9.values[6] += color * GetY20(dir);
	sh9.values[7] += color * GetY2p1(dir);
	sh9.values[8] += color * GetY2p2(dir);
}

SH9C Add(in SH9C a, in SH9C b)
{
	SH9C sh9;
	for (int i = 0; i < 9; i++)
	{
		sh9.values[i] = a.values[i] + b.values[i];
	}
	return sh9;
}

SH9C Add(in SH9C a, in SH9C b, float bweight)
{
	SH9C sh9;
	for (int i = 0; i < 9; i++)
	{
		sh9.values[i] = a.values[i] + b.values[i]* bweight;
	}
	return sh9;
}