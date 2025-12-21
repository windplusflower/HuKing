using System.Collections;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private GameObject[] platFrame = new GameObject[3];
    [State]
    private IEnumerator<Transition> SawNailRoom()
    {
        yield return new ToState { State = nameof(Appear) };
    }
    private void registerSawNailRoom()
    {
        skillTable.Add(nameof(SawNailRoom), new SkillPhases(
            () => Appear_SawNailRoom(),
            () => Loop_SawNailRoom(),
            () => Disappear_SawNailRoom()
        ));
        InitializePlatFrame();
    }
    private IEnumerator<Transition> Appear_SawNailRoom()
    {
        SetPlatFrameToHeroCenter();
        HuKing.instance.Log($"appearing SawNailRoom level {level}");
        platFrame[level - 1].SetActive(true);
        yield return null;
    }
    private IEnumerator<Transition> Disappear_SawNailRoom()
    {
        HuKing.instance.Log($"Disappearing SawNailRoom level {level}");
        platFrame[level - 1].SetActive(false);
        yield return null;
    }
    [State]
    private IEnumerator<Transition> Loop_SawNailRoom()
    {
        yield return null;
    }
    private void InitializePlatFrame()
    {
        for (int i = 0; i < 3; i++)
        {
            platFrame[i] = new GameObject($"PlatFrame_Level_{i + 1}");
        }
        CreatePlatSquare(8f, platFrame[0]);
        CreatePlatSquare(6f, platFrame[1]);
        CreatePlatSquare(4f, platFrame[2]);
        HuKing.instance.Log("PlatFrame_Container created");
    }
    private void CreatePlatSquare(float size, GameObject parent)
    {
        var rb = parent.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        parent.layer = (int)GlobalEnums.PhysLayers.TERRAIN;

        var mainResponder = parent.AddComponent<PlatFrameResponder>();
        mainResponder.OnBeenHit = (direction) =>
        {
            StartCoroutine(MoveFrame(direction, parent));
        };

        float offset = size / 2f;
        float thickness = 0.95f;

        Vector3 baseScale = new Vector3(size * 0.25f, thickness, 1);

        CreatePlatformEdge(parent, new Vector3(0, -offset, 0), baseScale, 0f);
        CreatePlatformEdge(parent, new Vector3(-offset, 0, 0), baseScale, -90f);
        CreatePlatformEdge(parent, new Vector3(offset, 0, 0), baseScale, 90f);
        CreatePlatformEdge(parent, new Vector3(0, offset, 0), baseScale, 180f);

        parent.SetActive(false);
    }

    private void CreatePlatformEdge(GameObject parent, Vector3 localPos, Vector3 scale, float rotationZ)
    {
        GameObject edge = Instantiate(platPrefab, parent.transform);
        edge.transform.localPosition = localPos;
        edge.transform.localScale = scale;

        edge.transform.localRotation = Quaternion.Euler(0, 0, rotationZ);
        edge.layer = (int)GlobalEnums.PhysLayers.TERRAIN;
        var edgeResponder = edge.AddComponent<PlatFrameResponder>();
        edgeResponder.OnBeenHit = (dir) =>
        {
            parent.GetComponent<PlatFrameResponder>().OnBeenHit?.Invoke(dir);
        };
        edge.SetActive(true);
    }
    private bool isMoving = false;

    private IEnumerator MoveFrame(Vector2 direction, GameObject targetFrame)
    {
        if (isMoving) yield break;
        isMoving = true;

        Rigidbody2D rb = targetFrame.GetComponent<Rigidbody2D>();
        Vector3 startPos = targetFrame.transform.position;

        float currentSize = 8f;
        if (level == 2) currentSize = 6f;
        if (level == 3) currentSize = 4f;

        float maxMoveDist = 1.51f;
        Vector2 boxSize = new Vector2(currentSize * 0.9f, currentSize * 0.9f); // 稍微缩一点，避免摩擦侧墙

        RaycastHit2D hit = Physics2D.BoxCast(startPos, boxSize, 0f, direction, maxMoveDist, 1 << 8);

        float finalMoveDist = maxMoveDist;

        if (hit.collider != null)
        {
            finalMoveDist = Mathf.Max(0, hit.distance - 0.05f);
        }
        if (direction.y > 0.1f)
        {
            float remainingDist = upWall - startPos.y;
            finalMoveDist = Mathf.Min(finalMoveDist, Mathf.Max(0, remainingDist));
        }
        Vector3 targetPos = startPos + (Vector3)direction * finalMoveDist;

        float elapsed = 0f;
        float duration = 0.1f;

        while (elapsed < duration)
        {
            if (targetFrame == null) yield break;
            elapsed += Time.deltaTime;

            float t = elapsed / duration;
            float easedT = Mathf.SmoothStep(0, 1, t);
            Vector3 nextPos = Vector3.Lerp(startPos, targetPos, easedT);
            rb.MovePosition(nextPos);

            yield return null;
        }

        rb.MovePosition(targetPos);
        isMoving = false;
    }
    public void SetPlatFrameToHeroCenter()
    {
        if (platFrame[level - 1] == null) return;

        Vector3 heroPos = HeroController.instance.transform.position;
        platFrame[level - 1].transform.position = heroPos;
        HuKing.instance.Log("PlatFrame positioned to Hero center");
        isMoving = false;
    }
}