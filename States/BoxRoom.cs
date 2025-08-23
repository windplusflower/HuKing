using System.Collections;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine {
    private float speedRate;
    [State]
    private IEnumerator<Transition> BoxRoom() {
        //HuKing.instance.Log("BoxRoom state entered");
        yield return new ToState { State = nameof(Appear) };
    }
    private IEnumerator BoxLoop() {
        //HuKing.instance.Log("BoxLoop started");
        mCoroutines.Add(StartCoroutine(ShotBeamLoop()));
        mCoroutines.Add(StartCoroutine(StomperLoop()));
        while (true) {
            yield return null;
        }
    }
    private IEnumerator ShotBeamLoop() {
        while (true) {
            beam.transform.position = transform.position;
            var dir = (Target().transform.position - transform.position).normalized;
            var degree = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            beam.transform.rotation = Quaternion.Euler(0, 0, degree);
            beam.SetActive(true);
            var fsm = beam.LocateMyFSM("Control");
            fsm.SendEvent("ANTIC");
            yield return new WaitForSeconds(0.65f);
            fsm.SendEvent("FIRE");
            yield return new WaitForSeconds(0.16f);
            fsm.SendEvent("END");
            yield return new WaitForSeconds(0.6f);
            beam.SetActive(false);
            yield return null;
        }
    }
    private IEnumerator StomperLoop() {
        speedRate = 5f;
        while (true) {
            var isRandom = HPManager.hp <= originalHp / 3;
            var stomper = stompers.Dequeue();
            var heightScale = UnityEngine.Random.Range(1f, 2.8f);
            stomper.GetComponent<StomperStateMachine>().init(heightScale, -1f, speedRate, isRandom);
            stomper.SetActive(true);
            stompers.Enqueue(stomper);

            if (HPManager.hp <= originalHp * 2 / 3) {
                stomper = stompers.Dequeue();
                heightScale = UnityEngine.Random.Range(1f, 3.8f - heightScale);
                stomper.GetComponent<StomperStateMachine>().init(heightScale, 1f, speedRate, isRandom);
                stomper.SetActive(true);
                stompers.Enqueue(stomper);
            }
            if (speedRate > 1f) {
                speedRate -= 1f;
                yield return null;
            }
            else {
                speedRate = 1f;
                yield return new WaitForSeconds(1f);
            }
        }
    }
}