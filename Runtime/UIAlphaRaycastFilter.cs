using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Optional per-Graphic alpha test used by InteractionSystem after GraphicRaycaster reports a hit.
    /// The Graphic's generated mesh and texture alpha both participate in the result.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIAlphaRaycastFilter : MonoBehaviour
    {
        [Tooltip("Graphic on this GameObject whose generated geometry and alpha are tested.")]
        [SerializeField] private Graphic targetGraphic;
        [Tooltip("When enabled, transparent pixels and empty generated geometry do not receive InteractionSystem UI hits.")]
        [SerializeField] private bool alphaAffectsRaycast;
        [Tooltip("Combined vertex, CanvasRenderer, and texture alpha required to accept the hit.")]
        [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.1f;

        private readonly List<Vector3> vertices = new(64);
        private readonly List<Vector2> uvs = new(64);
        private readonly List<Color32> colors = new(64);
        private readonly List<int> triangles = new(96);
        private bool warnedInvalidGraphic;
        private bool warnedUnreadableTexture;

        public Graphic TargetGraphic
        {
            get => targetGraphic;
            set
            {
                targetGraphic = value;
                warnedInvalidGraphic = false;
                warnedUnreadableTexture = false;
            }
        }

        public bool AlphaAffectsRaycast
        {
            get => alphaAffectsRaycast;
            set => alphaAffectsRaycast = value;
        }

        public float MinimumAlpha
        {
            get => minimumAlpha;
            set => minimumAlpha = Mathf.Clamp01(value);
        }

        private void OnValidate()
        {
            minimumAlpha = Mathf.Clamp01(minimumAlpha);
            warnedInvalidGraphic = false;
            warnedUnreadableTexture = false;
        }

        public bool AllowsRaycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!alphaAffectsRaycast)
                return true;
            if (!IsGraphicValid())
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(targetGraphic.rectTransform, screenPoint, eventCamera, out var localPoint))
                return false;

            var generatedMesh = targetGraphic.canvasRenderer.GetMesh();
            if (!generatedMesh)
                return false;
            ReadMeshData(generatedMesh);
            if (vertices.Count == 0 || triangles.Count < 3)
                return false;

            bool samplesTextureAlpha = targetGraphic is Image || targetGraphic is RawImage;
            var texture = samplesTextureAlpha ? targetGraphic.mainTexture as Texture2D : null;
            if (samplesTextureAlpha && (!texture || !texture.isReadable))
            {
                WarnUnreadableTexture(targetGraphic.mainTexture);
                return false;
            }

            for (int index = 0; index <= triangles.Count - 3; index += 3)
            {
                int first = triangles[index];
                int second = triangles[index + 1];
                int third = triangles[index + 2];
                if (!TryGetBarycentric(localPoint, vertices[first], vertices[second], vertices[third], out var weights))
                    continue;

                float vertexAlpha = GetVertexAlpha(first, second, third, weights);
                float textureAlpha = GetTextureAlpha(texture, first, second, third, weights);
                if (vertexAlpha * targetGraphic.canvasRenderer.GetAlpha() * textureAlpha >= minimumAlpha)
                    return true;
            }

            return false;
        }

        private bool IsGraphicValid()
        {
            if (targetGraphic && targetGraphic.gameObject == gameObject)
                return true;
            if (!warnedInvalidGraphic)
            {
                warnedInvalidGraphic = true;
                Debug.LogError("[InteractionSystem] Assign the Graphic on the same GameObject to UIAlphaRaycastFilter.", this);
            }
            return false;
        }

        private void ReadMeshData(Mesh generatedMesh)
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            generatedMesh.GetVertices(vertices);
            generatedMesh.GetUVs(0, uvs);
            generatedMesh.GetColors(colors);
            if (generatedMesh.subMeshCount > 0)
                generatedMesh.GetTriangles(triangles, 0);
        }

        private float GetVertexAlpha(int first, int second, int third, Vector3 weights)
        {
            if (colors.Count != vertices.Count)
                return targetGraphic.color.a;
            return (colors[first].a * weights.x + colors[second].a * weights.y + colors[third].a * weights.z) / 255f;
        }

        private float GetTextureAlpha(Texture2D texture, int first, int second, int third, Vector3 weights)
        {
            if (!texture || uvs.Count != vertices.Count)
                return 1f;
            Vector2 uv = uvs[first] * weights.x + uvs[second] * weights.y + uvs[third] * weights.z;
            return texture.GetPixelBilinear(uv.x, uv.y).a;
        }

        private void WarnUnreadableTexture(Texture texture)
        {
            if (warnedUnreadableTexture)
                return;
            warnedUnreadableTexture = true;
            string textureName = texture ? texture.name : "<missing>";
            Debug.LogError($"[InteractionSystem] Alpha raycast on {name} requires readable texture {textureName}. Enable Read/Write on its import settings.", this);
        }

        private static bool TryGetBarycentric(Vector2 point, Vector2 first, Vector2 second, Vector2 third, out Vector3 weights)
        {
            Vector2 edgeA = second - first;
            Vector2 edgeB = third - first;
            Vector2 offset = point - first;
            float dotAA = Vector2.Dot(edgeA, edgeA);
            float dotAB = Vector2.Dot(edgeA, edgeB);
            float dotBB = Vector2.Dot(edgeB, edgeB);
            float dotPA = Vector2.Dot(offset, edgeA);
            float dotPB = Vector2.Dot(offset, edgeB);
            float denominator = dotAA * dotBB - dotAB * dotAB;
            if (Mathf.Abs(denominator) <= Mathf.Epsilon)
            {
                weights = default;
                return false;
            }

            float secondWeight = (dotBB * dotPA - dotAB * dotPB) / denominator;
            float thirdWeight = (dotAA * dotPB - dotAB * dotPA) / denominator;
            float firstWeight = 1f - secondWeight - thirdWeight;
            weights = new Vector3(firstWeight, secondWeight, thirdWeight);
            const float tolerance = -0.0001f;
            return firstWeight >= tolerance && secondWeight >= tolerance && thirdWeight >= tolerance;
        }
    }
}
