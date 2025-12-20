using System.Collections;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    [State]
    private IEnumerator<Transition> SawNailRoom()
    {
        //HuKing.instance.Log("Entering SawRoom state");
        SawNailGenerateSaw();
        yield return new ToState { State = nameof(Appear) };
    }
    private void SawNailGenerateSaw()
    {
        SetSawFrameToHeroCenter();
        sawFrame.SetActive(true);
    }
    private IEnumerator SawNailLoop()
    {
        yield return null;
    }

    private void InitializeSawFrame()
    {
        sawFrame = new GameObject("SawFrame");

        // 添加刚体，这是实现“子碰撞箱、父脚本”的关键
        var rb = sawFrame.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // 不受重力，由代码控制移动
        rb.simulated = true;

        // 只在父物体上挂一个脚本
        var responder = sawFrame.AddComponent<SawFrameResponder>();
        responder.OnBeenHit = (direction) =>
        {
            // 这里可以加一个简单的冷却（CD），防止同一帧内多次位移
            StartCoroutine(MoveFrame(direction));
        };

        CreateSawSquare(6);
    }
    private void CreateSawSquare(int size)
    {
        // 间距建议稍微大于 1.0，防止电锯挨得太紧（如果不希望间距随 sawSize 变化，就固定一个值）
        float step = 1.2f;
        float offset = (size - 1) * step / 2f;

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                // 只有四条边生成电锯
                if (i == 0 || i == size - 1 || j == 0 || j == size - 1)
                {
                    GameObject saw = Instantiate(sawPrefab, sawFrame.transform);

                    // --- 解决问题 1：强制激活子电锯 ---
                    saw.SetActive(true);

                    // --- 解决问题 2：设置电锯个体的大小 ---
                    // 只修改这个电锯实例的缩放，不影响父物体的坐标计算
                    saw.transform.localScale = new Vector3(sawSize, sawSize, 1f);

                    // 设置位置（相对于父物体中心）
                    float x = i * step - offset;
                    float y = j * step - offset;
                    saw.transform.localPosition = new Vector2(x, y);

                    // 物理与逻辑设置
                    saw.layer = (int)GlobalEnums.PhysLayers.ENEMIES;
                    var col = saw.GetComponent<Collider2D>();
                    if (col != null) col.isTrigger = true;

                    // 如果电锯有动画/音效逻辑，顺便触发它
                    var fsm = saw.GetComponent<PlayMakerFSM>();
                    if (fsm != null) fsm.SendEvent("SPAWN");
                }
            }
        }
    }
    private bool isMoving = false;

    private System.Collections.IEnumerator MoveFrame(Vector2 direction)
    {
        if (isMoving) yield break; // 如果正在移动处理中，忽略多余的打击
        isMoving = true;

        // 执行移动
        sawFrame.transform.position += (Vector3)direction * 0.5f;

        // 等待一小会儿（或者等到下一帧）
        yield return new WaitForSeconds(0.05f);
        isMoving = false;
    }
    public void SetSawFrameToHeroCenter()
    {
        if (sawFrame == null) return;

        Vector3 heroPos = HeroController.instance.transform.position;
        sawFrame.transform.position = heroPos;

        isMoving = false;
    }
}