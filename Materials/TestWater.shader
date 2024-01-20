// Made with Amplify Shader Editor v1.9.2.2
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "TestWater"
{
	Properties
	{
		_Color0("Color 0", Color) = (0,0.218298,1,1)
		_Color1("Color 1", Color) = (0,0.6855712,1,1)
		_WaveScale("WaveScale", Float) = 1
		_WaveHeight("WaveHeight", Float) = 1
		_TimeScale("TimeScale", Float) = 1
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
		};

		uniform float _WaveScale;
		uniform float _TimeScale;
		uniform float _WaveHeight;
		uniform float4 _Color0;
		uniform float4 _Color1;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float temp_output_24_0_g1 = _TimeScale;
			float mulTime19_g1 = _Time.y * temp_output_24_0_g1;
			float temp_output_16_0 = (0.0 + (sin( ( ( ase_worldPos.x * _WaveScale ) + mulTime19_g1 ) ) - -1.0) * (1.0 - 0.0) / (1.0 - -1.0));
			float3 appendResult10 = (float3(0.0 , 0.0 , ( temp_output_16_0 * _WaveHeight )));
			v.vertex.xyz += appendResult10;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float3 ase_worldPos = i.worldPos;
			float temp_output_24_0_g1 = _TimeScale;
			float mulTime19_g1 = _Time.y * temp_output_24_0_g1;
			float temp_output_16_0 = (0.0 + (sin( ( ( ase_worldPos.x * _WaveScale ) + mulTime19_g1 ) ) - -1.0) * (1.0 - 0.0) / (1.0 - -1.0));
			float4 lerpResult6 = lerp( _Color0 , _Color1 , temp_output_16_0);
			o.Albedo = lerpResult6.rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19202
Node;AmplifyShaderEditor.LerpOp;6;22,-170;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;4;-303,-353;Inherit;False;Property;_Color0;Color 0;0;0;Create;True;0;0;0;False;0;False;0,0.218298,1,1;0,0.218298,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;5;-298,-164;Inherit;False;Property;_Color1;Color 1;1;0;Create;True;0;0;0;False;0;False;0,0.6855712,1,1;0,0.6855712,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;283.2747,-103.0057;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;TestWater;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.FunctionNode;16;-479.7036,121.7235;Inherit;False;WaveFunction;-1;;1;c7900e82f74a80649af6c092884b701c;0;2;24;FLOAT;0;False;26;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;10;84.15742,125.479;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-111.0907,162.3996;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;18;-327.0907,250.3996;Inherit;False;Property;_WaveHeight;WaveHeight;3;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;19;-689.5822,127.8292;Inherit;False;Property;_TimeScale;TimeScale;4;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;20;-684.9592,197.3667;Inherit;False;Property;_WaveScale;WaveScale;2;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
WireConnection;6;0;4;0
WireConnection;6;1;5;0
WireConnection;6;2;16;0
WireConnection;0;0;6;0
WireConnection;0;11;10;0
WireConnection;16;24;19;0
WireConnection;16;26;20;0
WireConnection;10;2;17;0
WireConnection;17;0;16;0
WireConnection;17;1;18;0
ASEEND*/
//CHKSM=732D8204E1000C86B6D5E7C5B9C1500397A9A471