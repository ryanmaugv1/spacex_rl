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
    /// Agent thruster transform used for applying force at position for rocket.
    public Transform AgentThruster;
    /// Landing pad transform used for relative positioning of rocket and reward calculation.
    public Transform LandingPad;

    /// Rigidbody Component belonging to agent (used for applying actions).
    private Rigidbody AgentRigidbody;

    
    // Start is called before the first frame update
    void Start() {
        AgentRigidbody = GetComponent<Rigidbody>();
    }


    /**
     * Initializing & Resetting Agent On Episode Begin
     *
     * We will initialise/reset our agent in the followng state:
     *      1) Set Y-axis transform position within 250-500CM range relative to landing pad.
     *      2) Set X & Z axis transform position within 50-100CM radius relative to landing pad.
     *      3) Set X, Y & Z axis transform rotation randomly.
     *
     * We want to reset our agent if we end up in the following states:
     *      1) Agent Lands Upright (and doesn't move for 5 second)
     *      2) Agent Crashes
     *      3) Agent Falls Below Landing Pad
     */
    public override void OnEpisodeBegin() {}


    /**
     *  Collect Environment & Agent Sensor Observations
     *
     *  The observations we gather from the environment and agent are:
     *      1) Agent relative position to landing pad (x, y, z).
     *      2) Agent relative rotation to landing pad (x, y, z).
     *      3) Agent velocity (x, y, z).
     *      4) Agent distance to the landing pad (y in metres).
     *      5) Agent angular momentum (x, y, z).
     *      6) Agent thrust vector rotation (x, z).
     *      7) Agent thrust force being applied (Kilo Newton).
     *
     *  We collect a total of 16 observations to train our agent with.
     */
    public override void CollectObservations(VectorSensor sensor) {}


    /**
     *  Performing Action & Rewarding Agent
     *
     *  Our agent action are as follows:
     *      1) Rotating thrust vector (x, z).
     *      2) Thrust force output (newton).
     *
     *  The agent rewarding logic looks like this:
     *      1) Reward in range of 0 to 1 where:
     *          0 = Landed just outside edge of landing pad.
     *          1 = Landed dead centre of landing pad.
     *      2) Reward in range of 0 to 0.1 where:
     *          0 = Rocket orientation isn't within upright range.
     *          1 = Rocket orientation is within upright range.
     *      3) Reward in range of 0 to 1 where:
     *          0 = Touched ground at velocity greater than 10 m/s.
     *          1 = Touched ground at velocity less than 1 m/s.
     *          TODO: Find better MPH values that aren't so arbitrary.
     *
     *  Using the defined action an reward space above we should be able to
     *  find an optimal policy for self-landing the agent rocket upright on
     *  a designated landing pad.
     */
    public override void OnActionReceived(ActionBuffers actionBuffers) {}
}
