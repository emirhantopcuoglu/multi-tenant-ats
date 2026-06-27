using Prometheus;

namespace Ats.Shared.Infrastructure;

// Static, lazily-initialized Prometheus metrics. prometheus-net counters and histograms are
// thread-safe singletons — creating them multiple times would throw, so they live here as static
// fields and are referenced directly where needed.
public static class AppMetrics
{
    public static readonly Counter ApplicationsSubmittedTotal = Metrics.CreateCounter(
        "ats_applications_submitted_total",
        "Number of job applications received and processed end-to-end (candidate email sent).");

    public static readonly Histogram CvParsingDurationSeconds = Metrics.CreateHistogram(
        "ats_cv_parsing_duration_seconds",
        "Elapsed time for one CV parsing LLM call (download → extract text → parse).",
        new HistogramConfiguration
        {
            // Buckets matched to real latency ranges: fast (<2s), normal (2-8s), slow (>8s).
            Buckets = Histogram.ExponentialBuckets(start: 0.5, factor: 2, count: 8)
        });

    public static readonly Counter EmailFailuresTotal = Metrics.CreateCounter(
        "ats_email_failures_total",
        "Number of email send attempts that threw an SMTP exception.");
}
