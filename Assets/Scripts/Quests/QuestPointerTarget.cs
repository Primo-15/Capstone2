using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on each NPC, item or area the quest pointer (QuestPointer) should be able to point at.
/// The pin shows over it only while the quest needs the player to go there.
///
/// ON AN OBJECT WITH QuestNPCInteraction: leave Quest ID EMPTY and everything is copied from it:
///   - "Starts Quest" ticked     > marked before the quest starts (until the player talks to them)
///   - its Objective ID          > marked while that is the quest's current objective (e.g. the knife)
///   - its Delivery Objective ID > marked while that is the current objective
///   - its NPC Name              > shown above the pin (unless you type a Label)
///
/// DELIVERY AREA (no NPC): put this on an empty object with a Box Collider (Is Trigger) covering the area.
///   Fill in Quest ID + Objective IDs (the delivery objective), tick "Complete When Reached" and set
///   "Required Item ID". Walking in while carrying the item completes the objective and takes the item
///   out of the inventory. (No collider = a circle of Reach Radius instead.)
/// </summary>
public class QuestPointerTarget : MonoBehaviour
{
    [Header("Quest - leave Quest ID empty to copy it from QuestNPCInteraction")]
    [Tooltip("Same Quest ID as in QuestManager. Empty = copied from the QuestNPCInteraction on this object.")]
    public string questID = "";
    [Tooltip("The objective(s) the player completes HERE. The pin shows while one of them is the current objective. " +
             "With Quest ID empty, these are added to the ones copied from QuestNPCInteraction.")]
    public string[] objectiveIDs = new string[0];
    [Tooltip("Also show the pin before the quest starts (the NPC who gives the quest). " +
             "Automatic when the QuestNPCInteraction has Starts Quest ticked.")]
    public bool markBeforeQuestStarts;

    [Header("Marker")]
    [Tooltip("Where the pin points. Empty = this object (or the bottom centre of its area trigger).")]
    public Transform pointAt;
    [Tooltip("Put the pin just above the top of this object's model. Off (or with Point At set) = use Height Above.")]
    public bool autoHeight = true;
    [Tooltip("How far above the point the pin's tip sits.")]
    public float heightAbove = 2.2f;
    public Color color = new Color(1f, 0.85f, 0.2f);
    [Tooltip("Name shown above the pin. Empty = the NPC Name from QuestNPCInteraction (if it isn't the default 'NPC').")]
    public string label = "";

    [Header("Delivery area / place (no NPC)")]
    [Tooltip("Complete the objective here when the player is in the area (only while it's the current objective). " +
             "The area is this object's trigger collider (e.g. a Box Collider with Is Trigger), or Reach Radius if it has none.")]
    public bool completeWhenReached;
    [Tooltip("Used only when this object has no trigger collider.")]
    public float reachRadius = 3f;
    [Tooltip("The player must be carrying this item (InventoryManager item ID). Empty = just being in the area is enough.")]
    public string requiredItemID = "";
    [Tooltip("Take the required item out of the inventory when it's delivered.")]
    public bool removeItem = true;
    [Tooltip("Off = delivered as soon as the player walks in. On = they have to press the key inside the area.")]
    public bool pressKeyToComplete;
    public KeyCode key = KeyCode.E;
    [Tooltip("Optional 'Press E to deliver' prompt, shown while the player is in the area and can deliver.")]
    public GameObject prompt;
    [Tooltip("Switched ON once it's delivered (e.g. the item now lying in the area). Keep them switched off in the scene. " +
             "Also switched on after loading if the objective is already done.")]
    public GameObject[] showWhenDone = new GameObject[0];
    [Tooltip("Fires when the objective is completed here (sound, message, etc.).")]
    public UnityEvent onCompleted;

    // what's actually used (own fields, or copied from QuestNPCInteraction)
    string qid;
    string[] objectives = new string[0];
    bool beforeStart;
    string npcLabel;
    float height;
    Collider area;
    bool doneShown;

    static readonly List<QuestPointerTarget> all = new List<QuestPointerTarget>();
    internal static List<QuestPointerTarget> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { all.Clear(); }

    /// <summary>The point the pin and distance are measured from.</summary>
    public Vector3 Position
    {
        get
        {
            if (pointAt != null) return pointAt.position;
            if (AreaUsable)
            {
                Bounds b = area.bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }
            return transform.position;
        }
    }

    public Vector3 MarkerPosition => Position + Vector3.up * height;
    public string Label => !string.IsNullOrEmpty(label) ? label : npcLabel;

    bool AreaUsable => area != null && area.enabled && area.gameObject.activeInHierarchy;

    void Awake()
    {
        height = heightAbove;

        foreach (Collider c in GetComponents<Collider>())
        {
            if (c.isTrigger)
            {
                area = c;
                break;
            }
        }

        if (prompt != null) prompt.SetActive(false);
        ResolveQuest();
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
        if (prompt != null) prompt.SetActive(false);
    }

    void Start()
    {
        if (autoHeight && pointAt == null) height = MeasureHeight(heightAbove);
        QuestPointer.EnsureExists();
    }

    void Update()
    {
        // already delivered (also after loading a save): put the "done" look back
        if (!doneShown && showWhenDone != null && showWhenDone.Length > 0 && ObjectiveDone())
            ShowDone();

        if (!completeWhenReached) return;

        string current;
        bool can = CanCompleteHere(out current);

        bool showPrompt = can && pressKeyToComplete;
        if (prompt != null && prompt.activeSelf != showPrompt) prompt.SetActive(showPrompt);

        if (!can) return;
        if (pressKeyToComplete && !Input.GetKeyDown(key)) return;

        Complete(current);
    }

    // ---------- delivery area ----------

    bool CanCompleteHere(out string current)
    {
        current = null;
        if (QuestNPCInteraction.IsInDialogue) return false;

        QuestManager m = QuestManager.Instance;
        if (m == null) return false;

        QuestData q = m.ActiveQuest;
        if (q == null || q.questID != qid) return false;

        current = CurrentObjective(m, q);
        if (current == null || !HasObjective(current)) return false;

        if (!string.IsNullOrWhiteSpace(requiredItemID))
        {
            InventoryManager inv = InventoryManager.Instance;
            if (inv == null || !inv.HasItem(requiredItemID)) return false;
        }

        return PlayerInArea();
    }

    void Complete(string objective)
    {
        QuestManager m = QuestManager.Instance;
        if (!m.CompleteCurrentObjective(objective)) return;

        if (removeItem && !string.IsNullOrWhiteSpace(requiredItemID))
        {
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null) inv.RemoveItem(requiredItemID);
        }

        if (prompt != null) prompt.SetActive(false);
        ShowDone();
        onCompleted?.Invoke();
    }

    bool PlayerInArea()
    {
        Transform p = QuestPointer.Instance != null ? QuestPointer.Instance.Player : null;
        if (p == null) return false;

        if (AreaUsable)
        {
            // feet and about waist height, so the box doesn't have to reach exactly into the floor
            return Inside(area, p.position) || Inside(area, p.position + Vector3.up);
        }

        return (p.position - Position).sqrMagnitude <= reachRadius * reachRadius;
    }

    static bool Inside(Collider c, Vector3 point)
    {
        return (c.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }

    void ShowDone()
    {
        doneShown = true;
        if (showWhenDone == null) return;
        foreach (GameObject g in showWhenDone)
            if (g != null) g.SetActive(true);
    }

    /// <summary>True once the quest has moved past this target's objective (or the quest is finished).</summary>
    bool ObjectiveDone()
    {
        QuestManager m = QuestManager.Instance;
        if (m == null || string.IsNullOrWhiteSpace(qid)) return false;
        if (m.IsQuestCompleted(qid)) return true;

        QuestData q = m.ActiveQuest;
        if (q == null || q.questID != qid || q.objectives == null) return false;

        for (int i = 0; i < q.objectives.Length && i < m.CurrentObjectiveIndex; i++)
            if (HasObjective(q.objectives[i].objectiveID)) return true;
        return false;
    }

    // ---------- used by QuestPointer ----------

    /// <summary>True when the quest currently needs the player to come here.</summary>
    internal bool IsWanted(QuestManager m)
    {
        if (string.IsNullOrWhiteSpace(qid) || m.IsQuestCompleted(qid)) return false;

        QuestData q = m.ActiveQuest;
        if (q != null && q.questID == qid)
        {
            string current = CurrentObjective(m, q);
            return current != null && HasObjective(current);
        }

        // not started yet: only the quest giver, and only when the quest can actually start now
        // (QuestNPCInteraction only starts a quest when no other quest is active)
        return beforeStart && q == null && m.ArePrerequisitesMet(qid);
    }

    static string CurrentObjective(QuestManager m, QuestData q)
    {
        if (q.objectives == null) return null;
        int i = m.CurrentObjectiveIndex;
        return i >= 0 && i < q.objectives.Length ? q.objectives[i].objectiveID : null;
    }

    bool HasObjective(string id)
    {
        foreach (string o in objectives)
            if (o == id) return true;
        return false;
    }

    // ---------- setup ----------

    const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    void ResolveQuest()
    {
        QuestNPCInteraction npc = GetComponentInParent<QuestNPCInteraction>();

        if (npc != null)
        {
            string n = Read<string>(npc, "npcName");
            if (!string.IsNullOrWhiteSpace(n) && n != "NPC") npcLabel = n;
        }

        // filled in by hand (or nothing to copy from)
        if (!string.IsNullOrWhiteSpace(questID) || npc == null)
        {
            qid = questID;
            objectives = objectiveIDs ?? new string[0];
            beforeStart = markBeforeQuestStarts;

            if (string.IsNullOrWhiteSpace(qid))
                Debug.LogWarning($"QuestPointerTarget '{name}': no Quest ID and no QuestNPCInteraction on this " +
                                 "object, so its pin will never show. Fill in Quest ID and Objective IDs.", this);
            return;
        }

        // copy from QuestNPCInteraction (its fields are private, so they're read directly)
        if (typeof(QuestNPCInteraction).GetField("questID", FieldFlags) == null)
        {
            Debug.LogWarning($"QuestPointerTarget '{name}': couldn't read QuestNPCInteraction's fields " +
                             "(renamed?). Fill in Quest ID and Objective IDs by hand.", this);
            return;
        }

        qid = Read<string>(npc, "questID");

        List<string> list = new List<string>();
        AddId(list, Read<string>(npc, "objectiveID"));
        AddId(list, Read<string>(npc, "deliveryObjectiveID"));
        if (objectiveIDs != null)
            foreach (string extra in objectiveIDs) AddId(list, extra);
        objectives = list.ToArray();

        beforeStart = Read<bool>(npc, "startsQuest") || markBeforeQuestStarts;

        if (string.IsNullOrWhiteSpace(qid))
            Debug.LogWarning($"QuestPointerTarget '{name}': its QuestNPCInteraction has no Quest ID, " +
                             "so there's nothing to point at here.", this);
    }

    static void AddId(List<string> list, string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && !list.Contains(id)) list.Add(id);
    }

    static T Read<T>(object o, string field)
    {
        FieldInfo f = o.GetType().GetField(field, FieldFlags);
        if (f != null && f.GetValue(o) is T value) return value;
        return default(T);
    }

    float MeasureHeight(float fallback)
    {
        bool any = false;
        Bounds b = new Bounds();

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!any) return fallback;
        return Mathf.Clamp(b.max.y - Position.y + 0.3f, 0.3f, 10f);
    }

    void OnDrawGizmosSelected()
    {
        if (area == null)
            foreach (Collider c in GetComponents<Collider>())
                if (c.isTrigger) { area = c; break; }

        float h = Application.isPlaying ? height : heightAbove;
        Gizmos.color = color;
        Gizmos.DrawLine(Position, Position + Vector3.up * h);
        Gizmos.DrawWireSphere(Position + Vector3.up * h, 0.25f);
        if (completeWhenReached && !AreaUsable) Gizmos.DrawWireSphere(Position, reachRadius);
    }
}
