using System.Collections;
using UnityEngine;

// --- 1. THE CONTEXT (Main MonoBehaviour) ---
public class TacticalPlayer : MonoBehaviour
{
    [Header("Run Settings")]
    public float runSpeed = 4f;

    [Header("Jump Settings")]
    public float jumpSpeed = 10f;
    public float minJumpDistance = 2f;
    public float maxJumpDistance = 8f;
    [Tooltip("The apex height for a minimum distance jump.")]
    public float minJumpHeight = 1f;
    [Tooltip("The apex height for a maximum distance jump.")]
    public float maxJumpHeight = 4f;

    [Header("Attack Settings")]
    public float attackRange = 10f;
    public float attackDisplayDuration = 0.2f;
    public LineRenderer attackLine;

    [Tooltip("Minimum time (in seconds) required between attacks.")]
    public float attackCooldown = 0.5f; 
    private float nextAttackTime = 0f;

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    [Tooltip("Stamina consumed per single attack ray burst.")]
    public float attackStaminaCost = 25f; // Exactly 1/4 of a 100-point bar
    [Tooltip("Stamina recovered per second while resting.")]
    public float restRegenRate = 20f; 

    [Header("Environment")]
    public LayerMask groundLayer;

    [Header("Visual Juice")]
    [Tooltip("Assign the child GameObject containing your cylinder/mesh here.")]
    public Transform visualContainer;
    [Tooltip("How fast the player scales up/down when changing stances.")]
    public float scaleSpeed = 5f;
    [Tooltip("The local scale multiplier of the visuals when sitting.")]
    public Vector3 sittingScale = new Vector3(1f, 0.4f, 1f); // Squashes Y to 40% height

    private Coroutine stanceCoroutine;
    private Vector3 originalVisualScale = Vector3.one;

    // FSM Core
    private PlayerState currentState;
    [HideInInspector] public bool sitQueued = false;

    void Start()
    {
        if (visualContainer != null)
        {
            originalVisualScale = visualContainer.localScale;
        }

        if (attackLine != null)
        {
            attackLine.enabled = false;
        }

        currentStamina = maxStamina; // Start completely topped off
        ChangeState(new IdleState(this));
    }

    void Update()
    {
        // 1. Process inputs for the current state
        currentState?.HandleInput();

        // 2. Run movement/logic tick
        currentState?.Update();
    }

    public void ChangeState(PlayerState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    // --- NEW COMBINED RESOURCE/COOLDOWN CHECK ---
    public bool CanAttack()
    {
        // Check if cooldown has finished
        if (Time.time < nextAttackTime)
        {
            float remaining = nextAttackTime - Time.time;
            Debug.LogWarning($"Attack on Cooldown! Wait {remaining:F2}s");
            return false;
        }

        // Check if we have the stamina
        return HasEnoughStamina(attackStaminaCost);
    }

    // Call this right when an attack succeeds to lock out further inputs
    public void TriggerAttackCooldown()
    {
        nextAttackTime = Time.time + attackCooldown;
    }

    // --- STAMINA SYSTEM UTILITIES ---
    public bool HasEnoughStamina(float cost)
    {
        return currentStamina >= cost;
    }

    public bool TryConsumeStamina(float cost)
    {
        if (HasEnoughStamina(cost))
        {
            currentStamina -= cost;
            Debug.Log($"Stamina Consumed: -{cost}. Remaining: {currentStamina}/{maxStamina}");
            return true;
        }
        
        Debug.LogWarning($"Action Denied: Insufficient Stamina! Need {cost}, only have {currentStamina}");
        return false;
    }

    public void RegenerateStamina(float amount)
    {
        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
    }

    // --- SHARED UTILITIES ---
    public bool GetMouseGroundPoint(out Vector3 point)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            point = hit.point;
            point.y = transform.position.y; // Keep it flat
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    public void FireAttackRay(Vector3 direction)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + (direction * attackRange);

        if (attackLine != null)
        {
            StartCoroutine(ShowAttackVisuals(startPos, endPos));
        }

        Debug.Log("Attack Fired!");
    }

    private IEnumerator ShowAttackVisuals(Vector3 start, Vector3 end)
    {
        attackLine.SetPosition(0, start);
        attackLine.SetPosition(1, end);
        attackLine.enabled = true;

        yield return new WaitForSeconds(attackDisplayDuration);

        attackLine.enabled = false;
    }

    // A utility helper to cleanly smoothly blend between scales
    public void SetStanceScale(Vector3 targetScale)
    {
        if (visualContainer == null) return;
        
        if (stanceCoroutine != null)
        {
            StopCoroutine(stanceCoroutine);
        }
        stanceCoroutine = StartCoroutine(AnimateStance(targetScale));
    }

    private IEnumerator AnimateStance(Vector3 targetScale)
    {
        while (Vector3.Distance(visualContainer.localScale, targetScale) > 0.001f)
        {
            visualContainer.localScale = Vector3.Lerp(
                visualContainer.localScale, 
                targetScale, 
                Time.deltaTime * scaleSpeed
            );
            yield return null;
        }
        visualContainer.localScale = targetScale;
    }
    
    // Helper shortcuts for our states to call
    public void SitDown() => SetStanceScale(Vector3.Scale(originalVisualScale, sittingScale));
    public void StandUp() => SetStanceScale(originalVisualScale);
}


// --- 2. THE STATE BASE CLASS ---
public abstract class PlayerState
{
    protected TacticalPlayer player;

    public PlayerState(TacticalPlayer player)
    {
        this.player = player;
    }

    public virtual void Enter() { }
    public virtual void HandleInput() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}


// --- 3. CONCRETE STATES ---
public class IdleState : PlayerState
{
    public IdleState(TacticalPlayer player) : base(player) { }

    public override void Enter() 
    { 
        // If a sit was queued right as we became idle, consume it immediately
        if (player.sitQueued)
        {
            player.sitQueued = false;
            player.ChangeState(new RestingState(player));
        }
    }

    public override void HandleInput()
    {
        // Transition to Resting State manually
        if (Input.GetKeyDown(KeyCode.X))
        {
            player.ChangeState(new RestingState(player));
            return;
        }

        if (Input.GetMouseButton(0))
        {
            if (player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    player.ChangeState(new JumpState(player, clickPoint));
                }
                else
                {
                    player.ChangeState(new RunState(player, clickPoint));
                }
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                // Verify BOTH cooldown and stamina before attacking
                if (player.CanAttack())
                {
                    player.TriggerAttackCooldown(); // Start the clock
                    player.TryConsumeStamina(player.attackStaminaCost);

                    Vector3 direction = (clickPoint - player.transform.position).normalized;
                    player.FireAttackRay(direction);
                }
            }
        }
    }
}

public class RunState : PlayerState
{
    private Vector3 targetPoint;

    public RunState(TacticalPlayer player, Vector3 targetPoint) : base(player)
    {
        this.targetPoint = targetPoint;
    }

    public override void Update()
    {
        player.transform.position = Vector3.MoveTowards(player.transform.position, targetPoint, player.runSpeed * Time.deltaTime);

        if (Vector3.Distance(player.transform.position, targetPoint) < 0.05f)
        {
            player.transform.position = targetPoint;

            // Check if they queued a sit while running
            if (player.sitQueued)
            {
                player.sitQueued = false; // Clear the queue flag
                player.ChangeState(new RestingState(player));
                return;
            }

            if (Input.GetMouseButton(0) && player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    player.ChangeState(new JumpState(player, clickPoint));
                    return;
                }
                else if (Vector3.Distance(player.transform.position, clickPoint) > 0.1f)
                {
                    player.ChangeState(new RunState(player, clickPoint));
                    return;
                }
            }

            player.ChangeState(new IdleState(player));
        }
    }

    public override void HandleInput()
    {
        // Queue the sit if they hit X while running
        if (Input.GetKeyDown(KeyCode.X))
        {
            player.sitQueued = true;
            Debug.Log("Sit Queued! Player will rest upon reaching target.");
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                player.sitQueued = false; // New movement cancels a queued sit
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    player.ChangeState(new JumpState(player, clickPoint));
                }
                else
                {
                    player.ChangeState(new RunState(player, clickPoint));
                }
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                if (player.CanAttack())
                {
                    player.TriggerAttackCooldown();
                    player.TryConsumeStamina(player.attackStaminaCost);

                    Vector3 direction = (clickPoint - player.transform.position).normalized;
                    player.FireAttackRay(direction);

                    if (player.sitQueued)
                    {
                        player.sitQueued = false;
                        player.ChangeState(new RestingState(player));
                    }
                    else
                    {
                        player.ChangeState(new IdleState(player));
                    }
                }
            }
        }
    }
}

public class JumpState : PlayerState
{
    private Vector3 start;
    private Vector3 target;

    private float totalJumpDuration;
    private float elapsedTime;
    private float currentJumpHeight;

    private bool attackQueued;
    private Vector3 queuedAttackDir;

    public JumpState(TacticalPlayer player, Vector3 targetPoint) : base(player)
    {
        this.start = player.transform.position;
        this.elapsedTime = 0f;
        this.attackQueued = false;

        Vector3 direction = targetPoint - start;
        float actualDistance = direction.magnitude;

        if (actualDistance < player.minJumpDistance)
        {
            Vector3 jumpDir = actualDistance < 0.01f ? player.transform.forward : direction.normalized;
            this.target = start + (jumpDir * player.minJumpDistance);
        }
        else if (actualDistance > player.maxJumpDistance)
        {
            this.target = start + (direction.normalized * player.maxJumpDistance);
        }
        else
        {
            this.target = targetPoint;
        }

        float cleanDistance = Vector3.Distance(start, target);
        this.totalJumpDuration = cleanDistance / player.jumpSpeed;

        float distanceRange = player.maxJumpDistance - player.minJumpDistance;
        float currentProgressFactor = distanceRange > 0f
            ? Mathf.Clamp01((cleanDistance - player.minJumpDistance) / distanceRange)
            : 0f;

        this.currentJumpHeight = Mathf.Lerp(player.minJumpHeight, player.maxJumpHeight, currentProgressFactor);
    }

    public override void Update()
    {
        if (totalJumpDuration <= 0f)
        {
            EvaluateLandingTransitions();
            return;
        }

        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / totalJumpDuration;

        if (progress >= 1f)
        {
            EvaluateLandingTransitions();
        }
        else
        {
            Vector3 currentPos = Vector3.Lerp(start, target, progress);
            currentPos.y += Mathf.Sin(progress * Mathf.PI) * currentJumpHeight;
            player.transform.position = currentPos;
        }
    }

    private void EvaluateLandingTransitions()
    {
        player.transform.position = target;

        // Priority 1: Execute queued mid-air attack
        if (attackQueued)
        {
            // double check they haven't somehow bypassed cooldown constraints
            if (player.CanAttack())
            {
                player.TriggerAttackCooldown();
                player.TryConsumeStamina(player.attackStaminaCost);
                player.FireAttackRay(queuedAttackDir);
            }
            player.sitQueued = false;
            player.ChangeState(new IdleState(player));
            return;
        }

        // Priority 2: Drop into queued rest
        if (player.sitQueued)
        {
            player.sitQueued = false;
            player.ChangeState(new RestingState(player));
            return;
        }

        if (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftShift))
        {
            if (player.GetMouseGroundPoint(out Vector3 consecutiveClickPoint))
            {
                if (Vector3.Distance(target, consecutiveClickPoint) > 0.1f)
                {
                    player.ChangeState(new JumpState(player, consecutiveClickPoint));
                    return;
                }
            }
        }

        player.ChangeState(new IdleState(player));
    }

    public override void HandleInput()
    {
        // Queue sit mid-jump
        if (Input.GetKeyDown(KeyCode.X))
        {
            player.sitQueued = true;
            Debug.Log("Sit Queued mid-jump! Will rest upon landing.");
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                // Queue the attack if resources are green
                if (player.CanAttack())
                {
                    attackQueued = true;
                    queuedAttackDir = (clickPoint - target).normalized;
                    Debug.Log("Attack Queued! Will fire upon landing.");
                }
                else
                {
                    Debug.LogWarning("Cannot queue attack: Out of stamina.");
                }
            }
        }
    }
}

// --- NEW STATE: RESTING STATE ---
public class RestingState : PlayerState
{
    public RestingState(TacticalPlayer player) : base(player) { }

    public override void Enter()
    {
        Debug.Log("Player sits down to rest. Regenerating stamina...");
        player.SitDown(); // Smoothly squash the cylinder mesh
    }

    public override void Update()
    {
        // Regenerate stamina over time while resting
        player.RegenerateStamina(player.restRegenRate * Time.deltaTime);
    }

    public override void HandleInput()
    {
        // 1. Right-Click to Attack: This interrupts the rest and forces them to stand up
        if (Input.GetMouseButtonDown(1))
        {
            if (player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                if (player.CanAttack())
                {
                    player.TriggerAttackCooldown();
                    player.TryConsumeStamina(player.attackStaminaCost);

                    Vector3 direction = (clickPoint - player.transform.position).normalized;
                    player.FireAttackRay(direction);

                    Debug.Log("Rest interrupted by attack! Standing up.");
                    player.ChangeState(new IdleState(player));
                    return;
                }
            }
        }

        // 2. Manual Wake-up: Stand up if they press 'X' or left-click to move
        if (Input.GetKeyDown(KeyCode.X) || Input.GetMouseButton(0))
        {
            Debug.Log("Player stands up from resting.");
            player.ChangeState(new IdleState(player));
        }
    }

    public override void Exit()
    {
        player.StandUp(); // Smoothly restore the original scale
    }
}