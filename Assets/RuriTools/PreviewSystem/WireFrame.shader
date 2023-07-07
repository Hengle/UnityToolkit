// WireframeエフェクトのUnityシェーダー
Shader "UnityLibrary/Effects/Wireframe"
{
	// プロパティ
	Properties
	{
		// テクスチャ
		_MainTex("Texture", 2D) = "white" {}
		// Matcapテクスチャ
		_MatcapTex("Matcap", 2D) = "white" {}
		// 線の色
		_LineColor("LineColor", Color) = (1,1,1,1)
		// 塗りつぶしの色
		_FillColor("FillColor", Color) = (0,0,0,1)
		// 線の太さ
		_WireThickness("Wire Thickness", RANGE(0, 1500)) = 0
	}
	SubShader
	{
		// タグ
		Tags { "RenderType" = "Opaque" }

		// パス
		Pass
		{
			// Wireframeシェーダー
			// 参考: http://developer.download.nvidia.com/SDK/10/direct3d/Source/SolidWireframe/Doc/SolidWireframe.pdf
			CGPROGRAM
			// 頂点シェーダー
			#pragma vertex vert
			// ジオメトリシェーダー
			#pragma geometry geom
			// フラグメントシェーダー
			#pragma fragment frag
			// UnityCG.cgincをインクルード
			#include "UnityCG.cginc"

			// 線の太さ
			float _WireThickness;

			// アプリケーションデータ
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD;
				float3 normal : NORMAL;
			};

			// 頂点シェーダーからジオメトリシェーダーへの出力
			struct v2g
			{
				float4 projectionSpaceVertex : SV_POSITION;
				float4 worldSpacePosition : TEXCOORD1;
				float2 uv : TEXCOORD2;
				float4 worldNormal  : TEXCOORD3;
			};

			// ジオメトリシェーダーからフラグメントシェーダーへの出力
			struct g2f
			{
				float4 projectionSpaceVertex : SV_POSITION;
				float4 worldSpacePosition : TEXCOORD0;
				float4 dist : TEXCOORD1;
				float2 uv : TEXCOORD2;
				float4 worldNormal  : TEXCOORD3;
			};

			// テクスチャ
			sampler2D _MainTex;
			float4 _MainTex_ST;

			// 頂点シェーダー
			v2g vert(appdata v)
			{
				v2g o;
				o.projectionSpaceVertex = UnityObjectToClipPos(v.vertex);
				o.worldSpacePosition = mul(unity_ObjectToWorld, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				// ワールド空間の法線座標を取得
				o.worldNormal = mul(v.normal, unity_WorldToObject);
				return o;
			}
			// ジオメトリシェーダー
// シェーダーのジオメトリパスで、三角形ごとに頂点を生成するために使用されます
			[maxvertexcount(3)] // 生成される頂点の最大数を指定
			void geom(triangle v2g i[3], inout TriangleStream<g2f> triangleStream)
			{
				// 三角形の頂点をスクリーン座標系に変換
				float2 p0 = i[0].projectionSpaceVertex.xy / i[0].projectionSpaceVertex.w;
				float2 p1 = i[1].projectionSpaceVertex.xy / i[1].projectionSpaceVertex.w;
				float2 p2 = i[2].projectionSpaceVertex.xy / i[2].projectionSpaceVertex.w;
				// 辺のベクトルを計算
				float2 edge0 = p2 - p1;
				float2 edge1 = p2 - p0;
				float2 edge2 = p1 - p0;

				// 面積とベースの比を得る前にcross productを2倍にする
				// 逆数を取る代わりに除算を避けるため
				float area = abs(edge1.x * edge2.y - edge1.y * edge2.x);
				float wireThickness = 1500 - _WireThickness;

				g2f o;
				// 生成される頂点のプロパティを設定
				o.worldSpacePosition = i[0].worldSpacePosition;
				o.projectionSpaceVertex = i[0].projectionSpaceVertex;
				o.dist.xyz = float3((area / length(edge0)), 0.0, 0.0) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
				o.uv = i[0].uv;
				o.worldNormal = i[0].worldNormal;
				// 生成された頂点をストリームに追加
				triangleStream.Append(o);

				o.worldSpacePosition = i[1].worldSpacePosition;
				o.projectionSpaceVertex = i[1].projectionSpaceVertex;
				o.dist.xyz = float3(0.0, (area / length(edge1)), 0.0) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
				o.uv = i[1].uv;
				o.worldNormal = i[1].worldNormal;
				triangleStream.Append(o);

				o.worldSpacePosition = i[2].worldSpacePosition;
				o.projectionSpaceVertex = i[2].projectionSpaceVertex;
				o.dist.xyz = float3(0.0, 0.0, (area / length(edge2))) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
				o.uv = i[2].uv;
				o.worldNormal = i[2].worldNormal;
				triangleStream.Append(o);
			}

			// Matcapテクスチャ
			sampler2D _MatcapTex;
			half4 _MatcapTex_ST;

			// 線の色
			uniform fixed4 _LineColor;
			// 塗りつぶしの色
			uniform fixed4 _FillColor;

			// フラグメントシェーダー
			fixed4 frag(g2f i) : SV_Target
			{
				// 3つの辺の最小距離を計算
				float minDistanceToEdge = min(i.dist[0], min(i.dist[1], i.dist[2])) * i.dist[3];
	
				// テクスチャの色を取得
				fixed4 col = tex2D(_MainTex, i.uv);
				// 法線をカメラ空間に変換してMatcapのuvを取得
				half4 normalUV = (mul(i.worldNormal, UNITY_MATRIX_V) + 1.0) * 0.5;
				// Matcapの色
				half4 matcap = tex2D(_MatcapTex, normalUV);
				// 線分上にないことがわかっていれば、早期に出力
				if (minDistanceToEdge > 0.9)
				{
					return matcap * col;
				}
	
				// 塗りつぶしの色を出力
			   return _FillColor;
		   }
		   ENDCG
		}
	}
}