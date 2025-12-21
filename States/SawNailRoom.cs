using System.Collections;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private GameObject[] platFrame = new GameObject[3];
    private Queue<GameObject> nailPool = new Queue<GameObject>();
    private const int POOL_SIZE = 15;
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
        if (nailPool.Count > 0) return;

        for (int i = 0; i < POOL_SIZE; i++)
        {
            GameObject nail = Instantiate(nailPrefab);
            nail.SetActive(false);
            DontDestroyOnLoad(nail);

            PlayMakerFSM fsm = nail.LocateMyFSM("Control");
            if (fsm != null)
            {
                fsm.CopyState("Fan Antic", "Prepare");
                fsm.AddGlobalTransition("FORCE_START", "Prepare");
                fsm.ChangeTransition("Prepare", "FINISHED", "Idle");
            }
            nailPool.Enqueue(nail);
        }
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
        foreach (var nail in nailPool)
        {
            if (nail != null)
            {
                nail.SetActive(false);
            }
        }
        yield return null;
    }
    private IEnumerator Loop_SawNailRoom()
    {
        while (true)
        {
            for (int i = 0; i <= level; i++)
                StartCoroutine(FireNailCoroutine(HeroController.instance.transform.position, new Rect(leftWall, downWall, rightWall - leftWall, upWall - downWall)));
            yield return new WaitForSeconds(1.5f);
        }
    }
    private IEnumerator FireNailCoroutine(Vector3 heroPos, Rect battleArea)
    {
        if (nailPool.Count == 0) yield break;
        GameObject nail = nailPool.Dequeue();
        nailPool.Enqueue(nail);

        nail.SetActive(false);

        var rb = nail.GetComponent<Rigidbody2D>();
        var col = nail.GetComponent<Collider2D>();
        var fsm = nail.LocateMyFSM("Control");

        if (col != null) { col.enabled = true; col.isTrigger = true; }
        if (rb != null) { rb.isKinematic = false; rb.velocity = Vector2.zero; }

        Vector3 spawnPos = Vector3.zero;
        bool found = false;
        float radius = 16f;

        for (int i = 0; i < 20; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            Vector3 pos = heroPos + offset;
            if (battleArea.Contains(pos))
            {
                spawnPos = pos;
                found = true;
                break;
            }
        }
        if (!found) spawnPos = heroPos + Vector3.up * radius;

        Vector2 errorOffset = UnityEngine.Random.insideUnitCircle * 2.0f;
        Vector3 targetPosWithError = heroPos + (Vector3)errorOffset;
        Vector2 direction = (targetPosWithError - spawnPos).normalized;
        float lookAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        nail.transform.position = spawnPos;
        nail.transform.eulerAngles = new Vector3(0, 0, lookAngle - 90f);

        if (fsm != null)
        {
            fsm.enabled = true;
            fsm.SendEvent("FORCE_START");
        }

        nail.SetActive(true);
        yield return new WaitForSeconds(0.8f);

        if (rb != null) rb.velocity = direction * 35f;

        yield return new WaitForSeconds(2.0f);

        if (fsm != null)
        {
            fsm.SendEvent("TO_RECYCLE");
            yield return new WaitForSeconds(0.5f);
        }

        nail.SetActive(false);
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
        isMoving = false;
    }
}