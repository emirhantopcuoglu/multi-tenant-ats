using Prometheus;

namespace Ats.Shared.Infrastructure;

// Static because prometheus-net throws if the same metric is created twice.
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
            Buckets = Histogram.ExponentialBuckets(start: 0.5, factor: 2, count: 8)
        });

    public static readonly Counter EmailFailuresTotal = Metrics.CreateCounter(
        "ats_email_failures_total",
        "Number of email send attempts that threw an SMTP exception.");
}
