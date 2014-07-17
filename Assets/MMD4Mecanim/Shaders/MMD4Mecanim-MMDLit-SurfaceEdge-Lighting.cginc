half4 _EdgeColor;
float _EdgeSize;

#define MMDLIT_GLOBALLIGHTING (0.6)
#define MMDLIT_EDGE_ZOFST (0.00001)

inline float MMDLit_GetEdgeSize()
{
	return _EdgeSize;
}

inline float4 MMDLit_GetEdgeVertex(float4 vertex, float3 normal)
{
#if 1
	float edge_size = MMDLit_GetEdgeSize();
#else
	// Adjust edge_size by distance & fovY
	float4 world_pos = mul(UNITY_MATRIX_MV, vertex);
	float r_proj_y = UNITY_MATRIX_P[1][1];
	float edge_size = abs(MMDLit_GetEdgeSize() / r_proj_y * world_pos.z) * 2.0;
#endif
	return vertex + float4(normal.xyz * edge_size, 0.0);
}

inline float4 MMDLit_TransformEdgeVertex(float4 vertex)
{
#if 0
	vertex = mul(UNITY_MATRIX_MVP, vertex);
	vertex.z += MMDLIT_EDGE_ZOFST * vertex.w;
	return vertex;
#else
	return mul(UNITY_MATRIX_MVP, vertex);
#endif
}

inline half3 MMDLit_GetAlbedo(out half alpha)
{
	alpha = _EdgeColor.a;
	return (half3)_EdgeColor;
}

inline half3 MMDLit_Lighting(half3 albedo, half atten)
{
	return albedo * (half3)_LightColor0 * atten * (2.0 * MMDLIT_GLOBALLIGHTING);
}
