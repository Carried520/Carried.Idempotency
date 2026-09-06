namespace Carried.Idempotency.AspNet.Observability;

internal interface IIdempotencyMetricsRecorder
{
    void RecordResponse(string result);
}