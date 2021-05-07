/**
 *  State Agent Reward Map
 *
 *  State to reward mapping data class.
 *
 *  Authored By Ryan Maugin (@ryanmaugv1)
 */
public class StateRewardMap {
    public static float LANDED_UPRIGHT = 1.0f;
    /// Reward given to agent when in belly flop orientation range.
    public static float BELLY_FLOP_POSITION_REWARD = 0.0001f;
    /// Reward given to agent when in upright orientation range.
    public static float UPRIGHT_POSITION_REWARD = 0.0001f;
    /// Reward given to agent after landing on pad.
    public static float LANDED_ON_PAD_REWARD = 1.0f;
    /// Reward given to agent after crashing on pad.
    public static float CRASHED_ON_PAD_REWARD = 0.2f;
    /// Reward given to agent when approaching velocity is below max approach velocity.
    public static float APPROACHING_VELOCITY_REWARD = 0.01f;
}