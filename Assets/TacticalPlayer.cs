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

    [Header("Environment")]
    public LayerMask groundLayer;

    // FSM Core
    private PlayerState currentState;

    void Start()
    {
        if (attackLine != null)
        {
            attackLine.enabled = false;
        }

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

    public override void HandleInput()
    {
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
                Vector3 direction = (clickPoint - player.transform.position).normalized;
                player.FireAttackRay(direction);
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
        if (Input.GetMouseButtonDown(0))
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
                Vector3 direction = (clickPoint - player.transform.position).normalized;
                player.FireAttackRay(direction);
                player.ChangeState(new IdleState(player));
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
    private float currentJumpHeight; // Scaled specifically for this instance's distance

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

        // --- SCALE HEIGHT BASED ON DISTANCE ---
        // 1. Find where the current distance falls between min and max parameters (Returns 0.0 to 1.0)
        float distanceRange = player.maxJumpDistance - player.minJumpDistance;
        float currentProgressFactor = distanceRange > 0f
        ? Mathf.Clamp01((cleanDistance - player.minJumpDistance) / distanceRange)
        : 0f;

        // 2. Interpolate cleanly between min and max heights using that factor
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
            // Uses the uniquely evaluated height value calculated on initialization
            currentPos.y += Mathf.Sin(progress * Mathf.PI) * currentJumpHeight;
            player.transform.position = currentPos;
        }
    }

    private void EvaluateLandingTransitions()
    {
        player.transform.position = target;

        if (attackQueued)
        {
            player.FireAttackRay(queuedAttackDir);
            player.ChangeState(new IdleState(player));
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
        if (Input.GetMouseButtonDown(1))
        {
            if (player.GetMouseGroundPoint(out Vector3 clickPoint))
            {
                attackQueued = true;
                queuedAttackDir = (clickPoint - target).normalized;
                Debug.Log("Attack Queued! Will fire upon landing.");
            }
        }
    }
}
