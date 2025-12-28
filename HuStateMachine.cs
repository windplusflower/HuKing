using HutongGames.PlayMaker.Actions;
using MonoMod.Utils;
using RingLib;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;
namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private Config config = new();
    private tk2dSpriteAnimator animator = new();
    private GameObject sawPrefab, realSawPrefab, ringPrefab, beam, stomperPrefab, platPrefab, nailPrefab;
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

    protected override void EntityStateMachineStart()
    {
        // A separate GameObject for animation is good for adjusting offsets
        animator = gameObject.GetComponent<tk2dSpriteAnimator>();
        warpIn = gameObject.FindGameObjectInChildren("Warp");
        warpOut = gameObject.FindGameObjectInChildren("Warp Out");
        flash = gameObject.FindGameObjectInChildren("White Flash");
        for (int i = 0; i < 150; i++)
        {
            blankSaws.Enqueue(Instantiate(sawPrefab));
        }
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
        HPManager = gameObject.GetComponent<HealthManager>();
        HPManager.hp = 601;
        if (BossSceneController.Instance.BossLevel > 0)
        {
            HPManager.hp = 601;
        }
        originalHp = HPManager.hp;
        registerSkills();
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        //HuKing.instance.Log("HuStateMachine Initialized");
    }
    private void registerSkills()
    {
        registerSawRoom();
        registerBoxRoom();
        registerSawNailRoom();
        registerSawShotRoom();
    }

    private GameObject Target()
    {
        return HeroController.instance.gameObject;
    }

}