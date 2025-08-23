using HutongGames.PlayMaker.Actions;
using RingLib;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;
namespace HuKing;

internal partial class StomperStateMachine : EntityStateMachine {
    private float heightScale, dir;
    private float baseHeight;
    private float speedRate;
    private bool randomSpeed;

    public StomperStateMachine() : base(
        startState: nameof(Idle),
        globalTransitions: [],
        terrainLayer: "terrain",
        epsilon: 0.01f,
        horizontalCornerCorrection: false,
        spriteFacingLeft: true) {
    }
    protected override void EntityStateMachineStart() {
    }
    public void init(float heightScale, float dir, float speedRate, bool randomSpeed) {
        this.heightScale = heightScale;
        this.dir = dir;
        this.speedRate = speedRate;
        this.randomSpeed = randomSpeed;
    }
    [State]
    private IEnumerator<Transition> Idle() {
        transform.localScale = new Vector3(1, heightScale * dir, 1);
        baseHeight = gameObject.GetComponent<SpriteRenderer>().bounds.extents.y;
        transform.position = new Vector3(60, 9f + dir * (10f + baseHeight), 0);
        yield return new ToState { State = nameof(Appear) };
    }
    [State]
    private IEnumerator<Transition> Appear() {
        var time = 0.5f / speedRate;
        while (time > 0f) {
            time -= Time.deltaTime;
            transform.position += new Vector3(0, -dir * 2 * baseHeight * Time.deltaTime / 0.5f * speedRate, 0);
            yield return new NoTransition();
        }
        yield return new ToState { State = nameof(Move) };
    }
    [State]
    private IEnumerator<Transition> Move() {
        var time = 3f / speedRate;
        var height = transform.position.y;
        var randomSpeedRatio = 1.0f;
        if (randomSpeed) randomSpeedRatio = UnityEngine.Random.Range(0.8f, 1.2f);
        while (transform.position.x > 38) {
            time -= Time.deltaTime;
            if ((5 - speedRate) / 5 >= time / (3 / speedRate)) {
                time *= speedRate;
                speedRate = 1f;
            }

            transform.position += new Vector3(-7.3f * Time.deltaTime * speedRate * randomSpeedRatio, 0, 0);
            transform.position = new Vector3(transform.position.x, height + 2 * Mathf.Sin((3 - time) / 3 * Mathf.PI * 2), 0);
            yield return new NoTransition();
        }
        yield return new ToState { State = nameof(DisAppear) };
    }
    [State]
    private IEnumerator<Transition> DisAppear() {
        var time = 1f;
        while (time > 0f) {
            time -= Time.deltaTime;
            transform.position += new Vector3(0, dir * baseHeight * 2 * Time.deltaTime * 2, 0);
            yield return new NoTransition();
        }
        yield return new ToState { State = nameof(Recycle) };
    }
    [State]
    private IEnumerator<Transition> Recycle() {
        gameObject.SetActive(false);
        yield return new NoTransition();
    }
}