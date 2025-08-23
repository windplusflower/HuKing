using HutongGames.PlayMaker.Actions;
using RingLib;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;
namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine {
    private Config config = new();
    private tk2dSpriteAnimator animator = new();
    private GameObject sawPrefab, ringPrefab, beam, stomperPrefab;
    private GameObject warpIn, warpOut, flash;
    private bool enableShining = true;
    static private float leftWall = 32f, rightWall = 66f, downWall = 2.5f, upWall = 17f;
    private Queue<GameObject> saws = new();
    private Queue<GameObject> blankSaws = new();
    private Queue<GameObject> stompers = new();
    private HealthManager HPManager;
    private int originalHp;
    private Vector3 targetKnightPosition;
    private Vector3 targetHuPosition;
    private int hitCount = 0;
    private float sawsize = 0.3f;
    private string SkillChoosen = "None";

    public HuStateMachine() : base(
        startState: nameof(Idle),
        globalTransitions: [],
        terrainLayer: "terrain",
        epsilon: 0.01f,
        horizontalCornerCorrection: false,
        spriteFacingLeft: true) {
    }
    public void init(GameObject sawPrefab, GameObject ringPrefab, GameObject beamPrefab, GameObject stomperPrefab, float sawsize, bool enableShining) {
        this.sawPrefab = sawPrefab;
        this.ringPrefab = ringPrefab;
        this.beam = Instantiate(beamPrefab);
        this.stomperPrefab = stomperPrefab;
        this.sawsize = sawsize;
        this.enableShining = enableShining;
    }

    protected override void EntityStateMachineStart() {
        // A separate GameObject for animation is good for adjusting offsets
        animator = gameObject.GetComponent<tk2dSpriteAnimator>();
        warpIn = gameObject.FindGameObjectInChildren("Warp");
        warpOut = gameObject.FindGameObjectInChildren("Warp Out");
        flash = gameObject.FindGameObjectInChildren("White Flash");
        for (int i = 0; i < 150; i++) {
            blankSaws.Enqueue(Instantiate(sawPrefab));
        }
        for (int i = 0; i < 20; i++) {
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
        HPManager = gameObject.GetComponent<HealthManager>();
        HPManager.hp = 1001;
        if (BossSceneController.Instance.BossLevel > 0) {
            HPManager.hp = 1501;
        }
        originalHp = HPManager.hp;
        //HuKing.instance.Log("HuStateMachine Initialized");
    }

    protected override void EntityStateMachineUpdate() { }

    private GameObject Target() {
        return HeroController.instance.gameObject;
    }

}