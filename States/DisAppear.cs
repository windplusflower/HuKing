using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine {
    [State]
    private IEnumerator<Transition> DisAppear() {
        warpOut.SetActive(true);
        flash.SetActive(true);
        gameObject.transform.localScale = Vector3.zero;

        while (saws.Count > 0) {
            var saw = saws.Dequeue();
            saw.SetActive(false);
            blankSaws.Enqueue(saw);
        }
        while (movingSaws.Count > 0) {
            var saw = movingSaws.Dequeue();
            saw.SetActive(false);
            blankSaws.Enqueue(saw);
        }
        foreach (var stomper in stompers) {
            stomper.SetActive(false);
        }
        foreach (var coroutine in mCoroutines) {
            StopCoroutine(coroutine);
        }
        mCoroutines.Clear();
        beam.SetActive(false);

        if (HPManager.hp < 100) yield return new ToState { State = nameof(Death) };
        yield return new ToState { State = nameof(Choose) };
    }
}