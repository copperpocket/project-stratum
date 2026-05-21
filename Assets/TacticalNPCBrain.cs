using UnityEngine;

public class TacticalNPCBrain : MonoBehaviour
{
    public enum NPCBehaviorMode { StandStill, RandomJumping, AggressiveTurret }

    [Header("Behavior Settings")]
    [Tooltip("Choose what this NPC should focus on doing.")]
    public NPCBehaviorMode behaviorMode = NPCBehaviorMode.StandStill;
    
    [Tooltip("How often (in seconds) the NPC evaluates a new action.")]
    public float decisionInterval = 2.0f;

    [Header("Movement Constraints")]
    [Tooltip("The maximum radius the NPC can stray from where it spawned.")]
    public float tetherRadius = 5f;

    [Header("Targeting (For Attacks)")]
    [Tooltip("Assign your actual Player GameObject here so the NPC knows where to aim.")]
    public Transform playerTarget;

    // We change this back to TacticalPlayer to match your intact state machine!
    private TacticalPlayer actor; 
    private Vector3 originPoint;
    private float nextDecisionTime = 0f;

    void Start()
    {
        actor = GetComponent<TacticalPlayer>();
        originPoint = transform.position;
        nextDecisionTime = Time.time + decisionInterval;
    }

    void Update()
    {
        if (Time.time >= nextDecisionTime)
        {
            ExecuteBehavior();
            nextDecisionTime = Time.time + decisionInterval;
        }
    }

    private void ExecuteBehavior()
    {
        if (actor == null) return;

        switch (behaviorMode)
        {
            case NPCBehaviorMode.StandStill:
                break;

            case NPCBehaviorMode.RandomJumping:
                HandleRandomJumping();
                break;

            case NPCBehaviorMode.AggressiveTurret:
                HandleTrackingAttack();
                break;
        }
    }

    private void HandleRandomJumping()
    {
        Vector2 randomCirclePoint = Random.insideUnitCircle * tetherRadius;
        Vector3 targetDestination = originPoint + new Vector3(randomCirclePoint.x, 0f, randomCirclePoint.y);

        if (actor.HasEnoughStamina(actor.attackStaminaCost))
        {
            Debug.Log($"[NPC AI] Jumping to tethered destination: {targetDestination}");
            actor.ChangeState(new JumpState(actor, targetDestination));
        }
        else
        {
            Debug.Log("[NPC AI] Low Stamina! Sitting down to rest.");
            actor.ChangeState(new RestingState(actor));
        }
    }

    private void HandleTrackingAttack()
    {
        if (playerTarget == null) return;

        Vector3 targetDir = (playerTarget.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (distanceToPlayer <= actor.attackRange && actor.CanAttack())
        {
            actor.TriggerAttackCooldown();
            actor.TryConsumeStamina(actor.attackStaminaCost);
            actor.FireAttackRay(targetDir);
            Debug.Log("[NPC AI] Sniped at player target!");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? originPoint : transform.position;
        Gizmos.DrawWireSphere(center, tetherRadius);
    }
}