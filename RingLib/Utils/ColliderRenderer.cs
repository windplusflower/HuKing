using System.Collections.Generic;
using UnityEngine;

namespace RingLib.Utils
{
    public class ColliderRenderer : MonoBehaviour
    {
        private new Collider2D collider2D;
        private LineRenderer lineRenderer;

        private void Start()
        {
            collider2D = GetComponent<Collider2D>();

            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
            lineRenderer.startWidth = lineRenderer.endWidth = 0.01f;
            lineRenderer.useWorldSpace = true;
        }

        void DrawLines(List<Vector3> lines, Color color)
        {
            if (lines.Count == 0) return;

            GL.Begin(GL.LINES);
            GL.Color(color);

            for (int i = 0; i < lines.Count; i += 2)
            {
                GL.Vertex(lines[i]);
                GL.Vertex(lines[(i + 1) % lines.Count]);
            }

            GL.End();
        }

        private void Update()
        {
            if (collider2D is BoxCollider2D boxCollider2D)
            {
                var positions = new Vector3[5];
                positions[0] = new Vector3(boxCollider2D.bounds.min.x, boxCollider2D.bounds.min.y, 0);
                positions[1] = new Vector3(boxCollider2D.bounds.max.x, boxCollider2D.bounds.min.y, 0);
                positions[2] = new Vector3(boxCollider2D.bounds.max.x, boxCollider2D.bounds.max.y, 0);
                positions[3] = new Vector3(boxCollider2D.bounds.min.x, boxCollider2D.bounds.max.y, 0);
                positions[4] = positions[0];
                lineRenderer.positionCount = 5;
                lineRenderer.SetPositions(positions);

                DrawLines(new List<Vector3> { positions[0], positions[1], positions[2], positions[3], positions[0] }, Color.red);
            }
            else if (collider2D is PolygonCollider2D polygonCollider)
            {
                int pointCount = polygonCollider.pathCount > 0 ? polygonCollider.GetPath(0).Length : 0;
                Vector3[] positions = new Vector3[pointCount + 1];
                for (int i = 0; i < pointCount; i++)
                {
                    Vector2 point = polygonCollider.transform.TransformPoint(polygonCollider.GetPath(0)[i]); ;
                    positions[i] = (Vector3)point;
                }
                if (pointCount > 0)
                {
                    positions[pointCount] = positions[0];
                }
                lineRenderer.positionCount = pointCount + 1;
                lineRenderer.SetPositions(positions);
            }
            else
            {
                Log.LogError(GetType().Name, "Collider type not supported");
            }
        }

        private void OnDisable()
        {
            lineRenderer.positionCount = 0;
        }
    }
}

