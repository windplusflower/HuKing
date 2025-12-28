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
    private List<GameObject> activeObstacles = new List<GameObject>();
    private List<GameObject> activeSaws = new List<GameObject>();
    private List<UnityEngine.Coroutine> activeShotRoutines = new List<UnityEngine.Coroutine>();
    private GameObject currentBossTarget;
    private float lastSawShotTime = 0f;

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

        SpawnBossTarget(heroPos + new Vector3(35f, 0, 0));
        SpawnPinballObstacles();
        yield return null;
    }

    private IEnumerator Loop_SawShotRoom()
    {
        yield return null;
    }

    private IEnumerator<Transition> Disappear_SawShotRoom()
    {
        if (level > 0 && shotPlatFrame[level - 1] != null)
        {
            shotPlatFrame[level - 1].SetActive(false);
        }

        foreach (var r in activeShotRoutines) if (r != null) StopCoroutine(r);
        activeShotRoutines.Clear();
        foreach (var saw in activeSaws) if (saw != null) Destroy(saw);
        activeSaws.Clear();

        foreach (var obs in activeObstacles) if (obs != null) Destroy(obs);
        activeObstacles.Clear();
        if (currentBossTarget != null) Destroy(currentBossTarget);

        yield return null;
    }
    private IEnumerator LaunchBouncingSaw(Vector3 startPos, Vector2 direction)
    {
        Vector3 spawnPos = startPos + (Vector3)direction * 0.8f + Vector3.up * 0.1f;
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

        while (aliveTime < 4.5f)
        {
            aliveTime += Time.deltaTime;
            saw.transform.position += (Vector3)currentDir * 35f * Time.deltaTime;
            saw.transform.Rotate(0, 0, 1200 * Time.deltaTime);

            if (aliveTime > 0.02f)
            {
                Collider2D heroCol = Physics2D.OverlapCircle(saw.transform.position, 0.4f, 1 << 9);
                if (heroCol != null)
                {
                    HeroController.instance.TakeDamage(heroCol.gameObject, GlobalEnums.CollisionSide.other, 1, 0);
                    break;
                }

                RaycastHit2D envHit = Physics2D.Raycast(saw.transform.position, currentDir, 0.4f, (1 << 8) | (1 << 11));
                if (envHit.collider != null)
                {
                    GameObject target = envHit.collider.gameObject;

                    if (target.layer == 11 || target == currentBossTarget || target == gameObject)
                    {
                        HealthManager hm = gameObject.GetComponent<HealthManager>();
                        if (hm != null)
                        {
                            HitInstance hi = new HitInstance
                            {
                                DamageDealt = 13,
                                Source = saw,
                                Direction = 0f,
                                Multiplier = 1f,
                                IgnoreInvulnerable = true,
                                AttackType = AttackTypes.Generic
                            };
                            hm.Hit(hi);
                        }
                        break;
                    }
                    else if (target.layer == 8)
                    {
                        if (!target.name.Contains("ShotFrame") && !target.name.Contains("gg_plat_float_wide"))
                        {
                            currentDir = Vector2.Reflect(currentDir, envHit.normal);
                            bounceCount++;
                            saw.transform.position += (Vector3)currentDir * 0.2f;
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
        foreach (var o in activeObstacles) if (o != null) Destroy(o);
        activeObstacles.Clear();

        CreateBoundaryWall(new Vector3((leftWall + rightWall) / 2f, upWall, 0), rightWall - leftWall, true);

        float shieldX = leftWall + (rightWall - leftWall) * 0.7f;
        float shieldY = (upWall + downWall) / 2f + 0.5f;
        GameObject shieldSaw = Instantiate(realSawPrefab, new Vector3(shieldX, shieldY, 0), Quaternion.identity);
        shieldSaw.transform.localScale = Vector3.one * 1f;
        shieldSaw.layer = 8;
        shieldSaw.name = "Boss_Shield_Saw_Bounce";
        if (shieldSaw.TryGetComponent<DamageHero>(out var dh)) Destroy(dh);
        shieldSaw.SetActive(true);
        activeObstacles.Add(shieldSaw);
        StartCoroutine(UpdateObstacleAction(shieldSaw, false, true));

        Vector3[] cornerPos = new Vector3[] {
            new Vector3(rightWall, upWall, 0),
            new Vector3(rightWall, downWall, 0)
        };

        foreach (var p in cornerPos)
        {
            GameObject fs = Instantiate(realSawPrefab, p, Quaternion.identity);
            fs.transform.localScale = Vector3.one * 1.2f * UnityEngine.Random.Range(0.8f, 1.1f);
            fs.layer = 8;
            if (fs.TryGetComponent<DamageHero>(out var fdh)) Destroy(fdh);
            fs.SetActive(true);
            activeObstacles.Add(fs);
            StartCoroutine(UpdateObstacleAction(fs, false, true));
        }

        int totalTarget = 1 + (level * 3);
        float typeRatio = UnityEngine.Random.Range(0.33f, 0.66f);
        int sawCount = Mathf.Clamp(Mathf.RoundToInt(totalTarget * typeRatio), 1, totalTarget - 1);
        int platCount = totalTarget - sawCount;
        int moveCount = Mathf.CeilToInt(totalTarget / 2f);

        List<int> typePool = new List<int>();
        for (int i = 0; i < sawCount; i++) typePool.Add(0);
        for (int i = 0; i < platCount; i++) typePool.Add(1);

        List<bool> actionPool = new List<bool>();
        for (int i = 0; i < totalTarget; i++) actionPool.Add(i < moveCount);

        for (int i = 0; i < totalTarget; i++)
        {
            int r = UnityEngine.Random.Range(i, totalTarget);
            (typePool[i], typePool[r]) = (typePool[r], typePool[i]);
            int r2 = UnityEngine.Random.Range(i, totalTarget);
            (actionPool[i], actionPool[r2]) = (actionPool[r2], actionPool[i]);
        }

        float margin = 2.5f;
        float spawnMinX = leftWall + 5f;
        float spawnMaxX = rightWall - 2f;
        float spawnMinY = downWall;
        float spawnMaxY = upWall;
        float midX = (spawnMinX + spawnMaxX) / 2f;
        float midY = (spawnMinY + spawnMaxY) / 2f;

        float[][] quadrants = new float[][] {
            new float[] { spawnMinX, midX, midY, spawnMaxY },
            new float[] { spawnMinX, midX, spawnMinY, midY },
            new float[] { midX, spawnMaxX, midY, spawnMaxY },
            new float[] { midX, spawnMaxX, spawnMinY, midY }
        };

        int basePerQuad = totalTarget / 4;
        int remainder = totalTarget % 4;
        int currentIdx = 0;

        for (int q = 0; q < 4; q++)
        {
            int countInThisQuad = basePerQuad;
            if (remainder == 1 && q == 0) countInThisQuad++;
            else if (remainder == 2 && q <= 1) countInThisQuad++;
            else if (remainder == 3 && q <= 2) countInThisQuad++;

            float[] b = quadrants[q];

            for (int i = 0; i < countInThisQuad; i++)
            {
                if (currentIdx >= totalTarget) break;

                Vector3 pos = new Vector3(
                    UnityEngine.Random.Range(b[0] + margin, b[1] - margin),
                    UnityEngine.Random.Range(b[2] + margin, b[3] - margin),
                    0
                );

                if (q == 2) pos.y += 1.0f;
                else if (q == 3) pos.y -= 1.0f;

                if (Vector3.Distance(pos, shieldSaw.transform.position) < 3.5f)
                    pos += (pos - shieldSaw.transform.position).normalized * 3.5f;

                int objType = typePool[currentIdx];
                bool isActive = actionPool[currentIdx];
                currentIdx++;

                GameObject obs = Instantiate(objType == 0 ? realSawPrefab : platPrefab, pos,
                    objType == 0 ? Quaternion.identity : Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 180f)));

                obs.name = objType == 0 ? "Pinball_Saw_Bounce" : "Pinball_Plat_Bounce";

                float randomScale = UnityEngine.Random.Range(0.8f, 1.1f);
                if (objType == 0)
                    obs.transform.localScale = Vector3.one * 0.75f * randomScale;
                else
                    obs.transform.localScale = new Vector3(1.3f * randomScale, 1.0f * randomScale, 1f);

                obs.layer = 8;
                obs.SetActive(true);
                activeObstacles.Add(obs);

                if (isActive)
                {
                    if (objType == 0) StartCoroutine(UpdateObstacleAction(obs, true, false));
                    else StartCoroutine(UpdateObstacleAction(obs, UnityEngine.Random.value > 0.6f, true));
                }
            }
        }
    }
    private void CreateBoundaryWall(Vector3 pos, float length, bool isHorizontal)
    {
        GameObject wall = Instantiate(platPrefab, pos, isHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90f));
        wall.transform.localScale = new Vector3(length / 3f, 1f, 1f);
        wall.layer = 8;
        wall.name = "Pinball_Boundary_Bounce";

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