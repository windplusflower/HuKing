using System.Collections;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine {
    const int maxn = 2000;
    private int[] fa = new int[maxn];
    Queue<GameObject> movingSaws = new Queue<GameObject>();
    [State]
    private IEnumerator<Transition> SawRoom() {
        //HuKing.instance.Log("Entering SawRoom state");
        generateSaw();
        yield return new ToState { State = nameof(Appear) };
    }
    private (float, float, float, float) getLen() {
        int num1 = 3;
        if (HPManager.hp <= originalHp * 2 / 3) num1 = 4;
        float height = (upWall - downWall) / num1;
        int num2 = (int)((rightWall - leftWall) / (height * 1.2f));
        float width = (rightWall - leftWall) / num2;
        return (num1, num2, width, height);
    }
    private void generateSaw() {
        var saw = new List<(double, double)>();
        var edge = new List<(int, int)>();
        var block = new List<(int, int)>();
        var (num1, num2, width, height) = getLen();
        var sawSize = this.sawsize * 3 / num1;

        var rand = new System.Random();

        // 并查集
        for (int i = 0; i < maxn; i++) fa[i] = i;

        Func<int, int> find = null;
        find = x => fa[x] == x ? x : (fa[x] = find(fa[x]));

        Func<int, int, int> hash = (x, y) => x * 100 + y;
        Func<int, (int, int)> unhash = a => (a / 100, a % 100);

        for (int i = 0; i < num1; i++)
            for (int j = 0; j < num2; j++) {
                if (i < num1 - 1) edge.Add((hash(i, j), hash(i + 1, j)));
                if (j < num2 - 1) edge.Add((hash(i, j), hash(i, j + 1)));
            }

        Shuffle(edge, rand);

        foreach (var v in edge) {
            int x = find(v.Item1), y = find(v.Item2);
            if (x != y) fa[x] = y;
            else block.Add(v);
        }

        foreach (var v in block) {
            var v1 = unhash(v.Item1);
            var v2 = unhash(v.Item2);
            float y1 = (v1.Item1 + 0.5f) * height + downWall;
            float x1 = (v1.Item2 + 0.5f) * width + leftWall;
            float y2 = (v2.Item1 + 0.5f) * height + downWall;
            float x2 = (v2.Item2 + 0.5f) * width + leftWall;

            float mx = (x1 + x2) / 2f;
            float my = (y1 + y2) / 2f;

            float rx1 = 0, ry1 = 0;
            float rx2 = 0, ry2 = 0;
            int n = (int)height;
            if (v1.Item1 == v2.Item1) {
                rx1 = mx; ry1 = my - height / 2f;
                rx2 = mx; ry2 = my + height / 2f;
            }
            else {
                rx1 = mx - width / 2f; ry1 = my;
                rx2 = mx + width / 2f; ry2 = my;
                n = (int)width;
            }

            float dx = (rx2 - rx1) / n;
            float dy = (ry2 - ry1) / n;
            for (int i = 0; i <= n; i++) {
                var obj = blankSaws.Dequeue();
                obj.transform.position = new Vector3(rx1 + dx * i, ry1 + dy * i, 0);
                obj.transform.localScale = new Vector3(sawSize, sawSize, 1);
                saws.Enqueue(obj);
            }
        }
        var rdy1 = 0.5f * height + downWall;
        var rdy2 = ((int)(num1 / 2) + 0.5f) * height + downWall;
        targetKnightPosition = new Vector3(leftWall + 0.5f * width, rdy1, 0);
        targetHuPosition = new Vector3(rightWall - 0.5f * width, rdy2, 0);
        return;
    }

    private void Shuffle<T>(List<T> list, System.Random rand) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = rand.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    private IEnumerator SawLoop() {
        if (HPManager.hp <= originalHp / 3) {
            movingSaws = new Queue<GameObject>();
            Queue<bool> sawDirections = new Queue<bool>(); // true表示上下运动，false表示左右运动

            List<GameObject> eligibleSaws = new List<GameObject>();
            List<GameObject> allSaws = new List<GameObject>();
            while (saws.Count > 0) {
                GameObject saw = saws.Dequeue();
                allSaws.Add(saw);
                if (saw.transform.position.x > 40 || saw.transform.position.y > 5) {
                    eligibleSaws.Add(saw);
                }
            }

            foreach (var saw in allSaws) {
                saws.Enqueue(saw);
            }

            Shuffle(eligibleSaws, new System.Random());
            int count = Math.Min(8, eligibleSaws.Count);
            var rand = new System.Random();
            var (_, _, width, height) = getLen();

            for (int i = 0; i < count; i++) {
                GameObject originalSaw = eligibleSaws[i];
                GameObject newSaw = blankSaws.Dequeue();
                newSaw.transform.position = originalSaw.transform.position;
                newSaw.transform.localScale = originalSaw.transform.localScale;
                newSaw.SetActive(true);
                movingSaws.Enqueue(newSaw);
                sawDirections.Enqueue(rand.Next(2) == 1);
            }

            var time = 0f;
            Vector3[] initialPositions = new Vector3[movingSaws.Count];
            bool[] directions = new bool[movingSaws.Count];

            List<GameObject> sawList = new List<GameObject>(movingSaws);
            for (int i = 0; i < sawList.Count; i++) {
                initialPositions[i] = sawList[i].transform.position;
                directions[i] = sawDirections.Dequeue();
                sawDirections.Enqueue(directions[i]);
            }

            while (true) {
                time += Time.deltaTime;
                for (int i = 0; i < sawList.Count; i++) {
                    Vector3 newPosition = initialPositions[i];
                    if (directions[i]) {
                        newPosition.y = initialPositions[i].y + Mathf.Sin(time * 2f) * height;
                    }
                    else {
                        newPosition.x = initialPositions[i].x + Mathf.Sin(time * 2f) * width;
                    }

                    sawList[i].transform.position = newPosition;
                }

                yield return null;
            }
        }

        yield return null;
    }
}