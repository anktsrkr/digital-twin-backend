using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ResumeAssistant.Api.Telemetry;

public static class ResumeAssistantTelemetry
{
    public const string ServiceName = "ResumeAssistant.Api";
    public const string ActivitySourceName = "ResumeAssistant.Agent";
    public const string MeterName = "ResumeAssistant.Agent";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // Metrics for Grafana Cloud (Mimir)
    public static readonly Counter<long> InteractionCounter = Meter.CreateCounter<long>(
        "recruiter_interactions_total",
        unit: "interactions",
        description: "Total number of recruiter interactions with the Digital Twin");

    public static readonly Histogram<double> ResponseTimeHistogram = Meter.CreateHistogram<double>(
        "recruiter_response_time_seconds",
        unit: "seconds",
        description: "Time taken by the Digital Twin agent to stream responses");

    public static readonly Counter<long> InterviewBookingsCounter = Meter.CreateCounter<long>(
        "recruiter_interview_bookings_total",
        unit: "bookings",
        description: "Total number of Cal.com interview booking card triggers");

    public static readonly Counter<long> ResumeDownloadsCounter = Meter.CreateCounter<long>(
        "recruiter_resume_downloads_total",
        unit: "downloads",
        description: "Total number of PDF resume downloads triggered");

    public static readonly Counter<long> DisposableEmailsBlockedCounter = Meter.CreateCounter<long>(
        "recruiter_disposable_emails_blocked_total",
        unit: "blocked",
        description: "Total number of temporary / disposable emails blocked at recruiter gate");
}
