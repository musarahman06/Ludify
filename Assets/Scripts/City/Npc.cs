using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A city resident: blocky character like the player, wanders the city on the NavMesh, and can be talked to.
/// <see cref="QuestManager"/> decides what they say and which icon floats over their head.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Npc : MonoBehaviour
{
    public enum Icon { None, QuestAvailable, Hint, TurnIn, Shop }

    public string DisplayName { get; set; }
    public Icon CurrentIcon { get; private set; }
    public bool IsTalking { get; private set; }
    /// <summary>Stands in one place (e.g. the shopkeeper) instead of wandering.</summary>
    public bool Stationary { get; private set; }

    NavMeshAgent agent;
    Transform leftLeg, rightLeg, leftArm, rightArm, visual;
    TextMeshPro iconText;
    float idleUntil, walkPhase;
    bool idling = true;
    Transform faceTarget;
    Quaternion homeRotation;

    const float WanderRadius = 35f;

    public void Init(Transform visualRoot, Transform iconAnchor)
    {
        agent = GetComponent<NavMeshAgent>();
        visual = visualRoot;
        leftLeg = Find(visualRoot, "LeftLegPivot");
        rightLeg = Find(visualRoot, "RightLegPivot");
        leftArm = Find(visualRoot, "LeftArmPivot");
        rightArm = Find(visualRoot, "RightArmPivot");

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(iconAnchor, false);
        iconText = iconGo.AddComponent<TextMeshPro>();
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.fontSize = 9f;
        iconText.fontStyle = FontStyles.Bold;
        iconText.outlineWidth = 0.25f;
        iconText.outlineColor = new Color32(20, 20, 30, 255);
        iconText.rectTransform.sizeDelta = new Vector2(3f, 3f);
        SetIcon(Icon.None);
        idleUntil = Time.time + Random.Range(0f, 3f);
    }

    public void SetIcon(Icon icon)
    {
        CurrentIcon = icon;
        if (iconText == null) return;
        switch (icon)
        {
            case Icon.QuestAvailable: iconText.text = "!"; iconText.color = new Color(1f, 0.82f, 0.2f); break;
            case Icon.Hint: iconText.text = "?"; iconText.color = new Color(0.45f, 0.85f, 1f); break;
            case Icon.TurnIn: iconText.text = "!"; iconText.color = new Color(0.4f, 1f, 0.45f); break;
            case Icon.Shop: iconText.text = "$"; iconText.color = new Color(1f, 0.5f, 0.75f); break;
            default: iconText.text = ""; break;
        }
    }

    /// <summary>Stay put facing the current direction (no NavMesh needed).</summary>
    public void MakeStationary()
    {
        Stationary = true;
        homeRotation = transform.rotation;
        agent.enabled = false;
    }

    /// <summary>Stop walking and face someone while a conversation is open.</summary>
    public void BeginTalk(Transform other)
    {
        IsTalking = true;
        faceTarget = other;
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    public void EndTalk()
    {
        IsTalking = false;
        faceTarget = null;
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        idling = true;
        idleUntil = Time.time + Random.Range(1f, 3f);
    }

    void Update()
    {
        if (Stationary)
        {
            Vector3 look = IsTalking && faceTarget != null ? faceTarget.position - transform.position : homeRotation * Vector3.forward;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 6f * Time.deltaTime);
            Animate();
            Billboard();
            return;
        }
        if (agent == null || !agent.isOnNavMesh) return;

        if (IsTalking && faceTarget != null)
        {
            Vector3 d = faceTarget.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 8f * Time.deltaTime);
        }
        else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            if (!idling) { idling = true; idleUntil = Time.time + Random.Range(2f, 6f); }
            else if (Time.time >= idleUntil) { idling = false; PickDestination(); }
        }

        Animate();
        Billboard();
    }

    void PickDestination()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 candidate = transform.position + Random.insideUnitSphere * WanderRadius;
            candidate.y = transform.position.y;
            if (!CityArea.Contains(candidate)) continue;
            Vector3? street = CityNav.Snap(candidate, 4f);   // outside on the streets, never a roof or indoors
            if (street.HasValue)
            {
                agent.SetDestination(street.Value);
                return;
            }
        }
        idling = true;
        idleUntil = Time.time + 1f;
    }

    void Animate()
    {
        float speed = IsTalking || Stationary ? 0f : agent.velocity.magnitude;
        bool moving = speed > 0.15f;
        walkPhase = moving ? walkPhase + Time.deltaTime * 7f * Mathf.Clamp(speed / 1.8f, 0.6f, 1.6f)
                           : Mathf.Lerp(walkPhase, 0f, Time.deltaTime * 6f);
        float swing = moving ? Mathf.Sin(walkPhase) * 30f : 0f;
        if (leftLeg) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (rightLeg) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (leftArm) leftArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (rightArm) rightArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (visual) visual.localPosition = new Vector3(0f, moving ? Mathf.Abs(Mathf.Sin(walkPhase * 2f)) * 0.05f : 0f, 0f);
    }

    void Billboard()
    {
        if (iconText == null || iconText.text.Length == 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        var t = iconText.transform;
        t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
        t.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f) * 0.12f, 0f);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
