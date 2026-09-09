using System.Collections.Generic;
using UnityEngine;

namespace GameJamOcean.Diving
{
    [DefaultExecutionOrder(-10000)]
    public sealed class DiveBackgroundSelector : MonoBehaviour
    {
        private const int UniqueBackgroundsPerSequence = 3;

        [SerializeField] private GameObject[] backgroundPrefabs;
        [SerializeField] private bool destroySceneCopies = true;

        private static readonly Queue<int> pendingSequence = new Queue<int>();
        private static int backgroundCount = -1;
        private static int omittedFromPreviousSequence = -1;
        private static int lastPlayed = -1;

        public static int CurrentBackgroundIndex { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeSequence()
        {
            pendingSequence.Clear();
            backgroundCount = -1;
            omittedFromPreviousSequence = -1;
            lastPlayed = -1;
            CurrentBackgroundIndex = -1;
        }

        private void Awake()
        {
            List<GameObject> sceneCopies = DisableSceneCopies();
            List<GameObject> available = CollectUniquePrefabs();

            if (available.Count < UniqueBackgroundsPerSequence)
            {
                Debug.LogError($"Dive background selection needs at least {UniqueBackgroundsPerSequence} "
                    + $"different prefabs, but found {available.Count}.", this);
                RestoreFallback(sceneCopies);
                return;
            }

            int index = NextBackgroundIndex(available.Count);
            GameObject selected = Instantiate(available[index]);
            selected.name = available[index].name;
            selected.SetActive(true);
            CurrentBackgroundIndex = index;

            if (destroySceneCopies)
                foreach (GameObject copy in sceneCopies)
                    if (copy != null) Destroy(copy);
        }

        private List<GameObject> CollectUniquePrefabs()
        {
            var available = new List<GameObject>();
            if (backgroundPrefabs == null) return available;

            foreach (GameObject prefab in backgroundPrefabs)
            {
                if (prefab == null) continue;
                GameObject prefabRoot = prefab.transform.root.gameObject;
                if (!available.Contains(prefabRoot)) available.Add(prefabRoot);
            }
            return available;
        }

        private List<GameObject> DisableSceneCopies()
        {
            var copies = new List<GameObject>();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (!IsBackgroundCopy(root.name)) continue;
                root.SetActive(false);
                copies.Add(root);
            }
            return copies;
        }

        private static bool IsBackgroundCopy(string objectName)
        {
            return objectName == "BACKGROUND1" || objectName == "BACKGROUND2"
                || objectName == "BACKGROUND3" || objectName == "BACKGROUND4";
        }

        private static void RestoreFallback(List<GameObject> sceneCopies)
        {
            if (sceneCopies.Count == 0) return;
            GameObject fallback = sceneCopies.Find(item => item != null && item.name == "BACKGROUND1")
                ?? sceneCopies[0];
            if (fallback != null) fallback.SetActive(true);
        }

        private static int NextBackgroundIndex(int count)
        {
            if (backgroundCount != count)
            {
                pendingSequence.Clear();
                backgroundCount = count;
                omittedFromPreviousSequence = -1;
                lastPlayed = -1;
            }

            if (pendingSequence.Count == 0)
                BuildNextSequence(count);

            int selected = pendingSequence.Dequeue();
            lastPlayed = selected;
            return selected;
        }

        private static void BuildNextSequence(int count)
        {
            int sequenceSize = Mathf.Min(UniqueBackgroundsPerSequence, count);
            var pool = new List<int>(count);
            for (int i = 0; i < count; i++) pool.Add(i);

            var sequence = new List<int>(sequenceSize);
            int priority = omittedFromPreviousSequence;
            if (priority >= 0 && priority < count)
            {
                sequence.Add(priority);
                pool.Remove(priority);
                Shuffle(pool);
                while (sequence.Count < sequenceSize)
                {
                    sequence.Add(pool[0]);
                    pool.RemoveAt(0);
                }

                // The background omitted in the previous block starts first or second.
                // It moves to second only when this cannot repeat the last background played.
                if (sequence.Count > 1 && sequence[1] != lastPlayed && Random.value < .5f)
                    (sequence[0], sequence[1]) = (sequence[1], sequence[0]);
            }
            else
            {
                Shuffle(pool);
                while (sequence.Count < sequenceSize)
                {
                    sequence.Add(pool[0]);
                    pool.RemoveAt(0);
                }
                AvoidBoundaryRepeat(sequence);
            }

            var omitted = new List<int>();
            for (int i = 0; i < count; i++)
                if (!sequence.Contains(i)) omitted.Add(i);
            omittedFromPreviousSequence = omitted.Count > 0
                ? omitted[Random.Range(0, omitted.Count)] : -1;

            foreach (int index in sequence) pendingSequence.Enqueue(index);
        }

        private static void AvoidBoundaryRepeat(List<int> sequence)
        {
            if (sequence.Count < 2 || sequence[0] != lastPlayed) return;
            int swapIndex = Random.Range(1, sequence.Count);
            (sequence[0], sequence[swapIndex]) = (sequence[swapIndex], sequence[0]);
        }

        private static void Shuffle(List<int> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                (values[i], values[other]) = (values[other], values[i]);
            }
        }
    }
}
