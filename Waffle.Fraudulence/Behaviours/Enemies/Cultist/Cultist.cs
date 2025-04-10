using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using static Waffle.Fraudulence.Behaviours.Enemies.Cultist.CultistMovementState;

namespace Waffle.Fraudulence.Behaviours.Enemies.Cultist;

[HarmonyPatch]
public class Cultist : MonoBehaviour
{
    [Header("Attacks")]
    public GameObject Beam;

    [Header("Visuals")]
    public RifleTracking Rifle;
    public CultistVignetteEffect VignetteEffect;
    public GameObject BeamSpawnPoint;
    public GameObject BlueFlash;

    [Header("Movement")]
    public float JumpPower;
    public float FearRadius;
    public float SlideLength = 1;
    public float SlideSpeed = 30;
    public float DodgeCooldown = 1;
    public float DodgeOddsPerClock = 0.35f;

    [Header("Sounds")]
    public GameObject JumpSound;
    public GameObject BlockSound;

    [Header("References")]
    public Machine Machine;
    public NavMeshAgent Agent;
    public Rigidbody Rigidbody;

    private float _timeUntilNextShot;
    private bool _isShooting;
    private float _spinDirection = 1;
    private float _spinDirectionTimer;
    private bool _isJumping;
    private float _dodgeCooldown;
    private float _jumpClock;
    private float _stateLength;
    private float _startSpeed;
    private Coroutine _slideRoutine;
    private Coroutine _shootRoutine;
    private Coroutine _lastShootPlayerLook;

    private static readonly int s_shoot = Animator.StringToHash("Shoot");
    private static readonly int s_jumping = Animator.StringToHash("Jumping");
    private static readonly int s_aiming = Animator.StringToHash("Aiming");
    private static readonly int s_walking = Animator.StringToHash("Walking");
    private static readonly int s_sliding = Animator.StringToHash("Sliding");
    private static readonly int s_falling = Animator.StringToHash("Falling");

    public CultistMovementState MovementState { get; private set; }
    private float SpeedMultiplier => Machine?.eid?.totalSpeedModifier ?? 1;
    private float FrameTime => (1f / 24f) / SpeedMultiplier;
    private Vector3 JumpDirection
    {
        get
        {
            return MovementState switch
            {
                Spinning => transform.forward * Agent.speed,
                Still => Vector3.zero,
                TowardsPlayer => transform.forward * Agent.speed,
                Retreat => transform.forward * Agent.speed,
                Sliding => transform.forward * (Agent.speed * 1.5f),
                _ => throw null // this is literally impossible
            };
        }
    }

    private Vector3 LastPlayerFloorPos
    {
        get
        {
            if (Physics.Raycast(Machine.eid.target.position, Vector3.down, out RaycastHit hit,
                    float.MaxValue, LayerMaskDefaults.Get(LMD.Environment)))
            {
                _lastPlayerFloorPos = hit.point;
            }

            return _lastPlayerFloorPos;
        }
    }

    private Vector3 _lastPlayerFloorPos;

    private void Start()
    {
        _startSpeed = Agent.speed;
        CultistFireController.Instance.CurrentCultists.Add(this);
        Machine.onDeath.AddListener(OnDeath);
    }

    private void OnDeath()
    {
        try
        {
            CultistFireController.Instance.CurrentCultists.Remove(this);
            Rifle.GetComponent<BoxCollider>().enabled = true;
            Rifle.GetComponent<Rigidbody>().isKinematic = false; // make it ragdoll
            Rifle.enabled = false;
            StopCoroutine(_lastShootPlayerLook);
            StopCoroutine(_shootRoutine);
            StopCoroutine(_slideRoutine);
        }
        catch
        {
            Debug.LogWarning($"Error in cultist ondeath? Trycaught so it can die");
        }

        enabled = false;
    }

    private void Update()
    {
        if (ULTRAKILL.Cheats.BlindEnemies.Blind || Machine.eid.target == null)
        {
            Agent.destination = transform.position;
            return;
        }

        _timeUntilNextShot -= Time.deltaTime;

        if (MovementState is Sliding || _isJumping)
        {
            return;
        }

        if (Machine.gc.onGround)
        {
            _dodgeCooldown = Mathf.MoveTowards(_dodgeCooldown, 0, Time.deltaTime * SpeedMultiplier);
            if (MovementState is not Still)
            {
                _jumpClock += Time.deltaTime * SpeedMultiplier;
            }
        }

        if (_jumpClock > DodgeCooldown)
        {
            if (UnityEngine.Random.value < DodgeOddsPerClock)
            {
                if (Vector3.Distance(Machine.eid.target.position, transform.position) >= (FearRadius / 1.5f) && UnityEngine.Random.value < 0.5)
                {
                    Slide();
                    return;
                }

                Jump();
            }

            _jumpClock = 0;
        }

        _stateLength += Time.deltaTime * SpeedMultiplier;

        SetMovement();
    }

    private void FixedUpdate()
    {
        Agent.speed = _startSpeed * Machine.eid.totalSpeedModifier;
        Machine.anim.SetBool(s_falling, !Machine.gc.touchingGround);
    }

    [HarmonyPatch(typeof(HookArm), nameof(HookArm.FixedUpdate)), HarmonyPostfix]
    private static void DodgeHookPatch(HookArm __instance)
    {
        if (__instance.caughtEid?.TryGetComponent(out Cultist caughtCultist) ?? false)
        {
            caughtCultist.DodgeWhiplash();
        }
    }

    [HarmonyPatch(typeof(EnemyIdentifier), nameof(EnemyIdentifier.DeliverDamage)), HarmonyPrefix]
    private static bool StopCultistBleed(EnemyIdentifier __instance)
    {
        if (__instance.GetComponent<Cultist>() != null && __instance.hitter == "hook")
        {
            return false;
        }

        return true;
    }

    public void DodgeWhiplash()
    {
        CancelShoot();
        HookArm.Instance.StopThrow();
        Instantiate(BlockSound, Machine.chest.transform.position, Quaternion.identity);
        Dodge(true);
    }

    public void Dodge(bool force = false)
    {
        if ((MovementState == Sliding || _isShooting || _timeUntilNextShot < 0.5f) && !force)
        {
            return;
        }

        Debug.Log($"Distance {Vector3.Distance(Machine.eid.target.position, transform.position)} between {Machine.eid.target.position} and {transform.position}! {Vector3.Distance(Machine.eid.target.position, transform.position) >= (FearRadius * 1.5f)}");
        if (Vector3.Distance(Machine.eid.target.position, transform.position) >= (FearRadius * 1.5f) && UnityEngine.Random.value < 0.5f)
        {
            Slide(force);
            return;
        }

        if (_dodgeCooldown == 0 && NavMesh.SamplePosition(transform.position + JumpDirection, out _, 0.5f, NavMesh.AllAreas))
        {
            _dodgeCooldown = DodgeCooldown;
            Jump(force);
        }
        else
        {
            if (_dodgeCooldown != 0)
            {
                Debug.Log("Not sliding: due to point off navmesh");
            }
        }
    }

    public void Jump(bool force = false)
    {
        if ((_isJumping || !Machine.gc.onGround || _isShooting || _timeUntilNextShot < 0.5f) && !force)
        {
            Debug.Log("Not jumping");
            return;
        }

        CancelSlide(); //i dont even know what im doing i just want to get this done!!
        _isJumping = true;
        StartCoroutine(DoJump());
    }

    private IEnumerator DoJump()
    {
        Rigidbody.isKinematic = false;
        Machine.overrideFalling = true; //this prevents Machine.Update setting rb to kinematic! thanks hakita :3
        Rigidbody.velocity += JumpDirection;

        Instantiate(JumpSound, transform.position, Quaternion.identity);
        Machine.KnockBack(Vector3.up * JumpPower);
        Machine.anim.SetBool(s_jumping, true);

        yield return new WaitForSeconds(0.2f);

        while (!Machine.gc.touchingGround)
        {
            yield return null;
        }

        Machine.anim.SetBool(s_jumping, false);
        Machine.overrideFalling = false;
        _isJumping = false;
    }

    public void Slide(bool force = false)
    {
        if ((_isJumping || !Machine.gc.onGround || _isShooting || MovementState == Sliding) && !force)
        {
            Debug.Log("Ret on slide");
            return;
        }

        MovementState = Sliding;
        UpdateState();
        _slideRoutine = StartCoroutine(DoSlide());
    }

    private IEnumerator DoSlide()
    {
        Rigidbody.isKinematic = false;
        Agent.enabled = false;
        Machine.overrideFalling = true; //this prevents Machine.Update setting rb to kinematic! thanks hakita :3

        Vector3 playerVector = Machine.eid.target.position - transform.position;
        playerVector.y = 0;
        transform.forward = playerVector.normalized;
        Rigidbody.velocity = transform.forward * SlideSpeed;

        float timer = 0;
        while (timer < SlideLength && NavMesh.SamplePosition(transform.position + transform.forward * 2, out _, 0.5f, NavMesh.AllAreas) && Machine.gc.onGround)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        Rigidbody.isKinematic = Machine.gc.onGround;
        Agent.enabled = true;
        Machine.overrideFalling = false;

        MovementState = TowardsPlayer;
        UpdateState();
    }

    public void CancelSlide()
    {
        if (_slideRoutine == null)
        {
            return;
        }

        StopCoroutine(_slideRoutine);
        Rigidbody.isKinematic = Machine.gc.onGround;
        Agent.enabled = true;
        Machine.overrideFalling = false;

        MovementState = TowardsPlayer;
        SetMovement();
        UpdateState();
    }

    private void SetMovement()
    {
        if (!Machine.gc.onGround || !Agent.isOnNavMesh || MovementState == Sliding)
        {
            return;
        }

        Vector3 playerPosOnEnemyHeight = Machine.eid.target.position;
        playerPosOnEnemyHeight.y = transform.position.y;
        bool canReachPlayer = CanTravelPath(playerPosOnEnemyHeight);

        if (!canReachPlayer)
        {
            MovementState = Still;
            UpdateState();
        }

        Agent.updateRotation = true;

        if (Agent.pathStatus == NavMeshPathStatus.PathComplete && (!_isShooting || MovementState is not Still))
        {
            if (Vector3.Distance(Machine.eid.target.position, transform.position) > FearRadius)
            {
                if ((MovementState is not (TowardsPlayer or Retreat) || _stateLength > 1f) && canReachPlayer)
                {
                    //MovementState = UnityEngine.Random.value > 0.15f ? TowardsPlayer : Retreat;
                    MovementState = TowardsPlayer;
                    UpdateState();
                }
            }
            else
            {
                if (MovementState != Spinning && canReachPlayer)
                {
                    MovementState = Spinning;
                    UpdateState();
                }
            }
        }

        if (MovementState == Spinning)
        {
            _spinDirectionTimer += Time.deltaTime;
            if (_spinDirectionTimer > 2)
            {
                _spinDirection *= -1;
                _spinDirectionTimer = 0;
            }
        }
        else
        {
            _spinDirectionTimer = 0;
        }

        switch (MovementState)
        {
            case TowardsPlayer:
                Agent.stoppingDistance = FearRadius - 0.1f;
                Agent.SetDestination(LastPlayerFloorPos);
                break;

            case Retreat:
                Vector3 forwardDirection = (playerPosOnEnemyHeight - transform.position).normalized;
                Agent.stoppingDistance = 0;
                Agent.SetDestination(transform.position - forwardDirection);
                transform.forward = -forwardDirection;
                break;

            case Still:
                Agent.updateRotation = false;
                Agent.SetDestination(transform.position);
                break;

            case Spinning:
                Agent.updateRotation = false;
                Vector3 playerVector = Machine.eid.target.position - transform.position;
                playerVector.y = 0;

                transform.forward = playerVector;

                Vector3 dir = transform.position + transform.right * _spinDirection;
                if (Vector3.Distance(Machine.eid.target.position, transform.position) <= FearRadius / 2)
                {
                    dir += -transform.forward * 0.5f;
                }

                //transform.forward = transform.right * _spinDirection;

                Agent.stoppingDistance = 0;
                Agent.SetDestination(dir);
                break;
        }
    }

    private void UpdateState()
    {
        switch (MovementState)
        {
            case TowardsPlayer or Retreat:
                Machine.anim.SetBool(s_walking, true);
                Machine.anim.SetBool(s_sliding, false);
                break;

            case Still:
                Machine.anim.SetBool(s_walking, false);
                Machine.anim.SetBool(s_sliding, false);
                break;

            case Spinning:
                Machine.anim.SetBool(s_walking, true);
                Machine.anim.SetBool(s_sliding, false);
                break;

            case Sliding:
                Machine.anim.SetBool(s_walking, false);
                Machine.anim.SetBool(s_sliding, true);
                break;
        }

        _stateLength = 0;
    }

    private bool CanTravelPath(Vector3 position, bool allowPartial = true)
    {
        NavMeshPath path = new();
        Agent.CalculatePath(position, path);

        return allowPartial ? path.status is NavMeshPathStatus.PathComplete or NavMeshPathStatus.PathPartial : path.status is NavMeshPathStatus.PathComplete;
    }

    public void Shoot(int shots) => _shootRoutine = !ULTRAKILL.Cheats.BlindEnemies.Blind ? StartCoroutine(ShootCoroutine(shots)) : null;

    public void CancelShoot()
    {
        if (_shootRoutine == null || !_isShooting)
        {
            return;
        }

        StopCoroutine(_shootRoutine);

        if (_lastShootPlayerLook != null)
        {
            StopCoroutine(_lastShootPlayerLook);
        }

        Machine.anim.SetBool(s_aiming, false);
        Rifle.LookAtAlternateTarget = false;
        Rifle.UseTrackedObjectRotation = true;
        _isShooting = false;
    }

    private IEnumerator ShootCoroutine(int shots)
    {
        _timeUntilNextShot = CultistFireController.Instance.IntervalPerCultist;

        if (MovementState == Sliding)
        {
            Debug.LogWarning("Shoot called while sliding, cancelling slide!");
            CancelSlide();
        }

        if (MovementState == Retreat)
        {
            MovementState = TowardsPlayer;
            UpdateState();
        }

        _isShooting = true;
        //MovementState = CultistMovementState.Still;
        UpdateState();

        yield return new WaitForSeconds(FrameTime * 25);

        while (shots > 0)
        {
            Machine.anim.SetBool(s_aiming, true);

            _lastShootPlayerLook = StartCoroutine(FacePlayer());

            yield return new WaitForSeconds(FrameTime * 7);
            Rifle.AlternateTarget = Machine.eid.target.targetTransform;
            Rifle.UseTrackedObjectRotation = false;
            Rifle.LookAtAlternateTarget = true;
            yield return new WaitForSeconds(FrameTime * 6);

            Vector3 oldCameraPos = Machine.eid.target.PredictTargetPosition(FrameTime * 12, true);
            EnsureVignetteAndFlash();
            Instantiate(BlueFlash, BeamSpawnPoint.transform.position + transform.forward, transform.rotation).transform.localScale *= 5f;
            StopCoroutine(_lastShootPlayerLook);
            Rifle.LookAtAlternateTarget = false;
            Rifle.UseTrackedObjectRotation = true;
            yield return new WaitForSeconds(FrameTime * 12); //1s

            Machine.anim.SetTrigger(s_shoot);
            yield return null;
            ShootBeam(oldCameraPos - BeamSpawnPoint.transform.position);

            shots--;
        }

        Machine.anim.SetBool(s_aiming, false);

        yield return new WaitForSeconds(FrameTime * 25);
        //MovementState = CultistMovementState.TowardsPlayer;
        _isShooting = false;
    }

    private void EnsureVignetteAndFlash()
    {
        if (CultistVignetteEffect.Instance == null)
        {
            CultistVignetteEffect.Instance = Instantiate(VignetteEffect.gameObject, CanvasController.Instance.transform).GetComponent<CultistVignetteEffect>();
        }

        CultistVignetteEffect.Instance.Flash();
    }

    private RevolverBeam ShootBeam(Vector3 direction)
    {
        GameObject beam = Instantiate(Beam, BeamSpawnPoint.transform.position, BeamSpawnPoint.transform.rotation);
        RevolverBeam rb = beam.GetComponent<RevolverBeam>();

        beam.transform.forward = direction;
        rb.damage *= Machine.eid.totalDamageModifier;
        return rb;
    }

    private IEnumerator FacePlayer()
    {
        while (true)
        {
            Vector3 playerVector = Machine.eid.target.targetTransform.position - transform.position;
            playerVector.y = 0;
            transform.forward = playerVector;

            yield return null;
        }
    }

    public bool CanFire => MovementState is not Retreat && !_isJumping && Machine.gc.onGround && Machine.eid.target != null;
}
