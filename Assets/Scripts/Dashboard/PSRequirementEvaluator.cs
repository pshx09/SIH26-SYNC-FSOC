using UnityEngine;
using FSOC.Contracts;

namespace FSOC.Dashboard
{
    public class PSRequirementEvaluator
    {
        public enum EvalStatus { Pass, Fail, Unknown }

        public EvalStatus EvaluateTrackingError(float currentError)
        {
            if (currentError < 0) return EvalStatus.Unknown; // No lock
            return currentError <= 10f ? EvalStatus.Pass : EvalStatus.Fail;
        }

        public EvalStatus EvaluateTargetLoss(float lossRatePercent)
        {
            return lossRatePercent < 5f ? EvalStatus.Pass : EvalStatus.Fail;
        }

        public EvalStatus EvaluateAcquisitionTime(float acqTime)
        {
            if (acqTime <= 0) return EvalStatus.Unknown;
            return acqTime <= 2f ? EvalStatus.Pass : EvalStatus.Fail;
        }

        public EvalStatus EvaluateReacquisitionTime(float reacqTime)
        {
            if (reacqTime <= 0) return EvalStatus.Unknown;
            return reacqTime <= 1f ? EvalStatus.Pass : EvalStatus.Fail;
        }

        public EvalStatus EvaluateFPS(float fps)
        {
            if (fps <= 0) return EvalStatus.Unknown;
            return fps >= 20f ? EvalStatus.Pass : EvalStatus.Fail;
        }

        public EvalStatus EvaluatePTZRate(float ptzHz)
        {
            if (ptzHz <= 0) return EvalStatus.Unknown;
            return ptzHz >= 20f ? EvalStatus.Pass : EvalStatus.Fail;
        }
    }
}
