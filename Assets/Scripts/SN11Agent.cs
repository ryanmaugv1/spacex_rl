using System;
using TMPro;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Random = UnityEngine.Random;

/**
 *  SpaceX's SN-11 Starship Rocket Agent
 *
 *  Agent modelled after SpaceX's SN series (SN-11 to be specific) used as
 *  first stage of Starship rocket. This agent is trained to self-land on 
 *  a designated landing pad using Thrust Vector Control (TVC).
 *
 *  Authored By Ryan Maugin (@ryanmaugv1)
 */
public class SN11Agent : Agent
{
    [Header("Environment Properties")]
    /* Enable debug logs in console of agent data. */
    public bool DebugLogMode;
    /* Enable debug gizmos for agent. */
    public bool DebugVisualMode;
    /* Time in seconds before we timeout episode because agent took too long to complete episode. */
    public int EpisodeTimeout = 120;
    /* Defines out-of-range distance (both direction) for each rocket axis relative to landing pad. */
    public Vector3 OutOfRangeDistance = new Vector3(10000f, 10000f, 10000f);
    /* Landing pad transform used for relative positioning of rocket and reward calculation. */
    public Transform LandingPad;
    /* Iteration counter label we want to update with step count. */
    public TMP_Text InterationCounter;

    [Header("Agent Thruster Properties")]
    /* Agent thruster transform used for applying force at position for rocket. */
    public Transform ThrustVector;
    /* Agent thruster particle system (VFX). */
    public ParticleSystem ThrustVFX;

    public Transform ThrustVFXTransform;
    /* Maximum thrust force (Newtons) that can be outputted by from thruster. */
    public float MaxThrustForce = 12000f;
    /* Maximum thruster gimbal in any direction e.g. (30f, 0f, 30f) or (-30f, 0f, -30f). */
    public Vector3 MaxThrusterGimbal = new Vector3(30f, 0f, 30f);

    [Header("Agent Initalisation Properties")]
    /* Minimum positional value agent can be initialised at. */
    public Vector3 MinInitPosition = new Vector3(-100f, 1000f, -100f);
    /* Maximum positional value agent can be initialised at. */
    public Vector3 MaxInitPosition = new Vector3(100f, 2000f, 100f);

    [Header("Agent Reward Properties")]
    /* Max distance agent can land or crash at and get a reward (proportionate to distance). */
    public float MaxDistanceFromPad = 20f;
    /* Belly flop Pitch (z-axis) range agent can be within to get a reward. */
    public Vector2 BellyFlopPitchRange = new Vector2(85f, 95f);
    /* Upright Pitch (z-axis) and Yaw (x-axis) range (-range, 0, range) agent can be within to get a reward. */
    public float UprightOrientationRange = 5f;
    /* Maximum approach speed agent can have to get a reward. */
    public float MaxApproachSpeed = 4f;
    /* Maximum height which is considered as the "approach" height. */
    public float MaxApproachDistance = 25f;
    /* Minimum height which is considered as the "approach" height. */
    public float MinApproachDistance = 5f;
    /* Distance from the ground in metres before the agent should start being in upright orientation. */
    public float DistanceFromGroundBeforeUpright = 1000f;

    /* Rigidbody Component belonging to agent (used for applying actions). */
    private Rigidbody AgentRigidbody;
    /* Current amount of thrust produced by agent in Newtons. */
    private float AgentThrust;
    /* Whether agent can apply thrust force to agent. */
    private bool canApplyThrust = true;
    /* Holds minimal agent collision info needed. */
    private CollisionInfo AgentCollisionInfo = new CollisionInfo();
    /* Holds current episode time remaining till timeout in seconds. */
    private float EpisodeTimeRemaining;
    /* Mapping from agent states to respective reward. */
    private StateRewardMap StateRewardMap;
    

    
    // Start is called before the first frame update
    private void Start() {
        AgentRigidbody = GetComponent<Rigidbody>();
    }


    /**
     *  Fixed Update Loop (called before internal physics update)
     * 
     *  Reward agent:
     *      - when in belly flop position (100+ metres above ground).
     *          - +0.0001 for being in (85, 95) range on z-axis.
     *      - when in upright position (+100 metres above ground).
     *          - +0.0001 for being upright in (-5, 5) range on x and z axis.
     *      - when landing upright (+1).
     *      - when landing or crashing on landing pad.
     *          - +1.0 for landing on pad.
     *          - +0.2 for crashing on pad.
     *      - when landing or crashing within certain distance range of pad.
     *          - +1.0 for landing center of landing pad.
     *          - +0.0 for landing on edge of distance range from pad (20 metres).
     *      - when approaching ground with speed less than 4 m/s.
     *          - +0.001 when 25 metres above ground and speed is less than max defined threshold.
     *
     *  Punish Agent:
     *      - when colliding with anything other than landing pad (-0.1).
     *
     *  End Episode:
     *      - Agent lands upright (and doesn't move for 5 second)
     *      - Agent crashed.
     *      - Agent out-of-range.
     *      - Episode timed out.
     *
     *  Using the defined reward specification above we should be able to
     *  find an optimal policy for the agent to self-land SpaceX SN style.
     */
    private void FixedUpdate() {
        if (DebugLogMode) {
            Debug.Log("Upright? " + IsAgentUpright());
            Debug.Log("Stationary? " + IsAgentStationary());
            Debug.Log("Landed? " + HasAgentLanded());
            Debug.Log("Landed On Pad? " + HasAgentLandedOnPad());
            Debug.Log("Crashed? " + HasAgentCrashLanded());
            Debug.Log("Out Of Range? " + IsAgentOutOfRange());
        }

        // Episode timeout logic.
        EpisodeTimeRemaining -= Time.deltaTime;
        if (EpisodeTimeRemaining < 0) {
            Debug.Log("End Episode - Timeout!");
            EndEpisode();
            return;
        }

        float agentDistanceFromGround = GetAgentDistanceFromGround();

        // Reward agent for being in upright position when 100 metres or less from ground.
        if (IsAgentInUprightWithinRange() && agentDistanceFromGround < DistanceFromGroundBeforeUpright)
            AddReward(StateRewardMap.UPRIGHT_POSITION_REWARD);

        // Reward agent for being in belly flop position when 100 metres or less from ground.
        if (IsAgentInBellyFlopOrientation() && agentDistanceFromGround > DistanceFromGroundBeforeUpright) {
            AddReward(StateRewardMap.BELLY_FLOP_POSITION_REWARD);
        }

        // Reward agent for being below max approach speed when in approach distance range.
        if (GetAgentSpeed() < MaxApproachSpeed 
            && agentDistanceFromGround > MinApproachDistance
            && agentDistanceFromGround < MaxApproachDistance) {
            AddReward(StateRewardMap.APPROACHING_SPEED_REWARD);
        }
        
        // TODO: Reward agent for reducing angular velocity.

        if (HasAgentLandedOnPad()) {
            Debug.Log("End Episode - Landed On Pad!");
            AddReward(StateRewardMap.LANDED_UPRIGHT);
            AddReward(StateRewardMap.LANDED_ON_PAD_REWARD);
            AddReward(CalculateRewardFromDistanceToLandingPad());
            EndEpisode();
            return;
        }

        if (HasAgentLanded()) {
            Debug.Log("End Episode - Landed (but not on pad)!");
            AddReward(StateRewardMap.LANDED_UPRIGHT);
            AddReward(CalculateRewardFromDistanceToLandingPad());
            EndEpisode();
            return;
        }

        if (HasAgentCrashLandedOnPad()) {
            Debug.Log("End Episode - Crashed On Pad!");
            AddReward(StateRewardMap.CRASHED_ON_PAD_REWARD);
            AddReward(CalculateRewardFromDistanceToLandingPad());
            EndEpisode();
            return;
        }

        if (HasAgentCrashLanded()) {
            Debug.Log("End Episode - Crashed (not on pad)!");
            AddReward(CalculateRewardFromDistanceToLandingPad());
            EndEpisode();
            return;
        }
    }


    /**
     *  Initializing & Resetting Agent On Episode Begin
     *
     *  We will initialise/reset our agent in the following state:
     *      1) Set Y-axis transform position within 250-500CM range relative to landing pad y-axis.
     *      2) Set X & Z axis transform position within 50-100CM radius relative to landing pad.
     *          - Prevents positioning of agent directly above landing pad.
     *      3) Set X, Y & Z axis transform rotation randomly.
     *
     *  This should ensure reset/init the environment in random acceptable states
     *  so that our agent can learn a more general and robust policy.
     */
    public override void OnEpisodeBegin() {
        // Reset episode timeout timer and increment counter.
        EpisodeTimeRemaining = EpisodeTimeout;

        // Reset agent velocity and angular velocity to prevent carrying over between episodes.
        AgentRigidbody.velocity = new Vector3(0.1f, 0.1f, 0.1f);
        AgentRigidbody.angularVelocity = new Vector3(0f, 0f, 0.1f);

        // Increment iteration counter (support multi-agent env by incrementing text value).
        InterationCounter.text = (int.Parse(InterationCounter.text) + 1).ToString();
        
        // Reset state for thrust control.
        canApplyThrust = true;

        // Initialise agent.
        SetAgentYPosition();
        SetRandomAgentOrientation();
        SetAgentXZPosition();

        if (DebugLogMode) {
            Debug.Log("Iteration: " + InterationCounter.text);
            DebugLogAgentObservations();
        }
    }


    /**
     *  Collect Environment & Agent Sensor Observations
     *
     *  The observations we gather from the environment and agent are:
     *      - Agent orientation (x, y, z).
     *      - Agent velocity (x, y, z).
     *      - Agent distance to the ground (metres).
     *      - Agent distance to landing pad (x, y, z).
     *      - Agent angular velocity (x, y, z).
     *      - Agent thrust vector orientation (x, y, z).
     *      - Agent thrust force being applied (Newtons).
     *
     *  We collect a total of 17 observations to train our agent with.
     */
    public override void CollectObservations(VectorSensor sensor) {
        sensor.AddObservation(GetAgentOrientation());
        sensor.AddObservation(GetAgentVelocity());
        sensor.AddObservation(GetAgentDistanceFromGround());
        sensor.AddObservation(GetAgentPositionRelativeToLandingPad());
        sensor.AddObservation(GetAgentAngularVelocity());
        sensor.AddObservation(GetAgentThrustVectorOrientation());
        sensor.AddObservation(GetAgentCurrentThrust());       
    }


    /**
     *  Performing Actions
     *
     *  Implements logic for turning agent control signals to in-game actions.
     *
     *  Our agent action are as follows:
     *      - Rotating thrust vector (x, z).
     *      - Thrust force output (Newtons).
     */
    public override void OnActionReceived(ActionBuffers actionBuffers) {
        if (DebugLogMode) {
            Debug.Log("======= PRE-NORMALISATION CONTROL SIGNALS =======");
            Debug.Log("Thrust X Control Signal: " + actionBuffers.ContinuousActions[0]);
            Debug.Log("Thrust Z Control Signal: " + actionBuffers.ContinuousActions[1]);
            Debug.Log("Thrust Control Signal: "   + actionBuffers.ContinuousActions[2]);
        }
        
        // Get respective control signals from agent action buffers.
        float xThrustVecControlSignal = 
            DenormalizeFloatNeg(actionBuffers.ContinuousActions[0], -MaxThrusterGimbal.x, MaxThrusterGimbal.x);
        float zThrustVecControlSignal = 
            DenormalizeFloatNeg(actionBuffers.ContinuousActions[1], -MaxThrusterGimbal.z, MaxThrusterGimbal.z);
        float thrustControlSignal = 
            DenormalizeFloat(actionBuffers.ContinuousActions[2], 0f, MaxThrustForce);

        // Set thrust to 0f if thrust can be applied.
        thrustControlSignal = canApplyThrust ? thrustControlSignal : 0f;

        // TODO: Show control signals in UI.
        // TODO: Find best way to scale Thrust VFX size based on thruster force.

        // Display thrust fire VFX when thrust force control signal is not 0.
        if (thrustControlSignal != 0f) {
            ThrustVFX.Play();
        } else {
            ThrustVFX.Clear();
            ThrustVFX.Stop();
        }

        if (DebugLogMode) {
            Debug.Log("======= POST NORMALISATION CONTROL SIGNALS =======");
            Debug.Log("Thrust X Control Signal: " + xThrustVecControlSignal);
            Debug.Log("Thrust Z Control Signal: " + zThrustVecControlSignal);
            Debug.Log("Thrust Control Signal: "   + thrustControlSignal);
        }

        // Hold original thruster orientation for resetting it later.
        Quaternion originalThrusterOrientation = ThrustVector.rotation;

        // Update thrust vector orientation relative to agents based on control signals.
        Vector3 thrusterOrientationOffset = new Vector3(xThrustVecControlSignal, 0f, zThrustVecControlSignal);
        Vector3 newThrusterOrientation = ThrustVector.localEulerAngles + thrusterOrientationOffset;
        ThrustVector.localRotation = Quaternion.Euler(newThrusterOrientation);
        
        // Also update Thrust VFX orientation to match thrust vectors.
        ThrustVFXTransform.localRotation = ThrustVector.localRotation;

        // Apply force to agent at thruster position in direction of thruster.
        Vector3 thrustDirection = ThrustVector.up;
        AgentRigidbody.AddForceAtPosition(thrustDirection * thrustControlSignal,  ThrustVector.position);

        // Reset rotation of thruster back to original to prevent offsets from carrying over.
        ThrustVector.rotation = originalThrusterOrientation;
    }


    /**
     *  Manually Control Agent Action (Heuristic)
     *
     *  When agent behaviour type is set to "Heuristic" we will be able to control
     *  the agent manually using keyboard inputs.
     */
    public override void Heuristic(in ActionBuffers actionsBuffers) {
        // TODO: Allow thrust adjustment using scroll wheel.

        // Set agent thrust vector x-axis rotation on D or A input.
        float thrustVectorRotationX = 0f;
        if (Input.GetKey(KeyCode.D))
            thrustVectorRotationX = -30f;
        else if (Input.GetKey(KeyCode.A))
            thrustVectorRotationX = 30f;

        // Set agent thrust vector z-axis rotation on W or S input.
        float thrustVectorRotationZ = 0f;
        if (Input.GetKey(KeyCode.W))
            thrustVectorRotationZ = 30f;
        else if (Input.GetKey(KeyCode.S))
            thrustVectorRotationZ = -30f;

        // Set thrust force if W, A, S, D or SPACE is pressed.
        float thrustForce = 0f;
        if (IsWASDKeyDown() && canApplyThrust)
            thrustForce = 6000f;
        if (Input.GetKey(KeyCode.Space) && canApplyThrust)
            thrustForce = 12000f;
        
        // Populate continuous action buffer with actions derived from input.
        var continuousActionsOut = actionsBuffers.ContinuousActions;
        continuousActionsOut[0] = NormalizeFloat(thrustVectorRotationX, -MaxThrusterGimbal.x, MaxThrusterGimbal.x);
        continuousActionsOut[1] = NormalizeFloat(thrustVectorRotationZ, -MaxThrusterGimbal.z, MaxThrusterGimbal.z);
        continuousActionsOut[2] = NormalizeFloat(thrustForce, 0f, MaxThrustForce, 0f);
    }


    /**
     *  Collision Enter Event (One-Shot)
     *
     *  Extracts bare-minimal collision information needed by agent into
     *  an agent collision info structure.
     */
    private void OnCollisionEnter(Collision collision) {
        AgentCollisionInfo.AddCollision(collision.gameObject.tag);
        if (DebugLogMode) 
            AgentCollisionInfo.DebugLogState();

        // Penalty given to agent when landing or crashing off pad.
        if (collision.gameObject.CompareTag("Landing Pad"))
            AddReward(StateRewardMap.LANDED_OR_CRASHED_OFF_PAD_PENALTY);
        
        // TODO: Penalty and end episode on collision that is too high in terms of valocity.
        
        // Disable ability to apply thrust control on collision.
        canApplyThrust = false;
    }


    /**
     *  Collision Exit Event (One-Shot)
     *
     *  Updates agent collision information to remove collision info for
     *  object we just exited collision with.
     */
    private void OnCollisionExit(Collision collision) {
        AgentCollisionInfo.RemoveCollision(collision.gameObject.tag);
        if (DebugLogMode) 
            AgentCollisionInfo.DebugLogState();
    }
    

    /**
     *  Draws Debug Mode Gizmos
     *
     *  Draws all agent gizmos that aids with visually debugging of non-visual
     *  elements e.g. distance rays.
     */
    private void OnDrawGizmos() {
        if (!DebugVisualMode) return;
        
        // Draw ray showing agent ray cast to ground (used for gauging distance from ground).
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + new Vector3(0f, 0.1f, 0f), Vector3.down * GetAgentDistanceFromGround());
    }


    #region Agent Setters


    /// Set agent Y position randomly within height range relative to landing pad.
    private void SetAgentYPosition() {
        float heightAboveLandingPad = Random.Range(MinInitPosition.y, MaxInitPosition.y) + LandingPad.position.y;
        transform.position = new Vector3(transform.position.x, heightAboveLandingPad, transform.position.z);
    }


    /// Set agent X & Z position randomly within area range relative to landing pad.
    private void SetAgentXZPosition() {
        float xOffsetFromLandingPad = Random.Range(MinInitPosition.x, MaxInitPosition.x) + LandingPad.position.x;
        float zOffsetFromLandingPad = Random.Range(MinInitPosition.z, MaxInitPosition.z) + LandingPad.position.z;
        transform.position = new Vector3(xOffsetFromLandingPad, transform.position.y, zOffsetFromLandingPad);
    }


    /// Set agent orientation randomly between the values 0 and 360 for each axis.
    private void SetRandomAgentOrientation() {
        transform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
    }


    #endregion


    #region Agent Getters


    /// Return agent orientation (x, y, z respectively with value between 0 - 360).
    private Vector3 GetAgentOrientation() => transform.localEulerAngles;


    /// Return agent velocity (x, y, z respectively).
    private Vector3 GetAgentVelocity() => AgentRigidbody.velocity;


    /// Return agent speed which is just the magnitude of the velocity vector.
    private float GetAgentSpeed() => AgentRigidbody.velocity.magnitude;


    /// Return agent distance from ground below in metres (else -1 if no ground below).
    private float GetAgentDistanceFromGround() {
        LayerMask layerMask = LayerMask.GetMask("Agent");
        Vector3 rayOriginPosition = transform.position + new Vector3(0f, 0.1f, 0f);
        if (Physics.Raycast(rayOriginPosition, Vector3.down, out var hit, Mathf.Infinity, ~layerMask))
            return hit.distance;
        return -1;
    }


    /// Return agent distance from landing pad.
    private float GetAgentDistanceFromLandingPad() => Vector3.Distance(transform.position, LandingPad.position);


    /// Return agent relative position from landing pad.
    private Vector3 GetAgentPositionRelativeToLandingPad() => transform.position - LandingPad.position;


    /// Return agent angular velocity (x, y, z respectively).
    private Vector3 GetAgentAngularVelocity() => AgentRigidbody.angularVelocity;


    /// Return agent thrust vector orientation (x, z respectively).
    private Vector3 GetAgentThrustVectorOrientation() {
        return new Vector3(ThrustVector.localEulerAngles.x, 0f, ThrustVector.localEulerAngles.z);
    }

    
    /// Return agent current thrust in Newtons (N).
    private float GetAgentCurrentThrust() => AgentThrust;


    #endregion


    #region Agent State Checkers


    /// Check if agent is within specified upright orientation range.
    private bool IsAgentInUprightWithinRange() {
        float xAngle = transform.eulerAngles.x;
        float zAngle = transform.eulerAngles.z;
        float xNegativeRange = xAngle - 360f;
        float zNegativeRange = zAngle - 360f;
        return (xNegativeRange >= -UprightOrientationRange || xAngle <= UprightOrientationRange)
            && (zNegativeRange >= -UprightOrientationRange || zAngle <= UprightOrientationRange);
    }


    /// Check if agent (x, z) axis rotations are both within range (-ε >= 0.0 <= ε).
    private bool IsAgentUpright() {
        float axisEpsilon = 0.01f;
        float xAngle = transform.eulerAngles.x;
        float zAngle = transform.eulerAngles.z;
        float xNegativeRange = xAngle - 360f;
        float zNegativeRange = zAngle - 360f;
        return (xNegativeRange >= -axisEpsilon || xAngle <= axisEpsilon)
            && (zNegativeRange >= -axisEpsilon || zAngle <= axisEpsilon);
    }

    /// Check if agent is within specified belly flop orientation range.
    private bool IsAgentInBellyFlopOrientation() {
        float rollEpsilon = 5f;
        float zAngle = transform.eulerAngles.z;
        float xAngle = transform.eulerAngles.x;
        float xNegativeRange = xAngle - 360f;
        bool validRoll = xNegativeRange >= -rollEpsilon || xAngle <= rollEpsilon;
        bool validPitch = zAngle >= BellyFlopPitchRange.x && zAngle <= BellyFlopPitchRange.y;
        return validRoll && validPitch;
    }


    /// Check if agent speed is zero.
    private bool IsAgentStationary() => GetAgentSpeed() < /*epsilon=*/ 0.0001f;


    /// Check if agent has landed defined as being upright, stationary and colliding.
    private bool HasAgentLanded() {
        return IsAgentStationary() && IsAgentUpright() && AgentCollisionInfo.Colliding();
    }


    /// Check if agent has not only landed but also landed on landing pad.
    private bool HasAgentLandedOnPad() {
        return HasAgentLanded() && AgentCollisionInfo.CheckTagExists("Landing Pad");
    }


    /// Check if agent has crashed (not landed upright).
    private bool HasAgentCrashLanded() {
        return !IsAgentUpright() && IsAgentStationary() && AgentCollisionInfo.Colliding();
    }


    /// Check if agent has crashed on landing pad.
    private bool HasAgentCrashLandedOnPad() {
        return HasAgentCrashLanded() && AgentCollisionInfo.CheckTagExists("Landing Pad");
    }


    /// Check if agent is out-of-range on any positional axis relative to landing pad.
    private bool IsAgentOutOfRange() {
        Vector3 padRelPos = GetAgentPositionRelativeToLandingPad();
        if (padRelPos.x >= OutOfRangeDistance.x 
            || padRelPos.y >= OutOfRangeDistance.y 
            || padRelPos.z >= OutOfRangeDistance.z
            || padRelPos.x <= -OutOfRangeDistance.x
            || padRelPos.y <= -OutOfRangeDistance.y
            || padRelPos.z <= -OutOfRangeDistance.z)
            return true;
        return false;
    }


    #endregion


    #region Reward Calculation Helpers


    /// Calculate and return reward in range of (0, 1) proportionate to agent distance from landing pad.
    private float CalculateRewardFromDistanceToLandingPad() {
        return Mathf.Clamp(GetAgentDistanceFromLandingPad() / MaxDistanceFromPad, 0.0f, 1.0f);
    }


    #endregion


    #region Debug Helpers


    /// Logs all agent observations for current state.
    private void DebugLogAgentObservations() {
        Debug.Log("===================== AGENT DATA =====================");
        Debug.Log("Agent Orientation: " + GetAgentOrientation());
        Debug.Log("Agent Velocity: " + GetAgentVelocity());
        Debug.Log("Agent Speed: " + GetAgentSpeed());
        Debug.Log("Agent Distance From Landing Pad: " + GetAgentPositionRelativeToLandingPad());
        Debug.Log("Agent Distance From Ground: " + GetAgentDistanceFromGround());
        Debug.Log("Agent Angular Velocity: " + GetAgentAngularVelocity());
        Debug.Log("Agent Thrust Vector Orientation: " + GetAgentThrustVectorOrientation());
        Debug.Log("Agent Thrust: " + GetAgentCurrentThrust());
        Debug.Log("=======================================================");
    }


    #endregion


    #region Code Helpers


    /// Returns true if W, A, S, or D key is currently being held down.    
    private static bool IsWASDKeyDown() =>
        Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
    
    
    /// Checks if any axis in `x` is less than `y`.
    private bool CheckPerAxisLT_V3(Vector3 x, Vector3 y) => x.x < y.x || x.y < y.y || x.z < y.z;
    
    
    /// Checks if any axis in `x` is greater than `y`.
    private bool CheckPerAxisGT_V3(Vector3 x, Vector3 y) => x.x > y.x || x.y > y.y || x.z > y.z;


    /// Normalizes float to value between given lower and upper bound.
    private float NormalizeFloat(
        float x, float min, float max, float lowerBound = -1f, float upperBound = 1f) => 
        (upperBound - lowerBound) * ((x - min) / (max - min)) + lowerBound;

    
    /**
     * Denormalizes float to value between given min and max when min is positive.
     *
     * Note: `min` should always be positive for this to work. 
    */
    private float DenormalizeFloat(float x, float min, float max) => x * (max - min) + min;

    
    /**
     * Denormalize float to value between given min and max when min is negative.
     *
     * Note: `min` should always be negative for this to work.
     */
    private float DenormalizeFloatNeg(float x, float min, float max) {
        if (min > 0) 
            throw new Exception("Min parameter must be negative.");
        return x * (max - min) / 2;
    }

    #endregion
}
