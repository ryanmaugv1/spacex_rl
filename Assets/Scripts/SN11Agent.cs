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
    /// Landing pad transform used for relative positioning of rocket and reward calculation.
    public Transform LandingPad;

    [Header("Agent Properties")]
    /// Agent thruster transform used for applying force at position for rocket.
    public Transform ThrustVector;
    /// Minimum positional value agent can be initialised at.
    public Vector3 MinInitPosition;
    /// Maximum positional value agent can be initialised at.
    public Vector3 MaxInitPosition;

    /// Rigidbody Component belonging to agent (used for applying actions).
    private Rigidbody AgentRigidbody;

    
    // Start is called before the first frame update
    void Start() {
        AgentRigidbody = GetComponent<Rigidbody>();
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
    }


    #region OnEpisodeBegin Helper Methods


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


    /**
     *  Collect Environment & Agent Sensor Observations
     *
     *  The observations we gather from the environment and agent are:
     *      - Agent relative position to landing pad (x, y, z).
     *      - Agent orientation (x, y, z).
     *      - Agent velocity (x, y, z).
     *      - Agent distance to the ground (y in metres).
     *      - Agent angular momentum (x, y, z).
     *      - Agent thrust vector orientation (x, z).
     *      - Agent thrust force being applied (Newtons).
     *
     *  We collect a total of 16 observations to train our agent with.
     */
    public override void CollectObservations(VectorSensor sensor) {}

    #region CollectObservations Helper Methods


    /// Return agent orientation (x, y, z respectively with value between 0 - 360).
    private Vector3 GetAgentOrientation() => transform.localEulerAngles;


    /// Return agent velocity (x, y, z respectively).
    private Vector3 GetAgentVelocity() => AgentRigidbody.velocity;


    /// Return agent distance from ground below in metres (else -1 if no ground below).
    private float GetAgentDistanceFromGround() {
        RaycastHit hit;
        Vector3 direction = Vector3.down;
        if (Physics.Raycast(transform.position, direction, out hit, Mathf.Infinity)) {
            if (DebugMode) 
                Debug.DrawRay(transform.position, direction * hit.distance, Color.red);
            return hit.distance;
        }
        return -1;
    }


    /// Return agent distance from landing pad.
    private float GetAgentDistanceFromLandingPad() {
        return Vector3.Distance(transform.position, LandingPad.position);
    }


    /// Return agent angular velocity (x, y, z respectively).
    private Vector3 GetAgentAngularVelocity() => AgentRigidbody.angularVelocity;


    /// Return agent thrust vector orientation (x, z respectively).
    private (float x, float z) GetThrustVectorOrientation() {
        return (ThrustVector.localEulerAngles.x, ThrustVector.localEulerAngles.z);
    }

    
    /// Returns agent current thrust force in Newtons (N).
    private float GetAgentCurrentThrustForce() {
        return 0.0f;
    }


    #endregion


    /**
     *  Performing Action & Rewarding Agent
     *
     *  Our agent action are as follows:
     *      - Rotating thrust vector (x, z).
     *      - Thrust force output (Newtons).
     *
     *  The agent rewarding logic looks like this:
     *      - Reward in range of 0 to 1 where:
     *          0 = Landed just outside edge of landing pad.
     *          1 = Landed dead centre of landing pad.
     *      - Reward in range of 0 to 0.1 where:
     *          0 = Rocket orientation isn't within upright range.
     *          1 = Rocket orientation is within upright range.
     *      - Reward in range of 0 to 1 where:
     *          0 = Touched ground at velocity greater than 10 m/s.
     *          1 = Touched ground at velocity less than 1 m/s.
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
    public override void OnActionReceived(ActionBuffers actionBuffers) {}


    #region OnActionReceived Helper Methods


    #region State Check Helpers


    /// Checks if agent has landed upright (we don't care about whether it's on pad or not).
    private bool HasAgentLanded() {
        return false;
    }


    /// Check if agent has landed on landing pad (we don't care about orientation).
    private bool HasAgentLandedOnPad() {
        return false;
    }


    /// Check if agent has crashed (not landed upright) on pad or not.
    private bool HasAgentCrashed() {
        return false;
    }


    /// Check if agent has flown too far out of range on any positional axis relative to landing pad.
    private bool IsAgentOutOfRange() {
        return false;
    }


    #endregion


    #region Action Helpers


    /// Set thrust vector orientation (x, z) based on agent predicted continuous actions.
    private void SetThrusterOrientation(ActionBuffers actionBuffers) {}


    /// Set thrust force within 0 to 12,000 Newtons (N) based on agent predicted continuous action.
    private void SetThrustForce(ActionBuffers actionBuffers) {}


    #endregion


    #endregion
}
