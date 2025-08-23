using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine {
    [State]
    private IEnumerator<Transition> Idle() {
        var height = 4f;
        var width = 4f;
        var rdy1 = 0.5f * height + downWall;
        var rdy2 = (int)(2.5f) * height + downWall;
        targetKnightPosition = new Vector3(leftWall + 0.5f * width, rdy1, 0);
        targetHuPosition = new Vector3(rightWall - 0.5f * width, rdy2, 0);
        warpIn.SetActive(true);
        flash.SetActive(true);
        yield return new WaitFor { Seconds = 0.5f };
        //HuKing.instance.Log("Idle state entered");
        yield return new ToState { State = nameof(Hit) };
    }
}