using System.Collections;
using System.Collections.Generic;
using GlobalEnums;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private GameObject[] shotPlatFrame = new GameObject[3];
    private List<GameObject> activeObstacles = new List<GameObject>(); // 替换了 activeColumns
    private List<GameObject> activeSaws = new List<GameObject>(); // 记录玩家射出的电锯实体
    private List<UnityEngine.Coroutine> activeShotRoutines = new List<UnityEngine.Coroutine>();
    private GameObject currentBossTarget; // 代表 Boss 的实体
    private float lastSawShotTime = 0f;
    private float originalZoom = 1f;

    [State]
    private IEnumerator<Transition> SawShotRoom()
    {
        yield return new ToState { State = nameof(Appear) };
    }

    private void registerSawShotRoom()
    {
        skillTable.Add(nameof(SawShotRoom), new SkillPhases(
            () => Appear_SawShotRoom(),
            () => Loop_SawShotRoom(),
            () => Disappear_SawShotRoom()
        ));

        for (int i = 0; i < 3; i++)
        {
            shotPlatFrame[i] = new GameObject($"ShotFrame_Level_{i + 1}");
            CreatePlatSquare(3f, shotPlatFrame[i]);

            PlatFrameResponder[] responders = shotPlatFrame[i].GetComponentsInChildren<PlatFrameResponder>(true);
            foreach (var res in responders)
            {
                res.AllowHorizontal = false;
            }
        }

        On.HeroController.Attack -= OnSawShotHeroAttack;
        On.HeroController.Attack += OnSawShotHeroAttack;
    }

    private IEnumerator<Transition> Appear_SawShotRoom()
    {
        Vector3 heroPos = HeroController.instance.transform.position;
        shotPlatFrame[level - 1].transform.position = heroPos;
        shotPlatFrame[level - 1].SetActive(true);

        if (GameCameras.instance.tk2dCam != null)
            originalZoom = GameCameras.instance.tk2dCam.ZoomFactor;

        // --- 初始化三维弹球环境 ---
        SpawnBossTarget(heroPos + new Vector3(35f, 0, 0));
        SpawnPinballObstacles();

        yield return null;
    }

    private IEnumerator Loop_SawShotRoom()
    {
        while (true)
        {
            GameObject currentFrame = shotPlatFrame[level - 1];
            if (currentFrame != null && IsSawShotActive())
            {
                float centerX = currentFrame.transform.position.x;
                Vector3 hp = HeroController.instance.transform.position;

                var camCtrl = GameCameras.instance.cameraController;
                var cam = GameCameras.instance.tk2dCam;

                if (camCtrl != null && cam != null)
                {
                    cam.ZoomFactor = 0.82f;
                    // 视角往右拉，方便看弹道
                    float visionFocusX = centerX + 15f;
                    float visionFocusY = hp.y + 4.5f;

                    camCtrl.transform.position = new Vector3(visionFocusX, visionFocusY, camCtrl.transform.position.z);
                    camCtrl.mode = CameraController.CameraMode.LOCKED;

                    var type = typeof(CameraController);
                    type.GetField("xLockPos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(camCtrl, visionFocusX);
                    type.GetField("yLockPos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(camCtrl, visionFocusY);
                }
            }
            yield return null;
        }
    }
    private IEnumerator<Transition> Disappear_SawShotRoom()
    {
        var cam = GameCameras.instance.tk2dCam;
        if (cam != null) cam.ZoomFactor = originalZoom;

        // 停止所有发射协程
        foreach (var r in activeShotRoutines) if (r != null) StopCoroutine(r);
        activeShotRoutines.Clear();

        // 销毁所有还在飞的电锯实体 (新增)
        foreach (var saw in activeSaws) if (saw != null) Destroy(saw);
        activeSaws.Clear();

        // 清理障碍物和 Boss
        foreach (var obs in activeObstacles) if (obs != null) Destroy(obs);
        activeObstacles.Clear();

        if (currentBossTarget != null) Destroy(currentBossTarget);

        if (level > 0 && shotPlatFrame[level - 1] != null)
            shotPlatFrame[level - 1].SetActive(false);

        yield return null;
    }
    private IEnumerator LaunchBouncingSaw(Vector3 startPos, Vector2 direction)
    {
        Vector3 spawnPos = startPos + (Vector3)direction * 2.2f + Vector3.up * 0.1f;
        GameObject saw = Instantiate(realSawPrefab, spawnPos, Quaternion.identity);
        activeSaws.Add(saw);
        saw.transform.localScale = Vector3.one * 0.22f;
        saw.SetActive(true);

        var hazard = saw.GetComponent<DamageHero>();
        if (hazard != null) Destroy(hazard);
        var col = saw.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        int bounceCount = 0;
        Vector2 currentDir = direction;
        float aliveTime = 0f;

        while (aliveTime < 4f)
        {
            aliveTime += Time.deltaTime;
            saw.transform.position += (Vector3)currentDir * 35f * Time.deltaTime;
            saw.transform.Rotate(0, 0, 1200 * Time.deltaTime);

            if (aliveTime > 0.02f)
            {
                // 1. 小骑士判定
                Collider2D heroCol = Physics2D.OverlapCircle(saw.transform.position, 0.4f, 1 << 9);
                if (heroCol != null)
                {
                    HeroController.instance.TakeDamage(heroCol.gameObject, GlobalEnums.CollisionSide.other, 1, 0);
                    break;
                }
                // 2. 环境与 Boss 判定 (探测距离设为 1.0f 确保高速下不穿透)
                RaycastHit2D envHit = Physics2D.Raycast(saw.transform.position, currentDir, 1.0f, (1 << 8) | (1 << 11));
                if (envHit.collider != null)
                {
                    GameObject target = envHit.collider.gameObject;

                    // --- 还原伤害逻辑 ---
                    if (target.layer == 11 || target == currentBossTarget || target == gameObject)
                    {
                        HealthManager hm = gameObject.GetComponent<HealthManager>();
                        if (hm != null)
                        {
                            HitInstance hi = new HitInstance
                            {
                                DamageDealt = 13,        // 你可以根据 level 调整伤害
                                Source = saw,
                                Direction = 0f,
                                Multiplier = 1f,
                                IgnoreInvulnerable = true, // 确保弹球必定能造成伤害
                                AttackType = AttackTypes.Generic
                            };

                            // 执行真正的伤害
                            hm.Hit(hi);
                        }
                        break; // 击中 Boss 后销毁电锯
                    }
                    // --- 反弹逻辑 (已包含白名单修正) ---
                    else if (target.layer == 8)
                    {
                        // 排除玩家方框，其余环境物体（包括护盾、墙壁、地面）全部反弹
                        if (!target.name.Contains("ShotFrame") && !target.name.Contains("gg_plat_float_wide"))
                        {
                            currentDir = Vector2.Reflect(currentDir, envHit.normal);
                            bounceCount++;

                            // 增加位移修正防止卡进碰撞体
                            saw.transform.position += (Vector3)currentDir * 0.6f;
                        }
                    }
                }
            }
            yield return null;
        }
        activeSaws.Remove(saw);
        Destroy(saw);
    }
    private void SpawnPinballObstacles()
    {
        // 1. 基础清理
        foreach (var o in activeObstacles) if (o != null) Destroy(o);
        activeObstacles.Clear();

        // 2. 环境固定物生成 (天花板、护盾)
        CreateBoundaryWall(new Vector3((leftWall + rightWall) / 2f, upWall, 0), rightWall - leftWall, true);

        float shieldX = leftWall + (rightWall - leftWall) * 0.7f;
        float shieldY = (upWall + downWall) / 2f;
        GameObject shieldSaw = Instantiate(realSawPrefab, new Vector3(shieldX, shieldY, 0), Quaternion.identity);
        shieldSaw.transform.localScale = Vector3.one * 1f;
        shieldSaw.layer = 8;
        shieldSaw.name = "Boss_Shield_Saw_Bounce";
        if (shieldSaw.TryGetComponent<DamageHero>(out var dh)) Destroy(dh);
        shieldSaw.SetActive(true);
        activeObstacles.Add(shieldSaw);
        StartCoroutine(UpdateObstacleAction(shieldSaw, false, true));

        // 3. 数量规划
        int totalTarget = 1 + (level * 3);

        // 类型比例规划
        float typeRatio = UnityEngine.Random.Range(0.33f, 0.66f);
        int sawCount = Mathf.Clamp(Mathf.RoundToInt(totalTarget * typeRatio), 1, totalTarget - 1);
        int platCount = totalTarget - sawCount;

        // 动作比例规划：严格确保至少一半
        int moveCount = Mathf.CeilToInt(totalTarget / 2f);

        // 4. 构建并洗牌属性池 (保证数量绝对精确)
        List<int> typePool = new List<int>();
        for (int i = 0; i < sawCount; i++) typePool.Add(0); // Saw
        for (int i = 0; i < platCount; i++) typePool.Add(1); // Plat

        List<bool> actionPool = new List<bool>();
        for (int i = 0; i < totalTarget; i++) actionPool.Add(i < moveCount);

        // Fisher-Yates 洗牌
        for (int i = 0; i < totalTarget; i++)
        {
            int r = UnityEngine.Random.Range(i, totalTarget);
            (typePool[i], typePool[r]) = (typePool[r], typePool[i]);

            int r2 = UnityEngine.Random.Range(i, totalTarget);
            (actionPool[i], actionPool[r2]) = (actionPool[r2], actionPool[i]);
        }

        // 5. 象限坐标与余数分配逻辑
        float margin = 2.5f;
        float spawnMinX = leftWall + 5f;
        float spawnMaxX = rightWall - 2f;
        float spawnMinY = downWall;
        float spawnMaxY = upWall;
        float midX = (spawnMinX + spawnMaxX) / 2f;
        float midY = (spawnMinY + spawnMaxY) / 2f;

        // 象限索引顺序：0:左上, 1:左下, 2:右上, 3:右下 (先排左侧)
        float[][] quadrants = new float[][] {
        new float[] { spawnMinX, midX, midY, spawnMaxY }, // 0: 左上
        new float[] { spawnMinX, midX, spawnMinY, midY }, // 1: 左下
        new float[] { midX, spawnMaxX, midY, spawnMaxY }, // 2: 右上
        new float[] { midX, spawnMaxX, spawnMinY, midY }  // 3: 右下
    };

        int basePerQuad = totalTarget / 4;
        int remainder = totalTarget % 4;
        int currentIdx = 0;

        for (int q = 0; q < 4; q++)
        {
            // 余数处理逻辑：
            // 1个余数 -> 给左上(q=0)
            // 2个余数 -> 给左上(q=0)和左下(q=1)
            // 3个余数 -> 给左上、左下和右上(q=2)
            int countInThisQuad = basePerQuad;
            if (remainder == 1 && q == 0) countInThisQuad++;
            else if (remainder == 2 && q <= 1) countInThisQuad++;
            else if (remainder == 3 && q <= 2) countInThisQuad++;

            float[] b = quadrants[q];

            for (int i = 0; i < countInThisQuad; i++)
            {
                if (currentIdx >= totalTarget) break; // 安全检查

                Vector3 pos = new Vector3(
                    UnityEngine.Random.Range(b[0] + margin, b[1] - margin),
                    UnityEngine.Random.Range(b[2] + margin, b[3] - margin),
                    0
                );

                if (Vector3.Distance(pos, shieldSaw.transform.position) < 3.5f)
                    pos += (pos - shieldSaw.transform.position).normalized * 3.5f;

                int objType = typePool[currentIdx];
                bool isActive = actionPool[currentIdx];
                currentIdx++;

                GameObject obs = Instantiate(objType == 0 ? realSawPrefab : platPrefab, pos,
                    objType == 0 ? Quaternion.identity : Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 180f)));

                obs.name = objType == 0 ? "Pinball_Saw_Bounce" : "Pinball_Plat_Bounce";
                obs.transform.localScale = objType == 0 ? Vector3.one * 0.75f : new Vector3(1.3f, 1.0f, 1f);
                obs.layer = 8;
                obs.SetActive(true);
                activeObstacles.Add(obs);

                if (isActive)
                {
                    if (objType == 0) // 如果是电锯
                    {
                        StartCoroutine(UpdateObstacleAction(obs, true, false));
                    }
                    else // 如果是平台
                    {
                        bool m = UnityEngine.Random.value > 0.6f;
                        bool r = !m || UnityEngine.Random.value > 0.4f;
                        StartCoroutine(UpdateObstacleAction(obs, m, r));
                    }
                }
            }
        }
    }
    // 辅助方法：在指定位置生成边界墙
    private void CreateBoundaryWall(Vector3 pos, float length, bool isHorizontal)
    {
        GameObject wall = Instantiate(platPrefab, pos, isHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90f));
        // platPrefab 原始长度大概是 3，所以缩放值为 length / 3
        wall.transform.localScale = new Vector3(length / 3f, 1f, 1f);
        wall.layer = 8;
        wall.name = "Pinball_Boundary_Bounce";

        // 如果不希望玩家看到这些边界平台，可以禁用 Renderer
        // var rend = wall.GetComponent<MeshRenderer>();
        // if(rend != null) rend.enabled = false;

        wall.SetActive(true);
        activeObstacles.Add(wall);
    }

    private IEnumerator UpdateObstacleAction(GameObject obs, bool move, bool rotate)
    {
        Vector3 start = obs.transform.position;
        float speed = UnityEngine.Random.Range(1.2f, 2.5f);
        float dist = UnityEngine.Random.Range(2f, 5f);
        float rotSpeed = UnityEngine.Random.Range(40f, 120f);

        while (obs != null)
        {
            if (move) obs.transform.position = start + new Vector3(0, Mathf.Sin(Time.time * speed) * dist, 0);
            if (rotate) obs.transform.Rotate(0, 0, rotSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private void SpawnBossTarget(Vector3 pos)
    {
        if (currentBossTarget != null) Destroy(currentBossTarget);
        // 使用本体作为目标，并剥离 AI 逻辑只留碰撞
        currentBossTarget = Instantiate(gameObject, pos, Quaternion.identity);
        var sm = currentBossTarget.GetComponent<HuStateMachine>();
        if (sm != null) Destroy(sm);

        currentBossTarget.layer = 11;
        currentBossTarget.name = "Boss_Pinball_Target";
        currentBossTarget.SetActive(true);
    }

    private void OnSawShotHeroAttack(On.HeroController.orig_Attack orig, HeroController self, GlobalEnums.AttackDirection attackDir)
    {
        if (IsSawShotActive())
        {
            if (GameManager.instance.inputHandler.inputActions.down.IsPressed)
            {
                attackDir = GlobalEnums.AttackDirection.downward;
                if (self.cState.onGround)
                {
                    self.cState.onGround = false;
                    orig(self, attackDir);
                    self.cState.onGround = true;
                    return;
                }
            }
            HandleSawShotRoomActions(self, attackDir);
        }
        orig(self, attackDir);
    }

    private void HandleSawShotRoomActions(HeroController self, GlobalEnums.AttackDirection dir)
    {
        if (dir == GlobalEnums.AttackDirection.normal && self.cState.facingRight)
        {
            if (Time.time > lastSawShotTime + 0.12f)
            {
                lastSawShotTime = Time.time;
                activeShotRoutines.Add(StartCoroutine(LaunchBouncingSaw(self.transform.position, Vector2.right)));
            }
        }
    }

    private bool IsSawShotActive() => level > 0 && shotPlatFrame[level - 1] != null && shotPlatFrame[level - 1].activeInHierarchy;
}