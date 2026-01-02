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
            yield return new CoroutineTransition { Routine = skill.Appear() };
        }
        bossAppear();
        flash.SetActive(true);
        yield return new ToState { State = nameof(Hit) };
    }
    private IEnumerator<Transition> moveKnight()
    {
        HeroController.instance.RelinquishControl();

        // 强制清空速度，防止起始瞬间的惯性干扰
        var rb = Target().GetComponent<Rigidbody2D>();
        rb.velocity = Vector2.zero;
        rb.gravityScale = 0;

        var ring = skillRing;
        try
        {
            var player = ring.GetComponent<tk2dSpriteAnimator>();

            ring.SetActive(true);
            player.Play("Ring Antic 1");
            player.Play("Ring Antic 2");

            Vector3 startPos = Target().transform.position;
            Vector3 endPos = targetKnightPosition;

            var pasttime = 0f;

            var wpin = Instantiate(warpIn);
            wpin.transform.position = startPos;
            wpin.SetActive(true);
            Destroy(wpin, 0.5f);

            while (pasttime < moveTime)
            {
                pasttime += Time.deltaTime;
                float t = pasttime / moveTime;

                float easedT = Mathf.SmoothStep(0, 1, t);

                Target().transform.position = Vector3.Lerp(startPos, endPos, easedT);

                yield return new NoTransition();
            }

            Target().transform.position = endPos;
        }
        finally
        {
            if (ring != null) ring.SetActive(false);
            rb.gravityScale = 1;
            HeroController.instance.RegainControl();
            PlayerData.instance.SetHazardRespawn(targetKnightPosition, true);
        }

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