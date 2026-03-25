using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LateGameLevelDirector
{
    private const int StartLevel = 16;
    private const int EndLevel = 20;
    private const int SlotCount = 4;
    private const string ExtraBlockName = "LV20_Extra_E";
    private static readonly string[] SlotSuffixes = { "A", "B", "C", "D" };
    private static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _hooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (_hooked)
        {
            return;
        }

        _hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        ConfigureScene(scene);
    }

    private static void ConfigureScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (!TryParseLevel(scene.name, out int level) || level < StartLevel || level > EndLevel)
        {
            return;
        }

        List<Transform> slots = CollectSlots(scene);
        EnsureSlots(scene, level, slots, SlotCount);
        if (slots.Count == 0)
        {
            return;
        }

        while (slots.Count < SlotCount)
        {
            slots.Add(null);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            Transform slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            ClearMovementComponents(slot.gameObject);
            slot.gameObject.SetActive(false);
        }

        RemoveExtraBlock(scene);

        switch (level)
        {
            case 16:
                ConfigureLevel16(slots);
                break;
            case 17:
                ConfigureLevel17(slots);
                break;
            case 18:
                ConfigureLevel18(slots);
                break;
            case 19:
                ConfigureLevel19(slots);
                break;
            case 20:
                ConfigureLevel20(slots);
                break;
        }

        Physics.SyncTransforms();
    }

    private static void ConfigureLevel16(List<Transform> slots)
    {
        Activate(slots[0], new Vector3(-2.55f, 7.02f, 28.4f));
        Activate(slots[1], new Vector3(2.2f, 7.3f, 31.8f));
        Activate(slots[2], new Vector3(-2.35f, 7.56f, 35.25f));
        Activate(slots[3], new Vector3(2.8f, 7.94f, 38.75f));

        AddPhaseGate(slots[0], 0, PhaseGateCloudPlatform.GatePattern.DiamondGate, 2.6f, 2.2f, 5.4f, 0.12f, 12f, 0.62f, 0.5f, 1.35f, false, 0f);
        AddMobius(slots[1], 1, 2.75f, 1.05f, 7f, 128f, 1.65f, 1.45f, 0.2f, 1.15f, false, 0f);
        AddQuantum(slots[2], 2, 2.55f, 2.2f, 0.46f, 0.12f, 1.85f, 264f, 6, true, 0.12f, 2.6f);
        AddMobius(slots[3], 3, 3.05f, -1.25f, 6.4f, 318f, 2.05f, 1.8f, 0.3f, 1.45f, true, 4.9f);
    }

    private static void ConfigureLevel17(List<Transform> slots)
    {
        Activate(slots[0], new Vector3(-2.9f, 7.08f, 28.25f));
        Activate(slots[1], new Vector3(2.15f, 7.36f, 31.55f));
        Activate(slots[2], new Vector3(-2.25f, 7.66f, 35.2f));
        Activate(slots[3], new Vector3(3.0f, 8.05f, 38.9f));

        AddVortex(slots[0], 0, VortexOrbitCloudPlatform.VortexMode.OrbitLobe, 3.35f, 5.2f, 0.52f, 1.05f, 1.7f, 1.85f, 18f, 6, false, 0f);
        AddQuantum(slots[1], 1, 2.6f, 2.2f, 0.42f, 0.06f, 2.2f, 58f, 8, true, 0.16f, 3.1f);
        AddKnot(slots[2], 2, KnotOrbitCloudPlatform.KnotPattern.RoseHelix, 3.45f, 1.3f, 4, 7, 5.4f, 104f, 1.7f, 0.31f, 1.45f, 0.44f, 1.05f, true, 3.1f);
        AddPhaseGate(slots[3], 3, PhaseGateCloudPlatform.GatePattern.SpiralBox, 3.7f, 3.3f, 4.7f, 0.06f, 142f, 0.78f, 1.06f, 2.2f, true, 2.4f);
    }

    private static void ConfigureLevel18(List<Transform> slots)
    {
        Activate(slots[0], new Vector3(-2.2f, 7.0f, 28.4f));
        Activate(slots[1], new Vector3(2.3f, 7.35f, 31.9f));
        Activate(slots[2], new Vector3(-2.5f, 7.45f, 35.2f));
        Activate(slots[3], new Vector3(2.6f, 7.7f, 38.5f));

        AddVortex(slots[0], 0, VortexOrbitCloudPlatform.VortexMode.HelixBurst, 2.5f, 4.8f, 0.35f, 0.9f, 1.2f, 1.5f, 0f, 4, false, 0f);
        AddVortex(slots[1], 1, VortexOrbitCloudPlatform.VortexMode.OrbitLobe, 2.9f, 5.4f, 0.45f, 0.75f, 1.6f, 1.2f, 28f, 5, true, 7.2f);
        AddVortex(slots[2], 2, VortexOrbitCloudPlatform.VortexMode.StarDrift, 3.2f, 6.0f, 0.55f, 0.65f, 2.0f, 1.05f, 55f, 7, false, 8f);
        AddVortex(slots[3], 3, VortexOrbitCloudPlatform.VortexMode.OrbitLobe, 3.5f, 6.6f, 0.6f, 0.58f, 2.3f, 0.95f, 92f, 8, true, 5.8f);
    }

    private static void ConfigureLevel19(List<Transform> slots)
    {
        Activate(slots[0], new Vector3(-2.65f, 7.18f, 28.55f));
        Activate(slots[1], new Vector3(2.2f, 7.42f, 31.8f));
        Activate(slots[2], new Vector3(-2.45f, 7.7f, 35.35f));
        Activate(slots[3], new Vector3(2.95f, 8.08f, 38.95f));

        AddPendulum(slots[0], 0, PendulumWaveCloudPlatform.WaveformMode.Interference, 5.4f, 2.7f, 0.08f, 0f, 0.42f, 0.3f, 6.6f, true, 1.7f);
        AddQuantum(slots[1], 1, 2.85f, 2.35f, 0.36f, 0.03f, 2.45f, 52f, 8, true, 0.2f, 3.2f);
        AddPhaseGate(slots[2], 2, PhaseGateCloudPlatform.GatePattern.SkewedCross, 3.45f, 3.0f, 4.5f, 0.05f, 118f, 0.9f, 0.92f, 2.0f, true, 2.2f);
        AddPendulum(slots[3], 3, PendulumWaveCloudPlatform.WaveformMode.SoftPulse, 6.0f, 2.55f, 0.08f, 150f, 0.58f, 0.34f, 7.0f, false, 1.35f);
    }

    private static void ConfigureLevel20(List<Transform> slots)
    {
        Activate(slots[0], new Vector3(-2.4f, 7.1f, 28.4f));
        Activate(slots[1], new Vector3(2.4f, 7.4f, 31.8f));
        Activate(slots[2], new Vector3(-2.6f, 7.6f, 35.4f));
        Activate(slots[3], new Vector3(2.8f, 7.9f, 38.8f));

        AddPendulum(slots[0], 0, PendulumWaveCloudPlatform.WaveformMode.Interference, 3.8f, 3.2f, 0.18f, 0f, 0.45f, 0.24f, 9.6f, false, 0f);
        AddPendulum(slots[1], 1, PendulumWaveCloudPlatform.WaveformMode.SoftPulse, 4.2f, 3.2f, 0.18f, 18f, 0.35f, 0.26f, 10.2f, true, 0f);
        AddPendulum(slots[2], 2, PendulumWaveCloudPlatform.WaveformMode.CrestHold, 4.6f, 3.2f, 0.18f, 40f, 0.25f, 0.28f, 10.8f, false, 6.5f);
        AddPendulum(slots[3], 3, PendulumWaveCloudPlatform.WaveformMode.Interference, 5f, 3.2f, 0.18f, 63f, 0.5f, 0.3f, 11.4f, true, 5.3f);
    }

    private static void Activate(Transform slot, Vector3 position)
    {
        if (slot == null)
        {
            return;
        }

        slot.gameObject.SetActive(true);
        slot.position = position;
    }

    private static void AddQuantum(
        Transform slot,
        int index,
        float radiusX,
        float radiusZ,
        float jumpDuration,
        float holdDuration,
        float arcHeight,
        float phaseOffset,
        int nodeCount,
        bool clockwise,
        float jitterAmplitude,
        float jitterFrequency)
    {
        if (slot == null)
        {
            return;
        }

        QuantumLeapCloudPlatform quantum = GetOrAddMovementComponent<QuantumLeapCloudPlatform>(slot.gameObject);
        SetPrivateField(quantum, "nodeCount", Mathf.Clamp(nodeCount, 3, 8));
        SetPrivateField(quantum, "clockwise", clockwise);
        SetPrivateField(quantum, "jitterAmplitude", Mathf.Max(0f, jitterAmplitude));
        SetPrivateField(quantum, "jitterFrequency", Mathf.Max(0f, jitterFrequency));
        quantum.Configure(index, radiusX, radiusZ, jumpDuration, holdDuration, arcHeight, phaseOffset, null);
    }

    private static void AddPhaseGate(
        Transform slot,
        int index,
        PhaseGateCloudPlatform.GatePattern pattern,
        float radiusX,
        float radiusZ,
        float cycleDuration,
        float holdTime,
        float phaseOffset,
        float liftPerGate,
        float waveAmplitude,
        float waveFrequency,
        bool reverseDirection,
        float directionFlipInterval)
    {
        if (slot == null)
        {
            return;
        }

        PhaseGateCloudPlatform phase = GetOrAddMovementComponent<PhaseGateCloudPlatform>(slot.gameObject);
        SetPrivateField(phase, "gatePattern", pattern);
        SetPrivateField(phase, "liftPerGate", Mathf.Max(0f, liftPerGate));
        SetPrivateField(phase, "verticalWaveAmplitude", Mathf.Max(0f, waveAmplitude));
        SetPrivateField(phase, "verticalWaveFrequency", Mathf.Max(0f, waveFrequency));
        SetPrivateField(phase, "reverseDirection", reverseDirection);
        SetPrivateField(phase, "directionFlipInterval", Mathf.Max(0f, directionFlipInterval));
        phase.Configure(index, radiusX, radiusZ, cycleDuration, holdTime, phaseOffset, null);
    }

    private static void AddVortex(
        Transform slot,
        int index,
        VortexOrbitCloudPlatform.VortexMode mode,
        float orbitRadius,
        float cycleDuration,
        float radialBreath,
        float radialBreathFrequency,
        float verticalAmplitude,
        float verticalFrequency,
        float angularOffsetDegrees,
        int lobeCount,
        bool reverseDirection,
        float directionFlipInterval)
    {
        if (slot == null)
        {
            return;
        }

        VortexOrbitCloudPlatform vortex = GetOrAddMovementComponent<VortexOrbitCloudPlatform>(slot.gameObject);
        SetPrivateField(vortex, "vortexMode", mode);
        SetPrivateField(vortex, "lobeCount", Mathf.Clamp(lobeCount, 2, 8));
        SetPrivateField(vortex, "reverseDirection", reverseDirection);
        SetPrivateField(vortex, "directionFlipInterval", Mathf.Max(0f, directionFlipInterval));
        vortex.Configure(index, orbitRadius, cycleDuration, radialBreath, radialBreathFrequency, verticalAmplitude, verticalFrequency, angularOffsetDegrees, null);
    }

    private static void AddKnot(
        Transform slot,
        int index,
        KnotOrbitCloudPlatform.KnotPattern pattern,
        float majorRadius,
        float minorRadius,
        int pTurns,
        int qTurns,
        float cycleDuration,
        float phaseOffsetDegrees,
        float verticalScale,
        float bobAmplitude,
        float bobFrequency,
        float radialPulse,
        float radialPulseFrequency,
        bool reverseDirection,
        float directionFlipInterval)
    {
        if (slot == null)
        {
            return;
        }

        KnotOrbitCloudPlatform knot = GetOrAddMovementComponent<KnotOrbitCloudPlatform>(slot.gameObject);
        SetPrivateField(knot, "pattern", pattern);
        SetPrivateField(knot, "verticalScale", Mathf.Max(0f, verticalScale));
        SetPrivateField(knot, "bobAmplitude", Mathf.Max(0f, bobAmplitude));
        SetPrivateField(knot, "bobFrequency", Mathf.Max(0f, bobFrequency));
        SetPrivateField(knot, "radialPulse", Mathf.Clamp(radialPulse, 0f, 0.9f));
        SetPrivateField(knot, "radialPulseFrequency", Mathf.Max(0f, radialPulseFrequency));
        SetPrivateField(knot, "reverseDirection", reverseDirection);
        SetPrivateField(knot, "directionFlipInterval", Mathf.Max(0f, directionFlipInterval));
        knot.Configure(index, majorRadius, minorRadius, pTurns, qTurns, cycleDuration, phaseOffsetDegrees, null);
    }

    private static void AddPendulum(
        Transform slot,
        int index,
        PendulumWaveCloudPlatform.WaveformMode mode,
        float amplitude,
        float baseCycleDuration,
        float cycleStepPerIndex,
        float phaseOffsetDegrees,
        float harmonicBlend,
        float envelopeAmount,
        float envelopeCycleDuration,
        bool reverseDirection,
        float directionFlipInterval)
    {
        if (slot == null)
        {
            return;
        }

        PendulumWaveCloudPlatform pendulum = GetOrAddMovementComponent<PendulumWaveCloudPlatform>(slot.gameObject);
        SetPrivateField(pendulum, "waveformMode", mode);
        SetPrivateField(pendulum, "reverseDirection", reverseDirection);
        SetPrivateField(pendulum, "directionFlipInterval", Mathf.Max(0f, directionFlipInterval));
        pendulum.Configure(index, amplitude, baseCycleDuration, cycleStepPerIndex, phaseOffsetDegrees, harmonicBlend, envelopeAmount, envelopeCycleDuration, null);
    }

    private static void AddMobius(
        Transform slot,
        int index,
        float majorRadius,
        float laneOffset,
        float cycleDuration,
        float phaseOffsetDegrees,
        float twistMultiplier,
        float verticalScale,
        float bobAmplitude,
        float bobFrequency,
        bool reverseDirection,
        float directionFlipInterval)
    {
        if (slot == null)
        {
            return;
        }

        MobiusRibbonCloudPlatform mobius = GetOrAddMovementComponent<MobiusRibbonCloudPlatform>(slot.gameObject);
        mobius.Configure(
            index,
            majorRadius,
            laneOffset,
            cycleDuration,
            phaseOffsetDegrees,
            twistMultiplier,
            verticalScale,
            bobAmplitude,
            bobFrequency,
            reverseDirection,
            directionFlipInterval,
            null);
    }

    private static void ClearMovementComponents(GameObject go)
    {
        DisableAndDestroy(go.GetComponent<FigureEightCloudPlatform>());
        DisableAndDestroy(go.GetComponent<VerticalWaveCloudPlatform>());
        DisableAndDestroy(go.GetComponent<RadialShuttleCloudPlatform>());
        DisableAndDestroy(go.GetComponent<FlyingMinecraftCloudPlatform>());
        DisableAndDestroy(go.GetComponent<PendulumWaveCloudPlatform>());
        DisableAndDestroy(go.GetComponent<VortexOrbitCloudPlatform>());
        DisableAndDestroy(go.GetComponent<KnotOrbitCloudPlatform>());
        DisableAndDestroy(go.GetComponent<PhaseGateCloudPlatform>());
        DisableAndDestroy(go.GetComponent<QuantumLeapCloudPlatform>());
        DisableAndDestroy(go.GetComponent<MobiusRibbonCloudPlatform>());
    }

    private static void DisableAndDestroy(MonoBehaviour component)
    {
        if (component == null)
        {
            return;
        }

        component.enabled = false;
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static T GetOrAddMovementComponent<T>(GameObject target) where T : MonoBehaviour
    {
        if (target == null)
        {
            return null;
        }

        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        if (component != null)
        {
            component.enabled = true;
        }

        return component;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            return;
        }

        field.SetValue(target, value);
    }

    private static List<Transform> CollectSlots(Scene scene)
    {
        Dictionary<int, Transform> map = new Dictionary<int, Transform>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.GetComponent<BoxCollider>() == null)
            {
                continue;
            }

            int slotIndex = InferSlotIndex(root.name);
            if (slotIndex >= 0 && !map.ContainsKey(slotIndex))
            {
                map.Add(slotIndex, root.transform);
            }
        }

        if (map.Count == 0)
        {
            List<Transform> fallback = new List<Transform>();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && root.GetComponent<BoxCollider>() != null)
                {
                    fallback.Add(root.transform);
                }
            }

            fallback.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            if (fallback.Count > 4)
            {
                fallback.RemoveRange(4, fallback.Count - 4);
            }

            return fallback;
        }

        List<Transform> ordered = new List<Transform>();
        for (int i = 0; i < SlotCount; i++)
        {
            map.TryGetValue(i, out Transform slot);
            ordered.Add(slot);
        }

        return ordered;
    }

    private static void EnsureSlots(Scene scene, int level, List<Transform> slots, int requiredCount)
    {
        if (slots == null)
        {
            return;
        }

        while (slots.Count < requiredCount)
        {
            slots.Add(null);
        }

        for (int i = 0; i < requiredCount; i++)
        {
            if (slots[i] != null)
            {
                continue;
            }

            slots[i] = CreateAutoSlot(scene, level, i);
        }
    }

    private static Transform CreateAutoSlot(Scene scene, int level, int slotIndex)
    {
        GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        string suffix = slotIndex >= 0 && slotIndex < SlotSuffixes.Length ? SlotSuffixes[slotIndex] : "X";
        slot.name = $"LV{level}_Auto_{suffix}";
        slot.transform.localScale = new Vector3(2.2f, 0.55f, 2.2f);
        slot.transform.position = new Vector3(0f, 7f + (slotIndex * 0.3f), 27f + (slotIndex * 3.2f));
        SceneManager.MoveGameObjectToScene(slot, scene);
        return slot.transform;
    }

    private static int InferSlotIndex(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return -1;
        }

        string upper = objectName.ToUpperInvariant();
        if (upper.Contains("_A"))
        {
            return 0;
        }

        if (upper.Contains("_B"))
        {
            return 1;
        }

        if (upper.Contains("_C"))
        {
            return 2;
        }

        if (upper.Contains("_D"))
        {
            return 3;
        }

        return -1;
    }

    private static void RemoveExtraBlock(Scene scene)
    {
        GameObject extra = FindRootByName(scene, ExtraBlockName);
        if (extra == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(extra);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(extra);
        }
    }

    private static GameObject FindRootByName(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                return root;
            }
        }

        return null;
    }

    private static bool TryParseLevel(string sceneName, out int levelIndex)
    {
        levelIndex = 0;
        if (string.IsNullOrWhiteSpace(sceneName) || !sceneName.StartsWith("LV", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = sceneName.Substring(2);
        if (!int.TryParse(suffix, out int parsed) || parsed <= 0)
        {
            return false;
        }

        levelIndex = parsed;
        return true;
    }
}
