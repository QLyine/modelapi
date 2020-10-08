using System;

namespace out_ai.Data
{
    public class StateMachine
    {
        public enum State
        {
            Ready,
            HoldRequests
        }

        public enum Input
        {
            Deploying,
            Deployed
        }

        State ChangeState(State current, Input input) => (current, input) switch
        {
            (State.Ready, Input.Deployed) => State.Ready,
            (State.Ready, Input.Deploying) => State.HoldRequests,
            (State.HoldRequests, Input.Deployed) => State.Ready,
            (State.HoldRequests, Input.Deploying) => State.HoldRequests,
            _ => throw new NotSupportedException("Invalid State")
        };
    }
}