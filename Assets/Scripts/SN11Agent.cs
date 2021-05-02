using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/**
 *  SpaceX's SN-11 Starship Rocket Agent
 *
 *  Agent modelled after SpaceX's SN series (SN-11 to be specifc) used as
 *  first stage of Starship rocket. This agent is trained to self-land on 
 *  a designated landing pad using Thrust Vector Control (TVC).
 *
 *  TODO:
 *      - Create a different version with fuel limits (no unlimited thrust).
 *      - Create a different version with fins (air drag) and TVC.
 *      - Create a different version with fins, TVC & deployable landing legs.
 *
 *  Authored By Ryan Maugin (@ryanmaugv1)
 */
public class SN11Agent : Agent
{
    [Header("Environment Properties")]
    /// Enable debug features like logging and ray drawing.
    public bool DebugMode;
    /// Time in seconds before we timeout episode because agent took too long to complete episode.
    public int EpisodeTimeout = 120;
    /// Defines out-of-range distance (both direction) for each rocket axis relative to landing pad.
    public Vector3 OutOfRangeDistance = new Vector3(2000f, 2000f, 2000f);
    /// Landing pad transform used for relative positioning of rocket and reward calculation.
    public Transform LandingPad;

    [Header("Agent Thruster Properties")]
    /// Agent thruster transform used for applying force at position for rocket.
    public Transform ThrustVector;
    /// Maximum thrust force (Newtons) that can be outputted by from thruster.
    public float MaxThrustForce = 12000f;
    /// Maximum thruster gimbal in any direction e.g. (30f, 0f, 30f) or (-30f, 0f, -30f).
    public Vector3 MaxThrusterGimbal = new Vector3(30f, 0f, 30f);

    [Header("Agent Initalisation Properties")]
    /// Minimum positional value agent can be initialised at.
    public Vector3 MinInitPosition = new Vector3(-100f, 250f, -100f);
    /// Maximum positional value agent can be initialised at.
    public Vector3 MaxInitPosition = new Vector3(100f, 500f, 100f);

    /// Rigidbody Component belonging to agent (used for applying actions).
    private Rigidbody AgentRigidbody;
    /// Current amount of thrust produced by agent in Newtons.
    private float AgentThrust;
    /// Holds minimal agent collision info needed.
    private CollisionInfo AgentCollisionInfo = new CollisionInfo();


    // TODO:
    // Make the floor and landing pad a cube rather than a plane because ground ray doesn't
    // interest when landing upright on plane. This will also fix bug where agent falls
    // through ground as collision isn't detected on-time due to surface being too thin.

    
    // Start is called before the first frame update
    void Start() {
        AgentRigidbody = GetComponent<Rigidbody>();
    }


    /**
     *  Fixed Update Loop (called before internal physics update)
     *
     *  The agent rewarding logic looks like this:
     *      - Reward in range of 0 to 1 where:
     *          0 = Landed just outside edge of landing pad.
     *          1 = Landed dead centre of landing pad.
     *      - Reward in range of 0 to 0.1 where:
     *          0 = Rocket orientation isn't within upright range.
     *          1 = Rocket orientation is within upright range.
     *      - Reward in range of 0 to 1 where:
     *          0 = Touched ground at velocity greater than 5 m/s.
     *          1 = Touched ground at velocity less than 5 m/s.
     *          TODO: Find better MPH values that aren't so arbitrary.
     *
     *  Using the defined action an reward space above we should be able to
     *  find an optimal policy for self-landing the agent rocket upright on
     *  a designated landing pad.
     *
     *  We want to reset our agent if we end up in the following states:
     *      - Agent Lands Upright (and doesn't move for 5 second)
     *      - Agent Crashed
     *      - Agent Out Of Range
     */
    void FixedUpdate() {
        // Debug.Log("Upright? " + IsAgentUpright());
        // Debug.Log("Stationary? " + IsAgentStationary());
        // Debug.Log("Landed? " + HasAgentLanded());
        // Debug.Log("Landed On Pad? " + HasAgentLandedOnPad());
        // Debug.Log("Crashed? " + HasAgentCrashLanded());
        // Debug.Log("Out Of Range? " + IsAgentOutOfRange());

        // TODO: Implement episode timeout.
    }


    /**
     *  Initializing & Resetting Agent On Episode Begin
     *
     *  We will initialise/reset our agent in the followng state:
     *      1) Set Y-axis transform position within 250-500CM range relative to landing pad y-axis.
     *      2) Set X & Z axis transform position within 50-100CM radius relative to landing pad.
     *          - Prevents positioning of agent directly above landing pad.
     *      3) Set X, Y & Z axis transform rotation randomly.
     *
     *  This should ensure reset/init the environment in random acceptable states
     *  so that our agent can learn a more general and robust policy.
     */
    public override void OnEpisodeBegin() {
        SetAgentYPosition();
        SetRandomAgentOrientation();
        SetAgentXZPosition();
        if (DebugMode)
            DebugLogAgentObservations();
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
     *      - Agent thrust vector orientation (x, z).
     *      - Agent thrust force being applied (Newtons).
     *
     *  We collect a total of 16 observations to train our agent with.
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
     *  Performing Action & Rewarding Agent
     *
     *  Our agent action are as follows:
     *      - Rotating thrust vector (x, z).
     *      - Thrust force output (Newtons).
     */
    public override void OnActionReceived(ActionBuffers actionBuffers) {
        // Get respective control signals from agent action buffers.
        float xThrustVecControlSignal = actionBuffers.ContinuousActions[0];
        float zThrustVecControlSignal = actionBuffers.ContinuousActions[1];
        float thrustControlSignal     = actionBuffers.ContinuousActions[2];

        // Clamp control signals values to expected range to prevent unrealistic values or behaviour.
        xThrustVecControlSignal = Mathf.Clamp(xThrustVecControlSignal, -MaxThrusterGimbal.x, MaxThrusterGimbal.x);
        zThrustVecControlSignal = Mathf.Clamp(zThrustVecControlSignal, -MaxThrusterGimbal.z, MaxThrusterGimbal.z);
        thrustControlSignal     = Mathf.Clamp(thrustControlSignal, 0f, MaxThrustForce);

        // TODO: Show control signals in UI.
        // TODO: Display thurst fire VFX when thrust force is not 0.

        if (DebugMode) {
            Debug.Log("Thrust X Control Signal: " + xThrustVecControlSignal);
            Debug.Log("Thrust Z Control Signal: " + zThrustVecControlSignal);
            Debug.Log("Thrust Control Signal: "   + thrustControlSignal);
        }

        // Hold original thruster orientation for resetting it later.
        Quaternion originalThrusterOrientation = ThrustVector.rotation;

        // Update thrust vector orientation relative to agents based on control signals.
        Vector3 thrusterOrientationOffset = new Vector3(xThrustVecControlSignal, 0f, zThrustVecControlSignal);
        Vector3 newThrusterOrientation = ThrustVector.eulerAngles + thrusterOrientationOffset;
        ThrustVector.rotation = Quaternion.Euler(newThrusterOrientation);
    
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
        if (Input.GetKey(KeyCode.W) 
            || Input.GetKey(KeyCode.A) 
            || Input.GetKey(KeyCode.S) 
            || Input.GetKey(KeyCode.D)
            || Input.GetKey(KeyCode.Space))
            thrustForce = 6000f;
        
        // Populate continuous action buffer with actions derived from input.
        var continuousActionsOut = actionsBuffers.ContinuousActions;
        continuousActionsOut[0]  = thrustVectorRotationX;
        continuousActionsOut[1]  = thrustVectorRotationZ;
        continuousActionsOut[2]  = thrustForce;
    }


    /**
     *  Collision Enter Event (One-Shot)
     *
     *  Extracts bare-minimal collision information needed by agent into
     *  an agent collision info structure.
     */
    void OnCollisionEnter(Collision collision) {
        AgentCollisionInfo.AddCollision(collision.gameObject.tag);
        if (DebugMode) 
            AgentCollisionInfo.DebugLogState();
    }


    /**
     *  Collision Exit Event (One-Shot)
     *
     *  Updates agent collision information to remove collision info for
     *  object we just exited collision with.
     */
    void OnCollisionExit(Collision collision) {
        AgentCollisionInfo.RemoveCollision(collision.gameObject.tag);
        if (DebugMode) 
            AgentCollisionInfo.DebugLogState();
    }
    

    /**
     *  Draws Debug Mode Gizmos
     *
     *  Draws all agent gizmos that aids with visually debugging of non-visual
     *  elements e.g. distance rays.
     */
    void OnDrawGizmos() {
        if (!DebugMode) return;
        
        // Draw ray showing agent ray cast to ground (used for gauging distance from ground).
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.down * GetAgentDistanceFromGround());
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


    /// Return agent distance from ground below in metres (else -1 if no ground below).
    private float GetAgentDistanceFromGround() {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, Mathf.Infinity))
            return hit.distance;
        return -1;
    }


    /// Return agent relative position from landing pad.
    private Vector3 GetAgentPositionRelativeToLandingPad() {
        return transform.position - LandingPad.position;
    }


    /// Return agent angular velocity (x, y, z respectively).
    private Vector3 GetAgentAngularVelocity() => AgentRigidbody.angularVelocity;


    /// Return agent thrust vector orientation (x, z respectively).
    private Vector2 GetAgentThrustVectorOrientation() {
        return new Vector2(ThrustVector.localEulerAngles.x, ThrustVector.localEulerAngles.z);
    }

    
    /// Return agent current thrust in Newtons (N).
    private float GetAgentCurrentThrust() => AgentThrust;


    #endregion


    #region Agent State Checkers


    // TODO: 
    // Do we want a IsAgentUprightWithinRange(Vector3 range) method for rewarding
    // agent on stabilising to upright-ish orientation during flight?


    /// Check if agent (x, z) axis rotations are both within range (-ε >= 0.0 <= ε).
    private bool IsAgentUpright() {
        float axisEpsilon = 0.01f;
        return transform.eulerAngles.x >= -axisEpsilon && transform.eulerAngles.x <= axisEpsilon
            && transform.eulerAngles.z >= -axisEpsilon && transform.eulerAngles.z <= axisEpsilon;
    }


    /// Check if agent velocity and angular velocity on all axis is 0.0f.
    private bool IsAgentStationary() {
        return AgentRigidbody.velocity == new Vector3(0.0f, 0.0f, 0.0f)
            && AgentRigidbody.angularVelocity == new Vector3(0.0f, 0.0f, 0.0f);
    }


    /// Check if agent has landed defined as being upright, stationary and colliding.
    private bool HasAgentLanded() {
        return IsAgentStationary() && IsAgentUpright() && AgentCollisionInfo.Colliding();
    }


    /// Check if agent has not only landed but also landed on landing pad.
    private bool HasAgentLandedOnPad() {
        return HasAgentLanded() && AgentCollisionInfo.CheckTagExists("Landing Pad");
    }


    /// Check if agent has crashed (not landed upright) on pad or not.
    private bool HasAgentCrashLanded() {
        return !IsAgentUpright() && IsAgentStationary() && AgentCollisionInfo.Colliding();
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


    #region Agent Action Helpers


    /// Set thrust vector orientation (x, z) based on agent predicted continuous actions.
    private void SetThrusterOrientation(ActionBuffers actionBuffers) {}


    /// Set thrust force within 0 to 12,000 Newtons (N) based on agent predicted continuous action.
    private void SetThrustForce(ActionBuffers actionBuffers) {}


    #endregion


    #region Debug Helpers


    /// Logs all agent observations for current state.
    private void DebugLogAgentObservations() {
        Debug.Log("===================== AGENT DATA =====================");
        Debug.Log("Agent Orientation: " + GetAgentOrientation());
        Debug.Log("Agent Velocity: " + GetAgentVelocity());
        Debug.Log("Agent Distance From Landing Pad: " + GetAgentPositionRelativeToLandingPad());
        Debug.Log("Agent Distance From Ground: " + GetAgentDistanceFromGround());
        Debug.Log("Agent Angular Velocity: " + GetAgentAngularVelocity());
        Debug.Log("Agent Thrust Vector Orientation: " + GetAgentThrustVectorOrientation());
        Debug.Log("Agent Thrust: " + GetAgentCurrentThrust());
        Debug.Log("=======================================================");
    }


    #endregion
}
