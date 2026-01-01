using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Modding;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private List<GameObject> activeMatchSaws = new List<GameObject>();
    private Color[] matchColors = {
    new Color(1f, 0.3f, 0.3f), // 亮红
    new Color(1f, 1f, 0.3f),   // 亮黄
    new Color(0.3f, 0.6f, 1f)  // 亮蓝
};

    private float currentScale;
    private float currentRadius;
    private float stepX;
    private float stepY;
    private float wallLeftEdgeX;

    [State]
    private IEnumerator<Transition> MatchThreeRoom()
    {
        float baseScale = 0.7f;
        currentScale = level >= 2 ? baseScale * 0.66f : baseScale;
        float gapMultiplier = 1.05f;

        currentRadius = currentScale * 1.8f;
        stepY = currentRadius * 2f * gapMultiplier;
        stepX = stepY * 0.866f;

        int initialCols = level switch { 1 => 4, 2 => 5, 3 => 8, _ => 4 };
        float wallStartX = HuStateMachine.rightWall - 5f;

        wallLeftEdgeX = wallStartX - ((initialCols - 1) * stepX);

        for (int c = 0; c < initialCols; c++)
        {
            float x = wallStartX - (c * stepX);
            for (float y = HuStateMachine.downWall; y <= HuStateMachine.upWall + 1f; y += stepY)
            {
                float yOffset = (c % 2 == 1) ? stepY / 2f : 0;
                Vector3 pos = new Vector3(x, y + yOffset, 0);

                GameObject saw = DequeueMatchSawWithBalance(pos, currentScale);

                var b = saw.GetComponent<FallingSawBehavior>();
                if (b == null) b = saw.AddComponent<FallingSawBehavior>();
                b.enabled = false;

                saw.SetActive(false);
                activeMatchSaws.Add(saw);
            }
        }

        ModHooks.SlashHitHook += OnSlashHitMatchSaw;

        yield return new ToState { State = nameof(Appear) };
    }

    private GameObject DequeueMatchSawWithBalance(Vector3 pos, float scale)
    {
        GameObject saw = blankSaws.Count > 0 ? blankSaws.Dequeue() : Instantiate(realSawPrefab);
        saw.transform.position = pos;
        saw.transform.localScale = Vector3.one * scale;

        var sr = saw.GetComponent<SpriteRenderer>();
        if (sr == null) sr = saw.AddComponent<SpriteRenderer>();

        List<Color> safeColors = new List<Color>();
        foreach (Color candidate in matchColors)
        {
            if (!WouldCauseInitialMatch(pos, candidate))
            {
                safeColors.Add(candidate);
            }
        }

        if (safeColors.Count > 0)
            sr.color = safeColors[UnityEngine.Random.Range(0, safeColors.Count)];
        else
            sr.color = matchColors[UnityEngine.Random.Range(0, 3)];

        return saw;
    }

    private bool WouldCauseInitialMatch(Vector3 pos, Color candidate)
    {
        Vector3[] directions = {
            new Vector3(0, stepY, 0),
            new Vector3(0, -stepY, 0),
            new Vector3(stepX, stepY/2f, 0),
            new Vector3(stepX, -stepY/2f, 0),
            new Vector3(-stepX, stepY/2f, 0),
            new Vector3(-stepX, -stepY/2f, 0)
        };

        foreach (var dir in directions)
        {
            GameObject firstNeighbor = FindSawAt(pos + dir);
            if (firstNeighbor != null && ColorsMatch(firstNeighbor.GetComponent<SpriteRenderer>().color, candidate))
            {
                GameObject secondNeighbor = FindSawAt(pos + dir * 2f);
                if (secondNeighbor != null && ColorsMatch(secondNeighbor.GetComponent<SpriteRenderer>().color, candidate))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private GameObject FindSawAt(Vector3 pos)
    {
        foreach (var saw in activeMatchSaws)
        {
            if (Vector2.Distance(saw.transform.position, pos) < currentRadius) return saw;
        }
        return null;
    }

    private void registerMatchThreeRoom()
    {
        skillTable.Add(nameof(MatchThreeRoom), new SkillPhases(
            () => Appear_MatchThree(),
            () => Loop_MatchThree(),
            () => Disappear_MatchThree()
        ));
        registerSkill(nameof(MatchThreeRoom));
    }

    private void OnSlashHitMatchSaw(Collider2D otherCollider, GameObject slash)
    {
        GameObject obj = otherCollider.gameObject;
        var behavior = obj.GetComponent<FallingSawBehavior>();
        if (behavior != null && behavior.enabled && !behavior.IsMovingRight)
        {
            behavior.LaunchRight();
        }
    }

    private IEnumerator Loop_MatchThree()
    {
        float spawnTimer = 0f;
        while (true)
        {
            spawnTimer += Time.deltaTime;
            float spawnInterval = level >= 3 ? 0.6f : 1.0f;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0;
                SpawnFallingSaw();
            }
            yield return null;
        }
    }
    private void SpawnFallingSaw()
    {
        float maxSpawnX = wallLeftEdgeX - (currentRadius * 2f);
        float minSpawnX = HuStateMachine.leftWall + 2f;
        float spawnX;

        float dynamicSafeDistance = currentRadius * 3.5f;
        float spawnSafeX = targetKnightPosition.x;

        int attempts = 0;
        while (true)
        {
            spawnX = UnityEngine.Random.Range(minSpawnX, maxSpawnX);

            if (Mathf.Abs(spawnX - spawnSafeX) > dynamicSafeDistance)
            {
                break;
            }

            attempts++;
            if (attempts > 10)
            {
                spawnX = (spawnSafeX - minSpawnX > maxSpawnX - spawnSafeX) ? minSpawnX : maxSpawnX;
                break;
            }
        }

        GameObject saw = DequeueMatchSaw(new Vector3(spawnX, HuStateMachine.upWall + 1f, 0), currentScale);
        var behavior = saw.GetComponent<FallingSawBehavior>() ?? saw.AddComponent<FallingSawBehavior>();

        behavior.enabled = true;
        behavior.Init(this, UnityEngine.Random.Range(5f, 10f));
        activeMatchSaws.Add(saw);
    }

    public class FallingSawBehavior : MonoBehaviour
    {
        private HuStateMachine machine;
        private float fallSpeed;
        public bool IsMovingRight { get; private set; } = false;

        public void Init(HuStateMachine m, float speed)
        {
            machine = m;
            fallSpeed = speed;
            IsMovingRight = false;
        }

        public void LaunchRight() { IsMovingRight = true; }

        void Update()
        {
            if (!IsMovingRight)
            {
                transform.position += Vector3.down * fallSpeed * Time.deltaTime;
                if (transform.position.y < HuStateMachine.downWall - 2f) machine.RecycleMatchSaw(gameObject);
            }
            else
            {
                transform.position += Vector3.right * 40f * Time.deltaTime;
                CheckCollision();
                if (transform.position.x > HuStateMachine.rightWall + 5f) machine.RecycleMatchSaw(gameObject);
            }
        }

        void CheckCollision()
        {
            GameObject nearest = null;
            float minDist = machine.currentRadius * 1.8f;

            foreach (var saw in machine.activeMatchSaws)
            {
                if (saw == gameObject) continue;
                var b = saw.GetComponent<FallingSawBehavior>();
                if (b != null && !b.enabled)
                {
                    float d = Vector2.Distance(transform.position, saw.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        nearest = saw;
                    }
                }
            }

            if (nearest != null)
            {
                machine.SnapToNearest(gameObject, nearest);
            }
        }
    }

    private void SnapToNearest(GameObject incoming, GameObject anchor)
    {
        var behavior = incoming.GetComponent<FallingSawBehavior>();
        behavior.enabled = false;

        Vector3 anchorPos = anchor.transform.position;
        Vector3 bestPos = anchorPos;
        float minD = float.MaxValue;

        Vector3[] directions = {
            new Vector3(0, stepY, 0),
            new Vector3(0, -stepY, 0),
            new Vector3(stepX, stepY/2f, 0),
            new Vector3(stepX, -stepY/2f, 0),
            new Vector3(-stepX, stepY/2f, 0),
            new Vector3(-stepX, -stepY/2f, 0)
        };

        foreach (var dir in directions)
        {
            Vector3 checkPos = anchorPos + dir;
            bool occupied = activeMatchSaws.Any(s => s != incoming && Vector2.Distance(s.transform.position, checkPos) < currentRadius);

            if (!occupied)
            {
                float d = Vector2.Distance(incoming.transform.position, checkPos);
                if (d < minD) { minD = d; bestPos = checkPos; }
            }
        }

        StartCoroutine(AnimateSnap(incoming, bestPos));

        if (incoming.transform.position.x < wallLeftEdgeX)
        {
            wallLeftEdgeX = incoming.transform.position.x;
        }
    }

    private void HandleMatchDynamic(GameObject source)
    {
        Color targetColor = source.GetComponent<SpriteRenderer>().color;
        List<GameObject> matches = new List<GameObject>();
        HashSet<GameObject> visited = new HashSet<GameObject>();

        RecursiveSearch(source, targetColor, matches, visited);

        if (matches.Count >= 3)
        {
            foreach (var m in matches) RecycleMatchSaw(m, true);
        }
    }

    private void RecursiveSearch(GameObject current, Color color, List<GameObject> matches, HashSet<GameObject> visited)
    {
        if (current == null || visited.Contains(current)) return;

        var sr = current.GetComponent<SpriteRenderer>();
        if (sr != null && ColorsMatch(sr.color, color))
        {
            visited.Add(current);
            matches.Add(current);

            foreach (var other in activeMatchSaws)
            {
                var b = other.GetComponent<FallingSawBehavior>();
                if (b != null && !b.enabled)
                {
                    if (Vector2.Distance(current.transform.position, other.transform.position) < stepY * 1.15f)
                        RecursiveSearch(other, color, matches, visited);
                }
            }
        }
    }

    private bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.1f && Mathf.Abs(a.g - b.g) < 0.1f && Mathf.Abs(a.b - b.b) < 0.1f;
    }

    private GameObject DequeueMatchSaw(Vector3 pos, float scale)
    {
        GameObject saw = blankSaws.Count > 0 ? blankSaws.Dequeue() : Instantiate(realSawPrefab);
        saw.transform.position = pos;
        saw.transform.localScale = Vector3.one * scale;

        var sr = saw.GetComponent<SpriteRenderer>();
        if (sr == null) sr = saw.AddComponent<SpriteRenderer>();
        sr.color = matchColors[UnityEngine.Random.Range(0, 3)];

        saw.SetActive(true);
        return saw;
    }
    // 特效 1：三消消除时的“变大、变白、消失”
    private IEnumerator AnimateMatchElimination(GameObject saw)
    {
        float duration = 0.2f;
        float elapsed = 0;
        Vector3 startScale = saw.transform.localScale;
        SpriteRenderer sr = saw.GetComponent<SpriteRenderer>();
        Color originalColor = (sr != null) ? sr.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            saw.transform.localScale = Vector3.Lerp(startScale, startScale * 1.5f, t);
            if (sr != null) sr.color = Color.Lerp(originalColor, Color.white, t);

            yield return null;
        }

        var behavior = saw.GetComponent<FallingSawBehavior>();
        if (behavior != null) behavior.enabled = false;
        if (sr != null) sr.color = Color.white;
        saw.transform.localScale = Vector3.one;

        saw.SetActive(false);
        blankSaws.Enqueue(saw);
    }

    private IEnumerator AnimateSnap(GameObject obj, Vector3 targetPos)
    {
        float duration = 0.08f;
        float elapsed = 0;
        Vector3 startPos = obj.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            obj.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }
        obj.transform.position = targetPos;
        HandleMatchDynamic(obj);
    }
    private void RecycleMatchSaw(GameObject saw, bool playEffect = false)
    {
        activeMatchSaws.Remove(saw);

        if (playEffect && saw.activeInHierarchy)
        {
            StartCoroutine(AnimateMatchElimination(saw));
        }
        else
        {
            var behavior = saw.GetComponent<FallingSawBehavior>();
            if (behavior != null) behavior.enabled = false;

            var sr = saw.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;
            saw.transform.localScale = Vector3.one;

            saw.SetActive(false);
            blankSaws.Enqueue(saw);
        }
    }

    private IEnumerator<Transition> Appear_MatchThree()
    {
        foreach (var saw in activeMatchSaws)
        {
            saw.SetActive(true);
        }
        yield return null;
    }

    private IEnumerator<Transition> Disappear_MatchThree()
    {
        ModHooks.SlashHitHook -= OnSlashHitMatchSaw;
        var toRecycle = activeMatchSaws.ToList();
        foreach (var s in toRecycle) RecycleMatchSaw(s);
        yield return null;
    }
}