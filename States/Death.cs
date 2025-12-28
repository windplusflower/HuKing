using RingLib.StateMachine;
using RingLib.Utils;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    [State]
    private IEnumerator<Transition> Death()
    {
        HPManager.hp = 1;
        foreach (var saw in saws)
        {
            saw.SetActive(false);
        }
        warpOut.SetActive(true);
        transform.localScale = Vector3.zero;
        yield return new WaitFor { Seconds = 0.5f };
        transform.position = new Vector3((leftWall + rightWall) / 2, (upWall + downWall) / 2, 0);
        warpIn.SetActive(true);
        flash.SetActive(true);
        transform.localScale = Vector3.one;
        HeroController.instance.StartCoroutine(RestoreCameraZoomGlobal());
        yield return null;
    }
}