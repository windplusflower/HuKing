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
    private List<GameObject> activeColumns = new List<GameObject>();
    private List<UnityEngine.Coroutine> activeShotRoutines = new List<UnityEngine.Coroutine>();
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
            CreatePlatSquare(4f, shotPlatFrame[i]);

            PlatFrameResponder[] responders = shotPlatFrame[i].GetComponentsInChildren<PlatFrameResponder>(true);
            foreach (var res in responders)
            {
                res.AllowHorizontal = false;
            }
        }

        On.HeroController.Attack += OnSawShotHeroAttack;
    }

    private IEnumerator<Transition> Appear_SawShotRoom()
    {
        Vector3 heroPos = HeroController.instance.transform.position;
        shotPlatFrame[level - 1].transform.position = heroPos;
        shotPlatFrame[level - 1].SetActive(true);

        SpawnColumns();
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

                HeroController.instance.transform.position = new Vector3(centerX, hp.y, hp.z);

                var camCtrl = GameCameras.instance.cameraController;
                var cam = GameCameras.instance.tk2dCam;

                if (camCtrl != null && cam != null)
                {
                    cam.ZoomFactor = 0.8f;

                    float visionFocusX = centerX + 15f;
                    float visionFocusY = hp.y + 5f;
                    camCtrl.transform.position = new Vector3(visionFocusX, visionFocusY, camCtrl.transform.position.z);
                    camCtrl.mode = CameraController.CameraMode.LOCKED;
                    var type = typeof(CameraController);
                    var xLockField = type.GetField("xLockPos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (xLockField != null) xLockField.SetValue(camCtrl, visionFocusX);

                    var yLockField = type.GetField("yLockPos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (yLockField != null) yLockField.SetValue(camCtrl, visionFocusY);
                }
            }
            yield return null;
        }
    }
    private IEnumerator<Transition> Disappear_SawShotRoom()
    {
        var cam = GameCameras.instance.tk2dCam;
        if (cam != null) cam.ZoomFactor = originalZoom;

        foreach (var r in activeShotRoutines) if (r != null) StopCoroutine(r);
        activeShotRoutines.Clear();
        foreach (var col in activeColumns) if (col != null) Destroy(col);
        activeColumns.Clear();

        shotPlatFrame[level - 1].SetActive(false);
        yield return new ToState { State = "Idle" };
    }

    private IEnumerator LaunchBouncingSaw(Vector3 startPos, Vector2 direction)
    {
        Vector3 spawnPos = startPos + (Vector3)direction * 2.2f + Vector3.up * 0.5f;
        GameObject saw = Instantiate(sawPrefab, spawnPos, Quaternion.identity);
        saw.transform.localScale = Vector3.one * 0.45f;
        saw.SetActive(true);

        var hazard = saw.GetComponent<DamageHero>();
        if (hazard != null) Destroy(hazard);
        var col = saw.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        int bounceCount = 0;
        Vector2 currentDir = direction;
        float aliveTime = 0f;

        while (bounceCount <= 8 && aliveTime < 7f)
        {
            aliveTime += Time.deltaTime;
            saw.transform.position += (Vector3)currentDir * 35f * Time.deltaTime;
            saw.transform.Rotate(0, 0, 1200 * Time.deltaTime);

            if (aliveTime > 0.2f)
            {
                RaycastHit2D heroHit = Physics2D.Raycast(saw.transform.position, currentDir, 0.7f, 1 << 9);
                if (heroHit.collider != null)
                {
                    HeroController.instance.TakeDamage(heroHit.collider.gameObject, GlobalEnums.CollisionSide.other, 1, 0);
                    break;
                }
            }
            RaycastHit2D envHit = Physics2D.Raycast(saw.transform.position, currentDir, 0.7f, 1 << 8 | 1 << 11);
            if (envHit.collider != null)
            {
                GameObject target = envHit.collider.gameObject;
                if (target.layer == 11)
                {
                    target.SendMessage("TakeDamage", 13, SendMessageOptions.DontRequireReceiver);
                    break;
                }
                else if (target.layer == 8)
                {
                    if (target.transform.IsChildOf(shotPlatFrame[level - 1].transform)) { yield return null; continue; }

                    currentDir = Vector2.Reflect(currentDir, envHit.normal);
                    bounceCount++;

                    saw.transform.position += (Vector3)currentDir * 0.2f;
                }
            }
            yield return null;
        }
        Destroy(saw);
    }

    private void SpawnColumns()
    {
        foreach (var c in activeColumns) if (c != null) Destroy(c);
        activeColumns.Clear();

        Vector3 hp = HeroController.instance.transform.position;
        int count = level + 1;

        for (int i = 0; i < count; i++)
        {
            GameObject group = new GameObject($"SawCol_{i}");
            // 再次增加偏移，让第一根柱子离英雄更远，方便观察
            group.transform.position = new Vector3(hp.x + 15f + (i * 9f), hp.y, 0);

            float gapY = UnityEngine.Random.Range(-3.5f, 3.5f);
            float gapSize = 2.8f;

            CreateColumnPart(group.transform, new Vector3(0, gapY + gapSize + 7.5f, 0));
            CreateColumnPart(group.transform, new Vector3(0, gapY - gapSize - 7.5f, 0));

            StartCoroutine(UpdateColumnMovement(group, i, group.transform.position));
            activeColumns.Add(group);
        }
    }

    private void CreateColumnPart(Transform parent, Vector3 localPos)
    {
        GameObject part = Instantiate(platPrefab, parent);
        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        part.transform.localScale = new Vector3(15f, 1.3f, 1f);
        part.layer = 8;
        part.SetActive(true);
    }

    private IEnumerator UpdateColumnMovement(GameObject col, int index, Vector3 start)
    {
        float speed = 1.0f + (index * 0.3f);
        while (col != null)
        {
            float y = start.y + Mathf.Sin(Time.time * speed) * 8f;
            float x = start.x + (level >= 3 ? Mathf.Cos(Time.time * speed * 0.5f) * 3f : 0);
            col.transform.position = new Vector3(x, y, 0);
            yield return null;
        }
    }

    private void OnSawShotHeroAttack(On.HeroController.orig_Attack orig, HeroController self, GlobalEnums.AttackDirection attackDir)
    {
        if (IsSawShotActive() && GameManager.instance.inputHandler.inputActions.down.IsPressed)
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
        else if (IsSawShotActive())
        {
            HandleSawShotRoomActions(self, attackDir);
        }
        orig(self, attackDir);
    }

    private void HandleSawShotRoomActions(HeroController self, GlobalEnums.AttackDirection dir)
    {
        if (dir == GlobalEnums.AttackDirection.normal && self.cState.facingRight)
        {
            if (Time.time > lastSawShotTime + 0.1f)
            {
                lastSawShotTime = Time.time;
                activeShotRoutines.Add(StartCoroutine(LaunchBouncingSaw(self.transform.position, Vector2.right)));
            }
        }
    }

    private bool IsSawShotActive() => shotPlatFrame[level - 1] != null && shotPlatFrame[level - 1].activeInHierarchy;
}