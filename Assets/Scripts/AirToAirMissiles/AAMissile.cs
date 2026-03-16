using UnityEngine;
using System.Collections.Generic;
using static GlobalHelper;

public enum GuidanceType
{
    Pursuit,
    Lead
}

// public enum UpdateType
// {
//     FixedUpdate,
//     Update
// }

// [RequireComponent(typeof(Rigidbody))]
// [RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(AAMissileEffects))]
public class AAMissile : MonoBehaviour
{
    new Transform transform;
    // new Rigidbody rigidbody;
    // new CapsuleCollider collider;

    [Header("General Parameters:")]
    // [Tooltip("Run movement code in fixed update versus update.\n\nIf you notice jittery movement, try changing this. Fixed Update is typically used for rigidbody based projects.")]
    // public UpdateType movementUpdateCycle = UpdateType.Update;

    // [Tooltip("Run guidance code in fixed update versus update.\n\nIf your target has a rigidbody and moved through physics, set this to Fixed.")]
    // public UpdateType targetUpdateCycle = UpdateType.Update;

    [Tooltip("Transform of the target. Typically assigned by launcher that shot the missile, but can be manually assigned for a missile already in the scene. If null on launch, the missile will have no guidance.")]
    public Transform target;

    [Tooltip("Launching object. Typically assigned by the launcher and only needs to be assigned if manually launching a missile already in the scene. When assigned, this will prevent the missile from colliding with whatever launched it.")]
    public Transform ownShip;

    [Tooltip("Position where this missile attaches to hardpoint style launchers. If not assigned, this will automatically search for a GameObject named \"Attach\". If no such GameObject, then the missile will attach at its origin.")]
    public Transform attachPoint;

    [Header("Missile parameters:")]

    [Tooltip("Pursuit flies directly towards the target. Lead will fly ahead to intercept, making it significantly more difficult to dodge.")]
    public GuidanceType guidanceType = GuidanceType.Pursuit;

    [Tooltip("How far off boresight the missile can see the target. Also restricts how far the missile can lead.")]
    public float seekerCone = 45.0f;

    [Tooltip("How far off boresight the missile can see the target. Also restricts how far the missile can lead.")]
    public float seekerRange = 5000.0f;

    [Tooltip("When true, initial speed will be taken from either the velocity passed into the Launch function, or from the forward velocity of the missile after a drop launch if a drop delay is used. This is useful for missiles that you want to inherit their start speed from their launchers.")]
    public bool overrideInitialSpeed = false;

    [Tooltip("Velocity that the missile has immediately on ignition.")]
    public float initialSpeed = 0.0f;

    [Tooltip("How long the missile will accelerate. After this, the missile maintains a constant speed.")]
    public float motorLifetime = 3.0f;

    [Tooltip("How much speed per second the missile will gain after launch.")]
    public float acceleration = 15.0f;

    [Tooltip("How many degrees per second the missile can turn.")]
    public float turnRate = 45.0f;

    [Tooltip("After this time, the missile will self-destruct. Timer starts on launch, not motor activation.")]
    public float timeToLive = 15.0f;

    [Header("Drop options:")]

    [Tooltip("If greater than 0, missile will free fall for this many seconds and then activate after this many seconds have elapsed.")]
    public float dropDelay = 0.0f;

    [Tooltip("Velocity (in local space) at which the missile will be ejected from its launch point.")]
    public Vector3 ejectVelocity = Vector3.zero;

    [Tooltip("Whether or not the missile should have gravity when dropping.")]
    public bool gravity = true;

    [Header("Active Range (in meters):")]
    public float ActiveRange = 5000f;
    [Header("Warhead parameters:")]
    public int Damage = 100;
    public int ExplodeRadius = 5;
    public int DetonationRadius = 3;

    [Header("Apperance")]
    public Vector3 MissileScale = Vector3.one * 0.5f;

    [Header("Faction")]
    public GlobalHelper.Faction SourceFaction = GlobalHelper.Faction.Player;

    AAMissileEffects missileEffect;
    private Vector3 launchVelocity = Vector3.zero;

    private float launchTime = 0.0f;
    private float activateTime = 0.0f;
    private float missileSpeed = 0.0f;

    private bool isLaunched = false;
    private bool missileActive = false;
    private bool motorActive = false;
    private bool targetTracking = true;

    private Vector3 targetPosLastFrame;
    private Quaternion guidedRotation;
    private readonly List<VehicleBase> _retargetCandidates = new List<VehicleBase>(16);

    // Applied by external forces (e.g. black hole gravity). Accumulates as a velocity (m/s).
    private Vector3 _externalVelocity = Vector3.zero;

    // Used to prevent lead markers from getting huge when missiles are very slow.
    private const float MINIMUM_GUIDE_SPEED = 1.0f;
    // private EnemyPredictionManager _predictionManager;

    public bool MissileLaunched { get { return isLaunched; } }
    public bool MotorActive { get { return motorActive; } }

    /// <summary>Adds an external velocity impulse (e.g. from black hole gravity). Accumulates each frame.</summary>
    public void AddExternalVelocity(Vector3 deltaV) { _externalVelocity += deltaV; }

    private void Awake()
    {
        transform = GetComponent<Transform>();
        // rigidbody = GetComponent<Rigidbody>();
        // collider = GetComponent<CapsuleCollider>();
        missileEffect = GetComponent<AAMissileEffects>();
    }

    private void Start()
    {
        // Sets it so that missile cannot collide with the thing that launched it.
        // if (ownShip != null)
        // {
        //     foreach (Collider col in ownShip.GetComponentsInChildren<Collider>())
        //         Physics.IgnoreCollision(collider, col);
        // }

        // Find attach point if necessary.
        if (attachPoint == null)
        {
            Transform[] potentialAttach = GetComponentsInChildren<Transform>();
            foreach (Transform xform in potentialAttach)
                if (xform.name == "Attach")
                    attachPoint = xform;

            if (attachPoint == null)
                Debug.Log("No attach point found for missile " + transform.name + ". Using missile center instead.");
        }

        // If this hasn't already been launched, make sure it's kinematic so that it can be mounted on
        // stuff. When a missile is spawned and then launched immediately, Launch happens before start.
        // if (!isLaunched)
        //     rigidbody.isKinematic = true;
    }

    private void Update()
    {
        if (missileActive && target != null)// && targetUpdateCycle == UpdateType.Update)
            MissileGuidance();

        // if (movementUpdateCycle == UpdateType.Update)
        RunMissile();

        if (target != null)
        {
            Vector3 dir = target.position - transform.position;

            // RaycastHit hit;
            // Physics.Raycast(transform.position, Vector3.Normalize(dir), out hit);
            // if (hit.collider != null)
            // {
            //     if (hit.collider.GetComponent<ShieldHitEffect>())
            //         hit.collider.GetComponent<ShieldHitEffect>().GetHit(hit);
            // }

            if (dir.magnitude <= DetonationRadius)
            {
                HitTarget();
            }
        }
        else if (isLaunched && missileActive)
        {
            if (!TryAcquireReplacementTarget())
            {
                target = null;
                DestroyMissile(false);
            }
        }
    }

    private bool TryAcquireReplacementTarget()
    {
        GlobalHelper.Faction targetFactions;
        if ((SourceFaction & (GlobalHelper.Faction.Player | GlobalHelper.Faction.Ally)) != 0)
        {
            targetFactions = GlobalHelper.Faction.Foe;
        }
        else
        {
            targetFactions = GlobalHelper.Faction.Player | GlobalHelper.Faction.Ally;
        }

        _retargetCandidates.Clear();
        CombatRegistry.GetNearbyEnemies(transform.position, ActiveRange, targetFactions, _retargetCandidates);

        VehicleBase bestTarget = null;
        int bestReservationCount = int.MaxValue;
        float bestDistanceSqr = Mathf.Infinity;
        TargetDistributor distributor = TargetDistributor.Instance;
        Vector3 missilePosition = transform.position;
        bool allowOverflow = distributor == null || distributor.AllowOrdnanceOverflow;

        for (int i = 0; i < _retargetCandidates.Count; i++)
        {
            VehicleBase candidate = _retargetCandidates[i];
            if (candidate == null)
                continue;

            Transform candidateTransform = candidate.transform;
            float distanceSqr = (candidateTransform.position - missilePosition).sqrMagnitude;
            int reservationCount = distributor != null ? distributor.GetReservedOrdnanceCount(candidateTransform) : 0;

            if (!allowOverflow && distributor != null && !distributor.CanReserveOrdnance(candidateTransform))
                continue;

            if (reservationCount > bestReservationCount)
                continue;

            if (reservationCount == bestReservationCount && distanceSqr >= bestDistanceSqr)
                continue;

            bestReservationCount = reservationCount;
            bestDistanceSqr = distanceSqr;
            bestTarget = candidate;
        }

        if (bestTarget == null)
            return false;

        target = bestTarget.transform;
        if (distributor != null)
        {
            float remainingLifetime = Mathf.Max(0.5f, timeToLive - TimeSince(launchTime));
            distributor.RegisterOrdnanceReservation(target, remainingLifetime);
        }

        if (target != null)
            targetPosLastFrame = target.position;

        targetTracking = true;
        return true;
    }

    void OnDrawGizmos()
    {
        // Draw a line heading to the target.
        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }

    // private void FixedUpdate()
    // {
    //     if (missileActive && target != null && targetUpdateCycle == UpdateType.FixedUpdate)
    //         MissileGuidance();

    //     if (movementUpdateCycle == UpdateType.FixedUpdate)
    //         RunMissile();
    // }

    // private void OnCollisionEnter(Collision collision)
    // {
    //     // Prevent missile from exploding if it hasn't activated yet.
    //     if (isLaunched && TimeSince(launchTime) > dropDelay && collision.gameObject != ownShip && collision.gameObject.tag == "Foe")
    //     {
    //         HitTarget();
    //         // This is a good place to apply damage based on what was collided with.
    //         DestroyMissile(true);
    //     }
    // }

    void HitTarget()
    {
        if (ExplodeRadius > 0)
        {
            // Get hostile factions based on who fired the missile
            // Use bitwise check since Faction is a flags enum
            GlobalHelper.Faction targetFactions;
            if ((SourceFaction & (GlobalHelper.Faction.Player | GlobalHelper.Faction.Ally)) != 0)
            {
                targetFactions = GlobalHelper.Faction.Foe;
            }
            else
            {
                targetFactions = GlobalHelper.Faction.Player | GlobalHelper.Faction.Ally;
            }

            List<VehicleBase> nearbyTargets = new List<VehicleBase>(16);
            CombatRegistry.GetNearbyEnemies(transform.position, ExplodeRadius, targetFactions, nearbyTargets, true);

            // Group VehicleModules and WeaponPlatforms by their parent vehicle to prevent multiple damage from same parent
            HashSet<VehicleBase> damagedParents = new HashSet<VehicleBase>();

            foreach (VehicleBase vehicle in nearbyTargets)
            {
                if (vehicle is VehicleModule vehicleModule)
                {
                    // For VehicleModule, get its parent vehicle
                    VehicleBase parentVehicle = vehicleModule.OwnerShip?.GetComponent<VehicleBase>();
                    if (parentVehicle != null && !damagedParents.Contains(parentVehicle))
                    {
                        parentVehicle.TakeDamage(Damage, GlobalHelper.AmmoType.Explosive);
                        damagedParents.Add(parentVehicle);
                    }
                }
                else if (vehicle is WeaponPlatform weaponPlatform)
                {
                    // For WeaponPlatform, get its parent vehicle
                    VehicleBase parentVehicle = weaponPlatform.OwnerShip != null ? weaponPlatform.OwnerShip.GetComponent<VehicleBase>() : null;
                    if (parentVehicle != null && !damagedParents.Contains(parentVehicle))
                    {
                        parentVehicle.TakeDamage(Damage, GlobalHelper.AmmoType.Explosive);
                        damagedParents.Add(parentVehicle);
                    }
                }
                else
                {
                    // For other vehicles, damage directly if not already damaged
                    if (!damagedParents.Contains(vehicle))
                    {
                        vehicle.TakeDamage(Damage, GlobalHelper.AmmoType.Explosive);
                        damagedParents.Add(vehicle);
                    }
                }
            }
        }
        else if (target != null)
        {
            VehicleBase targetVehicle = target.GetComponent<VehicleBase>();
            if (targetVehicle != null)
            {
                targetVehicle.TakeDamage(Damage, GlobalHelper.AmmoType.Explosive);
            }
        }

        DestroyMissile(true);
    }

    /// <summary>
    /// Launch the missile at the given target. If the missile has a drop delay, use the Launch function
    /// with inherited velocity for the correct drop behavior.
    /// </summary>
    /// <param name="newTarget">If no target is given, the missile will fire without guidance.</param>
    public void Launch(Transform newTarget)
    {
        Launch(newTarget, Vector3.zero);
    }

    /// <summary>
    /// Launch the missile at the given target with an inherited velocity for correct drop behavior.
    /// It's recommended to use this function in general as it will work for missiles with and without
    /// drop delays.
    /// </summary>
    /// /// <param name="newTarget">If no target is given, the missile will fire without guidance.</param>
    /// <param name="inheritedVelocity">Typically this is the velocity of the launching plane.</param>
    public void Launch(Transform newTarget, Vector3 inheritedVelocity)
    {
        if (!isLaunched)
        {
            isLaunched = true;
            launchTime = Time.time;
            transform.parent = null;
            transform.localScale = MissileScale;
            target = newTarget;
            launchVelocity = inheritedVelocity;
            // rigidbody.isKinematic = false;

            if (dropDelay > 0.0f)
            {
                // rigidbody.useGravity = gravity;
                // rigidbody.linearVelocity = inheritedVelocity + transform.TransformDirection(ejectVelocity);
                // Move the missile according to its initial ejection velocity without rigidbody physics.
                transform.Translate((inheritedVelocity + transform.TransformDirection(ejectVelocity)) * Time.deltaTime);
            }
            else
                ActivateMissile();
        }
    }


    // // In existing OnEnable or Start
    // private void OnEnable()
    // {
    //     CombatRegistry.RegisterMissile(this, SourceFaction);

    //     // Register hostile missiles with prediction manager for player
    //     if (SourceFaction == Faction.Foe)
    //     {
    //         _predictionManager = FindAnyObjectByType<EnemyPredictionManager>();
    //         if (_predictionManager != null)
    //             _predictionManager.RegisterMissile(this);
    //     }
    // }

    // private void OnDisable()
    // {
    //     CombatRegistry.UnregisterMissile(this, SourceFaction);

    //     if (_predictionManager != null)
    //         _predictionManager.UnregisterMissile(this);
    // }

    // private void OnDestroy()
    // {
    //     if (_predictionManager != null)
    //         _predictionManager.UnregisterMissile(this);
    // }

    /// <summary>
    /// Launch with faction info
    /// </summary>
    public void Launch(Transform newTarget, Vector3 inheritedVelocity, GlobalHelper.Faction faction)
    {
        SourceFaction = faction;

        // Re-register with correct faction if it changed
        CombatRegistry.UnregisterMissile(this, GlobalHelper.Faction.Player);
        CombatRegistry.UnregisterMissile(this, GlobalHelper.Faction.Ally);
        CombatRegistry.UnregisterMissile(this, GlobalHelper.Faction.Foe);
        CombatRegistry.RegisterMissile(this, SourceFaction);

        Launch(newTarget, inheritedVelocity);
    }

    private void RunMissile()
    {
        if (isLaunched)
        {
            // Don't start moving under own power until drop delay has passed (if applicable).
            if (!missileActive && dropDelay > 0.0f && TimeSince(launchTime) > dropDelay)
                ActivateMissile();

            // During drop delay, continue moving with inherited velocity
            if (!missileActive && dropDelay > 0.0f)
            {
                // Apply gravity if enabled
                if (gravity)
                    launchVelocity += Physics.gravity * Time.deltaTime;

                // Move with inherited velocity + eject velocity
                transform.Translate((launchVelocity + transform.TransformDirection(ejectVelocity)) * Time.deltaTime, Space.World);
            }

            // Missile active, move it and guide it in.
            if (missileActive)
            {
                // Motor is only active for the duration of its lifetime (if applicable).
                if (motorLifetime > 0.0f && TimeSince(activateTime) > motorLifetime)
                    motorActive = false;
                else
                    motorActive = true;

                // Accelerate missile while motor is active.
                if (motorActive)
                    missileSpeed += acceleration * Time.deltaTime;

                // Rotate missile to target vector.
                if (targetTracking)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, guidedRotation, turnRate * Time.deltaTime);

                // Move missile forwards.
                // If this is designed to use the fixed update, take advantage of the rigidbody and
                // update its velocity instead. This allows for rigidbody.velocity to be used accurately.
                // E.g., distance emitters for particle systems to work correctly.
                // if (movementUpdateCycle == UpdateType.Update)
                transform.Translate((transform.forward * missileSpeed + _externalVelocity) * Time.deltaTime, Space.World);
                // else if (movementUpdateCycle == UpdateType.FixedUpdate)
                //     rigidbody.linearVelocity = transform.forward * missileSpeed;
            }

            if (TimeSince(launchTime) > timeToLive)
                DestroyMissile(false);
        }
    }

    private void MissileGuidance()
    {
        // Get a vector to the target, use it to find angle to target for seeker cone check.
        Vector3 relPos = target.position - transform.position;
        float angleToTarget = Mathf.Abs(Vector3.Angle(transform.forward.normalized, relPos.normalized));
        float dist = Vector3.Distance(target.position, transform.position);

        // When the target gets out of line of sight of the seeker's FOV or out of range, it can no longer track.
        if (angleToTarget > seekerCone || dist > seekerRange)
            targetTracking = false;

        // Only turn the missile if the target is still within the seeker's limits.
        if (targetTracking)
        {
            // Pursuit guidance
            if (guidanceType == GuidanceType.Pursuit)
            {
                relPos = target.position - transform.position;
                guidedRotation = Quaternion.LookRotation(relPos, transform.up);
            }

            // Lead guidance
            else
            {
                // Get where target will be in one second.
                Vector3 targetVelocity = target.position - targetPosLastFrame;
                targetVelocity /= Time.deltaTime;

                //=====================================================

                // Figure out time to impact based on distance.                
                //float dist = Mathf.Max(Vector3.Distance(target.position, transform.position), missileSpeed);
                float predictedSpeed = Mathf.Min(initialSpeed + acceleration * motorLifetime, missileSpeed + acceleration * TimeSince(activateTime));
                float timeToImpact = dist / Mathf.Max(predictedSpeed, MINIMUM_GUIDE_SPEED);

                // Create lead position based on target velocity and time to impact.                
                Vector3 leadPos = target.position + targetVelocity * timeToImpact;
                Vector3 leadVec = leadPos - transform.position;

                //print(leadVec.magnitude.ToString());

                //=====================================================

                // It's very easy for the lead position to be outside of the seeker head. To prevent
                // this, only allow the target direction to be 90% of the seeker head's limit.
                relPos = Vector3.RotateTowards(relPos.normalized, leadVec.normalized, seekerCone * Mathf.Deg2Rad * 0.9f, 0.0f);
                guidedRotation = Quaternion.LookRotation(relPos, transform.up);

                //Debug.DrawRay(target.position, targetVelocity * timeToImpact, Color.red);
                //Debug.DrawRay(target.position, targetVelocity * timeToHit, Color.red);
                //Debug.DrawRay(transform.position, leadVec, Color.red);

                targetPosLastFrame = target.position;
            }
        }
    }

    private void ActivateMissile()
    {
        if (overrideInitialSpeed)
        {
            if (dropDelay > 0.0f)
            {
                // When dropping, use the forward speed component of inherited velocity
                // Project the launch velocity onto the missile's forward direction
                float localForwardSpeed = Vector3.Dot(launchVelocity, transform.forward);
                initialSpeed = Mathf.Max(0f, localForwardSpeed); // Don't allow negative initial speed
            }
            else
            {
                // When launching off the rail, use forward speed of the launcher's given speed.
                float localForwardSpeed = transform.InverseTransformDirection(launchVelocity).z;
                initialSpeed = localForwardSpeed;
            }
        }

        // rigidbody.useGravity = false;
        // rigidbody.linearVelocity = Vector3.zero;
        missileActive = true;

        // If no motor lifetime is present, then the motor will just always be active.
        if (motorLifetime <= 0.0f)
            motorActive = true;

        activateTime = Time.time;
        missileSpeed = initialSpeed;
        _externalVelocity = Vector3.zero;

        if (target != null)
            targetPosLastFrame = target.transform.position;

        BlackHoleGravity.RegisterMissile(this);
    }

    public void DestroyMissile(bool impact)
    {
        BlackHoleGravity.UnregisterMissile(this);
        Destroy(gameObject);

        if (missileEffect.playExplosionOnSelfDestruct)
            missileEffect.Explode();

        else if (impact)
            missileEffect.Explode();
    }

    private float TimeSince(float since)
    {
        return Time.time - since;
    }
}
