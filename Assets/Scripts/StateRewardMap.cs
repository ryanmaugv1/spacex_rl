/**
 *  State Agent Reward Map
 *
 *  State to reward mapping data class.
 *
 *  Authored By Ryan Maugin (@ryanmaugv1)
 */
public class StateRewardMap {
    public const float LANDED_UPRIGHT = 1.0f;
    /// Reward given to agent when in belly flop orientation range.
    public const float BELLY_FLOP_POSITION_REWARD = 0.0001f;
    /// Reward given to agent when in upright orientation range.
    public const float UPRIGHT_POSITION_REWARD = 0.0001f;
    /// Reward given to agent after landing on pad.
    public const float LANDED_ON_PAD_REWARD = 1.0f;
    /// Reward given to agent after crashing on pad.
    public const float CRASHED_ON_PAD_REWARD = 0.2f;
    /// Reward given to agent when approaching speed is below max approach speed.
    public const float APPROACHING_SPEED_REWARD = 0.0001f;
    /// Penalty given to agent when landing or crashing off pad.
    public const float LANDED_OR_CRASHED_OFF_PAD_PENALTY = -0.25f;
}