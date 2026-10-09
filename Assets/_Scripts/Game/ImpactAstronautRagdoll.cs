using UnityEngine;

/// <summary>
/// Manages the impact/knockdown astronaut's physics behavior.
/// Attach to the Impact_Astronaut_Knockdown root GameObject.
///
/// Before impact: Animator enabled, idle pose, rigidbody kinematic.
/// After impact:  Animator disabled, rigidbody non-kinematic,
///                impulse + torque create a natural tumbling death.
///
/// Uses the existing single Rigidbody + CapsuleCollider — no runtime
/// joint/rigidbody creation (which crashes Unity on scaled skinned meshes).
/// The improved center-of-mass, collider geometry, and force tuning produce
/// a convincing ragdoll-like tumble without engine instability.
/// </summary>
public class ImpactAstronautRagdoll : MonoBehaviour
{
    [Header("Impact Forces")]
    [Tooltip("Gentle push away from the impact point.")]
    public float impulseForce = 1.5f;

    [Tooltip("Small upward nudge (just enough to start tipping).")]
    public float upwardImpulse = 0.3f;

    [Tooltip("Torque for tipping over (natural stumble).")]
    public float torqueForce = 3f;

    [Tooltip("Random torque variation for variety.")]
    public float randomTorqueRange = 1.5f;

    [Header("Body Physics")]
    [Tooltip("Rigidbody mass (lower = more dramatic movement).")]
    public float bodyMass = 15f;

    [Tooltip("Linear drag.")]
    public float linearDrag = 0.3f;

    [Tooltip("Angular drag (lower = more spinning).")]
    public float angularDrag = 1.2f;

    [Tooltip("Center of mass Y offset (higher = tips over more naturally).")]
    public float centerOfMassY = 0.8f;

    [Header("Collider")]
    [Tooltip("Capsule height.")]
    public float capsuleHeight = 1.8f;

    [Tooltip("Capsule radius.")]
    public float capsuleRadius = 0.35f;

    [Tooltip("Capsule center Y offset.")]
    public float capsuleCenterY = 0.9f;

    [Header("Model Scale")]
    [Tooltip("Scale to match the player astronaut (same FBX at 0.15).")]
    public float modelScale = 0.15f;

    // ─── Runtime state ───────────────────────────────────────────────

    private Animator cachedAnimator;
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private bool isDead;

    // ─── Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        cachedAnimator = GetComponentInChildren<Animator>(true);
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
    }

    private void OnEnable()
    {
        if (!isDead)
        {
            SetAliveState();
        }
    }

    /// <summary>
    /// Sets the astronaut to alive/standing state.
    /// Animator enabled, rigidbody kinematic.
    /// </summary>
    public void SetAliveState()
    {
        isDead = false;

        // Enforce correct scale to match the player
        transform.localScale = Vector3.one * modelScale;

        // Enable animator for idle pose
        if (cachedAnimator != null)
        {
            cachedAnimator.enabled = true;

            if (cachedAnimator.isActiveAndEnabled && cachedAnimator.runtimeAnimatorController != null)
            {
                cachedAnimator.SetFloat("Speed", 0f);
                cachedAnimator.SetBool("Grounded", true);
            }
        }

        // Configure all rigidbodies for pre-impact (kinematic)
        foreach (Rigidbody r in GetComponentsInChildren<Rigidbody>(true))
        {
            r.isKinematic = true;
            r.linearVelocity = Vector3.zero;
            r.angularVelocity = Vector3.zero;
        }

        // Configure collider
        if (capsule != null)
        {
            capsule.enabled = true;
            capsule.height = capsuleHeight;
            capsule.radius = capsuleRadius;
            capsule.center = new Vector3(0f, capsuleCenterY, 0f);
            capsule.direction = 1; // Y-axis
        }
    }

    /// <summary>
    /// Triggers ragdoll death from an impact point.
    /// </summary>
    public void TriggerImpactDeath(Vector3 impactPoint)
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        // Disable animator — freeze the current pose, physics takes over
        if (cachedAnimator != null)
        {
            cachedAnimator.enabled = false;
        }

        // Call the loose profile configuration
        ApplyLooseRagdollJointProfile();

        // ── Compute forces ──
        Vector3 awayDir = (transform.position - impactPoint);
        awayDir.y = 0f;
        if (awayDir.sqrMagnitude < 0.001f)
        {
            awayDir = transform.forward;
        }
        awayDir = awayDir.normalized;

        Vector3 force = awayDir * impulseForce + Vector3.up * upwardImpulse;

        Vector3 tumbleAxis = Vector3.Cross(Vector3.up, awayDir).normalized;
        if (tumbleAxis.sqrMagnitude < 0.01f)
        {
            tumbleAxis = Vector3.right;
        }

        Vector3 randomComponent = new Vector3(
            Random.Range(-randomTorqueRange, randomTorqueRange),
            Random.Range(-randomTorqueRange * 0.5f, randomTorqueRange * 0.5f),
            Random.Range(-randomTorqueRange, randomTorqueRange)
        );

        Vector3 totalTorque = tumbleAxis * torqueForce + randomComponent;

        // Apply force and torque to all rigidbodies
        foreach (Rigidbody r in GetComponentsInChildren<Rigidbody>(true))
        {
            r.AddForce(force, ForceMode.Impulse);
            r.AddTorque(totalTorque, ForceMode.Impulse);
        }
    }

    private void ApplyLooseRagdollJointProfile()
    {
        // For CharacterJoint components
        foreach (CharacterJoint joint in GetComponentsInChildren<CharacterJoint>(true))
        {
            SoftJointLimitSpring twistSpring = joint.twistLimitSpring;
            twistSpring.spring = 0f;
            twistSpring.damper = 0f;
            joint.twistLimitSpring = twistSpring;

            SoftJointLimitSpring swingSpring = joint.swingLimitSpring;
            swingSpring.spring = 0f;
            swingSpring.damper = 0f;
            joint.swingLimitSpring = swingSpring;

            SoftJointLimit lowTwist = joint.lowTwistLimit;
            lowTwist.limit = Mathf.Clamp(lowTwist.limit - 20f, -177f, 177f);
            joint.lowTwistLimit = lowTwist;

            SoftJointLimit highTwist = joint.highTwistLimit;
            highTwist.limit = Mathf.Clamp(highTwist.limit + 20f, -177f, 177f);
            joint.highTwistLimit = highTwist;

            SoftJointLimit swing1 = joint.swing1Limit;
            swing1.limit = Mathf.Clamp(swing1.limit + 20f, 0f, 177f);
            joint.swing1Limit = swing1;

            SoftJointLimit swing2 = joint.swing2Limit;
            swing2.limit = Mathf.Clamp(swing2.limit + 20f, 0f, 177f);
            joint.swing2Limit = swing2;

            joint.enableProjection = false;
        }

        // For ConfigurableJoint components
        foreach (ConfigurableJoint joint in GetComponentsInChildren<ConfigurableJoint>(true))
        {
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            JointDrive xDrive = joint.angularXDrive;
            xDrive.positionSpring = 0f;
            xDrive.positionDamper = 0f;
            xDrive.maximumForce = 0f;
            joint.angularXDrive = xDrive;

            JointDrive yzDrive = joint.angularYZDrive;
            yzDrive.positionSpring = 0f;
            yzDrive.positionDamper = 0f;
            yzDrive.maximumForce = 0f;
            joint.angularYZDrive = yzDrive;

            JointDrive slerpDrive = joint.slerpDrive;
            slerpDrive.positionSpring = 0f;
            slerpDrive.positionDamper = 0f;
            slerpDrive.maximumForce = 0f;
            joint.slerpDrive = slerpDrive;

            SoftJointLimit lowX = joint.lowAngularXLimit;
            lowX.limit = Mathf.Clamp(lowX.limit - 20f, -177f, 177f);
            joint.lowAngularXLimit = lowX;

            SoftJointLimit highX = joint.highAngularXLimit;
            highX.limit = Mathf.Clamp(highX.limit + 20f, -177f, 177f);
            joint.highAngularXLimit = highX;

            SoftJointLimit yLimit = joint.angularYLimit;
            yLimit.limit = Mathf.Clamp(yLimit.limit + 20f, 0f, 177f);
            joint.angularYLimit = yLimit;

            SoftJointLimit zLimit = joint.angularZLimit;
            zLimit.limit = Mathf.Clamp(zLimit.limit + 20f, 0f, 177f);
            joint.angularZLimit = zLimit;

            joint.projectionMode = JointProjectionMode.None;
        }

        // For all ragdoll Rigidbodies
        foreach (Rigidbody r in GetComponentsInChildren<Rigidbody>(true))
        {
            r.isKinematic = false;
            r.useGravity = true;
            r.constraints = RigidbodyConstraints.None;
            r.interpolation = RigidbodyInterpolation.Interpolate;
            r.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            r.WakeUp();
            r.linearVelocity = Vector3.zero;
            r.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Resets to pre-impact state.
    /// </summary>
    public void ResetRagdoll()
    {
        SetAliveState();
    }
}
