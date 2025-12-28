using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private tk2dSpriteAnimator animator = new();
    private GameObject sawPrefab, realSawPrefab, ringPrefab, beam, stomperPrefab, platPrefab, nailPrefab;
    private GameObject warpIn, warpOut, flash;
    private bool enableShining = true;

    static private float leftWall = 32f, rightWall = 66f, downWall = 2.5f, upWall = 17f;
    private float fixedCameraX => (leftWall + rightWall) / 2f;
    private float fixedCameraY => (downWall + upWall) / 2f + 2f;

    private Queue<GameObject> saws = new();
    private Queue<GameObject> blankSaws = new();
    private Queue<GameObject> stompers = new();
    private HealthManager HPManager;
    private int originalHp;

    private Vector3 targetKnightPosition;
    private Vector3 targetHuPosition;

    private float globalZoom = 0.82f;
    private bool isBossActive = true;

    private int hitCount = 0;
    private float sawSize = 0.3f;
    private string SkillChoosen = "None";
    private int level = 1;
    private Dictionary<string, SkillPhases> skillTable = new Dictionary<string, SkillPhases>();

    public HuStateMachine() : base(
        startState: nameof(Idle),
        globalTransitions: [],
        terrainLayer: "terrain",
        epsilon: 0.01f,
        horizontalCornerCorrection: false,
        spriteFacingLeft: true)
    {
    }

    public void init(GameObject sawPrefab, GameObject realSawPrefab, GameObject ringPrefab, GameObject beamPrefab, GameObject stomperPrefab, GameObject platPrefab, GameObject nailPrefab, float sawsize, bool enableShining)
    {
        this.sawPrefab = sawPrefab;
        this.realSawPrefab = realSawPrefab;
        this.ringPrefab = ringPrefab;
        this.beam = Instantiate(beamPrefab);
        this.stomperPrefab = stomperPrefab;
        this.platPrefab = platPrefab;
        this.nailPrefab = nailPrefab;
        this.sawSize = sawsize;
        this.enableShining = enableShining;
    }

    protected override void EntityStateMachineFixedUpdate()
    {
        Modding.ReflectionHelper.SetField(HeroController.instance, "nailChargeTimer", 0f);
    }
    protected override void EntityStateMachineUpdate()
    {
        base.EntityStateMachineUpdate();
        if (entryFinish == false) return;
        var camCtrl = GameCameras.instance.cameraController;
        var tkCam = GameCameras.instance.tk2dCam;
        var type = typeof(CameraController);
        var fieldX = type.GetField("xLockPos", BindingFlags.Instance | BindingFlags.NonPublic);
        var fieldY = type.GetField("yLockPos", BindingFlags.Instance | BindingFlags.NonPublic);
        Vector3 startPos = camCtrl.transform.position;
        Vector3 finalTargetPos = new Vector3(fixedCameraX, fixedCameraY, startPos.z);
        if (camCtrl != null && tkCam != null)
        {
            camCtrl.mode = CameraController.CameraMode.LOCKED;
            tkCam.ZoomFactor = globalZoom;
            camCtrl.transform.position = finalTargetPos;
            fieldX?.SetValue(camCtrl, finalTargetPos.x);
            fieldY?.SetValue(camCtrl, finalTargetPos.y);
        }
    }

    protected override void EntityStateMachineStart()
    {
        animator = gameObject.GetComponent<tk2dSpriteAnimator>();
        warpIn = gameObject.FindGameObjectInChildren("Warp");
        warpOut = gameObject.FindGameObjectInChildren("Warp Out");
        flash = gameObject.FindGameObjectInChildren("White Flash");

        for (int i = 0; i < 150; i++)
        {
            blankSaws.Enqueue(Instantiate(sawPrefab));
        }

        SetupStompers();

        HPManager = gameObject.GetComponent<HealthManager>();
        HPManager.hp = 601;
        if (BossSceneController.Instance.BossLevel > 0)
        {
            HPManager.hp = 1501;
        }
        originalHp = HPManager.hp;

        registerSkills();

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        StartCoroutine(PermanentCameraLock());
    }
    private bool entryFinish = false;
    private IEnumerator PermanentCameraLock()
    {
        var camCtrl = GameCameras.instance.cameraController;
        var tkCam = GameCameras.instance.tk2dCam;
        var type = typeof(CameraController);
        var fieldX = type.GetField("xLockPos", BindingFlags.Instance | BindingFlags.NonPublic);
        var fieldY = type.GetField("yLockPos", BindingFlags.Instance | BindingFlags.NonPublic);

        float elapsed = 0f;
        float duration = 2.0f;
        float startZoom = tkCam.ZoomFactor;
        Vector3 startPos = camCtrl.transform.position;
        Vector3 finalTargetPos = new Vector3(fixedCameraX, fixedCameraY, startPos.z);

        // 阶段 1：平滑切入
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            float curZoom = Mathf.Lerp(startZoom, globalZoom, t);
            float curX = Mathf.Lerp(startPos.x, finalTargetPos.x, t);
            float curY = Mathf.Lerp(startPos.y, finalTargetPos.y, t);

            camCtrl.mode = CameraController.CameraMode.LOCKED;
            tkCam.ZoomFactor = curZoom;
            camCtrl.transform.position = new Vector3(curX, curY, startPos.z);
            fieldX?.SetValue(camCtrl, curX);
            fieldY?.SetValue(camCtrl, curY);
            yield return null;
        }
        entryFinish = true;
    }
    private IEnumerator RestoreCameraZoomGlobal()
    {
        var tkCam = GameCameras.instance?.tk2dCam;
        if (tkCam == null) yield break;

        float restoreElapsed = 0f;
        float restoreDuration = 1.2f; // 恢复动画持续时间
        float zoomAtDeath = tkCam.ZoomFactor;

        // 如果当前缩放已经接近 1.0，则无需执行，直接修正
        if (Mathf.Abs(zoomAtDeath - 1.0f) < 0.01f)
        {
            tkCam.ZoomFactor = 1.0f;
            yield break;
        }

        while (restoreElapsed < restoreDuration)
        {
            // 使用 unscaledDeltaTime 确保在游戏顿帧或暂停时逻辑依然平滑
            restoreElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, restoreElapsed / restoreDuration);

            if (tkCam != null)
            {
                // 平滑插值回到默认的 1.0 缩放
                tkCam.ZoomFactor = Mathf.Lerp(zoomAtDeath, 1.0f, t);
            }
            yield return null;
        }

        // 确保最终值精确为 1.0
        if (tkCam != null)
        {
            tkCam.ZoomFactor = 1.0f;
        }
        entryFinish = false;
    }

    private void SetupStompers()
    {
        for (int i = 0; i < 20; i++)
        {
            var stomper = Instantiate(stomperPrefab);
            stomper.GetComponent<Animator>().enabled = false;
            stomper.LocateMyFSM("damages_hero").enabled = false;
            stomper.LocateMyFSM("Spike Hit Effect").enabled = false;
            stomper.GetComponent<StopAnimatorsAtPoint>().enabled = false;
            Transform child = stomper.transform.Find("physical space");
            GameObject clone = Instantiate(child.gameObject, child.parent);
            clone.name = child.name + "_Copy";
            clone.transform.localPosition = child.localPosition + new Vector3(0.02f, 0f, 0f);
            stomper.AddComponent<StomperStateMachine>().init(1f, 1f, 1f, true);
            stompers.Enqueue(stomper);
        }
    }

    private void registerSkills()
    {
        registerSawRoom();
        registerBoxRoom();
        registerSawNailRoom();
        registerSawShotRoom();
    }

    private GameObject Target() => HeroController.instance.gameObject;

}