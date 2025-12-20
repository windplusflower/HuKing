using System.Collections.Generic;
using UnityEngine;

namespace RingLib.Entities.Water
{
    public class Water : MonoBehaviour
    {
        private BoxCollider2D boxCollider2D;
        private MeshRenderer meshRenderer;

        public int numSegments = 20;
        private float segmentOriginalHeight;
        private List<GameObject> segments = new();
        // Spring Movement: a = -stiffness * (y - y0) - dampening * v
        public float stiffness = 32;
        public float dampening = 1.75f;
        // Neighboring Effect: a += spreading * (yl - y) + spreading * (yr - y)
        public float spreading = 80;
        // Random Noise: a += noise * Random.Range(-1, 1)
        public float noise = 2;
        // Impact Effect: v += impact * v'
        public float impact = 0.25f;

        private readonly int numHorizontalRenderingSegments = 512;
        private readonly int numVerticalRenderingSegments = 8;
        public float renderingDampening = 1;

        private void CreateMesh()
        {
            var animationTransform = transform.Find("Animation");
            if (animationTransform == null)
            {
                Log.LogError(GetType().Name, "Water object must have an Animation child.");
                return;
            }
            var spriteRenderer = animationTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Log.LogError(GetType().Name, "Animation must have a SpriteRenderer component.");
                return;
            }

            var numVertices = (numHorizontalRenderingSegments + 1) * (numVerticalRenderingSegments + 1);
            var vertices = new Vector3[numVertices];
            var uv = new Vector2[numVertices];
            var xMin = boxCollider2D.offset.x - boxCollider2D.size.x / 2;
            var xMax = boxCollider2D.offset.x + boxCollider2D.size.x / 2;
            var yMin = boxCollider2D.offset.y - boxCollider2D.size.y / 2;
            var yMax = boxCollider2D.offset.y + boxCollider2D.size.y / 2;
            var renderingSegmentWidth = (xMax - xMin) / numHorizontalRenderingSegments;
            var renderingSegmentHeight = (yMax - yMin) / numVerticalRenderingSegments;
            for (int i = 0; i <= numHorizontalRenderingSegments; ++i)
            {
                for (int j = 0; j <= numVerticalRenderingSegments; ++j)
                {
                    int index = i * (numVerticalRenderingSegments + 1) + j;
                    var x = xMin + renderingSegmentWidth * i;
                    var y = yMin + renderingSegmentHeight * j;

                    vertices[index] = new Vector3(x, y, 0);
                    uv[index] = new Vector2(
                        (float)i / numHorizontalRenderingSegments,
                        (float)j / numVerticalRenderingSegments);
                }
            }

            var triangles = new List<int>();
            for (int i = 0; i < numHorizontalRenderingSegments; ++i)
            {
                for (int j = 0; j < numVerticalRenderingSegments; ++j)
                {
                    var start = i * (numVerticalRenderingSegments + 1) + j;
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + numVerticalRenderingSegments + 1);
                    triangles.Add(start + 1);
                    triangles.Add(start + numVerticalRenderingSegments + 2);
                    triangles.Add(start + numVerticalRenderingSegments + 1);
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles.ToArray();

            var newAnimation = new GameObject("NewAnimation");
            newAnimation.layer = gameObject.layer;
            newAnimation.transform.parent = transform;
            newAnimation.transform.localPosition = Vector3.zero;
            var meshFilter = newAnimation.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            meshRenderer = newAnimation.AddComponent<MeshRenderer>();
            meshRenderer.material = spriteRenderer.material;
            meshRenderer.material.mainTexture = spriteRenderer.sprite.texture;
            spriteRenderer.enabled = false;
        }

        private void Start()
        {
            boxCollider2D = GetComponent<BoxCollider2D>();
            if (boxCollider2D == null)
            {
                Log.LogError(GetType().Name, "Water object must have a BoxCollider2D component.");
                return;
            }

            CreateMesh();

            float totalWidth = boxCollider2D.size.x;
            float segmentWidth = totalWidth / numSegments;
            segmentOriginalHeight = boxCollider2D.size.y;
            Vector2 startPosition = new Vector2(
                boxCollider2D.offset.x - totalWidth / 2 + 0.5f * segmentWidth, boxCollider2D.offset.y);
            Destroy(boxCollider2D);
            GameObject segmentParent = new GameObject("Segments");
            segmentParent.layer = gameObject.layer;
            segmentParent.transform.parent = transform;
            segmentParent.transform.localPosition = Vector3.zero;
            for (int i = 0; i < numSegments; ++i)
            {
                GameObject segment = new GameObject($"Segment{i}");
                segment.layer = gameObject.layer;
                segment.transform.parent = segmentParent.transform;
                segment.transform.localPosition = new Vector3(
                    startPosition.x + segmentWidth * i, startPosition.y, 0);
                var newCollider = segment.AddComponent<BoxCollider2D>();
                newCollider.isTrigger = true;
                newCollider.size = new Vector2(segmentWidth, segmentOriginalHeight);
                segment.AddComponent<WaterSegment>().water = this;
                segments.Add(segment);
            }
            for (int i = 0; i < numSegments; ++i)
            {
                if (i > 0)
                {
                    segments[i].GetComponent<WaterSegment>().left =
                        segments[i - 1].GetComponent<WaterSegment>();
                }
                if (i < numSegments - 1)
                {
                    segments[i].GetComponent<WaterSegment>().right =
                        segments[i + 1].GetComponent<WaterSegment>();
                }
            }
        }

        private void Update()
        {
            var segmentX = new float[numSegments];
            var segmentTop = new float[numSegments];
            var segmentBottom = new float[numSegments];
            for (int i = 0; i < numSegments; ++i)
            {
                var segment = segments[i];
                var boxCollider2D = segment.GetComponent<BoxCollider2D>();
                var localSegmentTop = new Vector2(
                    boxCollider2D.offset.x, boxCollider2D.offset.y + boxCollider2D.size.y / 2);
                var localSegmentBottom = new Vector2(
                    boxCollider2D.offset.x, boxCollider2D.offset.y - boxCollider2D.size.y / 2);
                var worldSegmentTop = segment.transform.TransformPoint(localSegmentTop);
                var worldSegmentBottom = segment.transform.TransformPoint(localSegmentBottom);
                segmentX[i] = worldSegmentTop.x;
                segmentTop[i] = worldSegmentTop.y;
                segmentBottom[i] = worldSegmentBottom.y;
            }

            meshRenderer.material.SetInt("_NumSegments", numSegments);
            meshRenderer.material.SetFloat("_SegmentOriginalHeight", segmentOriginalHeight);
            meshRenderer.material.SetFloatArray("_SegmentX", segmentX);
            meshRenderer.material.SetFloatArray("_SegmentTop", segmentTop);
            meshRenderer.material.SetFloat("_SegmentOriginalBottom", segmentBottom[0]);
            meshRenderer.material.SetFloat("_RenderingDampening", renderingDampening);
        }
    }
}
