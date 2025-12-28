using System.Collections;
using Modding.Utils;
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
    private List<UnityEngine.Coroutine> activeNailRoutines = new List<UnityEngine.Coroutine>();
    public class PlatFrameData : MonoBehaviour
    {
        public float Size;
    }
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

            PlayMakerFSM fsm = nail.LocateMyFSM("Control");
            if (fsm != null)
            {
                fsm.CopyState("Fan Antic", "Prepare");
                fsm.AddGlobalTransition("FORCE_START", "Prepare");
                fsm.ChangeTransition("Prepare", "FINISHED", "Idle");
            }
            nailPool.Enqueue(nail);
        }
        On.HeroController.CanWallJump += OnCanWallJump;
        On.HeroController.CanWallSlide += OnCanWallSlide;
        On.HeroController.Attack += OnHeroAttack;
    }
    private IEnumerator<Transition> Appear_SawNailRoom()
    {
        SetPlatFrameToHeroCenter();
        HuKing.instance.Log($"appearing SawNailRoom level {level}");
        platFrame[level - 1].SetActive(true);
        yield return null;
    }
    [State]
    private IEnumerator<Transition> Disappear_SawNailRoom()
    {
        // 1. 停止所有追踪的协程
        if (activeNailRoutines != null)
        {
            foreach (var routine in activeNailRoutines)
            {
                if (routine != null) StopCoroutine(routine);
            }
            activeNailRoutines.Clear();
        }

        // 2. 停止并还原小骑士音效 (使用 instance)
        var hc = HeroController.instance;
        if (hc != null)
        {
            var ha = hc.GetComponent<HeroAudioController>();
            if (ha != null)
            {
                if (ha.dash != null)
                {
                    ha.dash.Stop();
                    ha.dash.pitch = 1f;
                    ha.dash.spatialBlend = 0.9f;
                }
                if (ha.jump != null)
                {
                    ha.jump.Stop();
                    ha.jump.pitch = 1f;
                }
            }
        }

        // 3. 隐藏飞刺并清理残留红线
        if (nailPool != null)
        {
            foreach (var nail in nailPool)
            {
                if (nail != null)
                {
                    Transform line = nail.transform.Find("AimLine");
                    if (line != null) Destroy(line.gameObject);
                    nail.SetActive(false);
                }
            }
        }

        // 4. 隐藏平台
        if (platFrame != null)
        {
            foreach (var plat in platFrame)
            {
                if (plat != null) plat.SetActive(false);
            }
        }

        yield return null;
    }
    private IEnumerator Loop_SawNailRoom()
    {
        while (true)
        {
            // 每次启动发射协程时，都将其句柄加入列表
            for (int i = 0; i < 2; i++)
            {
                activeNailRoutines.Add(StartCoroutine(FireNail_RandomCircle(HeroController.instance.transform.position, GetBattleArea())));
            }

            if (level >= 2)
            {
                activeNailRoutines.Add(StartCoroutine(FireNail_Horizontal(HeroController.instance.transform.position, GetBattleArea())));
            }

            if (level >= 3)
            {
                activeNailRoutines.Add(StartCoroutine(FireNail_Vertical(HeroController.instance.transform.position, GetBattleArea())));
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
        if (nailPool == null || nailPool.Count == 0) yield break;
        GameObject nail = nailPool.Dequeue();
        nailPool.Enqueue(nail);
        nail.SetActive(false);

        var rb = nail.GetComponent<Rigidbody2D>();
        // 直接获取 HeroAudioController
        var ha = (HeroController.instance != null) ? HeroController.instance.GetComponent<HeroAudioController>() : null;

        nail.transform.position = spawnPos;
        float lookAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        nail.transform.eulerAngles = new Vector3(0, 0, lookAngle + lookAngleOffset);

        // --- 视觉预警 (无声音) ---
        GameObject line = new GameObject("AimLine");
        line.transform.SetParent(nail.transform);
        line.transform.localPosition = Vector3.zero;
        var lr = line.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = 0.15f; lr.endWidth = 0.15f;
        lr.SetPosition(0, spawnPos);
        lr.SetPosition(1, spawnPos + (Vector3)moveDirection * 60f);

        nail.SetActive(true);

        float timer = 0f;
        while (timer < 0.8f)
        {
            timer += Time.deltaTime;
            if (lr != null)
                lr.startColor = new Color(1, 0, 0, Mathf.PingPong(Time.time * 20, 1));
            yield return null;
        }

        if (line != null) Destroy(line.gameObject);

        if (ha != null && ha.dash != null)
        {
            ha.dash.Stop();
            ha.dash.spatialBlend = 0f;
            ha.dash.pitch = 1.5f;
            ha.dash.volume = 1f;

            ha.dash.PlayOneShot(ha.dash.clip);
            ha.dash.PlayOneShot(ha.dash.clip);

            if (ha.jump != null)
            {
                ha.jump.pitch = 1.1f;
                ha.jump.volume = 0.7f;
                ha.jump.PlayOneShot(ha.jump.clip);
            }
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = moveDirection * 60f;
        }

        yield return new WaitForSeconds(2.0f);
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
        // 添加数据组件并记录尺寸
        var data = parent.GetOrAddComponent<PlatFrameData>();
        data.Size = size;

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
        if (isMoving || targetFrame == null) yield break;
        isMoving = true;

        // 1. 从对象本身获取尺寸，而非依赖外部的 level 变量
        float currentSize = 8f; // 默认值
        var data = targetFrame.GetComponent<PlatFrameData>();
        if (data != null)
        {
            currentSize = data.Size;
        }

        Rigidbody2D rb = targetFrame.GetComponent<Rigidbody2D>();
        Vector3 startPos = targetFrame.transform.position;

        float maxMoveDist = 2f;
        // 使用动态获取的尺寸来计算 BoxCast 大小
        Vector2 boxSize = new Vector2(currentSize * 0.9f, currentSize * 0.9f);

        // 2. 物理探测
        RaycastHit2D hit = Physics2D.BoxCast(startPos, boxSize, 0f, direction, maxMoveDist, 1 << 8);

        float finalMoveDist = maxMoveDist;
        if (hit.collider != null)
        {
            finalMoveDist = Mathf.Max(0, hit.distance - 0.05f);
        }

        // 3. 边界约束（可选：如果 UpWall 等是全局的话）
        if (direction.y > 0.1f)
        {
            float remainingDist = upWall - startPos.y;
            finalMoveDist = Mathf.Min(finalMoveDist, Mathf.Max(0, remainingDist));
        }

        Vector3 targetPos = startPos + (Vector3)direction * finalMoveDist;

        // 4. 平滑移动
        float elapsed = 0f;
        float duration = 0.1f;
        while (elapsed < duration)
        {
            if (targetFrame == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rb.MovePosition(Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0, 1, t)));
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
    private bool IsSawNailSkillActive()
    {
        // 遍历所有平台，只要有一个在场，就说明技能正在进行
        if (platFrame == null) return false;
        for (int i = 0; i < platFrame.Length; i++)
        {
            if (platFrame[i] != null && platFrame[i].activeInHierarchy) return true;
        }
        return false;
    }

    private bool OnCanWallJump(On.HeroController.orig_CanWallJump orig, HeroController self)
    {
        if (IsSawNailSkillActive()) return false;
        return orig(self);
    }

    private bool OnCanWallSlide(On.HeroController.orig_CanWallSlide orig, HeroController self)
    {
        if (IsSawNailSkillActive()) return false;
        return orig(self);
    }

    private void OnHeroAttack(On.HeroController.orig_Attack orig, HeroController self, GlobalEnums.AttackDirection attackDir)
    {
        if (IsSawNailSkillActive() && GameManager.instance.inputHandler.inputActions.down.IsPressed)
        {
            attackDir = GlobalEnums.AttackDirection.downward;
            bool wasOnGround = self.cState.onGround;
            if (wasOnGround)
            {
                self.cState.onGround = false;
                orig(self, attackDir);
                self.cState.onGround = wasOnGround;
                return;
            }
        }
        orig(self, attackDir);
    }

}