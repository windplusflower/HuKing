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
            // DontDestroyOnLoad(nail);

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
            for (int i = 0; i < 2; i++)
            {
                StartCoroutine(FireNail_RandomCircle(HeroController.instance.transform.position, GetBattleArea()));
            }

            if (level >= 2)
            {
                StartCoroutine(FireNail_Horizontal(HeroController.instance.transform.position, GetBattleArea()));
            }

            if (level >= 3)
            {
                StartCoroutine(FireNail_Vertical(HeroController.instance.transform.position, GetBattleArea()));
            }

            yield return new WaitForSeconds(1.5f);
        }
    }

    private Rect GetBattleArea() => new Rect(leftWall, downWall, rightWall - leftWall, upWall - downWall);
    private IEnumerator FireNail_RandomCircle(Vector3 heroPos, Rect area)
    {
        Vector3 spawnPos = Vector3.zero;
        bool found = false;
        for (int i = 0; i < 30; i++)
        {
            float rx = UnityEngine.Random.Range(area.xMin, area.xMax);
            float ry = UnityEngine.Random.Range(area.yMin, area.yMax);
            Vector3 candidate = new Vector3(rx, ry, 0);
            if (Vector3.Distance(candidate, heroPos) > 8f)
            {
                spawnPos = candidate;
                found = true;
                break;
            }
        }
        if (!found) spawnPos = new Vector3(area.xMax, area.yMax, 0);
        Vector3 targetPos = heroPos + (Vector3)(UnityEngine.Random.insideUnitCircle * 2.0f);
        Vector2 moveDir = (targetPos - spawnPos).normalized;

        yield return LaunchNail(spawnPos, moveDir, -90f);
    }

    private IEnumerator FireNail_Horizontal(Vector3 heroPos, Rect area)
    {
        float centerX = area.xMin + area.width / 2f;
        float spawnX = heroPos.x < centerX ? area.xMax : area.xMin;
        float spawnY = Mathf.Clamp(heroPos.y + UnityEngine.Random.Range(-1.5f, 1.5f), area.yMin, area.yMax);

        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);
        Vector2 moveDir = (heroPos.x < centerX) ? Vector2.left : Vector2.right;

        yield return LaunchNail(spawnPos, moveDir, -90f);
    }

    private IEnumerator FireNail_Vertical(Vector3 heroPos, Rect area)
    {
        float centerY = area.yMin + area.height / 2f;
        float spawnY = heroPos.y < centerY ? area.yMax : area.yMin;
        float spawnX = Mathf.Clamp(heroPos.x + UnityEngine.Random.Range(-1.0f, 1.0f), area.xMin, area.xMax);

        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);
        Vector2 moveDir = (heroPos.y < centerY) ? Vector2.down : Vector2.up;

        yield return LaunchNail(spawnPos, moveDir, -90f);
    }
    private IEnumerator LaunchNail(Vector3 spawnPos, Vector2 moveDirection, float lookAngleOffset = -90f)
    {
        if (nailPool.Count == 0) yield break;
        GameObject nail = nailPool.Dequeue();
        nailPool.Enqueue(nail);

        nail.SetActive(false);
        var rb = nail.GetComponent<Rigidbody2D>();
        var fsm = nail.LocateMyFSM("Control");

        nail.transform.position = spawnPos;

        if (moveDirection == Vector2.right) nail.transform.eulerAngles = new Vector3(0, 0, 270f);
        else if (moveDirection == Vector2.left) nail.transform.eulerAngles = new Vector3(0, 0, 90f);
        else if (moveDirection == Vector2.up) nail.transform.eulerAngles = new Vector3(0, 0, 0f);
        else if (moveDirection == Vector2.down) nail.transform.eulerAngles = new Vector3(0, 0, 180f);
        else
        {
            float lookAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            nail.transform.eulerAngles = new Vector3(0, 0, lookAngle + lookAngleOffset);
        }

        var trail = nail.GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            trail.Clear();
            trail.emitting = false;
        }

        if (fsm != null)
        {
            fsm.enabled = true;
            fsm.SendEvent("FORCE_START");
        }

        GameObject line = new GameObject("AimLine");
        line.transform.SetParent(nail.transform);
        line.transform.localPosition = Vector3.zero;
        line.transform.localRotation = Quaternion.identity;

        var lr = line.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, spawnPos);
        lr.SetPosition(1, spawnPos + (Vector3)moveDirection * 60f);

        nail.SetActive(true);

        float elapsed = 0f;
        while (elapsed < 1.0f)
        {
            elapsed += Time.deltaTime;
            if (lr != null)
            {
                float alpha = (Mathf.Sin(elapsed * 25f) + 1f) / 2f;
                lr.startColor = new Color(1f, 0.1f, 0.1f, alpha * 0.8f);
                lr.endColor = new Color(1f, 0f, 0f, 0f);
            }
            yield return null;
        }

        if (line != null) Destroy(line);

        if (trail != null) trail.emitting = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = moveDirection * 50f;
        }

        yield return new WaitForSeconds(2.0f);

        if (trail != null) trail.emitting = false;
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