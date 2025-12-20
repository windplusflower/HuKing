using Modding;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    const float moveTime = 1f;
    [State]
    private IEnumerator<Transition> Appear()
    {
        yield return new CoroutineTransition { Routine = moveKnight() };
        if (skillTable.TryGetValue(SkillChoosen, out var skill))
        {
            HuKing.instance.Log($"{SkillChoosen} skill Choosen");
            yield return new CoroutineTransition { Routine = skill.Appear() };
        }
        else
        {
            HuKing.instance.Log($"{SkillChoosen} skill not found");
        }
        bossAppear();
        flash.SetActive(true);
        yield return new ToState { State = nameof(Hit) };
    }
    private IEnumerator<Transition> moveKnight()
    {
        HeroController.instance.RelinquishControl();

        var ring = Instantiate(ringPrefab);
        ring.transform.parent = Target().transform;
        ring.transform.localPosition = new Vector3(0, 3.5f, 0);
        var player = ring.GetComponent<tk2dSpriteAnimator>();
        var fsm = ring.LocateMyFSM("Control");
        for (int i = 0; i < 4; i++)
            fsm.RemoveAction("Init", 0);
        fsm.RemoveTransition("Init", "FINISHED");
        ring.SetActive(true);
        player.Play("Ring Antic 1");
        player.Play("Ring Antic 2");

        var V = targetKnightPosition - Target().transform.position;
        var pasttime = 0f;
        Target().GetComponent<Rigidbody2D>().gravityScale = 0;

        var wpin = Instantiate(warpIn);
        wpin.transform.position = Target().transform.position;
        for (int i = 0; i < 5; i++)
        {
            wpin.SetActive(true);
            yield return new WaitFor { Seconds = 0.1f };
        }

        Destroy(wpin);

        while (pasttime < moveTime)
        {
            pasttime += Time.deltaTime;
            Target().transform.position += V * (Time.deltaTime / moveTime);
            yield return new NoTransition();
        }

        Target().GetComponent<Rigidbody2D>().gravityScale = 1;
        HeroController.instance.RegainControl();
        PlayerData.instance.SetHazardRespawn(targetKnightPosition, true);
        Destroy(ring);
        if (enableShining)
        {

            var fsh = Instantiate(warpIn);
            fsh.transform.position = new Vector3((leftWall + rightWall) / 2f, (upWall + downWall) / 2f, 0);
            fsh.transform.localScale = new Vector3(20f, 5f, 1);
            BrightnessUtil.SetBrightness(fsh, 0.01f);
            fsh.SetActive(true);
            yield return new WaitFor { Seconds = 0.1f };
            Destroy(fsh);
        }
    }
    private void bossAppear()
    {
        gameObject.transform.position = targetHuPosition;
        gameObject.transform.localScale = Vector3.one;
        warpIn.SetActive(true);
        flash.SetActive(true);
    }
}