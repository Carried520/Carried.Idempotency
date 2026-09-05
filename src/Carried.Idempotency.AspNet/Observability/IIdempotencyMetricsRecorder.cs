namespace Carried.Idempotency.AspNet.Observability;

public interface IIdempotencyMetricsRecorder
{
    void RecordResponse(string result);
}