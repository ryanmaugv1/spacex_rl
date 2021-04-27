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
    }


    #region OnEpisodeBegin Helper Methods


    /// Set agent Y position randomly within height range relative to landing pad.
    private void SetAgentYPosition() {
        float distanceFromLandingPad = Random.Range(MinInitPosition.y, MaxInitPosition.y) + LandingPad.position.y;
        transform.position = new Vector3(transform.position.x, distanceFromLandingPad, transform.position.z);
    }


    /// Set agent X & Z position randomly within area range relative to landing pad.
    private void SetAgentXZPosition() {}


    /// Set agent orientation randomly.
    private void SetRandomAgentOrientation() {}


    #endregion


    /**
     *  Collect Environment & Agent Sensor Observations
     *
     *  The observations we gather from the environment and agent are:
     *      - Agent relative position to landing pad (x, y, z).
     *      - Agent orientation (x, y, z).
     *      - Agent velocity (x, y, z).
     *      - Agent distance to the landing pad/ground (y in metres).
     *      - Agent angular momentum (x, y, z).
     *      - Agent thrust vector orientation (x, z).
     *      - Agent thrust force being applied (Kilo Newton).
     *
     *  We collect a total of 16 observations to train our agent with.
     */
    public override void CollectObservations(VectorSensor sensor) {}

    #region CollectObservations Helper Methods


    /// Returns agent relative position from landing pad (x, y, z respectively)
    private Vector3 GetAgentRelativePositionFromLandingPad() {
        return Vector3.zero;
    }


    /// Returns agent orientation (x, y, z respectively).
    private Vector3 GetAgentOrientation() {
        return Vector3.zero;
    }


    /// Returns agent velocity (x, y, z respectively).
    private Vector3 GetAgentVelocity() {
        return Vector3.zero;
    }


    /// Returns agent distance from ground below in metres.
    private float GetAgentDistanceFromGround() {
        return 0.0f;
    }


    /// Returns agent distance from landing pad.
    private float GetAgentDistanceFromLandingPad() {
        return 0.0f;
    }


    /// Returns agent angular momentum (x, y, z respectively).
    private Vector3 GetAgentAngularMomentum() {
        return Vector3.zero;
    }


    /// Returns agent thrust vector orientation (x, z respectively).
    private (float x, float z) GetThrustVectorOrientation() {
        return (0.0f, 0.0f);
    }

    
    /// Returns agent current thrust force in kilo Newton (kN).
    private float GetAgentCurrentThrustForce() {
        return 0.0f;
    }


    #endregion


    /**
     *  Performing Action & Rewarding Agent
     *
     *  Our agent action are as follows:
     *      - Rotating thrust vector (x, z).
     *      - Thrust force output (Kilo Newton).
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


    /// Set thrust force within 0 to 12 Kilo Newton (kN) based on agent predicted continuous action.
    private void SetThrustForce(ActionBuffers actionBuffers) {}


    #endregion


    #endregion
}
