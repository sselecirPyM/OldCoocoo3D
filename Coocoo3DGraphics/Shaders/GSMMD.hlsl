struct PSSkinnedIn
{
	float4 Pos	: POSITION;		//Position
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tan : TANGENT;		//Normalized Tangent vector
	float EdgeScale : EDGESCALE;
};

[maxvertexcount(3)]
void main(
	triangle PSSkinnedIn input[3],
	inout TriangleStream< PSSkinnedIn > triStream
)
{
	float fact = cross(float3(input[0].Tex - input[1].Tex,0), float3(input[1].Tex - input[2].Tex,0)).z > 0 ? 1 : -1;
	for (uint i = 0; i < 3; i++)
	{
		float2 a = normalize(input[i].Tex - input[(i+1)%3].Tex);
		float3 tan;
		float3 norm = normalize(input[i].Norm);
		float3 vecX = normalize(input[i].Pos.xyz - input[(i + 1) % 3].Pos.xyz);
		float3 vecY = normalize(cross(input[i].Pos.xyz - input[(i + 1) % 3].Pos.xyz, norm))* fact;
		float4x4 mat =
		{
			vecX,0,
			vecY,0,
			float4(0,0,0,0),
			float4(0,0,0,0)
		};
		//float4x4 mat =
		//{
		//	vecX.x,vecY.x,norm.x,0,
		//	vecX.y,vecY.y,norm.y,0,
		//	vecX.z,vecY.z,norm.z,0,
		//	float4(0,0,0,0)
		//};
		float3 b = mul(float4(a, 0, 0), mat).xyz* fact;

		PSSkinnedIn output;
		output = input[i];
		output.Tan = b;
		triStream.Append(output);
	}
	triStream.RestartStrip();
}